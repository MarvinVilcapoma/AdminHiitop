<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * Series de comprobantes electrónicos (F001, B001, FC01, BC01…)
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('invoice_series', function (Blueprint $table) {
            $table->id();
            // Tipo de comprobante: 01=Factura, 03=Boleta, 07=NC-Factura, 08=NC-Boleta
            $table->string('doc_type', 2);
            // Serie: F001, B001, FC01, BC01
            $table->string('serie', 10)->unique();
            // Siguiente número correlativo a usar
            $table->unsignedInteger('next_number')->default(1);
            $table->boolean('is_active')->default(true);
            $table->timestamps();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('invoice_series');
    }
};
