using AdminHiitop.Api.Application.DTOs.ElectronicBilling;
using AdminHiitop.Api.Domain.Sales.Entities;
using AdminHiitop.Api.Shared.Helpers;

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

    /// <summary>
    /// Builds a NubeFact request for a credit note (NC) that references the original invoice.
    /// Uses a single summary line item built from the credit note totals.
    /// </summary>
    public static NubeFactDocumentRequest MapCreditNote(Invoice creditNote)
    {
        decimal totalConIgv = creditNote.MtoImpVenta;
        decimal baseAmount  = Math.Round(totalConIgv / 1.18m, 2);
        decimal igvAmount   = Math.Round(totalConIgv - baseAmount, 2);

        string description = string.IsNullOrWhiteSpace(creditNote.NoteMotiveDesc)
            ? "Devolución de mercadería"
            : creditNote.NoteMotiveDesc;

        string customerDocNumber = string.IsNullOrWhiteSpace(creditNote.CustomerDocNumber)
            ? "00000000"
            : creditNote.CustomerDocNumber;

        return new NubeFactDocumentRequest
        {
            Operacion                     = "generar_comprobante",
            TipoDeComprobante             = MapInvoiceType(creditNote.DocType),
            Serie                         = creditNote.Serie,
            Numero                        = creditNote.Correlativo,
            SunatTransaction              = 1,
            ClienteTipoDeDocumento        = MapCustomerDocumentType(creditNote.CustomerDocType),
            ClienteNumeroDeDocumento      = customerDocNumber,
            ClienteDenominacion           = creditNote.CustomerName ?? "CLIENTE VARIOS",
            ClienteDireccion              = string.Empty,
            ClienteEmail                  = creditNote.CustomerEmail ?? string.Empty,
            FechaDeEmision                = creditNote.IssuedAt.ToString("dd-MM-yyyy"),
            Moneda                        = MapCurrency(creditNote.Currency),
            PorcentajeDeIgv               = 18.00m,
            TotalGravada                  = baseAmount,
            TotalIgv                      = igvAmount,
            Total                         = totalConIgv,
            Detraccion                    = false,
            Observaciones                 = creditNote.Observations ?? string.Empty,
            DocumentoQueSeModificaTipo    = creditNote.RefDocType,
            DocumentoQueSeModificaSerie   = ExtractReferenceSeries(creditNote.RefDocNumber),
            DocumentoQueSeModificaNumero  = ExtractReferenceNumber(creditNote.RefDocNumber),
            TipoDeNotaDeCredito           = creditNote.NoteMotive,
            EnviarAutomaticamenteALaSunat = true,
            EnviarAutomaticamenteAlCliente = !string.IsNullOrWhiteSpace(creditNote.CustomerEmail),
            FormatoDePdf                  = "A4",
            Items = new[]
            {
                new NubeFactItemRequest
                {
                    UnidadDeMedida = "NIU",
                    Codigo         = "DEVOL",
                    Descripcion    = description,
                    Cantidad       = 1,
                    ValorUnitario  = baseAmount,
                    PrecioUnitario = totalConIgv,
                    Subtotal       = baseAmount,
                    TipoDeIgv      = 1,
                    Igv            = igvAmount,
                    Total          = totalConIgv
                }
            }
        };
    }

    /// <summary>
    /// Maps an Order to a NubeFact guide request.
    /// </summary>
    /// <param name="guideDocType">
    /// SUNAT guide type: "09" = GRE Remitente (NubeFact tipo 7), "31" = GRE Transportista (NubeFact tipo 8).
    /// </param>
    public static NubeFactGuideDocumentRequest MapGuide(Order order, string serie, int correlativo, string guideDocType = "09")
    {
        var items = order.Items?
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .Select(i => new NubeFactGuideItemRequest
            {
                UnidadDeMedida = "NIU",
                Codigo         = i.ProductId?.ToString() ?? "ITEM",
                Descripcion    = string.IsNullOrWhiteSpace(i.ProductDescription)
                    ? i.Product?.Name ?? "PRODUCTO"
                    : i.ProductDescription,
                Cantidad = i.Quantity,
            }).ToList() ?? [];

        if (items.Count == 0)
            items.Add(new NubeFactGuideItemRequest { Codigo = "ITEM", Descripcion = "BIENES TRASLADADOS", Cantidad = 1 });

        DateTime todayDate  = PeruClock.Now.Date;
        string today        = todayDate.ToString("dd-MM-yyyy");

        // NubeFact requires fecha_de_inicio_de_traslado >= fecha_de_emision (today).
        // If the stored date is in the past, use today to avoid rejection.
        DateTime rawTransfer  = order.GuideTransferDate?.Date ?? todayDate;
        DateTime transferDt   = rawTransfer < todayDate ? todayDate : rawTransfer;
        string transferDate   = transferDt.ToString("dd-MM-yyyy");
        bool isTransportista = guideDocType == "31";

        // NubeFact tipo: 7 = GRE Remitente, 8 = GRE Transportista
        int tipoComprobante = isTransportista ? 8 : 7;

        // Split driver name into first/last name (NubeFact requires separate fields)
        string? driverFull = order.GuideDriverName;
        string? driverNombre   = null;
        string? driverApellidos = null;
        if (!string.IsNullOrWhiteSpace(driverFull))
        {
            int spaceIdx = driverFull.IndexOf(' ');
            driverNombre    = spaceIdx > 0 ? driverFull[..spaceIdx] : driverFull;
            driverApellidos = spaceIdx > 0 ? driverFull[(spaceIdx + 1)..] : "";
        }

        bool hasCarrier = !string.IsNullOrWhiteSpace(order.GuideCarrierDocNumber);
        bool hasDriver  = !string.IsNullOrWhiteSpace(order.GuideDriverDocNumber);
        bool publicTransport = (order.GuideTransferMode ?? "02") == "01";

        var req = new NubeFactGuideDocumentRequest
        {
            TipoDeComprobante             = tipoComprobante,
            Serie                         = serie,
            Numero                        = correlativo,
            FechaDeEmision                = today,
            FechaDeInicioDeTraslado       = transferDate,
            PesoBrutoTotal                = order.GuideTotalWeight ?? 1m,
            // NubeFact only accepts KGM or TNE — normalize any legacy "KG"
            PesoBrutoUnidadDeMedida       = NormalizeWeightUnit(order.GuideWeightUnit),
            Observaciones                 = order.GuideTransferReasonDescription,
            Items                         = items,
            EnviarAutomaticamenteAlCliente = "false",
            FormatoDePdf                  = "A4",
            // Origin
            PuntoDePartidaUbigeo          = order.GuideOriginUbigeo,
            PuntoDePartidaDireccion       = order.GuideOriginAddress,
            PuntoDePartidaCodigoEstablecimientoSunat = "0000",
            // Destination
            PuntoDeLlegadaUbigeo          = order.GuideDestinationUbigeo,
            PuntoDeLlegadaDireccion       = order.GuideDestinationAddress,
            PuntoDeLlegadaCodigoEstablecimientoSunat = "0000",
            // Vehicle plate (always required)
            TransportistaPlacaNumero      = order.GuideVehiclePlate,
        };

        if (!isTransportista)
        {
            // GRE Remitente: cliente_* = destinatario (who receives goods)
            req.ClienteTipoDeDocumento    = MapGuideDocTypeInt(order.GuideRecipientDocType ?? "1");
            req.ClienteNumeroDeDocumento  = order.GuideRecipientDocNumber ?? order.Dni ?? "00000000";
            req.ClienteDenominacion       = order.GuideRecipientName ?? order.CustomerName ?? "DESTINATARIO";
            req.ClienteDireccion          = order.GuideDestinationAddress ?? order.Address;
            req.MotivoDeTraslado          = order.GuideTransferReasonCode ?? "01";
            req.TipoDeTransporte          = order.GuideTransferMode ?? "02";
            req.NumeroDeBultos            = order.GuidePackageCount ?? 1;

            if (publicTransport)
            {
                req.FechaDeEntregaAlTransportista = transferDate;
                if (hasCarrier)
                {
                    req.TransportistaDocumentoTipo   = order.GuideCarrierDocType ?? "6";
                    req.TransportistaDocumentoNumero = order.GuideCarrierDocNumber;
                    req.TransportistaDenominacion    = order.GuideCarrierName;
                }
            }
            else if (hasDriver)
            {
                req.ConductorDocumentoTipo   = order.GuideDriverDocType ?? "1";
                req.ConductorDocumentoNumero = order.GuideDriverDocNumber;
                req.ConductorNombre          = driverNombre;
                req.ConductorApellidos       = driverApellidos;
                req.ConductorNumeroLicencia  = order.GuideDriverLicense;
            }
        }
        else
        {
            // GRE Transportista: cliente_* = remitente (who sends goods)
            req.ClienteTipoDeDocumento    = MapGuideDocTypeInt(order.GuideCarrierDocType ?? "6");
            req.ClienteNumeroDeDocumento  = order.GuideCarrierDocNumber ?? "00000000";
            req.ClienteDenominacion       = order.GuideCarrierName ?? "REMITENTE";
            // Destinatario (who receives)
            if (!string.IsNullOrWhiteSpace(order.GuideRecipientDocNumber))
            {
                req.DestinatarioDocumentoTipo   = order.GuideRecipientDocType ?? "1";
                req.DestinatarioDocumentoNumero = order.GuideRecipientDocNumber;
                req.DestinatarioDenominacion    = order.GuideRecipientName ?? "DESTINATARIO";
            }
            if (hasDriver)
            {
                req.ConductorDocumentoTipo   = order.GuideDriverDocType ?? "1";
                req.ConductorDocumentoNumero = order.GuideDriverDocNumber;
                req.ConductorNombre          = driverNombre;
                req.ConductorApellidos       = driverApellidos;
                req.ConductorNumeroLicencia  = order.GuideDriverLicense;
            }
        }

        return req;
    }

    private static string NormalizeWeightUnit(string? raw)
    {
        var u = (raw ?? "KGM").Trim().ToUpperInvariant();
        return u == "TNE" ? "TNE" : "KGM";
    }

    private static int MapGuideDocTypeInt(string? docType)
    {
        return docType?.Trim() switch
        {
            "6" or "RUC" => 6,
            "4" or "CE"  => 4,
            "7" or "PAS" => 7,
            _            => 1, // DNI default
        };
    }

    private static int MapInvoiceType(string? docType)
    {
        return docType switch
        {
            "01" => 1,   // Factura
            "03" => 2,   // Boleta de venta
            "07" => 3,   // Nota de crédito
            "08" => 4,   // Nota de débito
            "09" => 31,  // Guía de remisión remitente
            "31" => 32,  // Guía de remisión transportista
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
