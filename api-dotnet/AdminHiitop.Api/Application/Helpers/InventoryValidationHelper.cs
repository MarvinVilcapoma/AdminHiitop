using AdminHiitop.Api.Application.DTOs.Products;
using AdminHiitop.Api.Application.DTOs.Stocks;
using AdminHiitop.Api.Shared.Exceptions;

namespace AdminHiitop.Api.Application.Helpers;

public static class InventoryValidationHelper
{
    public static void ValidateProduct(ProductUpsertRequest request)
    {
        if (request is null)
        {
            throw new AppException("La solicitud del producto es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new AppException("El nombre del producto es obligatorio.");
        }

        if (request.BasePrice < 0 || request.UnitCost < 0)
        {
            throw new AppException("Los montos del producto no pueden ser negativos.");
        }
    }

    public static void ValidateStock(StockUpsertRequest request)
    {
        if (request is null)
        {
            throw new AppException("La solicitud del stock es obligatoria.");
        }

        if (request.ProductId <= 0 || request.WarehouseId <= 0)
        {
            throw new AppException("Producto y almacén son obligatorios.");
        }

        if (request.Quantity < 0 || request.Reserved < 0)
        {
            throw new AppException("La cantidad y la reserva no pueden ser negativas.");
        }

        if (request.Reserved > request.Quantity)
        {
            throw new AppException("La reserva no puede ser mayor al stock.");
        }
    }

    public static void ValidateAdjust(StockAdjustRequest request)
    {
        if (request is null)
        {
            throw new AppException("La solicitud de ajuste es obligatoria.");
        }

        if (request.Quantity < 0)
        {
            throw new AppException("La cantidad ajustada no puede ser negativa.");
        }
    }

    public static void ValidateTransfer(StockTransferRequest request)
    {
        if (request is null)
        {
            throw new AppException("La solicitud de transferencia es obligatoria.");
        }

        if (request.DestinationWarehouseId <= 0)
        {
            throw new AppException("El almacén destino es obligatorio.");
        }

        if (request.Quantity <= 0)
        {
            throw new AppException("La cantidad transferida debe ser mayor a cero.");
        }
    }

    public static void ValidateBulkTransfer(StockBulkTransferRequest request)
    {
        if (request is null || request.Items.Count == 0)
        {
            throw new AppException("La transferencia masiva requiere al menos un item.");
        }

        foreach (StockTransferItemRequest item in request.Items)
        {
            if (item.StockId <= 0 || item.TargetWarehouseId <= 0 || item.Quantity <= 0)
            {
                throw new AppException("La transferencia masiva contiene datos inválidos.");
            }
        }
    }
}
