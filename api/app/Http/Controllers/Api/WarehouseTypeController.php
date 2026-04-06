<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\WarehouseType;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

class WarehouseTypeController extends Controller
{
    public function index(): JsonResponse
    {
        return response()->json(WarehouseType::orderBy('name')->get());
    }

    public function store(Request $request): JsonResponse
    {
        $data = $request->validate([
            'name'        => 'required|string|max:100',
            'code'        => 'nullable|string|max:30|unique:warehouse_types,code',
            'description' => 'nullable|string|max:300',
            'is_active'   => 'boolean',
        ]);
        return response()->json(WarehouseType::create($data), 201);
    }

    public function show(WarehouseType $warehouseType): JsonResponse
    {
        return response()->json($warehouseType);
    }

    public function update(Request $request, WarehouseType $warehouseType): JsonResponse
    {
        $data = $request->validate([
            'name'        => 'sometimes|string|max:100',
            'code'        => 'nullable|string|max:30|unique:warehouse_types,code,' . $warehouseType->id,
            'description' => 'nullable|string|max:300',
            'is_active'   => 'boolean',
        ]);
        $warehouseType->update($data);
        return response()->json($warehouseType);
    }

    public function destroy(WarehouseType $warehouseType): JsonResponse
    {
        $warehouseType->delete();
        return response()->json(null, 204);
    }
}
