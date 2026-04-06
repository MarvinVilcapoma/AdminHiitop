<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\ProductType;
use App\Models\Size;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Validation\Rule;

class ProductTypeController extends Controller
{
    public function index(Request $request): JsonResponse
    {
        $query = ProductType::query()->with('sizes')->orderBy('name');
        if ($request->boolean('active_only')) {
            $query->where('is_active', true);
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
            'slug' => ['nullable', 'string', 'max:255', Rule::unique('product_types', 'slug')->whereNull('deleted_at')],
            'is_active' => 'nullable|boolean',
        ]);
        if (empty($data['slug'])) {
            $data['slug'] = \Illuminate\Support\Str::slug($data['name']);
        }
        $item = ProductType::create($data);
        return response()->json($item->load('sizes'), 201);
    }

    public function show(ProductType $productType): JsonResponse
    {
        return response()->json($productType->load('sizes'));
    }

    public function update(Request $request, ProductType $productType): JsonResponse
    {
        $data = $request->validate([
            'name' => 'sometimes|string|max:255',
            'slug' => ['sometimes', 'string', 'max:255', Rule::unique('product_types', 'slug')->ignore($productType->id)->whereNull('deleted_at')],
            'is_active' => 'nullable|boolean',
        ]);
        $productType->update($data);
        return response()->json($productType->load('sizes'));
    }

    public function destroy(ProductType $productType): JsonResponse
    {
        $productType->delete();
        return response()->json(null, 204);
    }

    /**
     * Syncs the sizes for a product type.
     * POST /product-types/{productType}/sizes
     * Body: { sizes: [{ name: "XS", sort_order: 0 }, ...] }
     * OR:   { size_ids: [1, 2, 3] }  (attach existing sizes)
     */
    public function syncSizes(Request $request, ProductType $productType): JsonResponse
    {
        $request->validate([
            'sizes'    => 'nullable|array',
            'sizes.*.name' => 'required_with:sizes|string|max:30',
            'sizes.*.sort_order' => 'nullable|integer',
            'size_ids' => 'nullable|array',
            'size_ids.*' => 'integer|exists:sizes,id',
        ]);

        $syncData = [];

        if ($request->filled('sizes')) {
            foreach ($request->sizes as $index => $s) {
                // find or create the global size
                $size = Size::firstOrCreate(['name' => $s['name']], ['sort_order' => $s['sort_order'] ?? $index]);
                $syncData[$size->id] = ['sort_order' => $s['sort_order'] ?? $index];
            }
        } elseif ($request->filled('size_ids')) {
            foreach ($request->size_ids as $index => $id) {
                $syncData[$id] = ['sort_order' => $index];
            }
        }

        $productType->sizes()->sync($syncData);

        return response()->json($productType->load('sizes'));
    }
}

