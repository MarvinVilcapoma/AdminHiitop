using AdminHiitop.Api.Application.DTOs.Common;
using AdminHiitop.Api.Application.DTOs.Stocks;
using AdminHiitop.Api.Application.Helpers;
using AdminHiitop.Api.Application.Interfaces.Repositories;
using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Inventory.Entities;
using AdminHiitop.Api.Shared.Exceptions;

namespace AdminHiitop.Api.Application.Services.Stocks;

public sealed class StockService : IStockService
{
    private readonly IStockRepository _stockRepository;

    public StockService(IStockRepository stockRepository)
    {
        _stockRepository = stockRepository;
    }

    public async Task<object> GetAsync(StockQueryRequest request)
    {
        return await _stockRepository.GetPagedAsync(request);
    }

    public async Task<IReadOnlyList<StockSummaryResponse>> GetSummaryAsync()
    {
        return await _stockRepository.GetSummaryAsync();
    }

    public async Task<object> GetAvailableGroupedAsync(int? productId, int? warehouseId)
    {
        return await _stockRepository.GetAvailableGroupedAsync(productId, warehouseId);
    }

    public async Task<IReadOnlyList<StockLookupResponse>> GetLookupAsync(
        string? search, int? warehouseId, int? colorId, bool availableOnly, int limit)
    {
        return await _stockRepository.GetLookupAsync(search, warehouseId, colorId, availableOnly, limit);
    }

    public async Task<StockResponse> GetByIdAsync(int id)
    {
        StockResponse? response = await _stockRepository.GetDetailByIdAsync(id);

        if (response is null)
        {
            throw new AppException("Stock no encontrado.", 404);
        }

        return response;
    }

    public async Task<StockResponse> CreateAsync(StockUpsertRequest request)
    {
        (Stock stock, _) = await ApplyStockDeltaAsync(request);

        await _stockRepository.SaveChangesAsync();

        return await GetByIdAsync(stock.Id);
    }

    public async Task<SuccessResponse> BulkCreateAsync(IReadOnlyList<StockUpsertRequest> request)
    {
        if (request.Count == 0)
        {
            throw new AppException("La carga masiva requiere al menos un registro.");
        }

        foreach (StockUpsertRequest item in request)
        {
            InventoryValidationHelper.ValidateStock(item);
        }

        List<StockUpsertRequest> groupedItems = request
            .GroupBy(item => new
            {
                item.ProductId,
                item.WarehouseId,
                item.ColorId,
                Size = NormalizeSize(item.Size),
                MovementType = NormalizeMovementType(item.MovementType)
            })
            .Select(group => new StockUpsertRequest
            {
                ProductId = group.Key.ProductId,
                WarehouseId = group.Key.WarehouseId,
                ColorId = group.Key.ColorId,
                Size = group.Key.Size,
                Quantity = group.Sum(item => item.Quantity),
                Reserved = group.Sum(item => item.Reserved),
                MovementType = group.Key.MovementType
            })
            .ToList();

        int updatedCount = 0;
        int createdCount = 0;

        foreach (StockUpsertRequest item in groupedItems)
        {
            (_, bool created) = await ApplyStockDeltaAsync(item);
            if (created) createdCount++;
            else updatedCount++;
        }

        await _stockRepository.SaveChangesAsync();

        return new SuccessResponse
        {
            Success = true,
            Message = createdCount > 0 && updatedCount > 0
                ? $"Se registraron {createdCount} nuevos stocks y {updatedCount} movimientos sobre existentes."
                : updatedCount > 0
                    ? $"Se registraron {updatedCount} movimientos de stock."
                    : $"Se registraron {createdCount} stocks."
        };
    }

    public async Task<SuccessResponse> BulkTransferAsync(StockBulkTransferRequest request)
    {
        InventoryValidationHelper.ValidateBulkTransfer(request);

        foreach (StockTransferItemRequest item in request.Items)
        {
            int sourceStockId = item.StockId > 0
                ? item.StockId
                : await ResolveTransferSourceStockIdAsync(request, item);

            int targetWarehouseId = item.TargetWarehouseId > 0
                ? item.TargetWarehouseId
                : request.ToWarehouseId.GetValueOrDefault();

            await ExecuteTransferAsync(sourceStockId, new StockTransferRequest
            {
                DestinationWarehouseId = targetWarehouseId,
                Quantity = item.Quantity
            });
        }

        return new SuccessResponse
        {
            Success = true,
            Message = "Transferencia masiva registrada."
        };
    }

    public async Task<StockResponse> UpdateAsync(int id, StockUpsertRequest request)
    {
        InventoryValidationHelper.ValidateStock(request);

        Stock stock = await FindStockAsync(id);
        stock.ProductId = request.ProductId;
        stock.WarehouseId = request.WarehouseId;
        stock.ColorId = request.ColorId;
        stock.Size = NormalizeSize(request.Size);
        stock.Quantity = request.Quantity;
        stock.Reserved = request.Reserved;

        await _stockRepository.SaveChangesAsync();
        return await GetByIdAsync(stock.Id);
    }

    public async Task DeleteAsync(int id)
    {
        await FindStockAsync(id);
        throw new AppException("Los movimientos de stock no se pueden eliminar. Ajusta o transfiere el stock en su lugar.", 422);
    }

