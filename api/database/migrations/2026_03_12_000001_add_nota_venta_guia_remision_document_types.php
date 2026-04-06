<?php

use App\Models\DocumentType;
use Illuminate\Database\Migrations\Migration;

return new class extends Migration
{
    public function up(): void
    {
        DocumentType::firstOrCreate(
            ['code' => 'NOTA_VENTA'],
            ['name' => 'Nota de Venta', 'is_active' => true]
        );

        DocumentType::firstOrCreate(
            ['code' => 'GUIA_REMISION'],
            ['name' => 'Guía de Remisión', 'is_active' => true]
        );
    }

    public function down(): void
    {
        DocumentType::whereIn('code', ['NOTA_VENTA', 'GUIA_REMISION'])->delete();
    }
};
