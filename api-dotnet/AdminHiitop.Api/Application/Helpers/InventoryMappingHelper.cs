using AdminHiitop.Api.Application.DTOs.Products;
using AdminHiitop.Api.Application.DTOs.Stocks;
using AdminHiitop.Api.Domain.Catalog.Entities;
using AdminHiitop.Api.Domain.Inventory.Entities;

namespace AdminHiitop.Api.Application.Helpers;

public static class InventoryMappingHelper
{
    public static ProductDetailResponse MapProductDetail(Product product)
    {
        return new ProductDetailResponse
        {
            Id = product.Id,
            Name = product.Name,
            Sku = product.Sku,
            ProductTypeId = product.ProductTypeId,
            CollectionId = product.CollectionId,
            UnitMeasureId = product.UnitMeasureId,
            Description = product.Description,
            BasePrice = product.BasePrice,
            UnitCost = product.UnitCost,
            IsActive = product.IsActive,
            ProductType = MapCatalogReference(product.ProductType),
            Collection = MapCatalogReference(product.Collection),
            UnitMeasure = MapCatalogReference(product.UnitMeasure),
            Colors = product.ProductColors
                .Where(item => item.Color is not null)
                .Select(item => new ProductColorResponse
                {
                    Id = item.Color!.Id,
                    Name = item.Color.Name,
                    HexCode = item.Color.HexCode
                })
                .ToList(),
            TotalStock = product.Stocks.Sum(item => item.Quantity)
        };
    }

    public static StockResponse MapStock(Stock stock)
    {
        return new StockResponse
        {
            Id = stock.Id,
            ProductId = stock.ProductId,
            WarehouseId = stock.WarehouseId,
            ColorId = stock.ColorId,
            Size = stock.Size,
            Quantity = stock.Quantity,
            Reserved = stock.Reserved,
            Available = stock.Quantity - stock.Reserved,
            Product = stock.Product is null ? null : new StockProductReferenceResponse
            {
                Id = stock.Product.Id,
                Name = stock.Product.Name,
                Sku = stock.Product.Sku,
                ProductType = MapStockCatalogReference(stock.Product.ProductType),
                Collection = MapStockCatalogReference(stock.Product.Collection)
            },
            Warehouse = stock.Warehouse is null ? null : new StockWarehouseReferenceResponse
            {
                Id = stock.Warehouse.Id,
                Name = stock.Warehouse.Name,
                Type = stock.Warehouse.Type
            },
            Color = stock.Color is null ? null : new StockColorReferenceResponse
            {
                Id = stock.Color.Id,
                Name = stock.Color.Name,
                HexCode = stock.Color.HexCode
            }
        };
    }

    private static ProductCatalogReferenceResponse? MapCatalogReference(ProductType? entity)
    {
        if (entity is null)
        {
            return null;
        }

        return new ProductCatalogReferenceResponse
        {
            Id = entity.Id,
            Name = entity.Name
        };
    }

    private static ProductCatalogReferenceResponse? MapCatalogReference(Collection? entity)
    {
        if (entity is null)
        {
            return null;
        }

        return new ProductCatalogReferenceResponse
        {
            Id = entity.Id,
            Name = entity.Name
        };
    }

    private static ProductCatalogReferenceResponse? MapCatalogReference(UnitMeasure? entity)
    {
        if (entity is null)
        {
            return null;
        }

        return new ProductCatalogReferenceResponse
        {
            Id = entity.Id,
            Name = entity.Name
        };
    }

    private static StockCatalogReferenceResponse? MapStockCatalogReference(ProductType? entity)
    {
        if (entity is null)
        {
            return null;
        }

        return new StockCatalogReferenceResponse
        {
            Id = entity.Id,
            Name = entity.Name
        };
    }

    private static StockCatalogReferenceResponse? MapStockCatalogReference(Collection? entity)
    {
        if (entity is null)
        {
            return null;
        }

        return new StockCatalogReferenceResponse
        {
            Id = entity.Id,
            Name = entity.Name
        };
    }
}
