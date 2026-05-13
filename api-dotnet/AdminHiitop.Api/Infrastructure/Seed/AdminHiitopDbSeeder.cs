using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Domain.Identity.Entities;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AdminHiitop.Api.Infrastructure.Seed;

public sealed class AdminHiitopDbSeeder
{
    private readonly AdminHiitopDbContext _context;

    public AdminHiitopDbSeeder(AdminHiitopDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await ApplySchemaPatchesAsync(cancellationToken);
        await SeedOrderStatusesAsync(cancellationToken);
        await SeedShippingAgenciesAsync(cancellationToken);
        await SeedDocumentTypesAsync(cancellationToken);
        await SeedDocumentPrintFormatsAsync(cancellationToken);
        await SeedDocumentTypePrintFormatsAsync(cancellationToken);
        await SeedPurchaseTypesAsync(cancellationToken);
        await SeedUnitMeasuresAsync(cancellationToken);
        await SeedColorsAsync(cancellationToken);
        await SeedWarehouseTypesAsync(cancellationToken);
        await SeedWarehousesAsync(cancellationToken);
        await SeedPaymentMethodsAsync(cancellationToken);
        await SeedInvoiceSeriesAsync(cancellationToken);
        await SeedSettingsAsync(cancellationToken);
        await SeedPermissionsAsync(cancellationToken);
        await SeedRolesAsync(cancellationToken);
        await SeedRolePermissionsAsync(cancellationToken);
        await SeedAdminUserAsync(cancellationToken);
    }

