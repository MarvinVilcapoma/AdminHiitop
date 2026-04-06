<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        // Add deleted_at to warehouse_types
        if (Schema::hasTable('warehouse_types') && !Schema::hasColumn('warehouse_types', 'deleted_at')) {
            Schema::table('warehouse_types', function (Blueprint $table) {
                $table->softDeletes();
            });
        }

        // Add deleted_at to payment_methods
        if (Schema::hasTable('payment_methods') && !Schema::hasColumn('payment_methods', 'deleted_at')) {
            Schema::table('payment_methods', function (Blueprint $table) {
                $table->softDeletes();
            });
        }

        // Add is_active to users
        if (Schema::hasTable('users') && !Schema::hasColumn('users', 'is_active')) {
            Schema::table('users', function (Blueprint $table) {
                $table->boolean('is_active')->default(true)->after('remember_token');
            });
        }

        // Add is_active to customers
        if (Schema::hasTable('customers') && !Schema::hasColumn('customers', 'is_active')) {
            Schema::table('customers', function (Blueprint $table) {
                $table->boolean('is_active')->default(true)->after('address');
            });
        }
    }

    public function down(): void
    {
        if (Schema::hasColumn('warehouse_types', 'deleted_at')) {
            Schema::table('warehouse_types', fn (Blueprint $t) => $t->dropSoftDeletes());
        }
        if (Schema::hasColumn('payment_methods', 'deleted_at')) {
            Schema::table('payment_methods', fn (Blueprint $t) => $t->dropSoftDeletes());
        }
        if (Schema::hasColumn('users', 'is_active')) {
            Schema::table('users', fn (Blueprint $t) => $t->dropColumn('is_active'));
        }
        if (Schema::hasColumn('customers', 'is_active')) {
            Schema::table('customers', fn (Blueprint $t) => $t->dropColumn('is_active'));
        }
    }
};
