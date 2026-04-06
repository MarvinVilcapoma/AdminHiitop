<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * Tallas configurables por tipo de producto
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('sizes', function (Blueprint $table) {
            $table->id();
            $table->string('name', 30);       // XS, S, M, L, XL, XXL, 28, 30 …
            $table->integer('sort_order')->default(0);
            $table->timestamps();
        });

        // Pivot: qué tallas están disponibles para cada tipo de producto
        Schema::create('product_type_size', function (Blueprint $table) {
            $table->foreignId('product_type_id')->constrained()->cascadeOnDelete();
            $table->foreignId('size_id')->constrained()->cascadeOnDelete();
            $table->integer('sort_order')->default(0);
            $table->primary(['product_type_id', 'size_id']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('product_type_size');
        Schema::dropIfExists('sizes');
    }
};
