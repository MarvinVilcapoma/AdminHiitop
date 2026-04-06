<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Stock;
use App\Models\StockMovement;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\DB;

class StockController extends Controller
{
    public function index(Request $request): JsonResponse
    {
        $query = Stock::query()->with(['product', 'warehouse', 'color'])
            ->orderBy('warehouse_id')->orderBy('product_id');

        if ($request->filled('product_id'))   $query->where('product_id',   $request->product_id);
        if ($request->filled('warehouse_id')) $query->where('warehouse_id', $request->warehouse_id);
        if ($request->filled('color_id'))     $query->where('color_id',     $request->color_id);
        if ($request->filled('size'))         $query->where('size',         $request->size);
        if ($request->boolean('low_stock'))   $query->where('quantity', '<=', 5);
        if ($request->filled('product_type_id')) {
            $query->whereHas('product', fn($q) => $q->where('product_type_id', $request->product_type_id));
        }
        if ($request->filled('collection_id')) {
            $query->whereHas('product', fn($q) => $q->where('collection_id', $request->collection_id));
        }
        if ($request->filled('search')) {
            $s = $request->search;
            $query->whereHas('product', fn($q) => $q->where('name', 'like', "%$s%")->orWhere('sku', 'like', "%$s%"));
        }

        $items = $request->has('per_page')
            ? $query->paginate((int) $request->get('per_page', 20))
            : $query->get();
        return response()->json($items);
    }

    public function store(Request $request): JsonResponse
    {
        $data = $request->validate([
            'product_id'   => 'required|exists:products,id',
            'warehouse_id' => 'required|exists:warehouses,id',
            'color_id'     => 'nullable|exists:colors,id',
            'size'         => 'nullable|string|max:20',
            'quantity'     => 'required|integer|min:0',
            'reserved'     => 'nullable|integer|min:0',
            'reason'       => 'nullable|string|max:255',
        ]);
        $qty    = $data['quantity'];
        $reason = $data['reason'] ?? 'Ingreso inicial';
        unset($data['reason']);
        $data['reserved'] = $data['reserved'] ?? 0;
        $stock = Stock::create($data);
        StockMovement::create([
            'stock_id' => $stock->id, 'product_id' => $stock->product_id,
            'warehouse_id' => $stock->warehouse_id, 'color_id' => $stock->color_id,
            'size' => $stock->size, 'movement_type' => 'entry',
            'quantity_before' => 0, 'quantity_change' => $qty, 'quantity_after' => $qty,
            'reason' => $reason, 'user_id' => auth()->id(),
        ]);
        return response()->json($stock->load(['product', 'warehouse', 'color']), 201);
    }