    /// <summary>
    /// Applies incremental schema changes that are not covered by EF Core migrations
    /// when the database was created via EnsureCreatedAsync (no migrations history).
    /// Each patch must be idempotent.
    /// </summary>
    private async Task ApplySchemaPatchesAsync(CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        // Patch: add province_id and district_id to warehouses (2026-06-03)
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'warehouses'
              AND COLUMN_NAME = 'province_id'
            """;
        var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));

        if (exists == 0)
        {
            cmd.CommandText = "ALTER TABLE `warehouses` ADD COLUMN `province_id` INT NULL, ADD COLUMN `district_id` INT NULL";
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            cmd.CommandText = "ALTER TABLE `warehouses` ADD INDEX `IX_warehouses_province_id` (`province_id`), ADD INDEX `IX_warehouses_district_id` (`district_id`)";
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            cmd.CommandText = "ALTER TABLE `warehouses` ADD CONSTRAINT `FK_warehouses_provinces_province_id` FOREIGN KEY (`province_id`) REFERENCES `provinces` (`id`) ON DELETE SET NULL";
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            cmd.CommandText = "ALTER TABLE `warehouses` ADD CONSTRAINT `FK_warehouses_districts_district_id` FOREIGN KEY (`district_id`) REFERENCES `districts` (`id`) ON DELETE SET NULL";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task SeedOrderStatusesAsync(CancellationToken cancellationToken)
    {
        foreach (OrderStatus item in HiitopSeedData.OrderStatuses)
        {
            OrderStatus? existing = await _context.OrderStatuses.FirstOrDefaultAsync(x => x.Slug == item.Slug, cancellationToken);
            if (existing is null)
            {
                _context.OrderStatuses.Add(item);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedShippingAgenciesAsync(CancellationToken cancellationToken)
    {
        foreach ((string code, string name) in HiitopSeedData.ShippingAgencies)
        {
            bool exists = await _context.ShippingAgencies.AnyAsync(x => x.Code == code, cancellationToken);
            if (!exists)
            {
                _context.ShippingAgencies.Add(new ShippingAgency { Code = code, Name = name });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedDocumentTypesAsync(CancellationToken cancellationToken)
    {
        foreach (DocumentType item in HiitopSeedData.DocumentTypes)
        {
            bool exists = await _context.DocumentTypes.AnyAsync(x => x.Code == item.Code, cancellationToken);
            if (!exists)
            {
                _context.DocumentTypes.Add(item);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedDocumentPrintFormatsAsync(CancellationToken cancellationToken)
    {
        foreach (DocumentPrintFormat item in HiitopSeedData.DocumentPrintFormats)
        {
            bool exists = await _context.DocumentPrintFormats.AnyAsync(x => x.Code == item.Code, cancellationToken);
            if (!exists)
            {
                _context.DocumentPrintFormats.Add(item);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedDocumentTypePrintFormatsAsync(CancellationToken cancellationToken)
    {
        List<DocumentType> documentTypes = await _context.DocumentTypes.ToListAsync(cancellationToken);
        List<DocumentPrintFormat> printFormats = await _context.DocumentPrintFormats.ToListAsync(cancellationToken);

        if (documentTypes.Count == 0 || printFormats.Count == 0)
        {
            return;
        }

        DocumentPrintFormat? pdfFormat = printFormats.FirstOrDefault(item => item.Code == "PDF");
        DocumentPrintFormat? ticketFormat = printFormats.FirstOrDefault(item => item.Code == "TICKET");
        DocumentPrintFormat? a4Format = printFormats.FirstOrDefault(item => item.Code == "A4");

        foreach (DocumentType documentType in documentTypes)
        {
            List<(DocumentPrintFormat Format, bool IsDefault)> desiredFormats = new();

            if (documentType.Code is "BOLETA" or "FACTURA" or "NOTA_CREDITO" or "NOTA_DEBITO")
            {
                if (pdfFormat is not null)
                {
                    desiredFormats.Add((pdfFormat, true));
                }

                if (ticketFormat is not null)
                {
                    desiredFormats.Add((ticketFormat, false));
                }

                if (a4Format is not null)
                {
                    desiredFormats.Add((a4Format, false));
                }
            }
            else
            {
                if (pdfFormat is not null)
                {
                    desiredFormats.Add((pdfFormat, true));
                }
            }

            foreach ((DocumentPrintFormat format, bool isDefault) in desiredFormats)
            {
                DocumentTypePrintFormat? existing = await _context.DocumentTypePrintFormats.FirstOrDefaultAsync(
                    item => item.DocumentTypeId == documentType.Id && item.DocumentPrintFormatId == format.Id,
                    cancellationToken);

                if (existing is null)
                {
                    _context.DocumentTypePrintFormats.Add(new DocumentTypePrintFormat
                    {
                        DocumentTypeId = documentType.Id,
                        DocumentPrintFormatId = format.Id,
                        IsDefault = isDefault
                    });
                }
                else
                {
                    existing.IsDefault = isDefault;
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPurchaseTypesAsync(CancellationToken cancellationToken)
    {
        foreach ((string name, string slug) in HiitopSeedData.PurchaseTypes)
        {
            bool exists = await _context.PurchaseTypes.AnyAsync(x => x.Slug == slug, cancellationToken);
            if (!exists)
            {
                _context.PurchaseTypes.Add(new PurchaseType { Name = name, Slug = slug });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedUnitMeasuresAsync(CancellationToken cancellationToken)
    {
        foreach ((string name, string code, string sunatCode) in HiitopSeedData.UnitMeasures)
        {
            bool exists = await _context.UnitMeasures.AnyAsync(x => x.Code == code, cancellationToken);
            if (!exists)
            {
                _context.UnitMeasures.Add(new UnitMeasure { Name = name, Code = code, SunatCode = sunatCode });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedColorsAsync(CancellationToken cancellationToken)
    {
        foreach ((string name, string hexCode, string slug) in HiitopSeedData.Colors)
        {
            bool exists = await _context.Colors.AnyAsync(x => x.Slug == slug, cancellationToken);
            if (!exists)
            {
                _context.Colors.Add(new Color { Name = name, HexCode = hexCode, Slug = slug });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedWarehouseTypesAsync(CancellationToken cancellationToken)
    {
        foreach ((string name, string code) in HiitopSeedData.WarehouseTypes)
        {
            bool exists = await _context.WarehouseTypes.AnyAsync(x => x.Code == code, cancellationToken);
            if (!exists)
            {
                _context.WarehouseTypes.Add(new WarehouseType { Name = name, Code = code });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedWarehousesAsync(CancellationToken cancellationToken)
    {
        int? storeTypeId = await _context.WarehouseTypes.Where(x => x.Code == "STORE").Select(x => (int?)x.Id).FirstOrDefaultAsync(cancellationToken);

        foreach ((string name, string code, string city, bool isPos) in HiitopSeedData.Warehouses)
        {
            bool exists = await _context.Warehouses.AnyAsync(x => x.Code == code, cancellationToken);
            if (!exists)
            {
                _context.Warehouses.Add(new Warehouse
                {
                    Name = name,
                    Code = code,
                    City = city,
                    IsPos = isPos,
                    Type = "store",
                    WarehouseTypeId = storeTypeId
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPaymentMethodsAsync(CancellationToken cancellationToken)
    {
        foreach ((string name, string code) in HiitopSeedData.PaymentMethods)
        {
            bool exists = await _context.PaymentMethods.AnyAsync(x => x.Code == code, cancellationToken);
            if (!exists)
            {
                _context.PaymentMethods.Add(new PaymentMethod { Name = name, Code = code });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedInvoiceSeriesAsync(CancellationToken cancellationToken)
    {
        foreach ((string docType, string serie, int nextNumber) in HiitopSeedData.InvoiceSeries)
        {
            bool exists = await _context.InvoiceSeries.AnyAsync(x => x.Serie == serie, cancellationToken);
            if (!exists)
            {
                _context.InvoiceSeries.Add(new InvoiceSeries { DocType = docType, Serie = serie, NextNumber = nextNumber });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSettingsAsync(CancellationToken cancellationToken)
    {
        foreach ((string key, string value, string label, string type, string group) in HiitopSeedData.Settings)
        {
            bool exists = await _context.Settings.AnyAsync(x => x.Key == key, cancellationToken);
            if (!exists)
            {
                _context.Settings.Add(new Setting { Key = key, Value = value, Label = label, Type = type, Group = group });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        foreach (string name in HiitopSeedData.Permissions)
        {
            bool exists = await _context.Permissions.AnyAsync(x => x.Name == name && x.GuardName == "api", cancellationToken);
            if (!exists)
            {
                _context.Permissions.Add(new Permission { Name = name, GuardName = "api" });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        foreach (string roleName in HiitopSeedData.Roles)
        {
            bool exists = await _context.Roles.AnyAsync(x => x.Name == roleName && x.GuardName == "api", cancellationToken);
            if (!exists)
            {
                _context.Roles.Add(new Role { Name = roleName, GuardName = "api" });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAdminUserAsync(CancellationToken cancellationToken)
    {
        User? adminUser = await _context.Users.FirstOrDefaultAsync(x => x.Email == "admin@hiitop.com", cancellationToken);
        if (adminUser is null)
        {
            adminUser = new User
            {
                Name = "Admin",
                Email = "admin@hiitop.com",
                Password = BCrypt.Net.BCrypt.HashPassword("password"),
                IsActive = true
            };

            _context.Users.Add(adminUser);
            await _context.SaveChangesAsync(cancellationToken);
        }

        int? adminRoleId = await _context.Roles
            .Where(x => x.Name == "admin" && x.GuardName == "api")
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!adminRoleId.HasValue)
        {
            return;
        }

        bool hasAdminRole = await _context.ModelHasRoles.AnyAsync(
            x => x.RoleId == adminRoleId.Value
                && x.ModelId == adminUser.Id
                && x.ModelType == ModelHasRole.UserModelType,
            cancellationToken);

        if (!hasAdminRole)
        {
            _context.ModelHasRoles.Add(new ModelHasRole
            {
                RoleId = adminRoleId.Value,
                ModelId = adminUser.Id,
                ModelType = ModelHasRole.UserModelType
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRolePermissionsAsync(CancellationToken cancellationToken)
    {
        Role? adminRole = await _context.Roles.FirstOrDefaultAsync(
            x => x.Name == "admin" && x.GuardName == "api",
            cancellationToken);

        if (adminRole is null)
        {
            return;
        }

        int[] assignedPermissionIds = await _context.RoleHasPermissions
            .Where(x => x.RoleId == adminRole.Id)
            .Select(x => x.PermissionId)
            .ToArrayAsync(cancellationToken);

        List<int> missingPermissionIds = await _context.Permissions
            .Where(x => x.GuardName == "api" && !assignedPermissionIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (missingPermissionIds.Count == 0)
        {
            return;
        }

        foreach (int permissionId in missingPermissionIds)
        {
            _context.RoleHasPermissions.Add(new RoleHasPermission
            {
                RoleId = adminRole.Id,
                PermissionId = permissionId
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
