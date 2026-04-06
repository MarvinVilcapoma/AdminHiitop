<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\User;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Hash;
use Illuminate\Validation\Rule;
use Illuminate\Validation\Rules\Password;
use Spatie\Permission\Models\Role;

class UserController extends Controller
{
    private function appGuard(): string
    {
        $authUser = auth()->user();
        if ($authUser && method_exists($authUser, 'roles')) {
            $guard = $authUser->roles()->pluck('guard_name')->filter()->first();
            if (is_string($guard) && $guard !== '') {
                return $guard;
            }
        }

        return (string) config('auth.defaults.guard', 'web');
    }

    public function index(Request $request): JsonResponse
    {
        $query = User::with('roles:id,name')
            ->orderBy('name');

        if ($request->filled('search')) {
            $s = $request->search;
            $query->where(fn($q) => $q->where('name', 'like', "%$s%")->orWhere('email', 'like', "%$s%"));
        }
        if ($request->filled('status')) {
            $query->where('is_active', $request->status === 'active');
        }

        $users = $request->has('per_page')
            ? $query->paginate((int) $request->get('per_page', 15))
            : $query->get();

        return response()->json($users);
    }

    public function store(Request $request): JsonResponse
    {
        $guard = $this->appGuard();

        $data = $request->validate([
            'name'     => 'required|string|max:255',
            'email'    => 'required|email|unique:users,email',
            'password' => ['required', 'confirmed', Password::min(8)],
            'roles'    => 'required|array|size:1',
            'roles.*'  => [
                'string',
                Rule::exists('roles', 'name')->where(fn ($q) => $q->where('guard_name', $guard)),
            ],
        ]);

        $user = User::create([
            'name'     => $data['name'],
            'email'    => $data['email'],
            'password' => Hash::make($data['password']),
        ]);

        if (!empty($data['roles'])) {
            $user->syncRoles($data['roles']);
        }

        return response()->json($user->load('roles:id,name'), 201);
    }

    public function show(User $user): JsonResponse
    {
        return response()->json($user->load('roles:id,name'));
    }

    public function update(Request $request, User $user): JsonResponse
    {
        $guard = $this->appGuard();

        $data = $request->validate([
            'name'     => 'sometimes|string|max:255',
            'email'    => 'sometimes|email|unique:users,email,' . $user->id,
            'password' => ['nullable', 'confirmed', Password::min(8)],
            'roles'    => 'nullable|array|size:1',
            'roles.*'  => [
                'string',
                Rule::exists('roles', 'name')->where(fn ($q) => $q->where('guard_name', $guard)),
            ],
            'is_active' => 'nullable|boolean',
        ]);

        $updateData = array_filter([
            'name'      => $data['name'] ?? null,
            'email'     => $data['email'] ?? null,
            'is_active' => $data['is_active'] ?? null,
        ], fn($v) => $v !== null);

        if (!empty($data['password'])) {
            $updateData['password'] = Hash::make($data['password']);
        }

        if (!empty($updateData)) {
            $user->update($updateData);
        }

        if (isset($data['roles'])) {
            $user->syncRoles($data['roles']);
        }

        return response()->json($user->fresh()->load('roles:id,name'));
    }

    public function destroy(User $user): JsonResponse
    {
        // Prevent deleting yourself
        if ($user->id === auth()->id()) {
            return response()->json(['message' => 'No puedes eliminar tu propio usuario.'], 422);
        }
        $user->delete();
        return response()->json(null, 204);
    }

    public function roles(): JsonResponse
    {
        return response()->json(
            Role::query()
                ->where('guard_name', $this->appGuard())
                ->orderBy('name')
                ->get(['id', 'name'])
        );
    }
}
