using AdminHiitop.Api.Application.DTOs.ElectronicBilling;
using AdminHiitop.Api.Domain.Sales.Entities;

namespace AdminHiitop.Api.Application.Helpers;

public static class NubeFactMappingHelper
{
    public static NubeFactDocumentRequest MapInvoice(Invoice invoice)
    {
        Order? order = invoice.Order;
        IReadOnlyList<OrderItem> items = order?.Items is null
            ? Array.Empty<OrderItem>()
            : order.Items
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .ToList();

        string customerName = string.IsNullOrWhiteSpace(invoice.CustomerName)
            ? order?.CustomerName ?? "CLIENTE VARIOS"
            : invoice.CustomerName;

        string customerDocumentNumber = string.IsNullOrWhiteSpace(invoice.CustomerDocNumber)
            ? order?.Dni ?? order?.Customer?.Dni ?? "00000000"
            : invoice.CustomerDocNumber;

        IReadOnlyList<NubeFactItemRequest> mappedItems = BuildItems(items);

        // Derive document totals from item sums to avoid header/line rounding discrepancies
        decimal totalGravada = mappedItems.Count > 0
            ? mappedItems.Sum(i => i.Subtotal)
            : Math.Round(invoice.MtoImpVenta / 1.18m, 2);
        decimal totalIgv = mappedItems.Count > 0
            ? mappedItems.Sum(i => i.Igv)
            : invoice.MtoIgv;
        decimal total = mappedItems.Count > 0
            ? mappedItems.Sum(i => i.Total)
            : invoice.MtoImpVenta;

        return new NubeFactDocumentRequest
        {
            Operacion                  = "generar_comprobante",
            TipoDeComprobante          = MapInvoiceType(invoice.DocType),
            Serie                      = invoice.Serie,
            Numero                     = invoice.Correlativo,
            SunatTransaction           = 1,
            ClienteTipoDeDocumento     = MapCustomerDocumentType(invoice.CustomerDocType),
            ClienteNumeroDeDocumento   = customerDocumentNumber,
            ClienteDenominacion        = customerName,
            ClienteDireccion           = order?.Address ?? string.Empty,
            ClienteEmail               = order?.CustomerEmail ?? string.Empty,
            FechaDeEmision             = invoice.IssuedAt.ToString("dd-MM-yyyy"),
            FechaDeVencimiento         = null,
            Moneda                     = MapCurrency(invoice.Currency),
            PorcentajeDeIgv            = 18.00m,
            TotalGravada               = totalGravada,
            TotalIgv                   = totalIgv,
            Total                      = total,
            Detraccion                 = false,
            Observaciones              = invoice.Observations ?? string.Empty,
            DocumentoQueSeModificaTipo    = invoice.RefDocType,
            DocumentoQueSeModificaSerie   = ExtractReferenceSeries(invoice.RefDocNumber),
            DocumentoQueSeModificaNumero  = ExtractReferenceNumber(invoice.RefDocNumber),
            TipoDeNotaDeCredito        = invoice.NoteMotive,
            TipoDeNotaDeDebito         = invoice.NoteMotive,
            EnviarAutomaticamenteALaSunat  = true,
            EnviarAutomaticamenteAlCliente = false,
            CondicionesDePago          = invoice.FormOfPayment,
            MedioDePago                = invoice.FormOfPayment,
            FormatoDePdf               = "A4",
            Items                      = mappedItems
        };
    }

    private static IReadOnlyList<NubeFactItemRequest> BuildItems(IReadOnlyList<OrderItem> items)
    {
        if (items.Count == 0)
        {
            return new List<NubeFactItemRequest>
            {
                new()
                {
                    UnidadDeMedida = "NIU",
                    Codigo = "SERVICIO",
                    Descripcion = "VENTA GENERAL",
                    Cantidad = 1,
                    ValorUnitario = 0,
                    PrecioUnitario = 0,
                    Subtotal = 0,
                    TipoDeIgv = 1,
                    Igv = 0,
                    Total = 0
                }
            };
        }

        List<NubeFactItemRequest> mappedItems = new(items.Count);

        // Prices include IGV (PricesIncludeIgv = true):
        //   item.Subtotal    = total con IGV  (precio final)
        //   baseAmount       = base sin IGV   (= subtotal / 1.18)
        //   igv              = item.Subtotal - baseAmount
        //   valor_unitario   = base unitaria sin IGV
        //   precio_unitario  = unit_price con IGV (precio final por unidad)
        foreach (OrderItem item in items)
        {
            decimal totalConIgv    = item.Subtotal;
            decimal baseAmount     = Math.Round(totalConIgv / 1.18m, 2);
            decimal igv            = Math.Round(totalConIgv - baseAmount, 2);
            decimal valorUnitario  = item.Quantity == 0 ? 0m : Math.Round(baseAmount / item.Quantity, 6);
            decimal precioUnitario = item.Quantity == 0 ? 0m : Math.Round(item.UnitPrice, 2);

            mappedItems.Add(new NubeFactItemRequest
            {
                UnidadDeMedida = "NIU",
                Codigo = item.Product?.Sku ?? item.ProductId.ToString(),
                Descripcion = string.IsNullOrWhiteSpace(item.ProductDescription)
                    ? item.Product?.Name ?? "ITEM"
                    : item.ProductDescription,
                Cantidad       = item.Quantity,
                ValorUnitario  = valorUnitario,
                PrecioUnitario = precioUnitario,
                Subtotal       = baseAmount,    // NubeFact: base sin IGV
                TipoDeIgv      = 1,
                Igv            = igv,
                Total          = totalConIgv    // NubeFact: total con IGV
            });
        }

        return mappedItems;
    }

    private static int MapInvoiceType(string? docType)
    {
        return docType switch
        {
            "01" => 1,
            "03" => 2,
            "07" => 3,
            "08" => 4,
            _ => 1
        };
    }

    private static int MapCustomerDocumentType(string? docType)
    {
        return docType switch
        {
            "1" => 1,
            "4" => 4,
            "6" => 6,
            "7" => 7,
            "A" => 0,
            _ => 1
        };
    }

    private static int MapCurrency(string? currency)
    {
        return string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
    }

    private static string? ExtractReferenceSeries(string? fullNumber)
    {
        if (string.IsNullOrWhiteSpace(fullNumber) || !fullNumber.Contains('-'))
        {
            return null;
        }

        return fullNumber.Split('-', 2)[0];
    }

    private static string? ExtractReferenceNumber(string? fullNumber)
    {
        if (string.IsNullOrWhiteSpace(fullNumber) || !fullNumber.Contains('-'))
        {
            return null;
        }

        return fullNumber.Split('-', 2)[1];
    }
}
