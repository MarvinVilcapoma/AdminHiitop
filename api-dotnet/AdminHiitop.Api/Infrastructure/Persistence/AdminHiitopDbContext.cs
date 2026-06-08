using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Domain.Common;
using AdminHiitop.Api.Domain.Finance.Entities;
using AdminHiitop.Api.Domain.Identity.Entities;
using AdminHiitop.Api.Domain.Inventory.Entities;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Domain.Shopify.Entities;
using AdminHiitop.Api.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AdminHiitop.Api.Infrastructure.Persistence;

public sealed class AdminHiitopDbContext : DbContext
{
    public AdminHiitopDbContext(DbContextOptions<AdminHiitopDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<OrderStatus> OrderStatuses => Set<OrderStatus>();
    public DbSet<ShippingAgency> ShippingAgencies => Set<ShippingAgency>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<DocumentPrintFormat> DocumentPrintFormats => Set<DocumentPrintFormat>();
    public DbSet<ProductType> ProductTypes => Set<ProductType>();
    public DbSet<Color> Colors => Set<Color>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseType> WarehouseTypes => Set<WarehouseType>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<PurchaseType> PurchaseTypes => Set<PurchaseType>();
    public DbSet<UnitMeasure> UnitMeasures => Set<UnitMeasure>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Size> Sizes => Set<Size>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SaleImport> SaleImports => Set<SaleImport>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<PromotionItem> PromotionItems => Set<PromotionItem>();
    public DbSet<InvoiceSeries> InvoiceSeries => Set<InvoiceSeries>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceDeliveryLog> InvoiceDeliveryLogs => Set<InvoiceDeliveryLog>();
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    public DbSet<ReturnRequestItem> ReturnRequestItems => Set<ReturnRequestItem>();
    public DbSet<CustomerCredit> CustomerCredits => Set<CustomerCredit>();
    public DbSet<DailySummary> DailySummaries => Set<DailySummary>();
    public DbSet<DailySummaryItem> DailySummaryItems => Set<DailySummaryItem>();
    public DbSet<SunatSendLog> SunatSendLogs => Set<SunatSendLog>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RoleHasPermission> RoleHasPermissions => Set<RoleHasPermission>();
    public DbSet<ModelHasRole> ModelHasRoles => Set<ModelHasRole>();
    public DbSet<ProductColor> ProductColors => Set<ProductColor>();
    public DbSet<DocumentTypePrintFormat> DocumentTypePrintFormats => Set<DocumentTypePrintFormat>();
    public DbSet<ShopifyTransfer>           ShopifyTransfers           => Set<ShopifyTransfer>();
    public DbSet<ShopifyStoreConnection>    ShopifyStoreConnections    => Set<ShopifyStoreConnection>();
    public DbSet<ShopifyLocation>           ShopifyLocations           => Set<ShopifyLocation>();

    // Finance module
    public DbSet<FinancialCategory>         FinancialCategories        => Set<FinancialCategory>();
    public DbSet<FinancialMovement>         FinancialMovements         => Set<FinancialMovement>();
    public DbSet<FixedFinancialMovement>    FixedFinancialMovements    => Set<FixedFinancialMovement>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInformation();
        return base.SaveChanges();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureSnakeCaseColumns(modelBuilder);
        ConfigureIdentitySchema(modelBuilder);
        ConfigureLaravelTableNames(modelBuilder);
        ConfigureSchemaCompatibility(modelBuilder);

        modelBuilder.Entity<ShopifyTransfer>().ToTable("shopify_transfers");
        modelBuilder.Entity<ShopifyTransfer>().HasKey(t => t.Id);
        modelBuilder.Entity<ShopifyTransfer>().Property(t => t.Id).ValueGeneratedOnAdd();

        modelBuilder.Entity<ShopifyStoreConnection>().ToTable("shopify_store_connections");
        modelBuilder.Entity<ShopifyStoreConnection>().HasKey(t => t.Id);
        modelBuilder.Entity<ShopifyStoreConnection>().Property(t => t.Id).ValueGeneratedOnAdd();
        modelBuilder.Entity<ShopifyStoreConnection>().HasIndex(t => t.ShopDomain).IsUnique();

        modelBuilder.Entity<ShopifyLocation>().ToTable("shopify_locations");
        modelBuilder.Entity<ShopifyLocation>().HasKey(t => t.Id);
        modelBuilder.Entity<ShopifyLocation>().Property(t => t.Id).ValueGeneratedOnAdd();
        modelBuilder.Entity<ShopifyLocation>().HasIndex(t => t.ShopifyLocationId).IsUnique();
        modelBuilder.Entity<ShopifyLocation>()
            .HasOne(t => t.LocalWarehouse)
            .WithMany()
            .HasForeignKey(t => t.LocalWarehouseId)
            .OnDelete(DeleteBehavior.SetNull);

        // Finance entities
        modelBuilder.Entity<FinancialCategory>().ToTable("financial_categories");
        modelBuilder.Entity<FinancialMovement>().ToTable("financial_movements");
        modelBuilder.Entity<FixedFinancialMovement>().ToTable("fixed_financial_movements");

        modelBuilder.Entity<FinancialMovement>()
            .HasOne(m => m.Category)
            .WithMany()
            .HasForeignKey(m => m.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FixedFinancialMovement>()
            .HasOne(m => m.Category)
            .WithMany()
            .HasForeignKey(m => m.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FinancialMovement>().Property(m => m.Amount).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<FixedFinancialMovement>().Property(m => m.Amount).HasColumnType("decimal(12,2)");

        modelBuilder.Entity<FinancialCategory>().HasIndex(c => c.Code).IsUnique();

        modelBuilder.Entity<Setting>().HasKey(item => item.Key);
        modelBuilder.Entity<Setting>().Property(item => item.Key).HasMaxLength(200);
        modelBuilder.Entity<Setting>().Property(item => item.Type).HasMaxLength(50);
        modelBuilder.Entity<Setting>().Property(item => item.Group).HasMaxLength(100);

        modelBuilder.Entity<ProductColor>().HasKey(item => new { item.ProductId, item.ColorId });
        modelBuilder.Entity<DocumentTypePrintFormat>().HasKey(item => new { item.DocumentTypeId, item.DocumentPrintFormatId });
        modelBuilder.Entity<RoleHasPermission>().HasKey(item => new { item.PermissionId, item.RoleId });
        modelBuilder.Entity<ModelHasRole>().HasKey(item => new { item.RoleId, item.ModelId, item.ModelType });

        modelBuilder.Entity<Product>().HasIndex(item => item.Sku).IsUnique().HasFilter("[Sku] IS NOT NULL");
        modelBuilder.Entity<Order>().HasIndex(item => item.OrderNumber).IsUnique();
        modelBuilder.Entity<Invoice>().HasIndex(item => item.FullNumber).IsUnique();

        modelBuilder.Entity<InvoiceDeliveryLog>().ToTable("invoice_delivery_logs");
        modelBuilder.Entity<InvoiceDeliveryLog>().HasKey(l => l.Id);
        modelBuilder.Entity<InvoiceDeliveryLog>().Property(l => l.Id).ValueGeneratedOnAdd();
        modelBuilder.Entity<InvoiceDeliveryLog>()
            .HasOne(l => l.Invoice)
            .WithMany(i => i.DeliveryLogs)
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Returns module
        modelBuilder.Entity<ReturnRequest>().ToTable("return_requests");
        modelBuilder.Entity<ReturnRequest>().HasKey(r => r.Id);
        modelBuilder.Entity<ReturnRequest>().Property(r => r.Id).ValueGeneratedOnAdd();
        modelBuilder.Entity<ReturnRequest>()
            .HasMany(r => r.Items)
            .WithOne(i => i.ReturnRequest)
            .HasForeignKey(i => i.ReturnRequestId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ReturnRequest>()
            .HasOne(r => r.CustomerCredit)
            .WithOne(c => c.ReturnRequest)
            .HasForeignKey<CustomerCredit>(c => c.ReturnRequestId);
        modelBuilder.Entity<ReturnRequest>()
            .HasOne(r => r.Customer)
            .WithMany()
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
        modelBuilder.Entity<ReturnRequest>()
            .HasOne(r => r.OriginalInvoice)
            .WithMany()
            .HasForeignKey(r => r.OriginalInvoiceId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
        modelBuilder.Entity<ReturnRequest>()
            .HasOne(r => r.CreditNoteInvoice)
            .WithMany()
            .HasForeignKey(r => r.CreditNoteInvoiceId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        modelBuilder.Entity<ReturnRequestItem>().ToTable("return_request_items");
        modelBuilder.Entity<ReturnRequestItem>().HasKey(i => i.Id);
        modelBuilder.Entity<ReturnRequestItem>().Property(i => i.Id).ValueGeneratedOnAdd();
        modelBuilder.Entity<ReturnRequestItem>()
            .HasOne(i => i.OrderItem)
            .WithMany()
            .HasForeignKey(i => i.OrderItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        modelBuilder.Entity<CustomerCredit>().ToTable("customer_credits");
        modelBuilder.Entity<CustomerCredit>().HasKey(c => c.Id);
        modelBuilder.Entity<CustomerCredit>().Property(c => c.Id).ValueGeneratedOnAdd();
        modelBuilder.Entity<CustomerCredit>().Property(c => c.Amount).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<CustomerCredit>().Property(c => c.UsedAmount).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<CustomerCredit>().Property(c => c.RemainingAmount).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<CustomerCredit>()
            .HasOne(c => c.Customer)
            .WithMany()
            .HasForeignKey(c => c.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InvoiceSeries>().HasIndex(item => item.Serie).IsUnique();
        modelBuilder.Entity<DocumentType>().HasIndex(item => item.Code).IsUnique();
        modelBuilder.Entity<DocumentPrintFormat>().HasIndex(item => item.Code).IsUnique();
        modelBuilder.Entity<OrderStatus>().HasIndex(item => item.Slug).IsUnique();
        modelBuilder.Entity<ShippingAgency>().HasIndex(item => item.Code).IsUnique();
        modelBuilder.Entity<Warehouse>().HasIndex(item => item.Code).IsUnique();
        modelBuilder.Entity<WarehouseType>().HasIndex(item => item.Code).IsUnique();
        modelBuilder.Entity<Province>().HasIndex(item => item.Code).IsUnique();
        modelBuilder.Entity<UnitMeasure>().HasIndex(item => item.Code).IsUnique();
        modelBuilder.Entity<Color>().HasIndex(item => item.Slug).IsUnique();
        modelBuilder.Entity<ProductType>().HasIndex(item => item.Slug).IsUnique();
        modelBuilder.Entity<Permission>().HasIndex(item => new { item.Name, item.GuardName }).IsUnique();
        modelBuilder.Entity<Role>().HasIndex(item => new { item.Name, item.GuardName }).IsUnique();

        modelBuilder.Entity<Order>().Property(item => item.Total).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Order>().Property(item => item.DeliveryCost).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Order>().Property(item => item.GuideTotalWeight).HasColumnType("decimal(12,3)");
        modelBuilder.Entity<Order>().Property(item => item.PickupKey).HasMaxLength(120);
        modelBuilder.Entity<Order>().Property(item => item.TrackingNumber).HasMaxLength(120);
        modelBuilder.Entity<OrderItem>().Property(item => item.ProductKey).HasMaxLength(120);
        modelBuilder.Entity<OrderItem>().Property(item => item.TrackingNumber).HasMaxLength(120);
        modelBuilder.Entity<OrderItem>().Property(item => item.UnitPrice).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<OrderItem>().Property(item => item.Subtotal).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Product>().Property(item => item.BasePrice).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Product>().Property(item => item.UnitCost).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Invoice>().Property(item => item.MtoOperGravadas).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Invoice>().Property(item => item.MtoIgv).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Invoice>().Property(item => item.ValorVenta).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Invoice>().Property(item => item.SubTotal).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Invoice>().Property(item => item.MtoImpVenta).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Promotion>().Property(item => item.FixedPrice).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<PromotionItem>().Property(item => item.UnitPrice).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Sale>().Property(item => item.TotalNet).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Sale>().Property(item => item.TotalTax).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Sale>().Property(item => item.TotalGross).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Sale>().Property(item => item.DiscountNet).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<Sale>().Property(item => item.DiscountGross).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<SaleItem>().Property(item => item.ListPrice).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<SaleItem>().Property(item => item.UnitNetPrice).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<SaleItem>().Property(item => item.UnitGrossPrice).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<SaleItem>().Property(item => item.Quantity).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<SaleItem>().Property(item => item.TotalNet).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<SaleItem>().Property(item => item.TotalTax).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<SaleItem>().Property(item => item.TotalGross).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<SaleItem>().Property(item => item.DiscountNet).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<SaleItem>().Property(item => item.DiscountGross).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<SaleItem>().Property(item => item.DiscountPct).HasColumnType("decimal(12,4)");
        modelBuilder.Entity<SaleItem>().Property(item => item.UnitCostNet).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<SaleItem>().Property(item => item.TotalCostNet).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<SaleItem>().Property(item => item.Margin).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<SaleItem>().Property(item => item.MarginPct).HasColumnType("decimal(12,4)");
        modelBuilder.Entity<ShippingAgency>().Property(item => item.ShippingRate).HasColumnType("decimal(12,2)");
        modelBuilder.Entity<DailySummaryItem>().Property(item => item.Total).HasColumnType("decimal(12,2)");

        ConfigureSoftDeleteFilters(modelBuilder);
    }

    private void ApplyAuditInformation()
    {
        DateTime utcNow = PeruClock.Now;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = utcNow;
                entry.Entity.UpdatedAt = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
            }
            else if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.DeletedAt = utcNow;
                entry.Entity.UpdatedAt = utcNow;
            }
        }
    }

    private static void ConfigureSoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!SupportsSoftDelete(entityType.ClrType))
            {
                continue;
            }

            if (typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(AdminHiitopDbContext)
                    .GetMethod(nameof(SetSoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);

                method.Invoke(null, new object[] { modelBuilder });
            }
        }
    }

    private static void SetSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : AuditableEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(item => item.DeletedAt == null);
    }

    private static bool SupportsSoftDelete(Type clrType)
    {
        return clrType == typeof(User)
            || clrType == typeof(OrderStatus)
            || clrType == typeof(ShippingAgency)
            || clrType == typeof(DocumentType)
            || clrType == typeof(DocumentPrintFormat)
            || clrType == typeof(ProductType)
            || clrType == typeof(Color)
            || clrType == typeof(Warehouse)
            || clrType == typeof(WarehouseType)
            || clrType == typeof(Collection)
            || clrType == typeof(PaymentMethod)
            || clrType == typeof(PurchaseType)
            || clrType == typeof(UnitMeasure)
            || clrType == typeof(Customer)
            || clrType == typeof(Product)
            || clrType == typeof(Stock)
            || clrType == typeof(Order)
            || clrType == typeof(OrderItem)
            || clrType == typeof(Promotion)
            || clrType == typeof(PromotionItem)
            || clrType == typeof(Invoice)
            || clrType == typeof(DailySummary)
            || clrType == typeof(FinancialCategory)
            || clrType == typeof(FinancialMovement)
            || clrType == typeof(FixedFinancialMovement);
    }

    private static void ConfigureIdentitySchema(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Name).HasColumnName("name");
            entity.Property(item => item.Email).HasColumnName("email");
            entity.Property(item => item.Password).HasColumnName("password");
            entity.Property(item => item.IsActive).HasColumnName("is_active");
            entity.Property(item => item.EmailVerifiedAt).HasColumnName("email_verified_at");
            entity.Property(item => item.RememberToken).HasColumnName("remember_token");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Name).HasColumnName("name");
            entity.Property(item => item.GuardName).HasColumnName("guard_name");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.Ignore(item => item.DeletedAt);
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("permissions");
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Name).HasColumnName("name");
            entity.Property(item => item.GuardName).HasColumnName("guard_name");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.Ignore(item => item.DeletedAt);
        });

        modelBuilder.Entity<ModelHasRole>(entity =>
        {
            entity.ToTable("model_has_roles");
            entity.Property(item => item.RoleId).HasColumnName("role_id");
            entity.Property(item => item.ModelId).HasColumnName("model_id");
            entity.Property(item => item.ModelType).HasColumnName("model_type");
        });

        modelBuilder.Entity<RoleHasPermission>(entity =>
        {
            entity.ToTable("role_has_permissions");
            entity.Property(item => item.RoleId).HasColumnName("role_id");
            entity.Property(item => item.PermissionId).HasColumnName("permission_id");
        });
    }

    private static void ConfigureSchemaCompatibility(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.Ignore(item => item.UnitMeasureId);
            entity.Ignore(item => item.UnitMeasure);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.Property(item => item.SunatNotesJson).HasColumnName("sunat_notes");
        });

        modelBuilder.Entity<DailySummary>(entity =>
        {
            entity.Property(item => item.SunatNotesJson).HasColumnName("sunat_notes");
        });

        modelBuilder.Entity<Province>().Ignore(item => item.DeletedAt);
        modelBuilder.Entity<District>().Ignore(item => item.DeletedAt);
        modelBuilder.Entity<InvoiceSeries>().Ignore(item => item.DeletedAt);

        modelBuilder.Entity<Size>(entity =>
        {
            entity.Ignore(item => item.Code);
            entity.Ignore(item => item.IsActive);
            entity.Ignore(item => item.DeletedAt);
        });

        modelBuilder.Entity<ProductType>()
            .HasMany(item => item.Sizes)
            .WithMany(item => item.ProductTypes)
            .UsingEntity<Dictionary<string, object>>(
                "product_type_size",
                right => right
                    .HasOne<Size>()
                    .WithMany()
                    .HasForeignKey("size_id"),
                left => left
                    .HasOne<ProductType>()
                    .WithMany()
                    .HasForeignKey("product_type_id"),
                join =>
                {
                    join.ToTable("product_type_size");
                    join.HasKey("product_type_id", "size_id");
                    join.Property<int>("sort_order").HasColumnName("sort_order");
                });
    }

    private static void ConfigureSnakeCaseColumns(ModelBuilder modelBuilder)
    {
        foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            Type clrType = entityType.ClrType;

            if (clrType == typeof(Dictionary<string, object>))
            {
                continue;
            }

            if (clrType == typeof(DocumentTypePrintFormat))
            {
                continue;
            }

            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableProperty property in entityType.GetProperties())
            {
                if (property.IsShadowProperty())
                {
                    continue;
                }

                modelBuilder.Entity(clrType)
                    .Property(property.Name)
                    .HasColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        System.Text.StringBuilder builder = new();

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];

            if (char.IsUpper(character) && index > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static void ConfigureLaravelTableNames(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().ToTable("products");
        modelBuilder.Entity<Customer>().ToTable("customers");
        modelBuilder.Entity<Order>().ToTable("orders");
        modelBuilder.Entity<Invoice>().ToTable("invoices");
        modelBuilder.Entity<OrderStatus>().ToTable("order_statuses");
        modelBuilder.Entity<OrderItem>().ToTable("order_items");
        modelBuilder.Entity<InvoiceSeries>().ToTable("invoice_series");
        modelBuilder.Entity<Stock>().ToTable("stocks");
        modelBuilder.Entity<StockMovement>().ToTable("stock_movements");
        modelBuilder.Entity<Warehouse>().ToTable("warehouses");
        modelBuilder.Entity<PaymentMethod>().ToTable("payment_methods");
        modelBuilder.Entity<ProductType>().ToTable("product_types");
        modelBuilder.Entity<PurchaseType>().ToTable("purchase_types");
        modelBuilder.Entity<ShippingAgency>().ToTable("shipping_agencies");
        modelBuilder.Entity<DocumentType>().ToTable("document_types");
        modelBuilder.Entity<DocumentPrintFormat>().ToTable("document_print_formats");
        modelBuilder.Entity<Collection>().ToTable("collections");
        modelBuilder.Entity<Color>().ToTable("colors");
        modelBuilder.Entity<UnitMeasure>().ToTable("unit_measures");
        modelBuilder.Entity<WarehouseType>().ToTable("warehouse_types");
        modelBuilder.Entity<Province>().ToTable("provinces");
        modelBuilder.Entity<District>().ToTable("districts");
        modelBuilder.Entity<Setting>().ToTable("settings");
        modelBuilder.Entity<Promotion>().ToTable("promotions");
        modelBuilder.Entity<PromotionItem>().ToTable("promotion_items");
        modelBuilder.Entity<Sale>().ToTable("sales");
        modelBuilder.Entity<SaleItem>().ToTable("sale_items");
        modelBuilder.Entity<SaleImport>().ToTable("sale_imports");
        modelBuilder.Entity<DailySummary>().ToTable("daily_summaries");
        modelBuilder.Entity<DailySummaryItem>().ToTable("daily_summary_items");
        modelBuilder.Entity<SunatSendLog>().ToTable("sunat_send_logs");
        modelBuilder.Entity<Size>().ToTable("sizes");
        modelBuilder.Entity<ProductColor>().ToTable("product_color");
        modelBuilder.Entity<DocumentTypePrintFormat>().ToTable("document_type_print_format");
    }
}
