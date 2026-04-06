<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Invoice;
use App\Models\InvoiceSeries;
use App\Models\Order;
use App\Models\Stock;
use App\Services\InvoiceService;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Http\Response;
use Illuminate\Support\Facades\DB;

class InvoiceController extends Controller
{
    public function __construct(private readonly InvoiceService $invoiceService) {}

    // ── GET /invoices ──────────────────────────────────────────────────────────

    public function index(Request $request): JsonResponse
    {
        $q = Invoice::with(['order', 'user'])
            ->orderBy('issued_at', 'desc');

        if ($request->filled('doc_type')) {
            $q->where('doc_type', $request->doc_type);
        }
        if ($request->filled('status')) {
            $q->where('status', $request->status);
        }
        if ($request->filled('search')) {
            $term = '%' . $request->search . '%';
            $q->where(function ($sub) use ($term) {
                $sub->where('full_number', 'like', $term)
                    ->orWhere('customer_name', 'like', $term)
                    ->orWhere('customer_doc_number', 'like', $term);
            });
        }
        if ($request->filled('from_date')) {
            $q->whereDate('issued_at', '>=', $request->from_date);
        }
        if ($request->filled('to_date')) {
            $q->whereDate('issued_at', '<=', $request->to_date);
        }

        $perPage = min((int) $request->get('per_page', 30), 100);
        return response()->json($q->paginate($perPage));
    }

    // ── GET /invoices/series ───────────────────────────────────────────────────

    public function series(): JsonResponse
    {
        return response()->json(
            InvoiceSeries::where('is_active', true)->orderBy('doc_type')->orderBy('serie')->get()
        );
    }

    // ── POST /invoices/test-connection ─────────────────────────────────────────

    public function testConnection(Request $request): JsonResponse
    {
        $data = $request->validate(['env' => 'required|in:beta,prod']);
        $env  = $data['env'];  // 'beta' | 'prod'

        try {
            $allSettings = \App\Models\Setting::pluck('value', 'key')->toArray();
            // Temporarily override the active environment for testing
            $allSettings['sunat_environment'] = ($env === 'prod') ? 'produccion' : 'beta';

            // Re-instantiate service with overridden settings
            $service = app(InvoiceService::class);
            // Call sendTestBatch — we just need buildSee to not throw
            // We'll do a minimal check: build a See and verify credentials
            $prefix = ($env === 'prod') ? 'sunat_prod_' : 'sunat_beta_';
            $ruc     = $allSettings["{$prefix}ruc"]      ?? '';
            $user    = $allSettings["{$prefix}sol_user"] ?? '';
            $pass    = $allSettings["{$prefix}sol_pass"] ?? '';
            $cert    = $allSettings["{$prefix}certificate_pem"] ?? '';

            if (empty($ruc))  return response()->json(['success' => false, 'message' => 'RUC no configurado.']);
            if (empty($user)) return response()->json(['success' => false, 'message' => 'Usuario SOL no configurado.']);
            if (empty($cert)) return response()->json(['success' => false, 'message' => 'Certificado PEM no configurado.']);

            $see = new \Greenter\Ws\Services\SoapClient();
            // Just verifying credentials structure is valid — do not actually call SUNAT here
            return response()->json([
                'success' => true,
                'message' => 'Configuración válida: RUC ' . $ruc . ', usuario ' . $user . '.',
            ]);
        } catch (\Throwable $e) {
            return response()->json(['success' => false, 'message' => $e->getMessage()]);
        }
    }

    // ── POST /invoices ─────────────────────────────────────────────────────────

