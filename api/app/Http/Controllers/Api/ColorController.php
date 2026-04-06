<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Color;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Validation\Rule;

class ColorController extends Controller
{
    public function index(Request $request): JsonResponse
    {
        $query = Color::query()->orderBy('name');

        if ($request->filled('search')) {
            $search = trim((string) $request->search);
            $query->where(function ($q) use ($search) {
                $q->where('name', 'like', '%' . $search . '%')
                    ->orWhere('slug', 'like', '%' . $search . '%')
                    ->orWhere('hex_code', 'like', '%' . $search . '%');
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
            'hex_code' => 'nullable|string|max:20',
            'slug' => ['nullable', 'string', 'max:255', Rule::unique('colors', 'slug')->whereNull('deleted_at')],
            'is_active' => 'nullable|boolean',
        ]);
        if (empty($data['slug'])) {
            $data['slug'] = \Illuminate\Support\Str::slug($data['name']);
        }
        $item = Color::create($data);
        return response()->json($item, 201);
    }

    public function show(Color $color): JsonResponse
    {
        return response()->json($color);
    }

    public function update(Request $request, Color $color): JsonResponse
    {
        $data = $request->validate([
            'name' => 'sometimes|string|max:255',
            'hex_code' => 'nullable|string|max:20',
            'slug' => ['sometimes', 'string', 'max:255', Rule::unique('colors', 'slug')->ignore($color->id)->whereNull('deleted_at')],
            'is_active' => 'nullable|boolean',
        ]);
        $color->update($data);
        return response()->json($color);
    }

    public function destroy(Color $color): JsonResponse
    {
        $color->delete();
        return response()->json(null, 204);
    }
}
