<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Product;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

class ProductController extends Controller
{
    public function index(Request $request): JsonResponse
    {
        $query = Product::query()->with(['productType.sizes', 'collection', 'colors'])->withSum('stocks', 'quantity')->orderBy('name');
        if ($request->boolean('active_only')) {
            $query->where('is_active', true);
        }
        if ($request->filled('status')) {
            $query->where('is_active', $request->status === 'active');
        }
        if ($request->filled('product_type_id')) {
            $query->where('product_type_id', $request->product_type_id);
        }
        if ($request->filled('collection_id')) {
            $query->where('collection_id', $request->collection_id);
        }
        // Only products with available stock in this warehouse
        if ($request->filled('warehouse_id')) {
            $query->whereHas('stocks', function ($q) use ($request) {
                $q->where('warehouse_id', $request->warehouse_id)->where('quantity', '>', 0);
            });
        }
        if ($request->filled('search')) {
            $query->where(function ($q) use ($request) {
                $q->where('name', 'like', '%' . $request->search . '%')
                    ->orWhere('sku', 'like', '%' . $request->search . '%');
            });
        }
        $items = $request->has('per_page')
            ? $query->paginate((int) $request->get('per_page', 15))
            : $query->get();
        return response()->json($items);
    }

    public function store(Request $request): JsonResponse
    {
        $data = $request->validate([
            'name' => 'required|string|max:255',
            'sku' => 'nullable|string|max:100|unique:products,sku',
            'product_type_id' => 'nullable|exists:product_types,id',
            'collection_id' => 'nullable|exists:collections,id',
            'description' => 'nullable|string',
            'base_price' => 'nullable|numeric|min:0',
            'unit_cost' => 'nullable|numeric|min:0',
            'is_active' => 'nullable|boolean',
        ]);
        $item = Product::create($data);
        if ($request->has('color_ids')) {
            $sync = [];
            foreach ((array) $request->color_ids as $idx => $cid) {
                $sync[(int)$cid] = ['sort_order' => $idx];
            }
            $item->colors()->sync($sync);
        }
        return response()->json($item->load(['productType', 'collection', 'colors']), 201);
    }

    public function show(Product $product): JsonResponse
    {
        $product->load(['productType', 'collection', 'colors', 'stocks.warehouse', 'stocks.color']);
        return response()->json($product);
    }

    public function update(Request $request, Product $product): JsonResponse
    {
        $data = $request->validate([
            'name' => 'sometimes|string|max:255',
            'sku' => 'nullable|string|max:100|unique:products,sku,' . $product->id,
            'product_type_id' => 'nullable|exists:product_types,id',
            'collection_id' => 'nullable|exists:collections,id',
            'description' => 'nullable|string',
            'base_price' => 'nullable|numeric|min:0',
            'unit_cost' => 'nullable|numeric|min:0',
            'is_active' => 'nullable|boolean',
        ]);
        $product->update($data);
        if ($request->has('color_ids')) {
            $sync = [];
            foreach ((array) $request->color_ids as $idx => $cid) {
                $sync[(int)$cid] = ['sort_order' => $idx];
            }
            $product->colors()->sync($sync);
        }
        return response()->json($product->load(['productType', 'collection', 'colors']));
    }

    public function destroy(Product $product): JsonResponse
    {
        $product->delete();
        return response()->json(null, 204);
    }
}
