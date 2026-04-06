<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * Configuración general del sistema (clave-valor)
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('settings', function (Blueprint $table) {
            $table->string('key', 100)->primary();
            $table->text('value')->nullable();
            $table->string('label', 255)->nullable();
            $table->string('type', 30)->default('string'); // string, boolean, integer, json
            $table->string('group', 60)->default('general');
            $table->timestamps();
        });

        // Insert defaults
        \Illuminate\Support\Facades\DB::table('settings')->insert([
            ['key' => 'igv_enabled',      'value' => 'true',  'label' => 'IGV activo',          'type' => 'boolean', 'group' => 'fiscal', 'created_at' => now(), 'updated_at' => now()],
            ['key' => 'igv_rate',         'value' => '0.18',  'label' => 'Tasa de IGV',          'type' => 'decimal', 'group' => 'fiscal', 'created_at' => now(), 'updated_at' => now()],
            ['key' => 'currency',         'value' => 'PEN',   'label' => 'Moneda',               'type' => 'string',  'group' => 'general','created_at' => now(), 'updated_at' => now()],
            ['key' => 'company_name',     'value' => 'Hiitop','label' => 'Nombre de empresa',    'type' => 'string',  'group' => 'general','created_at' => now(), 'updated_at' => now()],
            ['key' => 'prices_include_igv','value' => 'false', 'label' => 'Precios incluyen IGV', 'type' => 'boolean', 'group' => 'fiscal', 'created_at' => now(), 'updated_at' => now()],
        ]);
    }

    public function down(): void
    {
        Schema::dropIfExists('settings');
    }
};