    /**
     * Bulk stock movement (carga/descarga múltiple).
     * POST /stocks/bulk
     * Body: {
     *   warehouse_id: int,
     *   movement_type: 'entry' | 'exit',
     *   reason: string,
     *   purchase_type_id: int|null,
     *   items: [{ product_id, color_id?, size?, quantity }]
     * }
     */
    public function bulkStore(Request $request): JsonResponse
    {
        $data = $request->validate([
            'warehouse_id'     => 'required|exists:warehouses,id',
            'movement_type'    => 'required|in:entry,exit',
            'reason'           => 'nullable|string|max:255',
            'purchase_type_id' => 'nullable|exists:purchase_types,id',
            'items'            => 'required|array|min:1',
            'items.*.product_id' => 'required|exists:products,id',
            'items.*.color_id'   => 'nullable|exists:colors,id',
            'items.*.size'       => 'nullable|string|max:30',
            'items.*.quantity'   => 'required|integer|min:1',
        ]);

        $results  = [];
        $errors   = [];
        $userId   = auth()->id();
        $reason   = $data['reason'] ?? ($data['movement_type'] === 'entry' ? 'Carga de stock' : 'Descarga de stock');

        DB::beginTransaction();
        try {
            foreach ($data['items'] as $i => $item) {
                $change = (int) $item['quantity'];
                if ($data['movement_type'] === 'exit') $change = -$change;

                $stock = Stock::firstOrCreate(
                    [
                        'product_id'   => $item['product_id'],
                        'warehouse_id' => $data['warehouse_id'],
                        'color_id'     => $item['color_id'] ?? null,
                        'size'         => $item['size'] ?? null,
                    ],
                    ['quantity' => 0, 'reserved' => 0]
                );

                $before = $stock->quantity;
                $after  = max(0, $before + $change);

                if ($data['movement_type'] === 'exit' && $before < abs($change)) {
                    $errors[] = "Línea " . ($i + 1) . ": stock insuficiente (disponible: {$before})";
                    continue;
                }

                $stock->update(['quantity' => $after]);

                StockMovement::create([
                    'stock_id'        => $stock->id,
                    'product_id'      => $stock->product_id,
                    'warehouse_id'    => $stock->warehouse_id,
                    'color_id'        => $stock->color_id,
                    'size'            => $stock->size,
                    'movement_type'   => $data['movement_type'],
                    'quantity_before' => $before,
                    'quantity_change' => $change,
                    'quantity_after'  => $after,
                    'reason'          => $reason,
                    'user_id'         => $userId,
                ]);

                $results[] = $stock->fresh()->load(['product', 'warehouse', 'color']);
            }
            DB::commit();
        } catch (\Exception $e) {
            DB::rollBack();
            return response()->json(['message' => 'Error en la operación: ' . $e->getMessage()], 500);
        }

        return response()->json([
            'saved'  => $results,
            'errors' => $errors,
        ], count($errors) > 0 && count($results) === 0 ? 422 : 201);
    }

    public function show(Stock $stock): JsonResponse
    {
        $stock->load(['product', 'warehouse', 'color']);
        $movements = StockMovement::where('stock_id', $stock->id)
            ->with('user:id,name')->orderByDesc('created_at')->limit(20)->get();
        return response()->json(['stock' => $stock, 'movements' => $movements]);
    }

    public function update(Request $request, Stock $stock): JsonResponse
    {
        $data = $request->validate([
            'quantity' => 'sometimes|integer|min:0',
            'reserved' => 'nullable|integer|min:0',
            'reason'   => 'nullable|string|max:255',
        ]);
        $reason = $data['reason'] ?? 'Ajuste';
        unset($data['reason']);
        if (isset($data['quantity'])) {
            $before = $stock->quantity; $after = $data['quantity'];
            StockMovement::create([
                'stock_id' => $stock->id, 'product_id' => $stock->product_id,
                'warehouse_id' => $stock->warehouse_id, 'color_id' => $stock->color_id,
                'size' => $stock->size, 'movement_type' => 'adjustment',
                'quantity_before' => $before, 'quantity_change' => $after - $before, 'quantity_after' => $after,
                'reason' => $reason, 'user_id' => auth()->id(),
            ]);
        }
        $stock->update($data);
        return response()->json($stock->load(['product', 'warehouse', 'color']));
    }

