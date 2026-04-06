<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Warehouse;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

class WarehouseController extends Controller
{
    public function index(Request $request): JsonResponse
    {
        $query = Warehouse::query()->with('warehouseType')->orderBy('name');

        if ($request->filled('search')) {
            $search = trim((string) $request->search);
            $query->where(function ($q) use ($search) {
                $q->where('name', 'like', '%' . $search . '%')
                    ->orWhere('code', 'like', '%' . $search . '%')
                    ->orWhere('city', 'like', '%' . $search . '%');
            });
        }

        if ($request->boolean('active_only')) {
            $query->where('is_active', true);
        }

        if ($request->boolean('pos_only')) {
            $query->where('is_pos', true);
        }

        $items = $request->has('per_page')
            ? $query->paginate((int) $request->get('per_page', 15))
            : $query->get();
        return response()->json($items);
    }

    public function store(Request $request): JsonResponse
    {
        if (!$request->user()->hasRole('admin')) {
            return response()->json(['message' => 'Solo los administradores pueden crear almacenes.'], 403);
        }

        $data = $request->validate([
            'name'              => 'required|string|max:255',
            'warehouse_type_id' => 'nullable|exists:warehouse_types,id',
            'code'              => 'nullable|string|max:50|unique:warehouses,code',
            'address'           => 'nullable|string|max:500',
            'type'              => 'nullable|in:store,warehouse',
            'city'              => 'nullable|string|max:100',
            'is_active'         => 'nullable|boolean',
            'is_pos'            => 'nullable|boolean',
        ]);

        if (($data['is_pos'] ?? false) === true) {
            $data['type'] = 'store';
        }

        if (($data['type'] ?? null) === 'warehouse') {
            $data['is_pos'] = false;
        }

        if (!array_key_exists('is_pos', $data) && ($data['type'] ?? null) === 'store') {
            $data['is_pos'] = true;
        }

        $item = Warehouse::create($data);
        return response()->json($item->load('warehouseType'), 201);
    }

    public function show(Warehouse $warehouse): JsonResponse
    {
        return response()->json($warehouse->load('warehouseType'));
    }

    public function update(Request $request, Warehouse $warehouse): JsonResponse
    {
        $data = $request->validate([
            'name'              => 'sometimes|string|max:255',
            'warehouse_type_id' => 'nullable|exists:warehouse_types,id',
            'code'              => 'nullable|string|max:50|unique:warehouses,code,' . $warehouse->id,
            'address'           => 'nullable|string|max:500',
            'type'              => 'nullable|in:store,warehouse',
            'city'              => 'nullable|string|max:100',
            'is_active'         => 'nullable|boolean',
            'is_pos'            => 'nullable|boolean',
        ]);

        if (($data['is_pos'] ?? false) === true) {
            $data['type'] = 'store';
        }

        if (($data['type'] ?? null) === 'warehouse') {
            $data['is_pos'] = false;
        }

        if (!array_key_exists('is_pos', $data) && ($data['type'] ?? null) === 'store') {
            $data['is_pos'] = true;
        }

        $warehouse->update($data);
        return response()->json($warehouse->load('warehouseType'));
    }

    public function destroy(Warehouse $warehouse): JsonResponse
    {
        if (!request()->user()->hasRole('admin')) {
            return response()->json(['message' => 'Solo los administradores pueden eliminar almacenes.'], 403);
        }
        $warehouse->delete();
        return response()->json(null, 204);
    }
}
