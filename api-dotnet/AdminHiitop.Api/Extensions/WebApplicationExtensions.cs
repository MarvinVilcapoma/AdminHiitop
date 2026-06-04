using AdminHiitop.Api.Infrastructure.Seed;
using AdminHiitop.Api.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdminHiitop.Api.Extensions;

public static class WebApplicationExtensions
{
    public static async Task UseApiPipelineAsync(this WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        bool swaggerEnabled = app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment());
        if (swaggerEnabled)
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors(ApiServiceCollectionExtensions.DefaultCorsPolicyName);
        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        try
        {
            await ApplyDatabaseInitializationAsync(app);
        }
        catch (Exception ex)
        {
            // Log startup DB errors but don't crash the process — the HTTP server
            // still starts so health checks and login remain reachable.
            var logger = app.Services.GetRequiredService<ILogger<WebApplication>>();
            logger.LogCritical(ex, "Database initialization failed on startup.");
        }

        await app.RunAsync();
    }

    private static async Task ApplyDatabaseInitializationAsync(WebApplication app)
    {
        bool autoMigrate = app.Configuration.GetValue<bool>("Database:AutoMigrate");
        bool autoSeed    = app.Configuration.GetValue<bool>("Database:AutoSeed");
        string provider  = app.Configuration["Database:Provider"] ?? "SqlServer";

        if (!autoMigrate && !autoSeed) return;

        using IServiceScope scope = app.Services.CreateScope();
        IServiceProvider services  = scope.ServiceProvider;
        var logger = app.Services.GetRequiredService<ILogger<WebApplication>>();

        if (autoMigrate)
        {
            var context = services.GetRequiredService<AdminHiitop.Api.Infrastructure.Persistence.AdminHiitopDbContext>();

            // Step 1 — run EnsureCreated or MigrateAsync.
            // This CAN fail if the DB was bootstrapped with EnsureCreated and
            // MigrateAsync tries to re-create already-existing tables.
            // We catch and log that failure so Step 2 still runs.
            try
            {
                if (provider.Equals("MySql", StringComparison.OrdinalIgnoreCase)
                    && await ShouldUseEnsureCreatedForMySqlAsync(context))
                {
                    await context.Database.EnsureCreatedAsync();
                }
                else
                {
                    await context.Database.MigrateAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "EnsureCreated/MigrateAsync failed — continuing with schema patches.");
            }

            // Step 2 — idempotent column patches, ALWAYS run regardless of Step 1 outcome.
            // This guarantees new columns exist even if migrations couldn't be applied.
            if (provider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await ApplyMySqlSchemaPatchesAsync(context);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "MySQL schema patches failed on startup.");
                }
            }
        }

        if (autoSeed)
        {
            var seeder = services.GetRequiredService<AdminHiitopDbSeeder>();
            await seeder.SeedAsync();
        }
    }

    private static async Task ApplyMySqlSchemaPatchesAsync(
        AdminHiitop.Api.Infrastructure.Persistence.AdminHiitopDbContext context)
    {
        // ── invoice_series.name ──────────────────────────────────────────────
        // MySQL 5.7: TEXT can't have DEFAULT, add nullable then fix.
        bool nameExists = await ColumnExistsAsync(context, "invoice_series", "name");
        if (!nameExists)
        {
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `invoice_series` ADD COLUMN `name` longtext NULL");
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE `invoice_series` SET `name` = '' WHERE `name` IS NULL");
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `invoice_series` MODIFY COLUMN `name` longtext NOT NULL");
        }

        // ── Rename old series to match Nubefact-registered series ───────────
        // These old names (F001, B001, etc.) were never registered in Nubefact.
        // Nubefact requires FFF1, BBB1, TTT1, VVV1. Rename and remove duplicates.
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM `invoice_series` WHERE `serie` IN ('FC01','BC01','FD01','BD01')");
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE `invoice_series` SET `serie`='FFF1', `name`='Facturas Electronicas'           WHERE `serie`='F001'");
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE `invoice_series` SET `serie`='BBB1', `name`='Boletas de Venta'                WHERE `serie`='B001'");
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE `invoice_series` SET `serie`='TTT1', `name`='Guias de Remision Remitente'     WHERE `serie`='T001'");
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE `invoice_series` SET `serie`='VVV1', `name`='Guias de Remision Transportista' WHERE `serie`='V001'");

        // ── orders: new guide columns ────────────────────────────────────────
        if (!await ColumnExistsAsync(context, "orders", "guide_type"))
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `orders` ADD COLUMN `guide_type` VARCHAR(10) NULL");

        if (!await ColumnExistsAsync(context, "orders", "guide_pdf_link"))
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `orders` ADD COLUMN `guide_pdf_link` TEXT NULL");

        if (!await ColumnExistsAsync(context, "orders", "guide_consulted_at"))
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `orders` ADD COLUMN `guide_consulted_at` DATETIME NULL");
    }

    private static async Task<bool> ColumnExistsAsync(
        AdminHiitop.Api.Infrastructure.Persistence.AdminHiitopDbContext context,
        string table, string column)
    {
        var conn = context.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME   = '{table}'
              AND COLUMN_NAME  = '{column}';
            """;

        object? scalar = await cmd.ExecuteScalarAsync();
        return scalar is not null
            && long.TryParse(scalar.ToString(), out long n)
            && n > 0;
    }

    private static async Task<bool> ShouldUseEnsureCreatedForMySqlAsync(AdminHiitop.Api.Infrastructure.Persistence.AdminHiitopDbContext context)
    {
        if (!await context.Database.CanConnectAsync())
            return true;

        await using var command = context.Database.GetDbConnection().CreateCommand();

        if (command.Connection?.State != System.Data.ConnectionState.Open)
            await command.Connection!.OpenAsync();

        // If the migrations history table doesn't exist, use EnsureCreated (safe for pre-existing DBs)
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = DATABASE()
              AND table_name = '__EFMigrationsHistory';
            """;

        object? scalar = await command.ExecuteScalarAsync();
        bool historyTableExists = scalar is not null
            && int.TryParse(scalar.ToString(), out int tableCount)
            && tableCount > 0;

        if (!historyTableExists)
            return true;

        // History table exists but may be empty (DB created outside EF migrations).
        // If no migration rows are recorded, treat it the same as no history table —
        // use EnsureCreated so we don't try to recreate tables that already exist.
        command.CommandText = "SELECT COUNT(*) FROM `__EFMigrationsHistory`;";
        scalar = await command.ExecuteScalarAsync();
        bool hasMigrationRows = scalar is not null
            && int.TryParse(scalar.ToString(), out int rowCount)
            && rowCount > 0;

        return !hasMigrationRows;
    }
}