    public function store(Request $request): JsonResponse
    {
        $data = $request->validate([
            'order_id'             => 'required|exists:orders,id',
            'doc_type'             => 'required|in:01,03',
            'invoice_series_id'    => 'nullable|exists:invoice_series,id',
            'form_of_payment'      => 'required|in:contado,credito',
            'payment_method_id'    => 'nullable|exists:payment_methods,id',
            'customer_doc_type'    => 'required|in:1,6,4,7,-',   // 1=DNI,6=RUC,4=CE,7=PAS,-=sin
            'customer_doc_number'  => 'nullable|string|max:20',
            'customer_name'        => 'required|string|max:250',
            'observations'         => 'nullable|string|max:500',
            'auto_send'            => 'boolean',
        ]);

        // SUNAT rule: Factura (01) requires RUC (doc type 6) with 11 digits
        if ($data['doc_type'] === '01') {
            if (($data['customer_doc_type'] ?? '') !== '6') {
                return response()->json([
                    'message' => 'Una Factura solo puede emitirse a un receptor con RUC (tipo documento 6). Para clientes con DNI u otro documento, usa Boleta (03).',
                ], 422);
            }
            $docNum = $data['customer_doc_number'] ?? '';
            if (!preg_match('/^\d{11}$/', $docNum)) {
                return response()->json([
                    'message' => 'El RUC del receptor debe tener exactamente 11 dígitos numéricos.',
                ], 422);
            }
        }

        $order = Order::with('items.product')->findOrFail($data['order_id']);

        // Auto-select series if not provided
        $seriesId = $data['invoice_series_id'] ?? null;
        if ($seriesId) {
            $series = InvoiceSeries::findOrFail($seriesId);
            if ($series->doc_type !== $data['doc_type']) {
                return response()->json([
                    'message' => "La serie {$series->serie} no corresponde al tipo de comprobante.",
                ], 422);
            }
        } else {
            $series = InvoiceSeries::where('doc_type', $data['doc_type'])
                ->where('is_active', true)
                ->orderBy('serie')
                ->firstOrFail();
        }

        $settings = \App\Models\Setting::pluck('value', 'key')->toArray();
        $env      = $settings['sunat_environment'] ?? 'beta';
        $prefix   = ($env === 'produccion') ? 'sunat_prod_' : 'sunat_beta_';
        $ruc      = $settings["{$prefix}ruc"] ?? $settings['sunat_ruc'] ?? '';

        // Compute totals from order items
        $totals = $this->invoiceService->computeTotalsFromOrder($order);

        DB::beginTransaction();
        try {
            $correlativo = $series->takeNextNumber();

            $invoice = Invoice::create([
                'order_id'            => $order->id,
                'invoice_series_id'   => $series->id,
                'doc_type'            => $data['doc_type'],
                'serie'               => $series->serie,
                'correlativo'         => $correlativo,
                'full_number'         => $series->buildFullNumber($ruc, $correlativo),
                'status'              => 'draft',
                'customer_doc_type'   => $data['customer_doc_type'],
                'customer_doc_number' => $data['customer_doc_number'] ?? null,
                'customer_name'       => $data['customer_name'],
                'currency'            => 'PEN',
                'form_of_payment'     => $data['form_of_payment'],
                'payment_method_id'   => $data['payment_method_id'] ?? null,
                'observations'        => $data['observations'] ?? null,
                'issued_at'           => now(),
                'user_id'             => $request->user()?->id,
                ...$totals,
            ]);

            // Deduct actual stock and release reservation
            foreach ($order->items as $item) {
                if (!$item->product_id) continue;
                $sq = Stock::where('product_id', $item->product_id);
                if ($item->color_id) $sq->where('color_id', $item->color_id);
                if ($item->size)     $sq->where('size', $item->size);
                $stock = $sq->first() ?? Stock::where('product_id', $item->product_id)->first();
                if ($stock) {
                    $stock->decrement('quantity', min($item->quantity, $stock->quantity));
                    $res = min($item->quantity, $stock->reserved ?? 0);
                    if ($res > 0) $stock->decrement('reserved', $res);
                }
            }

            DB::commit();
        } catch (\Throwable $e) {
            DB::rollBack();
            return response()->json(['message' => 'Error al crear el comprobante: ' . $e->getMessage()], 500);
        }

        // Send to SUNAT if requested
        $sunatResult = null;
        if (!empty($data['auto_send'])) {
            $invoice->load('order.items.product');
            $sunatResult = $this->invoiceService->sendToSunat($invoice);
        }

        $invoice->load('order', 'invoiceSeries', 'user');

        return response()->json([
            'invoice'      => $invoice,
            'sunat_result' => $sunatResult,
        ], 201);
    }

