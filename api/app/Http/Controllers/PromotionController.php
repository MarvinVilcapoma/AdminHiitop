<?php

namespace App\Http\Controllers;

use App\Models\Promotion;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;

class PromotionController extends Controller
{
    public function index(Request $request): JsonResponse
    {
        $q = Promotion::with(['items.productType'])->latest();

        if ($s = $request->input('search')) {
            $q->where('name', 'like', "%$s%");
        }
        if ($request->input('active_only')) {
            $q->where('is_active', true);
        }

        $perPage = (int) $request->input('per_page', 50);
        return response()->json($q->paginate($perPage));
    }

    public function store(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'name'        => 'required|string|max:200',
            'description' => 'nullable|string',
            'is_active'   => 'boolean',
            'fixed_price' => 'nullable|numeric|min:0',
            'items'       => 'array',
            'items.*.product_id' => 'required|exists:products,id',
            'items.*.quantity'   => 'required|integer|min:1',
            'items.*.unit_price' => 'nullable|numeric|min:0',
            'items.*.notes'      => 'nullable|string|max:300',
        ]);

        $items = $validated['items'] ?? [];
        $promotion = Promotion::create(array_diff_key($validated, ['items' => null]));

        foreach ($items as $item) {
            $promotion->items()->create($item);
        }

        return response()->json($promotion->load('items.productType'), 201);
    }

    public function show(Promotion $promotion): JsonResponse
    {
        return response()->json($promotion->load('items.productType'));
    }

    public function update(Request $request, Promotion $promotion): JsonResponse
    {
        $validated = $request->validate([
            'name'        => 'sometimes|string|max:200',
            'description' => 'nullable|string',
            'is_active'   => 'nullable|boolean',
            'fixed_price' => 'nullable|numeric|min:0',
            'items'       => 'array',
            'items.*.product_type_id' => 'required|exists:product_types,id',
            'items.*.quantity'        => 'required|integer|min:1',
            'items.*.notes'           => 'nullable|string|max:300',
        ]);

        $items = $validated['items'] ?? null;
        $promotion->update(array_diff_key($validated, ['items' => null]));

        if ($items !== null) {
            $promotion->items()->delete();
            foreach ($items as $item) {
                $promotion->items()->create($item);
            }
        }

        return response()->json($promotion->load('items.productType'));
    }

    public function destroy(Promotion $promotion): JsonResponse
    {
        $promotion->delete();
        return response()->json(null, 204);
    }
}
