<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Province;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

class ProvinceController extends Controller
{
    public function index(Request $request): JsonResponse
    {
        $query = Province::query()->with('districts')->orderBy('name');

        if ($request->filled('search')) {
            $search = trim((string) $request->search);
            $query->where(function ($q) use ($search) {
                $q->where('name', 'like', '%' . $search . '%')
                    ->orWhere('code', 'like', '%' . $search . '%');
            });
        }

        if ($request->boolean('active_only')) {
            $query->where('is_active', true);
        }
        $items = $request->has('per_page')
            ? $query->paginate((int) $request->get('per_page', 50))
            : $query->get();
        return response()->json($items);
    }

    public function store(Request $request): JsonResponse
    {
        $data = $request->validate([
            'name' => 'required|string|max:255',
            'code' => 'nullable|string|max:10|unique:provinces,code',
            'is_active' => 'nullable|boolean',
        ]);
        $item = Province::create($data);
        return response()->json($item, 201);
    }

    public function show(Province $province): JsonResponse
    {
        $province->load('districts');
        return response()->json($province);
    }

    public function update(Request $request, Province $province): JsonResponse
    {
        $data = $request->validate([
            'name' => 'sometimes|string|max:255',
            'code' => 'nullable|string|max:10|unique:provinces,code,' . $province->id,
            'is_active' => 'nullable|boolean',
        ]);
        $province->update($data);
        return response()->json($province);
    }

    public function destroy(Province $province): JsonResponse
    {
        $province->delete();
        return response()->json(null, 204);
    }
}
