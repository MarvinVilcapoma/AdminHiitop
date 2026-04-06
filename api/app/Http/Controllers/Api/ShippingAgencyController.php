<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\ShippingAgency;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Schema;

class ShippingAgencyController extends Controller
{
    public function index(Request $request): JsonResponse
    {
        $query = ShippingAgency::query()->orderBy('name');

        if ($request->filled('search')) {
            $search = trim((string) $request->input('search'));
            $query->where(function ($q) use ($search) {
                $q->where('name', 'like', '%' . $search . '%')
                    ->orWhere('code', 'like', '%' . $search . '%');
            });
        }

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
            'code' => 'nullable|string|max:50|unique:shipping_agencies,code',
            'is_active' => 'nullable|boolean',
        ]);

        if (Schema::hasColumn('shipping_agencies', 'shipping_rate')) {
            if ($request->input('shipping_rate') === '') {
                $request->merge(['shipping_rate' => null]);
            }

            $data['shipping_rate'] = $request->validate([
                'shipping_rate' => 'nullable|numeric|min:0',
            ])['shipping_rate'] ?? null;
        }

        $item = ShippingAgency::create($data);

        return response()->json($item, 201);
    }

    public function show(ShippingAgency $shippingAgency): JsonResponse
    {
        return response()->json($shippingAgency);
    }

    public function update(Request $request, ShippingAgency $shippingAgency): JsonResponse
    {
        $data = $request->validate([
            'name' => 'sometimes|string|max:255',
            'code' => 'nullable|string|max:50|unique:shipping_agencies,code,' . $shippingAgency->id,
            'is_active' => 'nullable|boolean',
        ]);

        if (Schema::hasColumn('shipping_agencies', 'shipping_rate')) {
            if ($request->input('shipping_rate') === '') {
                $request->merge(['shipping_rate' => null]);
            }

            if ($request->has('shipping_rate')) {
                $data['shipping_rate'] = $request->validate([
                    'shipping_rate' => 'nullable|numeric|min:0',
                ])['shipping_rate'] ?? null;
            }
        }

        $shippingAgency->update($data);

        return response()->json($shippingAgency);
    }

    public function destroy(ShippingAgency $shippingAgency): JsonResponse
    {
        $shippingAgency->delete();
        return response()->json(null, 204);
    }
}
