<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\OrderStatus;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Schema;
use Illuminate\Validation\Rule;
use Illuminate\Validation\ValidationException;

class OrderStatusController extends Controller
{
    private const PROTECTED_SLUGS = ['pending', 'pendiente', 'cancelled', 'cancelado', 'delivered', 'entregado'];

    public function index(Request $request): JsonResponse
    {
        $query = OrderStatus::query()
            ->where('slug', '!=', 'pagado')
            ->orderBy('sort_order')
            ->orderBy('name');

        if ($request->filled('search')) {
            $search = trim((string) $request->search);
            $query->where(function ($q) use ($search) {
                $q->where('name', 'like', '%' . $search . '%')
                    ->orWhere('slug', 'like', '%' . $search . '%');
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
            'slug' => ['nullable', 'string', 'max:255', Rule::unique('order_statuses', 'slug')->whereNull('deleted_at')],
            'color' => 'nullable|string|max:50',
            'icon' => 'nullable|string|max:100',
            'sort_order' => 'nullable|integer',
            'is_active' => 'nullable|boolean',
        ]);
        if (empty($data['slug'])) {
            $data['slug'] = \Illuminate\Support\Str::slug($data['name']);
        }

        if (in_array(mb_strtolower(trim((string) $data['slug'])), self::PROTECTED_SLUGS, true) && Schema::hasColumn('order_statuses', 'is_protected')) {
            $data['is_protected'] = true;
        }

        $item = OrderStatus::create($data);
        return response()->json($item, 201);
    }

    public function show(OrderStatus $orderStatus): JsonResponse
    {
        return response()->json($orderStatus);
    }

    public function update(Request $request, OrderStatus $orderStatus): JsonResponse
    {
        if ($this->isProtectedStatus($orderStatus)) {
            throw ValidationException::withMessages([
                'order_status' => 'Este estado es predeterminado y está protegido. No se puede editar.',
            ]);
        }

        $data = $request->validate([
            'name' => 'sometimes|string|max:255',
            'slug' => ['sometimes', 'string', 'max:255', Rule::unique('order_statuses', 'slug')->ignore($orderStatus->id)->whereNull('deleted_at')],
            'color' => 'nullable|string|max:50',
            'icon' => 'nullable|string|max:100',
            'sort_order' => 'nullable|integer',
            'is_active' => 'nullable|boolean',
        ]);
        $orderStatus->update($data);
        return response()->json($orderStatus);
    }

    public function destroy(OrderStatus $orderStatus): JsonResponse
    {
        if ($this->isProtectedStatus($orderStatus)) {
            throw ValidationException::withMessages([
                'order_status' => 'Este estado es predeterminado y está protegido. No se puede eliminar.',
            ]);
        }

        if ($orderStatus->orders()->exists()) {
            throw ValidationException::withMessages([
                'order_status' => 'No se puede eliminar un estado que ya está siendo usado por pedidos.',
            ]);
        }

        $orderStatus->delete();
        return response()->json(null, 204);
    }

    private function isProtectedStatus(OrderStatus $orderStatus): bool
    {
        if (Schema::hasColumn('order_statuses', 'is_protected') && (bool) $orderStatus->is_protected) {
            return true;
        }

        return in_array(mb_strtolower(trim((string) $orderStatus->slug)), self::PROTECTED_SLUGS, true);
    }
}
