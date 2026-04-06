<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\DocumentType;
use App\Models\Order;
use App\Models\OrderStatus;
use App\Models\Setting;
use App\Models\Stock;
use App\Models\Warehouse;
use App\Services\OrderGuideValidationService;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Schema;
use Illuminate\Validation\ValidationException;

class OrderController extends Controller
{
    private const IMMUTABLE_STATUS_SLUGS = ['cancelled', 'cancelado', 'delivered', 'entregado'];
    private const POS_ALLOWED_DOCUMENT_CODES = ['BOLETA', 'FACTURA', 'TICKET', 'NOTA_VENTA'];

    public function __construct(private readonly OrderGuideValidationService $guideValidationService) {}

    public function index(Request $request): JsonResponse
    {
        $query = Order::query()
            ->with([
                'orderStatus', 'shippingAgency', 'purchaseType', 'customer',
                'province', 'district', 'documentType', 'warehouse', 'items.product', 'items.collection',
                'user',
                'invoices' => fn($q) => $q->whereNotIn('status', ['cancelled'])->select('id','order_id','status','doc_type','full_number'),
            ])
            ->orderByDesc('order_date')
            ->orderByDesc('id');

        if ($request->filled('search')) {
            $s = $request->search;
            $query->where(function ($q) use ($s) {
                $q->where('order_number', 'like', '%' . $s . '%')
                    ->orWhere('customer_name', 'like', '%' . $s . '%')
                    ->orWhere('dni', 'like', '%' . $s . '%')
                    ->orWhere('phone', 'like', '%' . $s . '%');
            });
        }
        if ($request->filled('order_status_id')) {
            $query->where('order_status_id', $request->order_status_id);
        }
        if ($request->filled('shipping_agency_id')) {
            $query->where('shipping_agency_id', $request->shipping_agency_id);
        }
        if ($request->filled('from_date')) {
            $query->whereDate('order_date', '>=', $request->from_date);
        }
        if ($request->filled('to_date')) {
            $query->whereDate('order_date', '<=', $request->to_date);
        }
        if ($request->filled('collection_id')) {
            $query->whereHas('items', fn ($q) => $q->where('collection_id', $request->collection_id));
        }
        if ($request->filled('product_type_id')) {
            $query->whereHas('items.product', fn ($q) => $q->where('product_type_id', $request->product_type_id));
        }
        if ($request->filled('source')) {
            $source = mb_strtolower(trim((string) $request->source));

            if ($source === 'pos') {
                $posStatusId = OrderStatus::query()->where('slug', 'venta-pos')->value('id');
                $query->where(function ($q) use ($posStatusId) {
                    if ($posStatusId) {
                        $q->where('order_status_id', $posStatusId)
                            ->orWhere('observations', 'like', 'POS%');
                        return;
                    }

                    $q->where('observations', 'like', 'POS%');
                });
            }
        }

        $perPage = (int) $request->get('per_page', 15);
        $orders = $query->paginate($perPage);

        // Resumen para dashboard
        if ($request->boolean('with_summary')) {
            $orders->summary = [
                'total_orders' => Order::count(),
                'pending_shipping' => Order::whereHas('orderStatus', fn ($q) => $q->whereIn('slug', ['pendiente', 'pending', 'en-camino', 'processing']))->count(),
                'total_revenue' => (float) Order::sum('total'),
            ];
        }

        return response()->json($orders);
    }

