<?php

namespace App\Services;

use App\Models\Invoice;
use App\Models\Setting;
use DateTime;
use Greenter\Model\Client\Client;
use Greenter\Model\Company\Address;
use Greenter\Model\Company\Company;
use Greenter\Model\Sale\FormaPagos\FormaPagoContado;
use Greenter\Model\Sale\FormaPagos\FormaPagoCredito;
use Greenter\Model\Sale\Invoice as GreenterInvoice;
use Greenter\Model\Sale\Legend;
use Greenter\Model\Sale\Note;
use Greenter\Model\Sale\SaleDetail;
use Greenter\See;
use Greenter\Ws\Services\SunatEndpoints;

class InvoiceService
{
    private ?array $settings = null;

    // ── Configuración ──────────────────────────────────────────────────────────

    private function getSettings(): array
    {
        if ($this->settings !== null) {
            return $this->settings;
        }

        $this->settings = Setting::whereIn('key', [
            // global
            'sunat_environment', 'igv_rate', 'prices_include_igv',
            // beta
            'sunat_beta_ruc', 'sunat_beta_razon_social', 'sunat_beta_nombre_comercial',
            'sunat_beta_ubigueo', 'sunat_beta_departamento', 'sunat_beta_provincia',
            'sunat_beta_distrito', 'sunat_beta_urbanizacion', 'sunat_beta_direccion',
            'sunat_beta_codigo_local', 'sunat_beta_sol_user', 'sunat_beta_sol_pass',
            'sunat_beta_certificate_pem',
            // produccion
            'sunat_prod_ruc', 'sunat_prod_razon_social', 'sunat_prod_nombre_comercial',
            'sunat_prod_ubigueo', 'sunat_prod_departamento', 'sunat_prod_provincia',
            'sunat_prod_distrito', 'sunat_prod_urbanizacion', 'sunat_prod_direccion',
            'sunat_prod_codigo_local', 'sunat_prod_sol_user', 'sunat_prod_sol_pass',
            'sunat_prod_certificate_pem',
            // legacy fallback keys
            'sunat_ruc', 'sunat_razon_social', 'sunat_nombre_comercial',
            'sunat_ubigueo', 'sunat_departamento', 'sunat_provincia',
            'sunat_distrito', 'sunat_urbanizacion', 'sunat_direccion', 'sunat_codigo_local',
            'sunat_sol_user', 'sunat_sol_pass', 'sunat_certificate_pem',
        ])->pluck('value', 'key')->toArray();

        return $this->settings;
    }

    private function buildSee(string $docType = '01'): See
    {
        $s = $this->getSettings();
        $env    = $s['sunat_environment'] ?? 'beta';
        $prefix = ($env === 'produccion') ? 'sunat_prod_' : 'sunat_beta_';

        // Helper: try env-prefixed key first, fall back to legacy key
        $get = fn(string $suffix) => $s["{$prefix}{$suffix}"] ?? $s["sunat_{$suffix}"] ?? '';

        $certPem = $get('certificate_pem');
        if (empty(trim($certPem))) {
            throw new \RuntimeException(
                'No hay certificado digital configurado. ' .
                'Ve a Configuración → Parámetros fiscales → sección SUNAT y pega el contenido de tu certificado .pem.'
            );
        }

        $see = new See();
        $see->setCertificate($certPem);

        // In Greenter v5, both Facturas and Boletas use the same FE endpoint
        if ($env === 'produccion') {
            $service = SunatEndpoints::FE_PRODUCCION;
        } else {
            $service = SunatEndpoints::FE_BETA;
        }

        $see->setService($service);
        $see->setClaveSOL(
            $get('ruc'),
            $get('sol_user'),
            $get('sol_pass')
        );

        return $see;
    }

    private function buildCompany(): Company
    {
        $s      = $this->getSettings();
        $env    = $s['sunat_environment'] ?? 'beta';
        $prefix = ($env === 'produccion') ? 'sunat_prod_' : 'sunat_beta_';
        $get    = fn(string $suffix) => $s["{$prefix}{$suffix}"] ?? $s["sunat_{$suffix}"] ?? '';

        $address = (new Address())
            ->setUbigueo($get('ubigueo')        ?: '150101')
            ->setDepartamento($get('departamento') ?: 'LIMA')
            ->setProvincia($get('provincia')    ?: 'LIMA')
            ->setDistrito($get('distrito')      ?: 'LIMA')
            ->setUrbanizacion($get('urbanizacion') ?: '-')
            ->setDireccion($get('direccion')    ?: '')
            ->setCodLocal($get('codigo_local')  ?: '0000');

        return (new Company())
            ->setRuc($get('ruc'))
            ->setRazonSocial($get('razon_social'))
            ->setNombreComercial($get('nombre_comercial'))
            ->setAddress($address);
    }

