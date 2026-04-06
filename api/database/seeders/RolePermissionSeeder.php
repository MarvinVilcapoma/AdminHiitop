<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;
use Spatie\Permission\Models\Permission;
use Spatie\Permission\Models\Role;

class RolePermissionSeeder extends Seeder
{
    public function run(): void
    {
        $this->command->info('Creando permisos y roles...');
        $guard = (string) config('auth.defaults.guard', 'web');

        $permissions = [
            // Pedidos
            'orders.view', 'orders.create', 'orders.update', 'orders.delete',
            // Guías de remisión
            'guides.view',
            // Clientes
            'customers.view', 'customers.create', 'customers.update', 'customers.delete',
            // Productos
            'products.view', 'products.create', 'products.update', 'products.delete',
            // Stock
            'stocks.view', 'stocks.create', 'stocks.update', 'stocks.delete', 'stocks.adjust',
            // Ventas importadas
            'sales.view', 'sales.import', 'sales.delete',
            // Dashboard
            'dashboard.view',
            // Usuarios
            'users.view', 'users.create', 'users.update', 'users.delete',
            // Configuración
            'config.order-statuses', 'config.shipping-agencies', 'config.document-types',
            'config.purchase-types', 'config.product-types', 'config.colors',
            'config.warehouses', 'config.provinces', 'config.districts',
            'config.collections',
        ];

        foreach ($permissions as $name) {
            Permission::firstOrCreate(['name' => $name, 'guard_name' => $guard]);
        }

        // Rol ADMIN: todos los permisos
        $admin = Role::firstOrCreate(['name' => 'admin', 'guard_name' => $guard]);
        $admin->syncPermissions(Permission::query()->where('guard_name', $guard)->get());

        // Rol MANAGER: operaciones, sin borrar ni gestionar usuarios/config
        $manager = Role::firstOrCreate(['name' => 'manager', 'guard_name' => $guard]);
        $manager->syncPermissions([
            'dashboard.view',
            'orders.view', 'orders.create', 'orders.update',
            'guides.view',
            'customers.view', 'customers.create', 'customers.update',
            'products.view', 'stocks.view', 'stocks.update', 'stocks.adjust',
            'sales.view', 'sales.import',
        ]);

        // Rol VENDEDOR: solo pedidos y clientes
        $seller = Role::firstOrCreate(['name' => 'vendedor', 'guard_name' => $guard]);
        $seller->syncPermissions([
            'dashboard.view',
            'orders.view', 'orders.create',
            'customers.view', 'customers.create',
            'stocks.view',
        ]);

        $this->command->info('Permisos y roles creados.');
    }
}