    public function store(Request $request): JsonResponse
    {
        $rules = [
            'order_date' => 'required|date',
            'is_pos' => 'sometimes|boolean',
            'order_status_id' => 'required_without:is_pos|nullable|exists:order_statuses,id',
            'shipping_agency_id' => 'nullable|exists:shipping_agencies,id',
            'purchase_type_id' => 'nullable|exists:purchase_types,id',
            'observations' => 'nullable|string',
            'phone' => 'nullable|string|max:30',
            'customer_id' => 'nullable|exists:customers,id',
            'customer_name' => 'nullable|string|max:255',
            'dni' => 'nullable|string|max:20',
            'province_id' => 'nullable|exists:provinces,id',
            'district_id' => 'nullable|exists:districts,id',
            'address' => 'nullable|string',
            'delivery_cost' => 'nullable|numeric|min:0',
            'total' => 'nullable|numeric|min:0',
            'document_type_id' => 'nullable|exists:document_types,id',
            'document_number' => 'nullable|string|max:50',
            'customer_email' => 'nullable|email',
            'needs_receipt' => 'nullable|boolean',
            'guide_transfer_reason_code' => 'nullable|string|max:4',
            'guide_transfer_reason_description' => 'nullable|string|max:255',
            'guide_transfer_mode' => 'nullable|in:01,02',
            'guide_transfer_date' => 'nullable|date',
            'guide_total_weight' => 'nullable|numeric|min:0.001',
            'guide_weight_unit' => 'nullable|string|max:3',
            'guide_package_count' => 'nullable|integer|min:1',
            'guide_origin_ubigeo' => 'nullable|string|max:6',
            'guide_origin_address' => 'nullable|string|max:255',
            'guide_destination_ubigeo' => 'nullable|string|max:6',
            'guide_destination_address' => 'nullable|string|max:255',
            'guide_recipient_doc_type' => 'nullable|string|max:2',
            'guide_recipient_doc_number' => 'nullable|string|max:20',
            'guide_recipient_name' => 'nullable|string|max:255',
            'guide_carrier_doc_type' => 'nullable|string|max:2',
            'guide_carrier_doc_number' => 'nullable|string|max:20',
            'guide_carrier_name' => 'nullable|string|max:255',
            'guide_vehicle_plate' => 'nullable|string|max:20',
            'guide_driver_doc_type' => 'nullable|string|max:2',
            'guide_driver_doc_number' => 'nullable|string|max:20',
            'guide_driver_name' => 'nullable|string|max:255',
            'guide_driver_license' => 'nullable|string|max:30',
            'guide_transport_certificate' => 'nullable|string|max:60',
            'items' => 'required|array|min:1',
            'items.*.product_id' => 'nullable|exists:products,id',
            'items.*.collection_id' => 'nullable|exists:collections,id',
            'items.*.product_description' => 'nullable|string',
            'items.*.size' => 'nullable|string|max:20',
            'items.*.quantity' => 'required|integer|min:1',
            'items.*.unit_price' => 'required|numeric|min:0',
            'items.*.subtotal' => 'required|numeric|min:0',
        ];

        if (Schema::hasColumn('order_items', 'color_id')) {
            $rules['items.*.color_id'] = 'nullable|exists:colors,id';
        }

        if (Schema::hasColumn('orders', 'warehouse_id')) {
            $rules['warehouse_id'] = 'nullable|exists:warehouses,id';
        }

        $data = $request->validate($rules);
        $this->guideValidationService->validate($data);
        $isPos = (bool) ($data['is_pos'] ?? false);
        unset($data['is_pos']);

        if ($isPos) {
            $this->assertPosDocumentType($data);
        }

        if (empty($data['order_status_id'])) {
            $data['order_status_id'] = $this->resolveDefaultOrderStatusId($isPos);
        }

        if ($isPos && Schema::hasColumn('orders', 'warehouse_id') && empty($data['warehouse_id'])) {
            $data['warehouse_id'] = $this->resolvePosWarehouseId();
        }

        if ($isPos && Schema::hasColumn('orders', 'warehouse_id') && empty($data['warehouse_id'])) {
            throw ValidationException::withMessages([
                'warehouse_id' => 'No hay almacén activo configurado para ventas POS.',
            ]);
        }

        if ($isPos && Schema::hasColumn('orders', 'warehouse_id')) {
            $this->assertPosWarehouseId($data['warehouse_id'] ?? null);
        }

        $data['user_id'] = $request->user()?->id;
        $items = $data['items'];
        unset($data['items']);

        $order = Order::create($data);

        if (empty($order->order_number)) {
            $orderDatePart = $order->order_date?->format('Ymd') ?? now()->format('Ymd');
            $order->order_number = 'ORD-' . $orderDatePart . '-' . str_pad((string) $order->id, 6, '0', STR_PAD_LEFT);
            $order->save();
        }

        $hasColorColumn = Schema::hasColumn('order_items', 'color_id');
        foreach ($items as $i => $item) {
            if (!$hasColorColumn) {
                unset($item['color_id']);
            }
            $item['order_id'] = $order->id;
            $item['sort_order'] = $i;
            $order->items()->create($item);
        }

        $order->load('items', 'orderStatus');
        if (!$this->isCancelledStatus($order->orderStatus?->slug)) {
            $this->applyReservationForOrder($order, true);
        }

        return response()->json($order->load(['orderStatus', 'shippingAgency', 'purchaseType', 'warehouse', 'items.product', 'items.collection']), 201);
    }

