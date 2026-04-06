<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('warehouse_types', function (Blueprint $table) {
            $table->id();
            $table->string('name');
            $table->string('code')->nullable()->unique();
            $table->text('description')->nullable();
            $table->boolean('is_active')->default(true);
            $table->timestamps();
        });

        Schema::table('warehouses', function (Blueprint $table) {
            $table->foreignId('warehouse_type_id')
                ->nullable()
                ->constrained('warehouse_types')
                ->nullOnDelete()
                ->after('name');
        });
    }

    public function down(): void
    {
        Schema::table('warehouses', function (Blueprint $table) {
            $table->dropConstrainedForeignId('warehouse_type_id');
        });
        Schema::dropIfExists('warehouse_types');
    }
};
