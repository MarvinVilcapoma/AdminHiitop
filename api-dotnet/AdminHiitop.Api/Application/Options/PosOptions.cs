namespace AdminHiitop.Api.Application.Options;

public sealed class PosOptions
{
    public const string SectionName = "Pos";

    public int MaxPosWarehouses { get; set; } = 3;
    public bool AllowNegativeStock { get; set; } = false;
    public bool AllowSaleWithoutCustomer { get; set; } = true;
    public bool PricesIncludeIgv { get; set; } = true;
    public bool AllowDraftSales { get; set; } = true;
    public int MaxItemsPerSale { get; set; } = 200;
}
