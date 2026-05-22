using AdminHiitop.Api.Application.Interfaces.Repositories;
using AdminHiitop.Api.Application.Options;
using AdminHiitop.Api.Infrastructure.Auth;
using AdminHiitop.Api.Infrastructure.ElectronicBilling;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Infrastructure.Repositories;
using AdminHiitop.Api.Infrastructure.Seed;
using AdminHiitop.Api.Infrastructure.Shopify;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AdminHiitop.Api.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        string provider = configuration["Database:Provider"] ?? "SqlServer";
        int commandTimeout = configuration.GetValue("Database:CommandTimeoutSeconds", 60);
        bool sensitiveLogging = configuration.GetValue("Database:EnableSensitiveDataLogging", false);

        services.AddDbContext<AdminHiitopDbContext>(options =>
        {
            if (sensitiveLogging)
                options.EnableSensitiveDataLogging();

            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(
                    configuration.GetConnectionString("SqlServerConnection"),
                    opts => opts.CommandTimeout(commandTimeout));
                return;
            }

            string connectionString = configuration.GetConnectionString("MySqlConnection")
                ?? throw new InvalidOperationException("MySqlConnection no esta configurado.");

            options
                .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                    opts => opts.CommandTimeout(commandTimeout))
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.MappedPropertyIgnoredWarning));
        });

        services.Configure<ApplicationOptions>(configuration.GetSection(ApplicationOptions.SectionName));
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<PaginationOptions>(configuration.GetSection(PaginationOptions.SectionName));
        services.Configure<SwaggerOptions>(configuration.GetSection(SwaggerOptions.SectionName));
        services.Configure<NubeFactOptions>(configuration.GetSection("ElectronicBilling:NubeFact"));
        services.Configure<DocumentDefaultsOptions>(configuration.GetSection(DocumentDefaultsOptions.SectionName));
        services.Configure<PosOptions>(configuration.GetSection(PosOptions.SectionName));
        services.Configure<ShopifyOptions>(configuration.GetSection(ShopifyOptions.SectionName));

        services.AddHttpClient<NubeFactClient>();
        services.AddHttpClient<ShopifyAdminClient>();
        services.AddScoped<AdminHiitopDbSeeder>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IInvoiceElectronicBillingRepository, InvoiceElectronicBillingRepository>();
        services.AddSingleton<SessionTokenStore>();

        return services;
    }
}
