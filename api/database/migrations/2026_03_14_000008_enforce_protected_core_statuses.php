<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        if (!Schema::hasTable('order_statuses')) {
            return;
        }

        $now = now();

        $pendingId = $this->upsertCoreStatus(
            primarySlug: 'pending',
            aliases: ['pending', 'pendiente'],
            name: 'Pending',
            color: '#f59e0b',
            sortOrder: 1,
            now: $now,
        );

        $cancelledId = $this->upsertCoreStatus(
            primarySlug: 'cancelled',
            aliases: ['cancelled', 'cancelado'],
            name: 'Cancelled',
            color: '#ef4444',
            sortOrder: 7,
            now: $now,
        );

        $deliveredId = $this->upsertCoreStatus(
            primarySlug: 'delivered',
            aliases: ['delivered', 'entregado'],
            name: 'Delivered',
            color: '#10b981',
            sortOrder: 4,
            now: $now,
        );

        if (Schema::hasColumn('order_statuses', 'is_protected')) {
            DB::table('order_statuses')
                ->whereIn('id', array_filter([$pendingId, $cancelledId, $deliveredId]))
                ->update(['is_protected' => true, 'updated_at' => $now]);
        }

        $pagado = DB::table('order_statuses')->where('slug', 'pagado')->first();
        if ($pagado) {
            if (Schema::hasTable('orders') && Schema::hasColumn('orders', 'order_status_id') && $deliveredId) {
                DB::table('orders')
                    ->where('order_status_id', $pagado->id)
                    ->update([
                        'order_status_id' => $deliveredId,
                        'updated_at' => $now,
                    ]);
            }

            $updatePayload = [
                'is_active' => false,
                'updated_at' => $now,
            ];

            if (Schema::hasColumn('order_statuses', 'is_protected')) {
                $updatePayload['is_protected'] = false;
            }

            if (Schema::hasColumn('order_statuses', 'deleted_at')) {
                $updatePayload['deleted_at'] = $now;
            }

            DB::table('order_statuses')->where('id', $pagado->id)->update($updatePayload);
        }
    }

    public function down(): void
    {
        if (!Schema::hasTable('order_statuses')) {
            return;
        }

        $now = now();

        $payload = [
            'is_active' => true,
            'updated_at' => $now,
        ];

        if (Schema::hasColumn('order_statuses', 'deleted_at')) {
            $payload['deleted_at'] = null;
        }

        DB::table('order_statuses')
            ->where('slug', 'pagado')
            ->update($payload);
    }

    private function upsertCoreStatus(
        string $primarySlug,
        array $aliases,
        string $name,
        string $color,
        int $sortOrder,
        $now
    ): ?int {
        $statuses = DB::table('order_statuses')
            ->whereIn('slug', $aliases)
            ->orderByRaw('CASE WHEN slug = ? THEN 0 ELSE 1 END', [$primarySlug])
            ->orderBy('id')
            ->get();

        $primary = $statuses->first();

        if (!$primary) {
            return (int) DB::table('order_statuses')->insertGetId([
                'name' => $name,
                'slug' => $primarySlug,
                'color' => $color,
                'sort_order' => $sortOrder,
                'is_active' => true,
                'created_at' => $now,
                'updated_at' => $now,
            ]);
        }

        $duplicates = $statuses->skip(1);
        foreach ($duplicates as $legacy) {
            if (Schema::hasTable('orders') && Schema::hasColumn('orders', 'order_status_id')) {
                DB::table('orders')
                    ->where('order_status_id', $legacy->id)
                    ->update([
                        'order_status_id' => $primary->id,
                        'updated_at' => $now,
                    ]);
            }

            DB::table('order_statuses')->where('id', $legacy->id)->delete();
        }

        DB::table('order_statuses')->where('id', $primary->id)->update([
            'name' => $name,
            'slug' => $primarySlug,
            'color' => $color,
            'sort_order' => $sortOrder,
            'is_active' => true,
            'updated_at' => $now,
        ]);

        return (int) $primary->id;
    }
};
