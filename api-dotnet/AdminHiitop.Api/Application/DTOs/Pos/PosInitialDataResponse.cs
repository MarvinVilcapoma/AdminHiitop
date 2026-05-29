namespace AdminHiitop.Api.Application.DTOs.Pos;

public sealed class PosInitialDataResponse
{
    public IReadOnlyList<PosWarehouseResponse> Warehouses { get; init; } = [];
    public IReadOnlyList<PosDocumentTypeResponse> DocumentTypes { get; init; } = [];
    public IReadOnlyList<PosPaymentMethodResponse> PaymentMethods { get; init; } = [];
    public IReadOnlyList<PosColorResponse> Colors { get; init; } = [];
    public IReadOnlyDictionary<string, PosSettingResponse> Settings { get; init; } = new Dictionary<string, PosSettingResponse>();
}

public sealed class PosWarehouseResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? City { get; init; }
    public bool IsActive { get; init; }
    public bool IsPos { get; init; }
}

public sealed class PosDocumentTypeResponse
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool IsProtected { get; init; }
    public bool IsSunatDocument { get; init; }
    public bool RequiresCustomer { get; init; }
    public bool RequiresRelatedDocument { get; init; }
    public bool CanBeConverted { get; init; }
    public bool IsCommercialDocument { get; init; }
    public int SortOrder { get; init; }
    public IReadOnlyList<PosPrintFormatResponse> PrintFormats { get; init; } = [];
    /// <summary>Active invoice series ID for this document type. Null if not a SUNAT document or no series configured.</summary>
    public int? InvoiceSeriesId { get; init; }
}

public sealed class PosPrintFormatResponse
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Mode { get; init; }
    public decimal? WidthMm { get; init; }
    public bool IsActive { get; init; }
    public PosPrintFormatPivotResponse Pivot { get; init; } = new();
}

public sealed class PosPrintFormatPivotResponse
{
    public bool IsDefault { get; init; }
}

public sealed class PosPaymentMethodResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed class PosColorResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? HexCode { get; init; }
    public string? Slug { get; init; }
}

public sealed class PosSettingResponse
{
    public string? Value { get; init; }
    public string? Label { get; init; }
    public string Type { get; init; } = "string";
    public string Group { get; init; } = "general";
}
