<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('sale_items', function (Blueprint $table) {
            $table->id();
            $table->foreignId('sale_id')->constrained('sales')->cascadeOnDelete();
            $table->string('tipo_producto_servicio')->nullable();
            $table->string('sku')->nullable();
            $table->string('producto_servicio')->nullable();
            $table->string('variante')->nullable();
            $table->string('otros_atributos')->nullable();
            $table->string('marca')->nullable();
            $table->string('detalle_pack')->nullable();
            $table->decimal('precio_lista', 12, 2)->default(0);
            $table->decimal('precio_neto_unitario', 12, 2)->default(0);
            $table->decimal('precio_bruto_unitario', 12, 2)->default(0);
            $table->decimal('cantidad', 10, 2)->default(1);
            $table->decimal('venta_total_neta', 12, 2)->default(0);
            $table->decimal('total_impuestos', 12, 2)->default(0);
            $table->decimal('venta_total_bruta', 12, 2)->default(0);
            $table->string('nombre_descuento')->nullable();
            $table->decimal('descuento_neto', 12, 2)->default(0);
            $table->decimal('descuento_bruto', 12, 2)->default(0);
            $table->decimal('pct_descuento', 7, 4)->default(0);
            $table->decimal('costo_neto_unitario', 12, 2)->default(0);
            $table->decimal('costo_total_neto', 12, 2)->default(0);
            $table->decimal('margen', 12, 2)->default(0);
            $table->decimal('pct_margen', 7, 4)->default(0);
            $table->timestamps();

            $table->index(['sale_id', 'sku']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('sale_items');
    }
};
