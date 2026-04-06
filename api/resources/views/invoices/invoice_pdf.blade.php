{{-- HIITOP PDF Template v4 --}}
<!DOCTYPE html>
<html lang="es">
<head>
<meta charset="UTF-8">
<title>{{ $invoice->full_number }}</title>
<style>
* { box-sizing: border-box; }
body { font-family: Arial, Helvetica, sans-serif; color: #111; font-size: 12px; margin: 0; padding: 24px; }
.document { width: 100%; }
.line { border-top: 1px solid #777; margin: 8px 0; }
.line-bold { border-top: 1px solid #333; margin: 10px 0; }
.header { width: 100%; border-collapse: collapse; margin-bottom: 8px; }
.header td { vertical-align: top; }
.company { width: 65%; padding-right: 16px; }
.company img { width: 70px; height: auto; margin-bottom: 6px; }
.voucher-box { width: 35%; text-align: center; border: 1px solid #444; padding: 12px 10px; vertical-align: middle; }
.company-name { font-weight: 700; font-size: 16px; margin-bottom: 4px; text-transform: uppercase; }
.voucher-title { font-weight: 700; font-size: 14px; line-height: 1.3; text-transform: uppercase; margin-bottom: 10px; }
.voucher-ruc   { font-size: 12px; margin-bottom: 2px; }
.voucher-num   { font-weight: 700; font-size: 18px; margin-top: 2px; }
.voucher-label { font-size: 10px; color: #555; margin-bottom: 1px; }
.meta-table, .client-table, .totals-table, .summary-table, .items-table { width: 100%; border-collapse: collapse; }
.meta-table td, .client-table td { padding: 3px 0; vertical-align: top; }
.label { font-weight: 700; width: 150px; white-space: nowrap; }
.items-table { margin-top: 10px; }
.items-table th, .items-table td { padding: 7px 6px; border-bottom: 1px solid #aaa; }
.items-table th { text-transform: uppercase; font-size: 11px; border-top: 1px solid #444; border-bottom: 1px solid #444; text-align: left; }
.text-center { text-align: center; }
.text-right  { text-align: right; }
.summary-right { width: 340px; float: right; }
.summary-table td, .totals-table td { padding: 4px 0; }
.total-box { margin-top: 8px; border-top: 2px solid #333; border-bottom: 2px solid #333; padding: 6px 0; }
.total-box-row { width: 100%; border-collapse: collapse; }
.total-box-row td { padding: 0; }
.total-label { font-weight: 700; font-size: 14px; text-transform: uppercase; }
.total-value { font-weight: 700; font-size: 16px; text-align: right; }
.amount-block { margin-top: 14px; padding-top: 10px; border-top: 1px solid #777; font-size: 12px; clear: both; }
.footer { margin-top: 18px; text-align: center; font-size: 11px; }
.thanks { font-weight: 700; margin: 8px 0 4px; font-size: 13px; }
.muted { color: #555; }
.clearfix { clear: both; }
</style>
</head>
<body>
@php
if (!function_exists('_hiitop_int_to_spanish')) {
    function _hiitop_int_to_spanish(int $n): string {
        if ($n === 0) return 'CERO';
        if ($n < 0)  return 'MENOS ' . _hiitop_int_to_spanish(-$n);
        $u = ['','UNO','DOS','TRES','CUATRO','CINCO','SEIS','SIETE','OCHO','NUEVE','DIEZ','ONCE','DOCE','TRECE','CATORCE','QUINCE','DIECISEIS','DIECISIETE','DIECIOCHO','DIECINUEVE'];
        $t = ['','','VEINTE','TREINTA','CUARENTA','CINCUENTA','SESENTA','SETENTA','OCHENTA','NOVENTA'];
        $h = ['','CIEN','DOSCIENTOS','TRESCIENTOS','CUATROCIENTOS','QUINIENTOS','SEISCIENTOS','SETECIENTOS','OCHOCIENTOS','NOVECIENTOS'];
        $r = '';
        if ($n >= 1000000) { $m = intdiv($n,1000000); $r .= ($m===1?'UN MILLON ':_hiitop_int_to_spanish($m).' MILLONES '); $n %= 1000000; }
        if ($n >= 1000)    { $k = intdiv($n,1000);    $r .= ($k===1?'MIL ':_hiitop_int_to_spanish($k).' MIL ');             $n %= 1000; }
        if ($n >= 100)     { $hv = intdiv($n,100); $sv = $n%100; $r .= ($hv===1&&$sv>0?'CIENTO ':$h[$hv].' '); $n = $sv; }
        if ($n >= 20)      { $tv = intdiv($n,10); $uv = $n%10;   $r .= $t[$tv].($uv>0?' Y '.$u[$uv].' ':' '); }
        elseif ($n > 0)    { $r .= $u[$n].' '; }
        return trim($r);
    }
}
if (!function_exists('_hiitop_amount_words')) {
    function _hiitop_amount_words(float $amount): string {
        $int = intval($amount);
        $dec = round(($amount - $int) * 100);
        return _hiitop_int_to_spanish($int) . ' CON ' . str_pad($dec, 2, '0', STR_PAD_LEFT) . '/100 SOLES';
    }
}

$s       = $settings ?? [];
$_env    = $s['sunat_environment'] ?? 'beta';
$_pre    = ($_env === 'produccion') ? 'sunat_prod_' : 'sunat_beta_';
$_g      = fn(string $k) => $s["{$_pre}{$k}"] ?? $s["sunat_{$k}"] ?? '';
$ruc     = $_g('ruc')              ?: '';
$razon   = $_g('razon_social')     ?: 'HIITOP S.A.C';
$nombre  = $_g('nombre_comercial') ?: $razon;
$dir     = $_g('direccion')        ?: '';
$dist    = $_g('distrito')         ?: '';
$prov    = $_g('provincia')        ?: '';
$dpto    = $_g('departamento')     ?: '';
$phone   = $s['company_phone']         ?? '';
$email   = $s['company_email']         ?? '';
$web     = $s['company_web']           ?? '';
$igvEnabled    = ($s['igv_enabled'] ?? 'true') === 'true';
$igvRate       = floatval($s['igv_rate'] ?? 0.18);
// prices_include_igv = true  → precios en BD ya traen IGV, hay que dividir para sacar el valor sin IGV
// prices_include_igv = false → precios en BD son sin IGV, se usan directamente
$pricesWithIgv = ($s['prices_include_igv'] ?? 'false') === 'true' && $igvEnabled;

$order   = $invoice->order;
$seller  = optional($order?->user)->name ?? '';

$docLabel = match($invoice->doc_type) {
    '01'    => "FACTURA DE VENTA\nELECTRÓNICA",
    '03'    => "BOLETA DE VENTA\nELECTRÓNICA",
    '07'    => "NOTA DE CRÉDITO\nELECTRÓNICA",
    '08'    => "NOTA DE DÉBITO\nELECTRÓNICA",
    default => "COMPROBANTE\nELECTRÓNICO",
};
$serieNumero = $invoice->serie . '-' . $invoice->correlativo;
$money = fn($v) => 'S/ ' . number_format((float)$v, 2);
@endphp

<div class="document">

  {{-- ═══ HEADER ═══ --}}
  <table class="header">
    <tr>
      <td class="company">
        @if($logoBase64)
          <img src="{{ $logoBase64 }}" alt="Logo"><br>
        @endif
        <div class="company-name">{{ $nombre }}</div>
        <div><strong>RUC:</strong> {{ $ruc }}</div>
        @if($dir)<div>{{ $dir }}</div>@endif
        @if($dist || $prov)<div>{{ $dist }}{{ $prov ? ' - '.$prov : '' }}{{ $dpto ? ' / '.$dpto : '' }}</div>@endif
        @if($phone)<div><strong>Tel:</strong> {{ $phone }}</div>@endif
        @if($email)<div><strong>Email:</strong> {{ $email }}</div>@endif
        @if($web)<div><strong>Web:</strong> {{ $web }}</div>@endif
      </td>
      <td class="voucher-box">
        <div class="voucher-title">{!! nl2br(e($docLabel)) !!}</div>
        <div class="voucher-label">RUC:</div>
        <div style="font-weight:700;font-size:14px;margin-bottom:8px">{{ $ruc }}</div>
        <div class="voucher-label">N°:</div>
        <div class="voucher-num">{{ $serieNumero }}</div>
      </td>
    </tr>
  </table>

  <div class="line"></div>

  {{-- ═══ META ═══ --}}
  <table class="meta-table">
    <tr>
      <td class="label">FECHA DE LA VENTA:</td>
      <td>{{ $invoice->issued_at->format('d/m/Y') }}</td>
      <td class="label">FORMA DE PAGO:</td>
      <td>{{ $paymentMethodName ?? ($invoice->form_of_payment === 'contado' ? 'AL CONTADO' : 'AL CRÉDITO') }}</td>
    </tr>
    @if($seller)
    <tr>
      <td class="label">VENDEDOR:</td>
      <td colspan="3">{{ strtoupper($seller) }}</td>
    </tr>
    @endif
    @if($order?->order_number)
    <tr>
      <td class="label">PEDIDO:</td>
      <td colspan="3">{{ $order->order_number }}</td>
    </tr>
    @endif
  </table>

  <div class="line"></div>

  {{-- ═══ CLIENTE ═══ --}}
  <table class="client-table">
    <tr>
      <td class="label">CLIENTE:</td>
      <td>{{ strtoupper($invoice->customer_name) }}</td>
    </tr>
    <tr>
      <td class="label">{{ $invoice->customer_doc_type === '6' ? 'RUC' : 'DNI' }}:</td>
      <td>{{ $invoice->customer_doc_number ?? '—' }}</td>
    </tr>
    @if($order?->address)
    <tr>
      <td class="label">DIRECCIÓN:</td>
      <td>{{ $order->address }}</td>
    </tr>
    @endif
  </table>

  {{-- ═══ ITEMS ═══ --}}
  <table class="items-table">
    <thead>
      <tr>
        <th style="width:10%" class="text-center">CANT.</th>
        <th style="width:55%">DESCRIPCIÓN</th>
        <th style="width:17%" class="text-right">VALOR U.</th>
        <th style="width:18%" class="text-right">TOTAL</th>
      </tr>
    </thead>
    <tbody>
      @forelse($order->items as $item)
      @php
        // Si los precios en BD ya incluyen IGV → dividir para obtener valor sin IGV (base imponible)
        // Si los precios en BD son sin IGV    → usar directamente
        $unitVal = $pricesWithIgv ? round($item->unit_price / (1 + $igvRate), 4) : $item->unit_price;
        $lineVal = $pricesWithIgv ? round($item->subtotal   / (1 + $igvRate), 2) : $item->subtotal;
        $desc    = $item->product_description ?? optional($item->product)->name ?? 'Producto';
        if ($item->size) $desc .= ' - Talla: ' . strtoupper($item->size);
      @endphp
      <tr>
        <td class="text-center">{{ $item->quantity }}</td>
        <td>{{ $desc }}</td>
        <td class="text-right">{{ $money($unitVal) }}</td>
        <td class="text-right">{{ $money($lineVal) }}</td>
      </tr>
      @empty
      <tr><td colspan="4" class="text-center">Sin productos</td></tr>
      @endforelse
    </tbody>
  </table>

  {{-- ═══ TOTALES (flotados a la derecha) ═══ --}}
  <div style="overflow:hidden;margin-top:10px">
    <div class="summary-right">
      <table class="summary-table">
        <tr><td>OP. GRAVADAS:</td>      <td class="text-right">{{ $money($invoice->mto_oper_gravadas) }}</td></tr>
        <tr><td>IGV ({{ round($igvRate*100,0) }}%):</td><td class="text-right">{{ $money($invoice->mto_igv) }}</td></tr>
        <tr><td>ISC:</td>               <td class="text-right">{{ $money(0) }}</td></tr>
        <tr><td>ICBPER:</td>            <td class="text-right">{{ $money(0) }}</td></tr>
        <tr><td>OP. EXONERADAS:</td>    <td class="text-right">{{ $money(0) }}</td></tr>
      </table>

      <div class="total-box">
        <table class="total-box-row">
          <tr>
            <td class="total-label">TOTAL:</td>
            <td class="total-value">{{ $money($invoice->mto_imp_venta) }}</td>
          </tr>
        </table>
      </div>

      <table class="totals-table" style="margin-top:6px">
        <tr><td>TOTAL PAGADO:</td><td class="text-right">{{ $money($invoice->mto_imp_venta) }}</td></tr>
        <tr><td>VUELTO:</td>      <td class="text-right">{{ $money(0) }}</td></tr>
      </table>
    </div>
  </div>

  {{-- ═══ SON (ancho completo, después de limpiar float) ═══ --}}
  <div class="amount-block">
    <strong>SON: {{ _hiitop_amount_words(floatval($invoice->mto_imp_venta)) }}</strong>
  </div>

  {{-- ═══ FOOTER ═══ --}}
  <div class="footer">
    @if($invoice->status === 'accepted')
      <div class="muted" style="margin-bottom:6px">✔ Aceptado por SUNAT{{ $invoice->sunat_code ? ' · Código '.$invoice->sunat_code : '' }}</div>
    @endif
    <div class="thanks">MUCHAS GRACIAS POR TU COMPRA</div>
  </div>

</div>
</body>
</html>