    public function show(Order $order): JsonResponse
    {
        $order->load([
            'orderStatus', 'shippingAgency', 'purchaseType', 'customer',
            'province', 'district', 'documentType', 'warehouse', 'items.product.colors', 'items.collection',
            'invoices' => fn($q) => $q->select('id','order_id','status','doc_type','full_number'),
        ]);
        return response()->json($order);
    }

    public function update(Request $request, Order $order): JsonResponse
    {
        $order->load('items', 'orderStatus');
        $wasCancelled = $this->isCancelledStatus($order->orderStatus?->slug);

        if ($this->isImmutableStatus($order->orderStatus?->slug)) {
            throw ValidationException::withMessages([
                'order_status_id' => 'Este pedido está en estado entregado/cancelado y ya no puede modificarse.',
            ]);
        }

        $rules = [
            'order_date' => 'sometimes|date',
            'order_status_id' => 'sometimes|exists:order_statuses,id',
            'shipping_agency_id' => 'nullable|exists:shipping_agencies,id',
            'purchase_type_id' => 'nullable|exists:purchase_types,id',
            'observations' => 'nullable|string',
            'phone' => 'nullable|string|max:30',
            'customer_id' => 'nullable|exists:customers,id',
            'customer_name' => 'nullable|string|max:255',
            'dni' => 'nullable|string|max:20',
            'province_id' => 'nullable|exists:provinces,id',
            'district_id' => 'nullable|exists:districts,id',
            'address' => 'nullable|string',
            'delivery_cost' => 'nullable|numeric|min:0',
            'total' => 'nullable|numeric|min:0',
            'document_type_id' => 'nullable|exists:document_types,id',
            'document_number' => 'nullable|string|max:50',
            'customer_email' => 'nullable|email',
            'needs_receipt' => 'nullable|boolean',
            'guide_transfer_reason_code' => 'nullable|string|max:4',
            'guide_transfer_reason_description' => 'nullable|string|max:255',
            'guide_transfer_mode' => 'nullable|in:01,02',
            'guide_transfer_date' => 'nullable|date',
            'guide_total_weight' => 'nullable|numeric|min:0.001',
            'guide_weight_unit' => 'nullable|string|max:3',
            'guide_package_count' => 'nullable|integer|min:1',
            'guide_origin_ubigeo' => 'nullable|string|max:6',
            'guide_origin_address' => 'nullable|string|max:255',
            'guide_destination_ubigeo' => 'nullable|string|max:6',
            'guide_destination_address' => 'nullable|string|max:255',
            'guide_recipient_doc_type' => 'nullable|string|max:2',
            'guide_recipient_doc_number' => 'nullable|string|max:20',
            'guide_recipient_name' => 'nullable|string|max:255',
            'guide_carrier_doc_type' => 'nullable|string|max:2',
            'guide_carrier_doc_number' => 'nullable|string|max:20',
            'guide_carrier_name' => 'nullable|string|max:255',
            'guide_vehicle_plate' => 'nullable|string|max:20',
            'guide_driver_doc_type' => 'nullable|string|max:2',
            'guide_driver_doc_number' => 'nullable|string|max:20',
            'guide_driver_name' => 'nullable|string|max:255',
            'guide_driver_license' => 'nullable|string|max:30',
            'guide_transport_certificate' => 'nullable|string|max:60',
            'items' => 'sometimes|array|min:1',
            'items.*.id' => 'nullable|exists:order_items,id',
            'items.*.product_id' => 'nullable|exists:products,id',
            'items.*.collection_id' => 'nullable|exists:collections,id',
            'items.*.product_description' => 'nullable|string',
            'items.*.size' => 'nullable|string|max:20',
            'items.*.quantity' => 'required_with:items|integer|min:1',
            'items.*.unit_price' => 'required_with:items|numeric|min:0',
            'items.*.subtotal' => 'required_with:items|numeric|min:0',
        ];

        if (Schema::hasColumn('order_items', 'color_id')) {
            $rules['items.*.color_id'] = 'nullable|exists:colors,id';
        }

        if (Schema::hasColumn('orders', 'warehouse_id')) {
            $rules['warehouse_id'] = 'nullable|exists:warehouses,id';
        }

        $data = $request->validate($rules);
        $this->guideValidationService->validate($data, $order);
        $hasItemsUpdate = array_key_exists('items', $data);

        if ($hasItemsUpdate && !$wasCancelled) {
            $this->applyReservationForOrder($order, false);
        }

        if ($hasItemsUpdate) {
            $this->syncOrderItems($order, $data);
        }

        $order->update($data);
        $order->load([
            'orderStatus', 'shippingAgency', 'purchaseType', 'customer',
            'province', 'district', 'documentType', 'warehouse', 'items.product', 'items.collection',
        ]);

        $this->syncReservationAfterUpdate($order, $wasCancelled, $hasItemsUpdate);

        return response()->json($order);
    }

