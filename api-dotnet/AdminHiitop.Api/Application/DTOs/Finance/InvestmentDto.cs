namespace AdminHiitop.Api.Application.DTOs.Finance;

public sealed class InvestmentCategoryResponse
{
    public int     Id          { get; set; }
    public string  Code        { get; set; } = string.Empty;
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool    IsActive    { get; set; }
}

public sealed class InvestmentResponse
{
    public int      Id                     { get; set; }
    public int      InvestmentCategoryId   { get; set; }
    public string   CategoryName           { get; set; } = string.Empty;
    public decimal  Amount                 { get; set; }
    public string   Description            { get; set; } = string.Empty;
    public DateTime InvestmentDate         { get; set; }
    public bool     IsActive               { get; set; }
    public DateTime CreatedAt              { get; set; }
}

public sealed class CreateInvestmentRequest
{
    public int      InvestmentCategoryId { get; set; }
    public decimal  Amount               { get; set; }
    public string   Description          { get; set; } = string.Empty;
    public DateTime InvestmentDate       { get; set; }
}

public sealed class UpdateInvestmentRequest
{
    public int      InvestmentCategoryId { get; set; }
    public decimal  Amount               { get; set; }
    public string   Description          { get; set; } = string.Empty;
    public DateTime InvestmentDate       { get; set; }
    public bool     IsActive             { get; set; }
}