    /** Transferencia entre almacenes */
    public function transfer(Request $request, Stock $stock): JsonResponse
    {
        $data = $request->validate([
            'destination_warehouse_id' => 'required|exists:warehouses,id',
            'quantity' => 'required|integer|min:1',
            'reason'   => 'nullable|string|max:255',
        ]);

        if ((int)$data['destination_warehouse_id'] === $stock->warehouse_id) {
            return response()->json(['message' => 'El almacén destino debe ser diferente al origen.'], 422);
        }
        if ($stock->quantity < $data['quantity']) {
            return response()->json(['message' => "Stock insuficiente (disponible: {$stock->quantity})."], 422);
        }

        $userId = auth()->id();
        $qty    = (int) $data['quantity'];
        $reason = $data['reason'] ?? 'Transferencia entre almacenes';

        DB::beginTransaction();
        try {
            // Salida del origen
            $before = $stock->quantity;
            $after  = $before - $qty;
            $stock->update(['quantity' => $after]);
            StockMovement::create([
                'stock_id' => $stock->id, 'product_id' => $stock->product_id,
                'warehouse_id' => $stock->warehouse_id, 'color_id' => $stock->color_id,
                'size' => $stock->size, 'movement_type' => 'transfer',
                'quantity_before' => $before, 'quantity_change' => -$qty, 'quantity_after' => $after,
                'reason' => $reason . ' (salida)', 'user_id' => $userId,
            ]);

            // Entrada al destino
            $dest = Stock::firstOrCreate(
                ['product_id' => $stock->product_id, 'warehouse_id' => $data['destination_warehouse_id'],
                 'color_id' => $stock->color_id, 'size' => $stock->size],
                ['quantity' => 0, 'reserved' => 0]
            );
            $destBefore = $dest->quantity;
            $destAfter  = $destBefore + $qty;
            $dest->update(['quantity' => $destAfter]);
            StockMovement::create([
                'stock_id' => $dest->id, 'product_id' => $dest->product_id,
                'warehouse_id' => $dest->warehouse_id, 'color_id' => $dest->color_id,
                'size' => $dest->size, 'movement_type' => 'transfer',
                'quantity_before' => $destBefore, 'quantity_change' => $qty, 'quantity_after' => $destAfter,
                'reason' => $reason . ' (entrada)', 'user_id' => $userId,
            ]);

            DB::commit();
        } catch (\Exception $e) {
            DB::rollBack();
            return response()->json(['message' => 'Error en la transferencia: ' . $e->getMessage()], 500);
        }

        return response()->json([
            'source'      => $stock->fresh()->load(['product', 'warehouse', 'color']),
            'destination' => $dest->fresh()->load(['product', 'warehouse', 'color']),
        ]);
    }

    /** Ajuste rápido: sumar/restar stock */
    public function adjust(Request $request, Stock $stock): JsonResponse
    {
        $data = $request->validate([
            'change'    => 'required|integer',
            'reason'    => 'nullable|string|max:255',
            'reference' => 'nullable|string|max:100',
        ]);
        $before = $stock->quantity;
        $after  = max(0, $before + $data['change']);
        $stock->update(['quantity' => $after]);
        StockMovement::create([
            'stock_id' => $stock->id, 'product_id' => $stock->product_id,
            'warehouse_id' => $stock->warehouse_id, 'color_id' => $stock->color_id,
            'size' => $stock->size, 'movement_type' => $data['change'] > 0 ? 'entry' : 'exit',
            'quantity_before' => $before, 'quantity_change' => $data['change'], 'quantity_after' => $after,
            'reason' => $data['reason'] ?? null, 'reference' => $data['reference'] ?? null,
            'user_id' => auth()->id(),
        ]);
        return response()->json($stock->fresh()->load(['product', 'warehouse', 'color']));
    }

    /** Resumen de stock por almacén */
    public function summary(): JsonResponse
    {
        $data = DB::table('stocks as s')
            ->join('warehouses as w', 'w.id', '=', 's.warehouse_id')
            ->select('w.id', 'w.name', 'w.type',
                DB::raw('COUNT(s.id) as sku_count'),
                DB::raw('SUM(s.quantity) as total_qty'),
                DB::raw('SUM(s.quantity - s.reserved) as available_qty'))
            ->where('w.is_active', true)
            ->groupBy('w.id', 'w.name', 'w.type')
            ->orderBy('w.name')->get();
        return response()->json($data);
    }

