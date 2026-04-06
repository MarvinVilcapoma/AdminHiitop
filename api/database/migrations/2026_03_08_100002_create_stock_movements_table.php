<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('stock_movements', function (Blueprint $table) {
            $table->id();
            $table->foreignId('stock_id')->constrained()->cascadeOnDelete();
            $table->foreignId('product_id')->constrained()->cascadeOnDelete();
            $table->foreignId('warehouse_id')->constrained()->cascadeOnDelete();
            $table->foreignId('color_id')->nullable()->constrained()->nullOnDelete();
            $table->string('size', 20)->nullable();
            // 'entry' = ingreso, 'exit' = salida, 'adjustment' = ajuste, 'transfer' = traslado
            $table->enum('movement_type', ['entry', 'exit', 'adjustment', 'transfer'])->default('adjustment');
            $table->integer('quantity_before')->default(0);
            $table->integer('quantity_change');      // puede ser negativo
            $table->integer('quantity_after')->default(0);
            $table->string('reason')->nullable();    // motivo del movimiento
            $table->string('reference')->nullable(); // número de orden, factura, etc.
            $table->foreignId('user_id')->nullable()->constrained()->nullOnDelete();
            $table->timestamps();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('stock_movements');
    }
};
