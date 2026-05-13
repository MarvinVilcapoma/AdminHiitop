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

    public async Task<IReadOnlyList<StockLookupResponse>> GetLookupAsync(string? search)
    {
        return await _stockRepository.GetLookupAsync(search);
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

        Stock stock = new()
        {
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            ColorId = request.ColorId,
            Size = string.IsNullOrWhiteSpace(request.Size) ? null : request.Size.Trim(),
            Quantity = request.Quantity,
            Reserved = request.Reserved
        };

        await _stockRepository.AddAsync(stock);
        await _stockRepository.SaveChangesAsync();

        return await GetByIdAsync(stock.Id);
    }

    public async Task<SuccessResponse> BulkCreateAsync(IReadOnlyList<StockUpsertRequest> request)
    {
        if (request.Count == 0)
        {
            throw new AppException("La carga masiva requiere al menos un registro.");
        }

        List<Stock> stocks = new(request.Count);

        foreach (StockUpsertRequest item in request)
        {
            InventoryValidationHelper.ValidateStock(item);
            stocks.Add(new Stock
            {
                ProductId = item.ProductId,
                WarehouseId = item.WarehouseId,
                ColorId = item.ColorId,
                Size = string.IsNullOrWhiteSpace(item.Size) ? null : item.Size.Trim(),
                Quantity = item.Quantity,
                Reserved = item.Reserved
            });
        }

        await _stockRepository.AddRangeAsync(stocks);
        await _stockRepository.SaveChangesAsync();

        return new SuccessResponse
        {
            Success = true,
            Message = $"Se registraron {stocks.Count} stocks."
        };
    }

    public async Task<SuccessResponse> BulkTransferAsync(StockBulkTransferRequest request)
    {
        InventoryValidationHelper.ValidateBulkTransfer(request);

        foreach (StockTransferItemRequest item in request.Items)
        {
            await ExecuteTransferAsync(item.StockId, new StockTransferRequest
            {
                TargetWarehouseId = item.TargetWarehouseId,
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
        Stock stock = await FindStockAsync(id);
        await _stockRepository.DeleteAsync(stock);
        await _stockRepository.SaveChangesAsync();
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
            request.TargetWarehouseId,
            source.ColorId,
            source.Size);

        if (target is null)
        {
            target = new Stock
            {
                ProductId = source.ProductId,
                WarehouseId = request.TargetWarehouseId,
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
}
