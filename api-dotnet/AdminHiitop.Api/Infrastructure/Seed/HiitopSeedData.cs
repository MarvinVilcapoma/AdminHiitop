using AdminHiitop.Api.Domain.Catalog.Entities;

namespace AdminHiitop.Api.Infrastructure.Seed;

public static class HiitopSeedData
{
    public static IReadOnlyList<OrderStatus> OrderStatuses { get; } =
    [
        new() { Name = "Reservado", Slug = "reservado", Color = "#6366f1", SortOrder = 0, IsProtected = false, IsActive = true },
        new() { Name = "Pendiente", Slug = "pendiente", Color = "#f59e0b", SortOrder = 1, IsProtected = true, IsActive = true },
        new() { Name = "En proceso", Slug = "en-proceso", Color = "#8b5cf6", SortOrder = 2, IsProtected = false, IsActive = true },
        new() { Name = "En camino", Slug = "en-camino", Color = "#3b82f6", SortOrder = 3, IsProtected = false, IsActive = true },
        new() { Name = "Entregado", Slug = "delivered", Color = "#10b981", SortOrder = 4, IsProtected = true, IsActive = true },
        new() { Name = "Devuelto", Slug = "devuelto", Color = "#f97316", SortOrder = 6, IsProtected = false, IsActive = true },
        new() { Name = "Cancelado", Slug = "cancelled", Color = "#ef4444", SortOrder = 7, IsProtected = true, IsActive = true }
    ];

    public static IReadOnlyList<(string Code, string Name)> ShippingAgencies { get; } =
    [
        ("SHALOM", "Shalom"),
        ("OLVA_COURIER", "Olva Courier"),
        ("SERPOST", "Serpost"),
        ("DHL", "DHL Express"),
        ("DINSIDES", "Dinsides"),
        ("RECOJO EN TIENDA", "Recojo en tienda")
    ];

    public static IReadOnlyList<DocumentType> DocumentTypes { get; } =
    [
        // SUNAT numeric: 03
        new() { Code = "BOLETA",                    Name = "Boleta",                         SortOrder = 10, IsProtected = true,  IsSunatDocument = true,  RequiresCustomer = false, RequiresRelatedDocument = false, CanBeConverted = false, IsCommercialDocument = false, IsActive = true },
        // SUNAT numeric: 01
        new() { Code = "FACTURA",                   Name = "Factura",                        SortOrder = 20, IsProtected = true,  IsSunatDocument = true,  RequiresCustomer = true,  RequiresRelatedDocument = false, CanBeConverted = false, IsCommercialDocument = false, IsActive = true },
        // Internal only
        new() { Code = "TICKET",                    Name = "Ticket",                         SortOrder = 25, IsProtected = true,  IsSunatDocument = false, RequiresCustomer = false, RequiresRelatedDocument = false, CanBeConverted = false, IsCommercialDocument = true,  IsActive = true },
        // SUNAT numeric: 07
        new() { Code = "NOTA_CREDITO",              Name = "Nota de Credito",                SortOrder = 30, IsProtected = true,  IsSunatDocument = true,  RequiresCustomer = true,  RequiresRelatedDocument = true,  CanBeConverted = false, IsCommercialDocument = false, IsActive = true },
        // SUNAT numeric: 08
        new() { Code = "NOTA_DEBITO",               Name = "Nota de Debito",                 SortOrder = 40, IsProtected = true,  IsSunatDocument = true,  RequiresCustomer = true,  RequiresRelatedDocument = true,  CanBeConverted = false, IsCommercialDocument = false, IsActive = true },
        // SUNAT numeric: 09 — Guía de remisión remitente
        new() { Code = "GUIA_REMISION",             Name = "Guia de remision remitente",     SortOrder = 50, IsProtected = true,  IsSunatDocument = true,  RequiresCustomer = false, RequiresRelatedDocument = false, CanBeConverted = false, IsCommercialDocument = false, IsActive = true },
        // SUNAT numeric: 31 — Guía de remisión transportista
        new() { Code = "GUIA_REMISION_TRANSP",      Name = "Guia de remision transportista", SortOrder = 55, IsProtected = true,  IsSunatDocument = true,  RequiresCustomer = false, RequiresRelatedDocument = false, CanBeConverted = false, IsCommercialDocument = false, IsActive = true },
        // Internal
        new() { Code = "COTIZACION",                Name = "Cotizacion",                     SortOrder = 60, IsProtected = true,  IsSunatDocument = false, RequiresCustomer = false, RequiresRelatedDocument = false, CanBeConverted = true,  IsCommercialDocument = true,  IsActive = true },
        new() { Code = "ORDEN_VENTA",               Name = "Orden de venta",                 SortOrder = 70, IsProtected = true,  IsSunatDocument = false, RequiresCustomer = false, RequiresRelatedDocument = false, CanBeConverted = true,  IsCommercialDocument = true,  IsActive = true }
    ];

