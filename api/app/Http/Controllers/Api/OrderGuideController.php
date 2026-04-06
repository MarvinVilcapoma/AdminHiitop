<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\DocumentType;
use App\Models\Order;
use App\Services\DespatchService;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Http\Response;
use Illuminate\Support\Facades\DB;
use Illuminate\Validation\ValidationException;

class OrderGuideController extends Controller
{
    public function __construct(private readonly DespatchService $despatchService) {}

    public function index(Request $request): JsonResponse
    {
        $query = Order::query()
            ->with(['orderStatus', 'documentType', 'customer', 'user'])
            ->whereHas('documentType', fn ($q) => $q->where('code', 'GUIA_REMISION'))
            ->orderByDesc('order_date')
            ->orderByDesc('id');

        if ($request->filled('search')) {
            $search = trim((string) $request->search);
            $query->where(function ($q) use ($search) {
                $q->where('order_number', 'like', '%' . $search . '%')
                    ->orWhere('customer_name', 'like', '%' . $search . '%')
                    ->orWhere('dni', 'like', '%' . $search . '%')
                    ->orWhere('guide_full_number', 'like', '%' . $search . '%')
                    ->orWhere('guide_recipient_name', 'like', '%' . $search . '%')
                    ->orWhere('guide_vehicle_plate', 'like', '%' . $search . '%');
            });
        }

        if ($request->filled('guide_status')) {
            $query->where('guide_status', $request->guide_status);
        }

        if ($request->filled('from_date')) {
            $query->whereDate('order_date', '>=', $request->from_date);
        }

        if ($request->filled('to_date')) {
            $query->whereDate('order_date', '<=', $request->to_date);
        }

        $perPage = (int) $request->get('per_page', 15);

        return response()->json($query->paginate($perPage));
    }

    public function show(Order $order): JsonResponse
    {
        return response()->json([
            'order_id' => $order->id,
            'guide_series' => $order->guide_series,
            'guide_correlativo' => $order->guide_correlativo,
            'guide_full_number' => $order->guide_full_number,
            'guide_status' => $order->guide_status,
            'guide_sunat_code' => $order->guide_sunat_code,
            'guide_sunat_description' => $order->guide_sunat_description,
            'guide_sent_at' => $order->guide_sent_at,
        ]);
    }

    public function send(Request $request, Order $order): JsonResponse
    {
        $data = $request->validate([
            'series' => 'nullable|string|max:10',
            'force_new_number' => 'nullable|boolean',
        ]);

        $this->assertGuideDocument($order);

        if ($order->guide_status === 'accepted' && !$request->boolean('force_new_number')) {
            throw ValidationException::withMessages([
                'guide_status' => 'La guía de remisión ya fue aceptada por SUNAT para este pedido.',
            ]);
        }

        DB::beginTransaction();
        try {
            $forceNewNumber = (bool) ($data['force_new_number'] ?? false);
            $series = trim((string) ($data['series'] ?? $order->guide_series ?? $this->despatchService->defaultSeries()));
            if ($series === '') {
                $series = $this->despatchService->defaultSeries();
            }

            if ($forceNewNumber || !$order->guide_series || !$order->guide_correlativo || $order->guide_series !== $series) {
                $nextCorrelativo = (int) (Order::query()
                        ->where('guide_series', $series)
                        ->lockForUpdate()
                        ->max('guide_correlativo') ?? 0) + 1;

                $order->update([
                    'guide_series' => $series,
                    'guide_correlativo' => $nextCorrelativo,
                    'guide_full_number' => $series . '-' . str_pad((string) $nextCorrelativo, 8, '0', STR_PAD_LEFT),
                    'guide_status' => 'draft',
                    'guide_sunat_code' => null,
                    'guide_sunat_description' => null,
                    'guide_xml_content' => null,
                    'guide_cdr_content' => null,
                    'guide_sent_at' => null,
                ]);
            }

            DB::commit();
        } catch (\Throwable $e) {
            DB::rollBack();

            return response()->json([
                'success' => false,
                'message' => 'No se pudo preparar la numeración de la guía: ' . $e->getMessage(),
            ], 500);
        }

        try {
            $result = $this->despatchService->sendToSunat($order->fresh());

            return response()->json([
                'success' => (bool) ($result['success'] ?? false),
                'result' => $result,
                'order' => $order->fresh(),
            ]);
        } catch (\Throwable $e) {
            $order->update([
                'guide_status' => 'error',
                'guide_sunat_description' => $e->getMessage(),
            ]);

            return response()->json([
                'success' => false,
                'message' => $e->getMessage(),
                'order' => $order->fresh(),
            ], 500);
        }
    }

    public function downloadXml(Order $order): Response
    {
        if (empty($order->guide_xml_content)) {
            return response('XML no disponible para esta guía.', 404);
        }

        $filename = ($order->guide_full_number ?: ('GUIA-' . $order->id)) . '.xml';

        return response($order->guide_xml_content, 200, [
            'Content-Type' => 'application/xml',
            'Content-Disposition' => "attachment; filename=\"{$filename}\"",
        ]);
    }

    public function downloadCdr(Order $order): Response
    {
        if (empty($order->guide_cdr_content)) {
            return response('CDR no disponible para esta guía.', 404);
        }

        $filename = 'R-' . ($order->guide_full_number ?: ('GUIA-' . $order->id)) . '.zip';

        return response(base64_decode($order->guide_cdr_content), 200, [
            'Content-Type' => 'application/zip',
            'Content-Disposition' => "attachment; filename=\"{$filename}\"",
        ]);
    }

    private function assertGuideDocument(Order $order): void
    {
        if (!$order->document_type_id) {
            throw ValidationException::withMessages([
                'document_type_id' => 'El pedido no tiene tipo de documento asignado.',
            ]);
        }

        $code = DocumentType::query()->where('id', $order->document_type_id)->value('code');
        if (mb_strtoupper((string) $code) !== 'GUIA_REMISION') {
            throw ValidationException::withMessages([
                'document_type_id' => 'Este pedido no está marcado como Guía de Remisión.',
            ]);
        }
    }
}