    private function buildClient(Invoice $invoice): Client
    {
        return (new Client())
            ->setTipoDoc($invoice->customer_doc_type   ?? '-')
            ->setNumDoc($invoice->customer_doc_number  ?? '-')
            ->setRznSocial($invoice->customer_name     ?? '-');
    }

    // ── IGV helpers ────────────────────────────────────────────────────────────

    private function getIgvRate(): float
    {
        $s = $this->getSettings();
        $rate = (float) ($s['igv_rate'] ?? 0.18);
        if ($rate > 1) {
            $rate /= 100; // stored as 18, convert to 0.18
        }
        return $rate;
    }

    private function pricesIncludeIgv(): bool
    {
        $s = $this->getSettings();
        return filter_var($s['prices_include_igv'] ?? '1', FILTER_VALIDATE_BOOLEAN);
    }

    // ── Compute item and total amounts ─────────────────────────────────────────

    /**
     * Returns computed amounts for a single line: [valorUnitario, precioUnitario, valorVenta, igv, baseIgv]
     */
    public function computeLineAmounts(float $unitPrice, float $subtotal): array
    {
        $igvRate       = $this->getIgvRate();
        $includesIgv   = $this->pricesIncludeIgv();

        if ($includesIgv) {
            $valorVenta     = round($subtotal / (1 + $igvRate), 6);
            $igvItem        = round($subtotal - $valorVenta, 6);
            $valorUnitario  = round($unitPrice / (1 + $igvRate), 6);
            $precioUnitario = $unitPrice;
        } else {
            $valorVenta     = $subtotal;
            $igvItem        = round($subtotal * $igvRate, 6);
            $valorUnitario  = $unitPrice;
            $precioUnitario = round($unitPrice * (1 + $igvRate), 6);
        }

        return [
            'valorUnitario'  => $valorUnitario,
            'precioUnitario' => $precioUnitario,
            'valorVenta'     => $valorVenta,
            'igv'            => $igvItem,
            'baseIgv'        => $valorVenta,
        ];
    }

    /**
     * Computes full invoice totals from order. Call before creating the Invoice record.
     */
    public function computeTotalsFromOrder(\App\Models\Order $order): array
    {
        $mtoOperGravadas = 0;
        $mtoIgv          = 0;

        foreach ($order->items as $item) {
            $amounts          = $this->computeLineAmounts((float) $item->unit_price, (float) $item->subtotal);
            $mtoOperGravadas += $amounts['valorVenta'];
            $mtoIgv          += $amounts['igv'];
        }

        // Delivery cost is NOT included in the invoice (only shown in the order)
        $mtoOperGravadas = round($mtoOperGravadas, 2);
        $mtoIgv          = round($mtoIgv, 2);
        $mtoImpVenta     = round($mtoOperGravadas + $mtoIgv, 2);

        return [
            'mto_oper_gravadas' => $mtoOperGravadas,
            'mto_igv'           => $mtoIgv,
            'valor_venta'       => $mtoOperGravadas,
            'sub_total'         => $mtoImpVenta,
            'mto_imp_venta'     => $mtoImpVenta,
        ];
    }

    // ── Build Greenter detail items ─────────────────────────────────────────────

    private function buildDetails(Invoice $invoice): array
    {
        $details = [];
        $igvRate = $this->getIgvRate();

        foreach ($invoice->order->items as $item) {
            $amounts = $this->computeLineAmounts((float) $item->unit_price, (float) $item->subtotal);

            $sku = optional($item->product)->sku ?? ('ITEM' . $item->id);
            $desc = $item->product_description
                ?? optional($item->product)->name
                ?? 'Producto';

            $details[] = (new SaleDetail())
                ->setCodProducto($sku)
                ->setUnidad('NIU')                 // Unidad – Catálogo 03
                ->setCantidad((float) $item->quantity)
                ->setMtoValorUnitario($amounts['valorUnitario'])
                ->setDescripcion($desc)
                ->setMtoBaseIgv($amounts['baseIgv'])
                ->setPorcentajeIgv($igvRate * 100)
                ->setIgv($amounts['igv'])
                ->setTipAfeIgv('10')               // Gravado Op. Onerosa – Catálogo 07
                ->setTotalImpuestos($amounts['igv'])
                ->setMtoValorVenta($amounts['valorVenta'])
                ->setMtoPrecioUnitario($amounts['precioUnitario']);
        }

        return $details;
    }

