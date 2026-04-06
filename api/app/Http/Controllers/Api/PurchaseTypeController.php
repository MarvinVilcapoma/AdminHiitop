<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\PurchaseType;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Validation\Rule;

class PurchaseTypeController extends Controller
{
    public function index(Request $request): JsonResponse
    {
        $query = PurchaseType::query()->orderBy('name');
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
            'slug' => ['nullable', 'string', 'max:255', Rule::unique('purchase_types', 'slug')->whereNull('deleted_at')],
            'is_active' => 'nullable|boolean',
        ]);
        if (empty($data['slug'])) {
            $data['slug'] = \Illuminate\Support\Str::slug($data['name']);
        }
        $item = PurchaseType::create($data);
        return response()->json($item, 201);
    }

    public function show(PurchaseType $purchaseType): JsonResponse
    {
        return response()->json($purchaseType);
    }

    public function update(Request $request, PurchaseType $purchaseType): JsonResponse
    {
        $data = $request->validate([
            'name' => 'sometimes|string|max:255',
            'slug' => ['sometimes', 'string', 'max:255', Rule::unique('purchase_types', 'slug')->ignore($purchaseType->id)->whereNull('deleted_at')],
            'is_active' => 'nullable|boolean',
        ]);
        $purchaseType->update($data);
        return response()->json($purchaseType);
    }

    public function destroy(PurchaseType $purchaseType): JsonResponse
    {
        $purchaseType->delete();
        return response()->json(null, 204);
    }
}