    public function destroy(Order $order): JsonResponse
    {
        $order->load('orderStatus', 'items');

        $validationError = $this->deletionValidationMessage($order);
        if ($validationError !== null) {
            return response()->json(['message' => $validationError], 422);
        }

        $this->applyReservationForOrder($order, false);

        $order->delete();

        return response()->json(null, 204);
    }

    private function resolveDefaultOrderStatusId(bool $isPos): int
    {
        if ($isPos) {
            $status = OrderStatus::firstOrCreate(
                ['slug' => 'venta-pos'],
                [
                    'name' => 'Venta POS',
                    'color' => '#10b981',
                    'sort_order' => 50,
                    'is_active' => true,
                ]
            );

            return (int) $status->id;
        }

        $fallbackId = OrderStatus::query()
            ->where('slug', 'reservado')
            ->value('id')
            ?? OrderStatus::query()->where('is_active', true)->orderBy('sort_order')->value('id')
            ?? OrderStatus::query()->orderBy('id')->value('id');

        if (!$fallbackId) {
            throw ValidationException::withMessages([
                'order_status_id' => 'No hay estados de pedido configurados.',
            ]);
        }

        return (int) $fallbackId;
    }

    private function assertPosDocumentType(array $data): void
    {
        $documentTypeId = $data['document_type_id'] ?? null;
        if (!$documentTypeId) {
            throw ValidationException::withMessages([
                'document_type_id' => 'Debes seleccionar un tipo de comprobante de tienda para POS.',
            ]);
        }

        $code = mb_strtoupper((string) DocumentType::query()->where('id', $documentTypeId)->value('code'));
        if (!in_array($code, self::POS_ALLOWED_DOCUMENT_CODES, true)) {
            throw ValidationException::withMessages([
                'document_type_id' => 'POS solo permite Boleta, Factura, Ticket o Nota de Venta.',
            ]);
        }
    }