    public static IReadOnlyList<DocumentPrintFormat> DocumentPrintFormats { get; } =
    [
        new() { Code = "A4", Name = "A4", Mode = "a4", WidthMm = null, IsActive = true },
        new() { Code = "TICKET", Name = "Ticket", Mode = "ticket", WidthMm = 80, IsActive = true },
        new() { Code = "PDF", Name = "PDF", Mode = "pdf", WidthMm = null, IsActive = true }
    ];

    public static IReadOnlyList<(string Name, string Slug)> PurchaseTypes { get; } =
    [
        ("CANCELO CLIENTE", "cancelo-cliente"),
        ("COMPROBADO", "comprobado"),
        ("CONTRAENTREGA", "contraentrega"),
        ("PREVENTA", "preventa"),
        ("SEPARADO", "separado")
    ];

    public static IReadOnlyList<(string Name, string Code, string SunatCode)> UnitMeasures { get; } =
    [
        ("S",  "S",  "S"),
        ("M",  "M",  "M"),
        ("L",  "L",  "L"),
        ("28", "28", "28"),
        ("30", "30", "30"),
        ("32", "32", "32"),
        ("34", "34", "34")
    ];

    public static IReadOnlyList<(string Name, string HexCode, string Slug)> Colors { get; } =
    [
        ("Negro", "#000000", "negro"),
        ("Blanco", "#ffffff", "blanco"),
        ("Gris", "#6b7280", "gris"),
        ("Rojo", "#ef4444", "rojo"),
        ("Azul", "#3b82f6", "azul"),
        ("Verde", "#22c55e", "verde"),
        ("Beige", "#f5f5dc", "beige"),
        ("Melange", "#9b9b9b", "melange"),
        ("Marrón", "#654321", "marron"),

    ];

    public static IReadOnlyList<(string Name, string Code)> WarehouseTypes { get; } =
    [
        ("Tienda", "STORE"),
        ("Almacén", "WAREHOUSE")
    ];

    public static IReadOnlyList<(string Name, string Code, string City, bool IsPos)> Warehouses { get; } =
    [
        ("Tienda Fisica", "TIENDA_FISICA", "Lima", true)
    ];

    public static IReadOnlyList<(string Name, string Code)> PaymentMethods { get; } =
    [
        ("Efectivo", "CASH"),
        ("Yape", "YAPE"),
        ("Plin", "PLIN"),
        ("Transferencia", "BANK_TRANSFER"),
        ("Tarjeta", "CARD")
    ];

    // Series match exactly what is registered in the Nubefact account.
    // NC prefix convention enforced by ReturnService:
    //   FC* → Nota de Crédito de Factura (doc_type "01")
    //   BC* → Nota de Crédito de Boleta  (doc_type "03")
    public static IReadOnlyList<(string DocType, string Serie, string Name, int NextNumber)> InvoiceSeries { get; } =
    [
        ("01", "FFF1", "Facturas Electronicas",                    1),
        ("03", "BBB1", "Boletas de Venta",                         1),
        ("07", "FFF1", "Notas de Credito de Factura",              1),
        ("07", "BBB1", "Notas de Credito de Boleta",               1),
        ("09", "TTT1", "Guias de Remision Remitente",              1),
        ("31", "VVV1", "Guias de Remision Transportista",          1),
    ];