    /** Stock disponible por producto + almacén (para pedidos) */
    public function available(Request $request): JsonResponse
    {
        $request->validate([
            'warehouse_id' => 'required|exists:warehouses,id',
            'product_id'   => 'required|exists:products,id',
        ]);

        $stocks = Stock::query()
            ->with(['color'])
            ->where('warehouse_id', $request->warehouse_id)
            ->where('product_id',   $request->product_id)
            ->whereRaw('(quantity - reserved) > 0')
            ->get();

        // Group by color: {color, sizes[], total_qty}
        $byColor = $stocks->groupBy('color_id')->map(function ($group) {
            $first = $group->first();
            return [
                'color_id'  => $first->color_id,
                'color'     => $first->color,
                'sizes'     => $group->pluck('size')->filter()->unique()->values(),
                'total_qty' => $group->sum(fn ($s) => max(0, ((int) $s->quantity) - ((int) $s->reserved))),
            ];
        })->values();

        return response()->json([
            'stocks'    => $stocks,
            'by_color'  => $byColor,
            'total_qty' => $stocks->sum(fn ($s) => max(0, ((int) $s->quantity) - ((int) $s->reserved))),
        ]);
    }

    public function bulkTransfer(Request $request): JsonResponse
    {
        $data = $request->validate([
            'from_warehouse_id'  => 'required|exists:warehouses,id',
            'to_warehouse_id'    => 'required|exists:warehouses,id|different:from_warehouse_id',
            'reason'             => 'required|string|max:255',
            'observations'       => 'nullable|string|max:500',
            'items'              => 'required|array|min:1',
            'items.*.product_id' => 'required|exists:products,id',
            'items.*.color_id'   => 'nullable|exists:colors,id',
            'items.*.size'       => 'nullable|string|max:30',
            'items.*.quantity'   => 'required|integer|min:1',
        ]);

        $saved  = [];
        $errors = [];

        DB::beginTransaction();
        try {
            foreach ($data['items'] as $idx => $item) {
                $qty = (int) $item['quantity'];

                // Find source stock record
                $src = Stock::where('warehouse_id', $data['from_warehouse_id'])
                    ->where('product_id',   $item['product_id'])
                    ->where('color_id',     $item['color_id'] ?? null)
                    ->where('size',         $item['size'] ?? null)
                    ->first();

                if (!$src) {
                    $errors[] = "Línea " . ($idx + 1) . ": no existe stock en el almacén de origen.";
                    continue;
                }
                if ($src->quantity < $qty) {
                    $errors[] = "Línea " . ($idx + 1) . ": stock insuficiente ({$src->quantity} disponibles, se solicitan {$qty}).";
                    continue;
                }

                // Decrement source
                $src->decrement('quantity', $qty);
                StockMovement::create([
                    'stock_id'         => $src->id,
                    'movement_type'    => 'exit',
                    'sub_movement_type'=> 'transfer',
                    'quantity_change'  => -$qty,
                    'reason'           => $data['reason'],
                    'notes'            => $data['observations'] ?? null,
                    'reference'        => 'Transferencia a almacén #' . $data['to_warehouse_id'],
                ]);

                // Increment (or create) destination
                $dst = Stock::firstOrCreate(
                    [
                        'warehouse_id' => $data['to_warehouse_id'],
                        'product_id'   => $item['product_id'],
                        'color_id'     => $item['color_id'] ?? null,
                        'size'         => $item['size'] ?? null,
                    ],
                    ['quantity' => 0, 'reserved' => 0]
                );
                $dst->increment('quantity', $qty);
                StockMovement::create([
                    'stock_id'         => $dst->id,
                    'movement_type'    => 'entry',
                    'sub_movement_type'=> 'transfer',
                    'quantity_change'  => $qty,
                    'reason'           => $data['reason'],
                    'notes'            => $data['observations'] ?? null,
                    'reference'        => 'Transferencia desde almacén #' . $data['from_warehouse_id'],
                ]);

                $saved[] = $item;
            }

            if (count($saved) === 0 && count($errors) > 0) {
                DB::rollBack();
                return response()->json(['saved' => [], 'errors' => $errors], 422);
            }

            DB::commit();
        } catch (\Throwable $e) {
            DB::rollBack();
            return response()->json(['message' => 'Error interno: ' . $e->getMessage()], 500);
        }

        return response()->json(['saved' => $saved, 'errors' => $errors]);
    }

    public function destroy(Stock $stock): JsonResponse
    {
        $stock->delete();
        return response()->json(null, 204);
    }
}
