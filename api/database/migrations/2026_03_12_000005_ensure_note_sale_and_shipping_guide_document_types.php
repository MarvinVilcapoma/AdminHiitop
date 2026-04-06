<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        if (!Schema::hasTable('document_types')) {
            return;
        }

        DB::table('document_types')->updateOrInsert(
            ['code' => 'NOTA_VENTA'],
            ['name' => 'Nota de Venta', 'is_active' => true, 'updated_at' => now(), 'created_at' => now()]
        );

        DB::table('document_types')->updateOrInsert(
            ['code' => 'GUIA_REMISION'],
            ['name' => 'Guía de Remisión', 'is_active' => true, 'updated_at' => now(), 'created_at' => now()]
        );
    }

    public function down(): void
    {
        if (!Schema::hasTable('document_types')) {
            return;
        }

        DB::table('document_types')
            ->whereIn('code', ['NOTA_VENTA', 'GUIA_REMISION'])
            ->delete();
    }
};
