<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        if (!Schema::hasTable('warehouses')) {
            return;
        }

        if (!Schema::hasColumn('warehouses', 'is_pos')) {
            Schema::table('warehouses', function (Blueprint $table) {
                $table->boolean('is_pos')->default(false)->after('is_active');
            });
        }

        $ids = DB::table('warehouses')
            ->where('code', 'TIENDA_FISICA')
            ->pluck('id')
            ->all();

        if (Schema::hasColumn('warehouses', 'type')) {
            $typeIds = DB::table('warehouses')
                ->where('type', 'store')
                ->pluck('id')
                ->all();
            $ids = array_merge($ids, $typeIds);
        }

        if (Schema::hasColumn('warehouses', 'warehouse_type_id') && Schema::hasTable('warehouse_types')) {
            $typeByRelation = DB::table('warehouses as w')
                ->join('warehouse_types as wt', 'wt.id', '=', 'w.warehouse_type_id')
                ->where(function ($q) {
                    $q->where('wt.code', 'TIENDA_FISICA')
                        ->orWhere('wt.code', 'STORE')
                        ->orWhere('wt.name', 'like', '%tienda%');
                })
                ->pluck('w.id')
                ->all();

            $ids = array_merge($ids, $typeByRelation);
        }

        $ids = array_values(array_unique(array_map('intval', $ids)));

        if (!empty($ids)) {
            DB::table('warehouses')
                ->whereIn('id', $ids)
                ->update([
                    'is_pos' => true,
                    'updated_at' => now(),
                ]);
        }
    }

    public function down(): void
    {
        if (!Schema::hasTable('warehouses') || !Schema::hasColumn('warehouses', 'is_pos')) {
            return;
        }

        Schema::table('warehouses', function (Blueprint $table) {
            $table->dropColumn('is_pos');
        });
    }
};