    private function resolvePosWarehouseId(): ?int
    {
        $resolvedWarehouseId = null;

        $activeWarehouses = Warehouse::query()->where('is_active', true);

        $configuredWarehouseId = (int) (Setting::query()->where('key', 'pos_default_warehouse_id')->value('value') ?? 0);
        if ($configuredWarehouseId > 0) {
            $existsQuery = Warehouse::query()
                ->where('id', $configuredWarehouseId)
                ->where('is_active', true);

            if (Schema::hasColumn('warehouses', 'is_pos')) {
                $existsQuery->where('is_pos', true);
            }

            $exists = $existsQuery->exists();

            if ($exists) {
                $resolvedWarehouseId = $configuredWarehouseId;
            }
        }

        if (!$resolvedWarehouseId && Schema::hasColumn('warehouses', 'is_pos')) {
            $posWarehouse = (clone $activeWarehouses)
                ->where('is_pos', true)
                ->orderBy('name')
                ->value('id');

            if ($posWarehouse) {
                $resolvedWarehouseId = (int) $posWarehouse;
            }
        }

        if (!$resolvedWarehouseId && Schema::hasColumn('warehouses', 'type')) {
            $storeByLegacyType = (clone $activeWarehouses)
                ->where('type', 'store')
                ->orderBy('name')
                ->value('id');

            if ($storeByLegacyType) {
                $resolvedWarehouseId = (int) $storeByLegacyType;
            }
        }

        if (
            !$resolvedWarehouseId
            && Schema::hasColumn('warehouses', 'warehouse_type_id')
            && Schema::hasTable('warehouse_types')
        ) {
            $storeByWarehouseType = (clone $activeWarehouses)
                ->whereHas('warehouseType', function ($q) {
                    $q->whereIn('code', ['TIENDA_FISICA', 'STORE'])
                        ->orWhere('name', 'like', '%tienda%');
                })
                ->orderBy('name')
                ->value('id');

            if ($storeByWarehouseType) {
                $resolvedWarehouseId = (int) $storeByWarehouseType;
            }
        }

        if (!$resolvedWarehouseId) {
            $firstActive = (clone $activeWarehouses)
                ->orderBy('name')
                ->value('id');

            if ($firstActive) {
                $resolvedWarehouseId = (int) $firstActive;
            }
        }

        return $resolvedWarehouseId;
    }

    private function assertPosWarehouseId(?int $warehouseId): void
    {
        if (!$warehouseId || !Schema::hasColumn('warehouses', 'is_pos')) {
            return;
        }

        $isPosWarehouse = Warehouse::query()
            ->where('id', $warehouseId)
            ->where('is_active', true)
            ->where('is_pos', true)
            ->exists();

        if (!$isPosWarehouse) {
            throw ValidationException::withMessages([
                'warehouse_id' => 'El almacén seleccionado no está habilitado como punto de venta.',
            ]);
        }
    }

    private function syncOrderItems(Order $order, array &$data): void
    {
        $hasColorColumn = Schema::hasColumn('order_items', 'color_id');
        $items = $data['items'] ?? [];

        $order->items()->whereNotIn('id', collect($items)->pluck('id')->filter())->delete();

        foreach ($items as $index => $item) {
            $itemData = collect($item)->except('id')->toArray();

            if (!$hasColorColumn) {
                unset($itemData['color_id']);
            }

            $itemData['sort_order'] = $index;

            if (!empty($item['id'])) {
                $order->items()->where('id', $item['id'])->update($itemData);
                continue;
            }

            $order->items()->create(array_merge($itemData, ['order_id' => $order->id]));
        }

        unset($data['items']);
    }

    private function syncReservationAfterUpdate(Order $order, bool $wasCancelled, bool $hasItemsUpdate): void
    {
        $isCancelled = $this->isCancelledStatus($order->orderStatus?->slug);
        if ($isCancelled) {
            $this->applyReservationForOrder($order, false);
            return;
        }

        if ($wasCancelled || $hasItemsUpdate) {
            $this->applyReservationForOrder($order, true);
        }
    }