    public async Task<StockResponse> AdjustAsync(int id, StockAdjustRequest request)
    {
        InventoryValidationHelper.ValidateAdjust(request);

        Stock stock = await FindStockAsync(id);
        stock.Quantity = request.Quantity;

        if (stock.Reserved > stock.Quantity)
        {
            stock.Reserved = stock.Quantity;
        }

        await _stockRepository.SaveChangesAsync();
        return await GetByIdAsync(stock.Id);
    }

    public async Task<SuccessResponse> TransferAsync(int id, StockTransferRequest request)
    {
        await ExecuteTransferAsync(id, request);

        return new SuccessResponse
        {
            Success = true,
            Message = "Transferencia registrada."
        };
    }

    private async Task ExecuteTransferAsync(int id, StockTransferRequest request)
    {
        InventoryValidationHelper.ValidateTransfer(request);

        Stock source = await FindStockAsync(id);

        if (request.DestinationWarehouseId == source.WarehouseId)
        {
            throw new AppException("El almacén de origen y destino deben ser diferentes.");
        }

        int availableQuantity = source.Quantity - source.Reserved;
        if (request.Quantity > availableQuantity)
        {
            throw new AppException("La cantidad a transferir supera el stock disponible.");
        }

        source.Quantity -= request.Quantity;

        if (source.Reserved > source.Quantity)
        {
            source.Reserved = source.Quantity;
        }

        Stock? target = await _stockRepository.FindTransferTargetAsync(
            source.ProductId,
            request.DestinationWarehouseId,
            source.ColorId,
            source.Size);

        if (target is null)
        {
            target = new Stock
            {
                ProductId = source.ProductId,
                WarehouseId = request.DestinationWarehouseId,
                ColorId = source.ColorId,
                Size = source.Size,
                Quantity = request.Quantity,
                Reserved = 0
            };

            await _stockRepository.AddAsync(target);
        }
        else
        {
            target.Quantity += request.Quantity;
        }

        await _stockRepository.SaveChangesAsync();
    }

    private async Task<Stock> FindStockAsync(int id)
    {
        Stock? stock = await _stockRepository.GetByIdAsync(id);

        if (stock is null)
        {
            throw new AppException("Stock no encontrado.", 404);
        }

        return stock;
    }

    private static string? NormalizeSize(string? size)
    {
        return string.IsNullOrWhiteSpace(size) ? null : size.Trim();
    }

    private static string NormalizeMovementType(string? movementType)
    {
        string normalized = movementType?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized == "exit" ? "exit" : "entry";
    }

    private async Task<(Stock Stock, bool Created)> ApplyStockDeltaAsync(StockUpsertRequest request)
    {
        InventoryValidationHelper.ValidateStock(request);

        string? normalizedSize = NormalizeSize(request.Size);
        bool isExitMovement = InventoryValidationHelper.IsExitMovement(request.MovementType);
        List<Stock> matchingStocks = await _stockRepository.FindMatchingStocksAsync(
            request.ProductId,
            request.WarehouseId,
            request.ColorId,
            normalizedSize);

        Stock stock;
        bool created = false;

        if (matchingStocks.Count > 0)
        {
            stock = await MergeMatchingStocksAsync(matchingStocks);
        }
        else
        {
            if (isExitMovement)
            {
                throw new AppException("No existe stock registrado para realizar la salida.");
            }

            stock = new Stock
            {
                ProductId = request.ProductId,
                WarehouseId = request.WarehouseId,
                ColorId = request.ColorId,
                Size = normalizedSize,
                Quantity = 0,
                Reserved = 0
            };

            await _stockRepository.AddAsync(stock);
            created = true;
        }

        if (isExitMovement)
        {
            int availableQuantity = stock.Quantity - stock.Reserved;
            if (request.Quantity > availableQuantity)
            {
                throw new AppException("La salida supera el stock disponible.");
            }

            stock.Quantity -= request.Quantity;
        }
        else
        {
            stock.Quantity += request.Quantity;
            stock.Reserved += request.Reserved;
        }

        if (stock.Reserved > stock.Quantity)
        {
            stock.Reserved = stock.Quantity;
        }

        return (stock, created);
    }

    private async Task<int> ResolveTransferSourceStockIdAsync(StockBulkTransferRequest request, StockTransferItemRequest item)
    {
        string? normalizedSize = NormalizeSize(item.Size);
        List<Stock> matchingStocks = await _stockRepository.FindMatchingStocksAsync(
            item.ProductId.GetValueOrDefault(),
            request.FromWarehouseId.GetValueOrDefault(),
            item.ColorId,
            normalizedSize);

        if (matchingStocks.Count == 0)
        {
            throw new AppException("No se encontró stock en el almacén de origen para uno de los items.");
        }

        Stock stock = await MergeMatchingStocksAsync(matchingStocks);
        return stock.Id;
    }

    private async Task<Stock> MergeMatchingStocksAsync(List<Stock> matchingStocks)
    {
        Stock primary = matchingStocks[0];

        if (matchingStocks.Count == 1)
        {
            return primary;
        }

        foreach (Stock duplicate in matchingStocks.Skip(1))
        {
            primary.Quantity += duplicate.Quantity;
            primary.Reserved += duplicate.Reserved;
            await _stockRepository.DeleteAsync(duplicate);
        }

        return primary;
    }
}