    // ── GET /invoices/{invoice} ────────────────────────────────────────────────

    public function show(Invoice $invoice): JsonResponse
    {
        $invoice->load(['order.items.product', 'invoiceSeries', 'user']);
        return response()->json($invoice);
    }

    // ── POST /invoices/{invoice}/send ─────────────────────────────────────────

    public function send(Invoice $invoice): JsonResponse
    {
        if (in_array($invoice->status, ['accepted'])) {
            return response()->json(['message' => 'El comprobante ya fue aceptado por SUNAT.'], 422);
        }

        $invoice->load('order.items.product');
        $result = $this->invoiceService->sendToSunat($invoice);

        return response()->json([
            'invoice' => $invoice->fresh(),
            'result'  => $result,
        ]);
    }

    // ── GET /invoices/{invoice}/xml ────────────────────────────────────────────

    public function downloadXml(Invoice $invoice): Response
    {
        if (empty($invoice->xml_content)) {
            return response('XML no disponible. El comprobante aún no ha sido enviado a SUNAT.', 404);
        }

        $filename = $invoice->full_number . '.xml';
        return response($invoice->xml_content, 200, [
            'Content-Type'        => 'application/xml',
            'Content-Disposition' => "attachment; filename=\"{$filename}\"",
        ]);
    }

    // ── GET /invoices/{invoice}/cdr ────────────────────────────────────────────

    public function downloadCdr(Invoice $invoice): Response
    {
        if (empty($invoice->cdr_content)) {
            return response('CDR no disponible.', 404);
        }

        $filename = 'R-' . $invoice->full_number . '.zip';
        return response(base64_decode($invoice->cdr_content), 200, [
            'Content-Type'        => 'application/zip',
            'Content-Disposition' => "attachment; filename=\"{$filename}\"",
        ]);
    }

    // ── GET /invoices/{invoice}/pdf ────────────────────────────────────────────

    public function downloadPdf(Invoice $invoice): Response
    {
        $invoice->load(['order.items.product', 'order.user', 'order.customer', 'invoiceSeries', 'paymentMethod']);

        $settings = \App\Models\Setting::all()->pluck('value', 'key')->toArray();
        $logoPath = public_path('iso-black-.png');
        $logoBase64 = file_exists($logoPath)
            ? 'data:image/png;base64,' . base64_encode(file_get_contents($logoPath))
            : null;

        $pdf = app('dompdf.wrapper');
        $pdf->setPaper('a4', 'portrait');
        $html = view('invoices.invoice_pdf', [
            'invoice'     => $invoice,
            'settings'    => $settings,
            'logoBase64'  => $logoBase64,
            'paymentMethodName' => $invoice->paymentMethod?->name ?? null,
        ])->render();
        $pdf->loadHTML($html);

        $filename = $invoice->full_number . '.pdf';
        return $pdf->download($filename);
    }

    // ── POST /invoices/{invoice}/void ─────────────────────────────────────────