    private function deletionValidationMessage(Order $order): ?string
    {
        $message = null;

        if ($this->isImmutableStatus($order->orderStatus?->slug)) {
            $message = 'No se puede eliminar un pedido entregado o cancelado.';
        } elseif ($order->invoices()->where('status', '!=', 'cancelled')->exists()) {
            $message = 'No se puede eliminar un pedido que tiene comprobantes emitidos. Anula los comprobantes primero.';
        } elseif ($order->document_number) {
            $message = 'No se puede eliminar un pedido con documento emitido.';
        }

        return $message;
    }

    private function isCancelledStatus(?string $slug): bool
    {
        if (!$slug) {
            return false;
        }

        return in_array(mb_strtolower(trim($slug)), ['cancelado', 'cancelled'], true);
    }

    private function isImmutableStatus(?string $slug): bool
    {
        if (!$slug) {
            return false;
        }

        return in_array(mb_strtolower(trim($slug)), self::IMMUTABLE_STATUS_SLUGS, true);
    }

    private function applyReservationForOrder(Order $order, bool $reserve): void
    {
        foreach ($order->items as $item) {
            $productId = (int) ($item->product_id ?? 0);
            $quantity = max(0, (int) ($item->quantity ?? 0));

            if ($productId <= 0 || $quantity <= 0) {
                continue;
            }

            $stock = $this->findStockForOrderItem(
                $order,
                $productId,
                $item->color_id ? (int) $item->color_id : null,
                $item->size ? (string) $item->size : null
            );

            if (!$stock) {
                continue;
            }

            if ($reserve) {
                $stock->increment('reserved', $quantity);
                continue;
            }

            $toRelease = min($quantity, (int) ($stock->reserved ?? 0));
            if ($toRelease > 0) {
                $stock->decrement('reserved', $toRelease);
            }
        }
    }

    private function findStockForOrderItem(Order $order, int $productId, ?int $colorId, ?string $size): ?Stock
    {
        $warehouseId = $this->resolveOrderWarehouseId($order);

        $exactStock = $this->findExactStockForItem($productId, $colorId, $size, $warehouseId);
        if ($exactStock) {
            return $exactStock;
        }

        if ($warehouseId) {
            return $this->findWarehouseFallbackStock($productId, $colorId, $warehouseId);
        }

        return $this->findGenericFallbackStock($productId, $colorId);
    }

    private function resolveOrderWarehouseId(Order $order): ?int
    {
        if (!Schema::hasColumn('orders', 'warehouse_id') || !$order->warehouse_id) {
            return null;
        }

        return (int) $order->warehouse_id;
    }

    private function findExactStockForItem(int $productId, ?int $colorId, ?string $size, ?int $warehouseId): ?Stock
    {
        $query = Stock::query()->where('product_id', $productId);
        if ($warehouseId) {
            $query->where('warehouse_id', $warehouseId);
        }
        if ($colorId) {
            $query->where('color_id', $colorId);
        }
        if ($size) {
            $query->where('size', $size);
        }

        return $query->first();
    }

    private function findWarehouseFallbackStock(int $productId, ?int $colorId, int $warehouseId): ?Stock
    {
        $warehouseFallback = Stock::query()->where('product_id', $productId)->where('warehouse_id', $warehouseId);
        if ($colorId) {
            $warehouseFallback->where('color_id', $colorId);
        }

        $stock = $warehouseFallback->first();
        if ($stock) {
            return $stock;
        }

        return Stock::query()
            ->where('product_id', $productId)
            ->where('warehouse_id', $warehouseId)
            ->first();
    }

    private function findGenericFallbackStock(int $productId, ?int $colorId): ?Stock
    {
        $colorFallback = Stock::query()->where('product_id', $productId);
        if ($colorId) {
            $colorFallback->where('color_id', $colorId);
        }

        $stock = $colorFallback->first();
        if ($stock) {
            return $stock;
        }

        return Stock::query()->where('product_id', $productId)->first();
    }
}
