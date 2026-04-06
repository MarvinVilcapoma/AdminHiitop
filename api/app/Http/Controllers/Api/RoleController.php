<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Validation\Rule;
use Spatie\Permission\Models\Role;
use Spatie\Permission\Models\Permission;

class RoleController extends Controller
{
    private const MODULE_PERMISSIONS = [
        'dashboard.view',
        'orders.view',
        'guides.view',
        'products.view',
        'stocks.view',
        'customers.view',
        'sales.view',
        'users.view',
        'config.order-statuses',
    ];

    private function appGuard(): string
    {
        $user = auth()->user();
        if ($user && method_exists($user, 'roles')) {
            $guard = $user->roles()->pluck('guard_name')->filter()->first();
            if (is_string($guard) && $guard !== '') {
                return $guard;
            }
        }

        return (string) config('auth.defaults.guard', 'web');
    }

    private function ensureModulePermissions(string $guard): void
    {
        foreach (self::MODULE_PERMISSIONS as $name) {
            Permission::firstOrCreate(['name' => $name, 'guard_name' => $guard]);
        }
    }

    public function index(): JsonResponse
    {
        $guard = $this->appGuard();

        $roles = Role::query()
            ->where('guard_name', $guard)
            ->withCount('users')
            ->with('permissions:id,name')
            ->orderBy('name')
            ->get();

        return response()->json($roles);
    }

    public function store(Request $request): JsonResponse
    {
        $guard = $this->appGuard();

        $data = $request->validate([
            'name'        => [
                'required',
                'string',
                'max:100',
                Rule::unique('roles', 'name')->where(fn ($q) => $q->where('guard_name', $guard)),
            ],
            'permissions' => 'nullable|array',
            'permissions.*' => [
                'string',
                Rule::exists('permissions', 'name')->where(fn ($q) => $q->where('guard_name', $guard)),
            ],
        ]);

        $role = Role::create(['name' => $data['name'], 'guard_name' => $guard]);

        if (!empty($data['permissions'])) {
            $role->syncPermissions($data['permissions']);
        }

        return response()->json($role->load('permissions'), 201);
    }

    public function show(Role $role): JsonResponse
    {
        return response()->json($role->load('permissions'));
    }

    public function update(Request $request, Role $role): JsonResponse
    {
        $guard = $role->guard_name;

        $data = $request->validate([
            'name'        => [
                'sometimes',
                'string',
                'max:100',
                Rule::unique('roles', 'name')
                    ->ignore($role->id)
                    ->where(fn ($q) => $q->where('guard_name', $guard)),
            ],
            'permissions' => 'nullable|array',
            'permissions.*' => [
                'string',
                Rule::exists('permissions', 'name')->where(fn ($q) => $q->where('guard_name', $guard)),
            ],
        ]);

        if (isset($data['name'])) {
            $role->update(['name' => $data['name']]);
        }

        if (isset($data['permissions'])) {
            $role->syncPermissions($data['permissions']);
        }

        return response()->json($role->fresh()->load('permissions'));
    }

    public function destroy(Role $role): JsonResponse
    {
        // Prevent deleting built-in roles
        if (in_array($role->name, ['admin', 'super-admin'])) {
            return response()->json(['message' => 'No se puede eliminar el rol admin.'], 422);
        }
        $role->delete();
        return response()->json(null, 204);
    }

    public function permissions(): JsonResponse
    {
        $guard = $this->appGuard();
        $this->ensureModulePermissions($guard);

        return response()->json(
            Permission::query()
                ->where('guard_name', $guard)
                ->orderBy('name')
                ->get(['id', 'name'])
        );
    }
}