    // ── Build Greenter document ────────────────────────────────────────────────

    public function buildDocument(Invoice $invoice): GreenterInvoice|Note
    {
        $invoice->loadMissing(['order.items.product']);

        $details = $this->buildDetails($invoice);
        $company = $this->buildCompany();
        $client  = $this->buildClient($invoice);
        $legend  = $this->buildLegend((float) $invoice->mto_imp_venta, $invoice->currency);

        $issuedAt = new DateTime($invoice->issued_at->format('Y-m-d\TH:i:sP'));

        $mtoOperGravadas = (float) $invoice->mto_oper_gravadas;
        $mtoIgv          = (float) $invoice->mto_igv;
        $valorVenta      = (float) $invoice->valor_venta;
        $subTotal        = (float) $invoice->sub_total;
        $mtoImpVenta     = (float) $invoice->mto_imp_venta;

        // ── Nota de Crédito ─────────────────────────────────────────────────
        if (in_array($invoice->doc_type, ['07', '08'])) {
            return (new Note())
                ->setUblVersion('2.1')
                ->setTipoDoc($invoice->doc_type)
                ->setSerie($invoice->serie)
                ->setCorrelativo((string) $invoice->correlativo)
                ->setFechaEmision($issuedAt)
                ->setTipoMoneda($invoice->currency)
                ->setCompany($company)
                ->setClient($client)
                ->setCodMotivo($invoice->note_motive ?? '01')
                ->setDesMotivo($invoice->note_motive_desc ?? 'Anulación de operación')
                ->setNumDocfectado($invoice->ref_doc_number ?? '')
                ->setTipDocAfectado($invoice->ref_doc_type ?? '03')
                ->setMtoOperGravadas($mtoOperGravadas)
                ->setMtoIGV($mtoIgv)
                ->setTotalImpuestos($mtoIgv)
                ->setValorVenta($valorVenta)
                ->setSubTotal($subTotal)
                ->setMtoImpVenta($mtoImpVenta)
                ->setDetails($details)
                ->setLegends([$legend]);
        }

        // ── Factura / Boleta ────────────────────────────────────────────────
        $formaPago = $invoice->form_of_payment === 'credito'
            ? new FormaPagoCredito(mtoNetoPendiente: $mtoImpVenta)
            : new FormaPagoContado();

        return (new GreenterInvoice())
            ->setUblVersion('2.1')
            ->setTipoOperacion('0101')           // Venta interna – Catálogo 51
            ->setTipoDoc($invoice->doc_type)
            ->setSerie($invoice->serie)
            ->setCorrelativo((string) $invoice->correlativo)
            ->setFechaEmision($issuedAt)
            ->setFormaPago($formaPago)
            ->setTipoMoneda($invoice->currency)
            ->setCompany($company)
            ->setClient($client)
            ->setMtoOperGravadas($mtoOperGravadas)
            ->setMtoIGV($mtoIgv)
            ->setTotalImpuestos($mtoIgv)
            ->setValorVenta($valorVenta)
            ->setSubTotal($subTotal)
            ->setMtoImpVenta($mtoImpVenta)
            ->setDetails($details)
            ->setLegends([$legend]);
    }

    // ── Send to SUNAT ──────────────────────────────────────────────────────────

