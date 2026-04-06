<?php

namespace App\Http\Controllers;

use App\Models\Sale;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;
use Illuminate\Support\Facades\Auth;

class SaleController extends Controller
{
    public function index(Request $request): JsonResponse
    {
        $q = Sale::with(['items', 'user'])->latest('sale_datetime');

        if ($s = $request->input('search')) {
            $q->where(function ($query) use ($s) {
                $query->where('series_number',  'like', "%$s%")
                      ->orWhere('customer_name','like', "%$s%")
                      ->orWhere('seller',        'like', "%$s%")
                      ->orWhere('branch',        'like', "%$s%");
            });
        }
        if ($from = $request->input('from')) {
            $q->where('sale_datetime', '>=', $from . ' 00:00:00');
        }
        if ($to = $request->input('to')) {
            $q->where('sale_datetime', '<=', $to . ' 23:59:59');
        }
        if ($branch = $request->input('branch')) {
            $q->where('branch', $branch);
        }

        $perPage = (int) $request->input('per_page', 30);
        return response()->json($q->paginate($perPage));
    }

    public function store(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'movement_type'       => 'nullable|string|max:60',
            'document_type_label' => 'nullable|string|max:60',
            'document_number'     => 'nullable|string|max:50',
            'issue_date'          => 'nullable|date',
            'series_number'       => 'nullable|string|max:30',
            'series_prefix'       => 'nullable|string|max:20',
            'tracking_number'     => 'nullable|string|max:100',
            'sale_datetime'       => 'nullable|date',
            'branch'              => 'nullable|string|max:100',
            'seller'              => 'nullable|string|max:100',
            'customer_name'       => 'nullable|string|max:200',
            'customer_tax_id'     => 'nullable|string|max:20',
            'customer_email'      => 'nullable|email|max:200',
            'customer_address'    => 'nullable|string|max:300',
            'customer_district'   => 'nullable|string|max:100',
            'customer_province'   => 'nullable|string|max:100',
            'customer_department' => 'nullable|string|max:100',
            'price_list'          => 'nullable|string|max:100',
            'delivery_type'       => 'nullable|string|max:60',
            'currency'            => 'nullable|string|max:5',
            'total_net'           => 'nullable|numeric',
            'total_tax'           => 'nullable|numeric',
            'total_gross'         => 'nullable|numeric',
            'discount_net'        => 'nullable|numeric',
            'discount_gross'      => 'nullable|numeric',
            'items'               => 'array',
            'items.*.sku'             => 'nullable|string|max:100',
            'items.*.product_name'    => 'nullable|string|max:200',
            'items.*.variant'         => 'nullable|string|max:100',
            'items.*.unit_gross_price'=> 'nullable|numeric',
            'items.*.quantity'        => 'nullable|numeric',
            'items.*.total_gross'     => 'nullable|numeric',
            'items.*.unit_net_price'  => 'nullable|numeric',
            'items.*.total_net'       => 'nullable|numeric',
            'items.*.total_tax'       => 'nullable|numeric',
        ]);

        $items    = $validated['items'] ?? [];
        $saleData = array_diff_key($validated, ['items' => null]);
        $saleData['import_source'] = 'manual';
        $saleData['user_id']       = Auth::id();

        $sale = Sale::create($saleData);
        foreach ($items as $item) {
            $sale->items()->create($item);
        }

        return response()->json($sale->load('items'), 201);
    }

    public function show(Sale $sale): JsonResponse
    {
        return response()->json($sale->load(['items', 'user']));
    }

    public function update(Request $request, Sale $sale): JsonResponse
    {
        $validated = $request->validate([
            'movement_type'       => 'nullable|string|max:60',
            'document_type_label' => 'nullable|string|max:60',
            'document_number'     => 'nullable|string|max:50',
            'issue_date'          => 'nullable|date',
            'series_number'       => 'nullable|string|max:30',
            'series_prefix'       => 'nullable|string|max:20',
            'tracking_number'     => 'nullable|string|max:100',
            'sale_datetime'       => 'nullable|date',
            'branch'              => 'nullable|string|max:100',
            'seller'              => 'nullable|string|max:100',
            'customer_name'       => 'nullable|string|max:200',
            'customer_tax_id'     => 'nullable|string|max:20',
            'customer_email'      => 'nullable|email|max:200',
            'customer_address'    => 'nullable|string|max:300',
            'currency'            => 'nullable|string|max:5',
            'total_net'           => 'nullable|numeric',
            'total_tax'           => 'nullable|numeric',
            'total_gross'         => 'nullable|numeric',
            'items'               => 'array',
        ]);

        $items = $validated['items'] ?? null;
        $sale->update(array_diff_key($validated, ['items' => null]));

        if ($items !== null) {
            $sale->items()->delete();
            foreach ($items as $item) {
                $sale->items()->create($item);
            }
        }

        return response()->json($sale->load('items'));
    }

    public function destroy(Sale $sale): JsonResponse
    {
        $sale->delete();
        return response()->json(null, 204);
    }

    /** GET /api/sales/branches - distinct branch values */
    public function branches(): JsonResponse
    {
        $list = Sale::select('branch')
            ->whereNotNull('branch')
            ->distinct()
            ->orderBy('branch')
            ->pluck('branch');

        return response()->json($list);
    }
}