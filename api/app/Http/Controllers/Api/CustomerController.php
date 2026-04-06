<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Customer;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

class CustomerController extends Controller
{
    public function index(Request $request): JsonResponse
    {
        $query = Customer::query()->with(['province', 'district'])->orderBy('full_name');
        if ($request->filled('search')) {
            $query->where(function ($q) use ($request) {
                $q->where('full_name', 'like', '%' . $request->search . '%')
                    ->orWhere('dni', 'like', '%' . $request->search . '%')
                    ->orWhere('email', 'like', '%' . $request->search . '%');
            });
        }
        if ($request->filled('status')) {
            $query->where('is_active', $request->status === 'active');
        }
        $items = $request->has('per_page')
            ? $query->paginate((int) $request->get('per_page', 15))
            : $query->get();
        return response()->json($items);
    }

    public function store(Request $request): JsonResponse
    {
        $data = $request->validate([
            'full_name' => 'required|string|max:255',
            'dni' => 'nullable|string|max:20|unique:customers,dni',
            'phone' => 'nullable|string|max:30',
            'email' => 'nullable|email',
            'province_id' => 'nullable|exists:provinces,id',
            'district_id' => 'nullable|exists:districts,id',
            'address' => 'nullable|string',
            'is_active' => 'nullable|boolean',
        ]);
        $item = Customer::create($data);
        return response()->json($item->load(['province', 'district']), 201);
    }

    public function show(Customer $customer): JsonResponse
    {
        $customer->load(['province', 'district']);
        return response()->json($customer);
    }

    public function update(Request $request, Customer $customer): JsonResponse
    {
        $data = $request->validate([
            'full_name' => 'sometimes|string|max:255',
            'dni' => 'nullable|string|max:20|unique:customers,dni,' . $customer->id,
            'phone' => 'nullable|string|max:30',
            'email' => 'nullable|email',
            'province_id' => 'nullable|exists:provinces,id',
            'district_id' => 'nullable|exists:districts,id',
            'address' => 'nullable|string',
            'is_active' => 'nullable|boolean',
        ]);
        $customer->update($data);
        return response()->json($customer->load(['province', 'district']));
    }

    public function destroy(Customer $customer): JsonResponse
    {
        $customer->delete();
        return response()->json(null, 204);
    }
}
