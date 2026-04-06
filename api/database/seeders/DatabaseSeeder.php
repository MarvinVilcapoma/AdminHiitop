<?php

namespace Database\Seeders;

use App\Models\User;
use Illuminate\Database\Seeder;
use Illuminate\Support\Facades\Hash;
use Spatie\Permission\Models\Permission;
use Spatie\Permission\Models\Role;

class DatabaseSeeder extends Seeder
{
    public function run(): void
    {
        $this->call([
            CatalogSeeder::class,
            RolePermissionSeeder::class,
            SunatSettingsSeeder::class,
            InvoiceSeriesSeeder::class,
        ]);

        // Usuario admin por defecto
        $admin = User::firstOrCreate(
            ['email' => 'admin@hiitop.com'],
            [
                'name' => 'Admin',
                'password' => Hash::make('password'),
            ]
        );
        $admin->assignRole('admin');
    }
}
