using AdminHiitop.Api.Application.DTOs.Common;
using AdminHiitop.Api.Application.Interfaces.Repositories;
using AdminHiitop.Api.Infrastructure.Auth;
using AdminHiitop.Api.Infrastructure.ElectronicBilling;
using AdminHiitop.Api.Infrastructure.Persistence;
using AdminHiitop.Api.Infrastructure.Repositories;
using AdminHiitop.Api.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AdminHiitop.Api.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        string provider = configuration["Database:Provider"] ?? "MySql";

        services.AddDbContext<AdminHiitopDbContext>(options =>
        {
            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(configuration.GetConnectionString("SqlServerConnection"));
                return;
            }

            string connectionString = configuration.GetConnectionString("MySqlConnection")
                ?? throw new InvalidOperationException("MySqlConnection no esta configurado.");

            options
                .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.MappedPropertyIgnoredWarning));
        });

        services.Configure<NubeFactOptions>(configuration.GetSection("ElectronicBilling:NubeFact"));
        services.Configure<PosOptions>(configuration.GetSection(PosOptions.SectionName));
        services.AddHttpClient<NubeFactClient>();
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