    public function sendToSunat(Invoice $invoice): array
    {
        try {
            $see = $this->buildSee($invoice->doc_type);
            $doc = $this->buildDocument($invoice);

            $result = $see->send($doc);
            $xml    = $see->getFactory()->getLastXml();

            // Always save the signed XML
            $invoice->update(['xml_content' => $xml ?? '', 'status' => 'pending']);

            if (!$result->isSuccess()) {
                $err  = $result->getError();
                $code = $err?->getCode() ?? 'CONN';
                $msg  = $err?->getMessage() ?? 'Error de conexión con SUNAT';
                $invoice->update([
                    'status'           => 'error',
                    'sunat_code'       => is_numeric($code) ? (int) $code : null,
                    'sunat_description' => "[{$code}] {$msg}",
                ]);
                return ['success' => false, 'error' => "[{$code}] {$msg}"];
            }

            $cdrZip = $result->getCdrZip();
            $cdr    = $result->getCdrResponse();
            $code   = (int) $cdr->getCode();

            $status = match (true) {
                $code === 0                         => 'accepted',
                $code >= 2000 && $code <= 3999 => 'rejected',
                default                         => 'exception',
            };

            $invoice->update([
                'status'            => $status,
                'sunat_code'        => $code,
                'sunat_description' => $cdr->getDescription(),
                'sunat_notes'       => $cdr->getNotes(),
                'cdr_content'       => base64_encode((string) $cdrZip),
            ]);

            return [
                'success'     => $code === 0,
                'status'      => $status,
                'code'        => $code,
                'description' => $cdr->getDescription(),
                'notes'       => $cdr->getNotes(),
            ];
        } catch (\Throwable $e) {
            $invoice->update([
                'status'            => 'error',
                'sunat_description' => $e->getMessage(),
            ]);
            return ['success' => false, 'error' => $e->getMessage()];
        }
    }

    // ── Legend (monto en letras) ───────────────────────────────────────────────

    private function buildLegend(float $amount, string $currency = 'PEN'): Legend
    {
        $currencyWord = $currency === 'USD' ? 'DÓLARES AMERICANOS' : 'SOLES';
        return (new Legend())
            ->setCode('1000')
            ->setValue($this->numberToWords($amount, $currencyWord));
    }

    private function numberToWords(float $amount, string $currency = 'SOLES'): string
    {
        $intPart = (int) floor($amount);
        $decPart = (int) round(($amount - $intPart) * 100);
        return $this->intToWords($intPart) . ' CON ' . sprintf('%02d', $decPart) . '/100 ' . $currency;
    }

    private function intToWords(int $n): string
    {
        if ($n === 0) return 'CERO';
        if ($n < 0)   return 'MENOS ' . $this->intToWords(-$n);

        $units    = ['', 'UN', 'DOS', 'TRES', 'CUATRO', 'CINCO', 'SEIS', 'SIETE', 'OCHO', 'NUEVE',
                     'DIEZ', 'ONCE', 'DOCE', 'TRECE', 'CATORCE', 'QUINCE', 'DIECISEIS', 'DIECISIETE',
                     'DIECIOCHO', 'DIECINUEVE', 'VEINTE'];
        $tens     = ['', '', 'VEINTE', 'TREINTA', 'CUARENTA', 'CINCUENTA', 'SESENTA', 'SETENTA', 'OCHENTA', 'NOVENTA'];
        $hundreds = ['', 'CIEN', 'DOSCIENTOS', 'TRESCIENTOS', 'CUATROCIENTOS', 'QUINIENTOS',
                     'SEISCIENTOS', 'SETECIENTOS', 'OCHOCIENTOS', 'NOVECIENTOS'];

        if ($n <= 20) return $units[$n];

        if ($n < 100) {
            $ten  = $tens[(int) ($n / 10)];
            $unit = $n % 10;
            return $unit === 0 ? $ten : $ten . ' Y ' . $units[$unit];
        }

        if ($n === 100) return 'CIEN';

        if ($n < 1000) {
            $hundred = $hundreds[(int) ($n / 100)];
            $rest    = $n % 100;
            if ($hundred === 'CIEN' && $rest > 0) $hundred = 'CIENTO';
            return $rest === 0 ? $hundred : $hundred . ' ' . $this->intToWords($rest);
        }

        if ($n < 2000) {
            $rest = $n % 1000;
            return $rest === 0 ? 'MIL' : 'MIL ' . $this->intToWords($rest);
        }

        if ($n < 1_000_000) {
            $thousands = (int) ($n / 1000);
            $rest      = $n % 1000;
            $str       = $this->intToWords($thousands) . ' MIL';
            return $rest === 0 ? $str : $str . ' ' . $this->intToWords($rest);
        }

        if ($n < 2_000_000) {
            $rest = $n % 1_000_000;
            return $rest === 0 ? 'UN MILLON' : 'UN MILLON ' . $this->intToWords($rest);
        }

        $millions = (int) ($n / 1_000_000);
        $rest     = $n % 1_000_000;
        $str      = $this->intToWords($millions) . ' MILLONES';
        return $rest === 0 ? $str : $str . ' ' . $this->intToWords($rest);
    }
}
