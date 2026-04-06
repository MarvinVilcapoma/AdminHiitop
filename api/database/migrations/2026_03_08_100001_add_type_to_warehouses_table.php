<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::table('warehouses', function (Blueprint $table) {
            // 'store' = tienda física, 'warehouse' = almacén
            $table->enum('type', ['store', 'warehouse'])->default('warehouse')->after('address');
            $table->string('city')->nullable()->after('type');
        });
    }

    public function down(): void
    {
        Schema::table('warehouses', function (Blueprint $table) {
            $table->dropColumn(['type', 'city']);
        });
    }
};