    public static IReadOnlyList<(string Key, string Value, string Label, string Type, string Group)> Settings { get; } =
    [
        ("sunat_ruc", "20000000001", "RUC del emisor", "string", "sunat"),
        ("sunat_razon_social", "EMPRESA DEMO SAC", "Razon Social", "string", "sunat"),
        ("sunat_nombre_comercial", "DEMO", "Nombre Comercial", "string", "sunat"),
        ("sunat_ubigueo", "150101", "Ubigeo", "string", "sunat"),
        ("sunat_departamento", "LIMA", "Departamento", "string", "sunat"),
        ("sunat_provincia", "LIMA", "Provincia", "string", "sunat"),
        ("sunat_distrito", "LIMA", "Distrito", "string", "sunat"),
        ("sunat_direccion", "Av. Demo 123", "Direccion", "string", "sunat"),
        ("sunat_codigo_local", "0000", "Codigo de establecimiento", "string", "sunat"),
        ("sunat_sol_user", "MODDATOS", "Usuario SOL", "string", "sunat"),
        ("sunat_sol_pass", "moddatos", "Contrasena SOL", "string", "sunat"),
        ("sunat_environment", "beta", "Ambiente SUNAT", "string", "sunat"),
        ("sunat_certificate_pem", "", "Certificado digital PEM", "string", "sunat"),
        ("igv_enabled", "1", "IGV habilitado", "boolean", "fiscal"),
        ("igv_rate", "0.18", "Tasa IGV (0.18 = 18%)", "decimal", "fiscal"),
        ("prices_include_igv", "1", "Los precios ya incluyen IGV", "boolean", "fiscal"),
        ("active_modules", "dashboard.view,pos.view,orders.view,guides.view,stocks.view,customers.view,invoices.view,finance.view,users.view,config.order-statuses", "Módulos activos del sistema", "string", "system")
    ];

    public static IReadOnlyList<string> Permissions { get; } =
    [
        "pos.view",
        "orders.view", "orders.create", "orders.update", "orders.delete",
        "guides.view",
        "customers.view", "customers.create", "customers.update", "customers.delete",
        "products.view", "products.create", "products.update", "products.delete",
        "stocks.view", "stocks.create", "stocks.update", "stocks.delete", "stocks.adjust",
        "invoices.view",
        "promotions.view",
        "sales.view", "sales.import", "sales.delete",
        "dashboard.view",
        "users.view", "users.create", "users.update", "users.delete",
        "config.order-statuses", "config.shipping-agencies", "config.document-types",
        "config.purchase-types", "config.product-types", "config.colors",
        "config.warehouses", "config.provinces", "config.districts", "config.collections",
        // Finance module
        "finance.view",
        "finance.expenses.create", "finance.expenses.update", "finance.expenses.delete",
        "finance.incomes.create",  "finance.incomes.update",  "finance.incomes.delete",
        "finance.fixed.manage",
        "finance.categories.manage",
        "finance.dashboard.view"
    ];

    public static IReadOnlyList<string> Roles { get; } = ["admin", "manager", "vendedor"];

    // (code, name, type, color, icon)
    public static IReadOnlyList<(string Code, string Name, string Type, string Color, string Icon)> FinancialCategories { get; } =
    [
        // Expenses
        ("ALQUILER",       "Alquiler",              "EXPENSE", "#ef4444", "bi-house"),
        ("SERVICIOS",      "Servicios",             "EXPENSE", "#f97316", "bi-lightning"),
        ("INTERNET",       "Internet",              "EXPENSE", "#3b82f6", "bi-wifi"),
        ("SUELDOS",        "Sueldos",               "EXPENSE", "#8b5cf6", "bi-people"),
        ("PUBLICIDAD",     "Publicidad",            "EXPENSE", "#ec4899", "bi-megaphone"),
        ("MERCADERIA",     "Compra de mercadería",  "EXPENSE", "#0ea5e9", "bi-cart"),
        ("TRANSPORTE",     "Transporte",            "EXPENSE", "#f59e0b", "bi-truck"),
        ("COMISIONES",     "Comisiones",            "EXPENSE", "#10b981", "bi-percent"),
        ("OTROS_GASTOS",   "Otros gastos",          "EXPENSE", "#6b7280", "bi-three-dots"),
        // Incomes
        ("VENTAS",         "Ventas",                "INCOME",  "#22c55e", "bi-bag-check"),
        ("SERVICIOS_ING",  "Servicios prestados",   "INCOME",  "#0d9488", "bi-briefcase"),
        ("REEMBOLSOS",     "Reembolsos",            "INCOME",  "#6366f1", "bi-arrow-counterclockwise"),
        ("OTROS_INGRESOS", "Otros ingresos",        "INCOME",  "#84cc16", "bi-plus-circle"),
    ];
}
