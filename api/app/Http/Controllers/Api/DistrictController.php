<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\District;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

class DistrictController extends Controller
{
    public function index(Request $request): JsonResponse
    {
        $query = District::query()->with('province')->orderBy('name');

        if ($request->filled('search')) {
            $search = trim((string) $request->search);
            $query->where(function ($q) use ($search) {
                $q->where('name', 'like', '%' . $search . '%')
                    ->orWhere('code', 'like', '%' . $search . '%');
            });
        }

        if ($request->filled('province_id')) {
            $query->where('province_id', $request->province_id);
        }
        if ($request->boolean('active_only')) {
            $query->where('is_active', true);
        }
        $items = $request->has('per_page')
            ? $query->paginate((int) $request->get('per_page', 100))
            : $query->get();
        return response()->json($items);
    }

    public function store(Request $request): JsonResponse
    {
        $data = $request->validate([
            'province_id' => 'required|exists:provinces,id',
            'name' => 'required|string|max:255',
            'code' => 'nullable|string|max:20',
            'is_active' => 'nullable|boolean',
        ]);
        $item = District::create($data);
        return response()->json($item->load('province'), 201);
    }

    public function show(District $district): JsonResponse
    {
        $district->load('province');
        return response()->json($district);
    }

    public function update(Request $request, District $district): JsonResponse
    {
        $data = $request->validate([
            'province_id' => 'sometimes|exists:provinces,id',
            'name' => 'sometimes|string|max:255',
            'code' => 'nullable|string|max:20',
            'is_active' => 'nullable|boolean',
        ]);
        $district->update($data);
        return response()->json($district->load('province'));
    }

    public function destroy(District $district): JsonResponse
    {
        $district->delete();
        return response()->json(null, 204);
    }
}