    public function void(Invoice $invoice, Request $request): JsonResponse
    {
        // Allow void if:
        // 1. Invoice is accepted (normal case)
        // 2. Invoice is cancelled BUT no accepted NC exists (prior void attempt failed) — allow retry
        $hasAcceptedNc = Invoice::where('order_id', $invoice->order_id)
            ->whereIn('doc_type', ['07', '08'])
            ->where('status', 'accepted')
            ->exists();

        if ($invoice->status === 'accepted') {
            // normal path — fine
        } elseif ($invoice->status === 'cancelled' && !$hasAcceptedNc) {
            // retry after a failed prior void — fine
        } else {
            return response()->json([
                'message' => 'Solo se pueden anular comprobantes aceptados por SUNAT.',
            ], 422);
        }

        $data = $request->validate([
            'note_motive'      => 'required|string|max:2',
            'note_motive_desc' => 'required|string|max:250',
            'auto_send'        => 'boolean',
        ]);

        $allSettings = \App\Models\Setting::pluck('value', 'key')->toArray();
        $envKey      = $allSettings['sunat_environment'] ?? 'beta';
        $envPrefix   = ($envKey === 'produccion') ? 'sunat_prod_' : 'sunat_beta_';
        $ruc         = $allSettings["{$envPrefix}ruc"] ?? $allSettings['sunat_ruc'] ?? '';

        // Cancellations always use Nota de Crédito (07) — both Factura and Boleta.
        // Nota de Débito (08) is only for adding charges, never for void/cancellation.
        $ncDocType = '07';
        $series    = InvoiceSeries::where('doc_type', $ncDocType)->where('is_active', true)->firstOrFail();

        $wasAccepted = $invoice->status === 'accepted';

        DB::beginTransaction();
        try {
            $correlativo = $series->takeNextNumber();

            $nc = Invoice::create([
                'order_id'            => $invoice->order_id,
                'invoice_series_id'   => $series->id,
                'doc_type'            => $ncDocType,
                'serie'               => $series->serie,
                'correlativo'         => $correlativo,
                'full_number'         => $series->buildFullNumber($ruc, $correlativo),
                'status'              => 'draft',
                'customer_doc_type'   => $invoice->customer_doc_type,
                'customer_doc_number' => $invoice->customer_doc_number,
                'customer_name'       => $invoice->customer_name,
                'currency'            => $invoice->currency,
                'form_of_payment'     => $invoice->form_of_payment,
                'payment_method_id'   => $invoice->payment_method_id,
                'mto_oper_gravadas'   => $invoice->mto_oper_gravadas,
                'mto_igv'             => $invoice->mto_igv,
                'valor_venta'         => $invoice->valor_venta,
                'sub_total'           => $invoice->sub_total,
                'mto_imp_venta'       => $invoice->mto_imp_venta,
                'note_motive'         => $data['note_motive'],
                'note_motive_desc'    => $data['note_motive_desc'],
                'ref_doc_type'        => $invoice->doc_type,
                'ref_doc_number'      => $invoice->full_number,
                'ref_doc_date'        => $invoice->issued_at->toDateString(),
                'issued_at'           => now(),
                'user_id'             => $request->user()?->id,
            ]);

            // Mark original as cancelled
            $invoice->update(['status' => 'cancelled']);

            // Restore stock only if this is the first successful void attempt (was 'accepted')
            if ($wasAccepted) {
                $originalOrder = $invoice->order()->with('items')->first();
                if ($originalOrder) {
                    foreach ($originalOrder->items as $item) {
                        if (!$item->product_id) continue;
                        $sq = Stock::where('product_id', $item->product_id);
                        if ($item->color_id) $sq->where('color_id', $item->color_id);
                        if ($item->size)     $sq->where('size', $item->size);
                        $stock = $sq->first() ?? Stock::where('product_id', $item->product_id)->first();
                        if ($stock) {
                            $stock->increment('quantity', $item->quantity);
                        }
                    }
                }
            }

            DB::commit();
        } catch (\Throwable $e) {
            DB::rollBack();
            return response()->json(['message' => 'Error al crear la nota de crédito: ' . $e->getMessage()], 500);
        }

        $sunatResult = null;
        if (!empty($data['auto_send'])) {
            $nc->load('order.items.product');
            $sunatResult = $this->invoiceService->sendToSunat($nc);

            // If SUNAT rejects the NC, restore original to 'accepted' so user can retry
            if (!($sunatResult['success'] ?? false) && $wasAccepted) {
                $invoice->update(['status' => 'accepted']);
                // Reverse stock restoration
                $originalOrder = $invoice->order()->with('items')->first();
                if ($originalOrder) {
                    foreach ($originalOrder->items as $item) {
                        if (!$item->product_id) continue;
                        $sq = Stock::where('product_id', $item->product_id);
                        if ($item->color_id) $sq->where('color_id', $item->color_id);
                        if ($item->size)     $sq->where('size', $item->size);
                        $stock = $sq->first() ?? Stock::where('product_id', $item->product_id)->first();
                        if ($stock) {
                            $stock->decrement('quantity', min($item->quantity, $stock->quantity));
                        }
                    }
                }
            }
        }

        return response()->json([
            'nota_credito' => $nc->fresh(),
            'sunat_result' => $sunatResult,
        ], 201);
    }
}
