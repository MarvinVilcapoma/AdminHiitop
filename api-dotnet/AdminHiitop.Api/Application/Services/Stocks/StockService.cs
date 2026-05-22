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
        InventoryValidationHelper.ValidateStock(request);

        string? normalizedSize = NormalizeSize(request.Size);
        List<Stock> matchingStocks = await _stockRepository.FindMatchingStocksAsync(
            request.ProductId,
            request.WarehouseId,
            request.ColorId,
            normalizedSize);

        Stock stock;
        if (matchingStocks.Count > 0)
        {
            stock = await MergeMatchingStocksAsync(matchingStocks);
            stock.Quantity += request.Quantity;
            stock.Reserved += request.Reserved;
        }
        else
        {
            stock = new Stock
            {
                ProductId = request.ProductId,
                WarehouseId = request.WarehouseId,
                ColorId = request.ColorId,
                Size = normalizedSize,
                Quantity = request.Quantity,
                Reserved = request.Reserved
            };

            await _stockRepository.AddAsync(stock);
        }

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

        var groupedItems = request
            .GroupBy(item => new
            {
                item.ProductId,
                item.WarehouseId,
                item.ColorId,
                Size = NormalizeSize(item.Size)
            })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.WarehouseId,
                group.Key.ColorId,
                group.Key.Size,
                Quantity = group.Sum(item => item.Quantity),
                Reserved = group.Sum(item => item.Reserved)
            })
            .ToList();

        int mergedCount = 0;
        int createdCount = 0;

        foreach (var item in groupedItems)
        {
            List<Stock> matchingStocks = await _stockRepository.FindMatchingStocksAsync(
                item.ProductId,
                item.WarehouseId,
                item.ColorId,
                item.Size);

            if (matchingStocks.Count > 0)
            {
                Stock stock = await MergeMatchingStocksAsync(matchingStocks);
                stock.Quantity += item.Quantity;
                stock.Reserved += item.Reserved;
                mergedCount++;
                continue;
            }

            await _stockRepository.AddAsync(new Stock
            {
                ProductId = item.ProductId,
                WarehouseId = item.WarehouseId,
                ColorId = item.ColorId,
                Size = item.Size,
                Quantity = item.Quantity,
                Reserved = item.Reserved
            });
            createdCount++;
        }

        await _stockRepository.SaveChangesAsync();

        return new SuccessResponse
        {
            Success = true,
            Message = createdCount > 0 && mergedCount > 0
                ? $"Se crearon {createdCount} registros y se acumularon {mergedCount} en stocks existentes."
                : mergedCount > 0
                    ? $"Se acumularon {mergedCount} cargas en stocks existentes."
                    : $"Se registraron {createdCount} stocks."
        };
    }

    public async Task<SuccessResponse> BulkTransferAsync(StockBulkTransferRequest request)
    {
        InventoryValidationHelper.ValidateBulkTransfer(request);

        foreach (StockTransferItemRequest item in request.Items)
        {
            await ExecuteTransferAsync(item.StockId, new StockTransferRequest
            {
                DestinationWarehouseId = item.TargetWarehouseId,
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
        stock.Size = string.IsNullOrWhiteSpace(request.Size) ? null : request.Size.Trim();
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

        if (request.Quantity > source.Quantity)
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
