using AdminHiitop.Api.Infrastructure.Seed;
using AdminHiitop.Api.Middleware;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Extensions;

public static class WebApplicationExtensions
{
    public static async Task UseApiPipelineAsync(this WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors(ApiServiceCollectionExtensions.DefaultCorsPolicyName);
        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        await ApplyDatabaseInitializationAsync(app);
        await app.RunAsync();
    }

    private static async Task ApplyDatabaseInitializationAsync(WebApplication app)
    {
        bool autoMigrate = app.Configuration.GetValue<bool>("Database:AutoMigrate");
        bool autoSeed = app.Configuration.GetValue<bool>("Database:AutoSeed");
        string databaseProvider = app.Configuration["Database:Provider"] ?? "MySql";

        if (!autoMigrate && !autoSeed)
        {
            return;
        }

        using IServiceScope scope = app.Services.CreateScope();
        IServiceProvider provider = scope.ServiceProvider;

        if (autoMigrate)
        {
            var context = provider.GetRequiredService<AdminHiitop.Api.Infrastructure.Persistence.AdminHiitopDbContext>();
            if (databaseProvider.Equals("MySql", StringComparison.OrdinalIgnoreCase)
                && await ShouldUseEnsureCreatedForMySqlAsync(context))
            {
                await context.Database.EnsureCreatedAsync();
            }
            else
            {
                await context.Database.MigrateAsync();
            }
        }

        if (autoSeed)
        {
            var seeder = provider.GetRequiredService<AdminHiitopDbSeeder>();
            await seeder.SeedAsync();
        }
    }

    private static async Task<bool> ShouldUseEnsureCreatedForMySqlAsync(AdminHiitop.Api.Infrastructure.Persistence.AdminHiitopDbContext context)
    {
        if (!await context.Database.CanConnectAsync())
        {
            return true;
        }

        const string sql = """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = DATABASE()
              AND table_name = '__EFMigrationsHistory';
            """;

        int historyTableCount = 0;
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        if (command.Connection?.State != System.Data.ConnectionState.Open)
        {
            await command.Connection!.OpenAsync();
        }

        object? scalar = await command.ExecuteScalarAsync();
        if (scalar is not null && int.TryParse(scalar.ToString(), out int parsed))
        {
            historyTableCount = parsed;
        }

        return historyTableCount == 0;
    }
}
