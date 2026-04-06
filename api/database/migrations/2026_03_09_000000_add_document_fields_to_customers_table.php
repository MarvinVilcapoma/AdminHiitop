<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * Agrega campos de documento fiscal al cliente (RUC, tipo doc, razón social)
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::table('customers', function (Blueprint $table) {
            // Tipo de documento: DNI, RUC, CE, PAS
            $table->string('document_type', 10)->default('DNI')->after('dni');
            // RUC (11 dígitos) para clientes empresa
            $table->string('ruc', 20)->nullable()->unique()->after('document_type');
            // Razón social para clientes empresa / RUC
            $table->string('razon_social')->nullable()->after('ruc');
            // Nombre comercial (opcional)
            $table->string('nombre_comercial')->nullable()->after('razon_social');
        });
    }

    public function down(): void
    {
        Schema::table('customers', function (Blueprint $table) {
            $table->dropUnique(['ruc']);
            $table->dropColumn(['document_type', 'ruc', 'razon_social', 'nombre_comercial']);
        });
    }
};
