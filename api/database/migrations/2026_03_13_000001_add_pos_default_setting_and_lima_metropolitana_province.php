<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        $now = now();

        if (Schema::hasTable('settings')) {
            $exists = DB::table('settings')->where('key', 'pos_default_warehouse_id')->exists();

            if ($exists) {
                DB::table('settings')
                    ->where('key', 'pos_default_warehouse_id')
                    ->update([
                        'label' => 'Almacén predeterminado POS',
                        'type' => 'integer',
                        'group' => 'general',
                        'updated_at' => $now,
                    ]);
            } else {
                DB::table('settings')->insert([
                    'key' => 'pos_default_warehouse_id',
                    'value' => null,
                    'label' => 'Almacén predeterminado POS',
                    'type' => 'integer',
                    'group' => 'general',
                    'created_at' => $now,
                    'updated_at' => $now,
                ]);
            }
        }

        if (Schema::hasTable('provinces')) {
            $province = DB::table('provinces')
                ->where('name', 'Lima Metropolitana')
                ->orWhere('code', 'LIMAMETRO')
                ->first();

            if ($province) {
                DB::table('provinces')
                    ->where('id', $province->id)
                    ->update([
                        'name' => 'Lima Metropolitana',
                        'code' => 'LIMAMETRO',
                        'is_active' => true,
                        'updated_at' => $now,
                    ]);
            } else {
                DB::table('provinces')->insert([
                    'name' => 'Lima Metropolitana',
                    'code' => 'LIMAMETRO',
                    'is_active' => true,
                    'created_at' => $now,
                    'updated_at' => $now,
                ]);
            }
        }
    }

    public function down(): void
    {
        if (Schema::hasTable('settings')) {
            DB::table('settings')->where('key', 'pos_default_warehouse_id')->delete();
        }

        if (Schema::hasTable('provinces')) {
            DB::table('provinces')->where('code', 'LIMAMETRO')->delete();
        }
    }
};
