using AdminHiitop.Api.Application.Interfaces.ElectronicBilling;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Application.Services.Shopify;
using AdminHiitop.Api.Application.Services.Auth;
using AdminHiitop.Api.Application.Services.Catalogs;
using AdminHiitop.Api.Application.Services.Collections;
using AdminHiitop.Api.Application.Services.Colors;
using AdminHiitop.Api.Application.Services.Customers;
using AdminHiitop.Api.Application.Services.DailySummaries;
using AdminHiitop.Api.Application.Services.Dashboard;
using AdminHiitop.Api.Application.Services.Districts;
using AdminHiitop.Api.Application.Services.DocumentPrintFormats;
using AdminHiitop.Api.Application.Services.DocumentTypes;
using AdminHiitop.Api.Application.Services.Invoices;
using AdminHiitop.Api.Application.Services.InvoiceSeries;
using AdminHiitop.Api.Application.Services.OrderGuides;
using AdminHiitop.Api.Application.Services.Orders;
using AdminHiitop.Api.Application.Services.OrderStatuses;
using AdminHiitop.Api.Application.Services.PaymentMethods;
using AdminHiitop.Api.Application.Services.Pos;
using AdminHiitop.Api.Application.Services.Products;
using AdminHiitop.Api.Application.Services.ProductTypes;
using AdminHiitop.Api.Application.Services.Promotions;
using AdminHiitop.Api.Application.Services.Provinces;
using AdminHiitop.Api.Application.Services.PurchaseTypes;
using AdminHiitop.Api.Application.Services.Roles;
using AdminHiitop.Api.Application.Services.SaleImports;
using AdminHiitop.Api.Application.Services.Sales;
using AdminHiitop.Api.Application.Services.Settings;
using AdminHiitop.Api.Application.Services.ShippingAgencies;
using AdminHiitop.Api.Application.Services.Stocks;
using AdminHiitop.Api.Application.Services.UnitMeasures;
using AdminHiitop.Api.Application.Services.Users;
using AdminHiitop.Api.Application.Services.Warehouses;
using AdminHiitop.Api.Application.Services.WarehouseTypes;
using Microsoft.Extensions.DependencyInjection;

namespace AdminHiitop.Api.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IInvoiceElectronicBillingService, InvoiceElectronicBillingService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IDistrictService, DistrictService>();
        services.AddScoped<IDocumentTypeService, DocumentTypeService>();
        services.AddScoped<IDocumentPrintFormatService, DocumentPrintFormatService>();
        services.AddScoped<IInvoiceSeriesService, InvoiceSeriesService>();
        services.AddScoped<IOrderGuideService, OrderGuideService>();
        services.AddScoped<IDashboardQueryService, DashboardQueryService>();
        services.AddScoped<ICatalogQueryService, CatalogQueryService>();
        services.AddScoped<IProductQueryService, ProductQueryService>();
        services.AddScoped<ICustomerQueryService, CustomerQueryService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IOrderQueryService, OrderQueryService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderStatusService, OrderStatusService>();
        services.AddScoped<IPosService, PosService>();
        services.AddScoped<IElectronicBillingProvider, NubeFactProviderFacade>();
        services.AddScoped<IShopifyOrderService, ShopifyOrderService>();
        services.AddScoped<IShopifyProductService, ShopifyProductService>();
        services.AddScoped<ICollectionService, CollectionService>();
        services.AddScoped<IColorService, ColorService>();
        services.AddScoped<IDailySummaryService, DailySummaryService>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();
        services.AddScoped<IProductTypeService, ProductTypeService>();
        services.AddScoped<IPromotionService, PromotionService>();
        services.AddScoped<IProvinceService, ProvinceService>();
        services.AddScoped<IPurchaseTypeService, PurchaseTypeService>();
        services.AddScoped<ISaleImportService, SaleImportService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IShippingAgencyService, ShippingAgencyService>();
        services.AddScoped<IUnitMeasureService, UnitMeasureService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IWarehouseTypeService, WarehouseTypeService>();

        return services;
    }
}
