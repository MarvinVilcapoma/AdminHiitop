<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\UnitMeasure;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

class UnitMeasureController extends Controller
{
    public function index(Request $request): JsonResponse
    {
        $query = UnitMeasure::query()->orderBy('name');

        if ($request->filled('search')) {
            $search = trim((string) $request->search);
            $query->where(function ($q) use ($search) {
                $q->where('name', 'like', '%' . $search . '%')
                    ->orWhere('code', 'like', '%' . $search . '%')
                    ->orWhere('sunat_code', 'like', '%' . $search . '%');
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
            'code' => 'required|string|max:20|unique:unit_measures,code',
            'sunat_code' => 'nullable|string|max:20',
            'is_active' => 'nullable|boolean',
        ]);

        $item = UnitMeasure::create($data);

        return response()->json($item, 201);
    }

    public function show(UnitMeasure $unitMeasure): JsonResponse
    {
        return response()->json($unitMeasure);
    }

    public function update(Request $request, UnitMeasure $unitMeasure): JsonResponse
    {
        $data = $request->validate([
            'name' => 'sometimes|string|max:255',
            'code' => 'sometimes|string|max:20|unique:unit_measures,code,' . $unitMeasure->id,
            'sunat_code' => 'nullable|string|max:20',
            'is_active' => 'nullable|boolean',
        ]);

        $unitMeasure->update($data);

        return response()->json($unitMeasure);
    }

    public function destroy(UnitMeasure $unitMeasure): JsonResponse
    {
        $unitMeasure->delete();

        return response()->json(null, 204);
    }
}
