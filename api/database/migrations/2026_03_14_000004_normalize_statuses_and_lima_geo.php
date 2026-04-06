<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;
use Illuminate\Support\Str;

return new class extends Migration
{
    public function up(): void
    {
        $now = now();

        $this->normalizeOrderStatuses($now);
        $this->normalizeLimaGeography($now);
    }

    public function down(): void
    {
        if (!Schema::hasTable('order_statuses') || !Schema::hasColumn('order_statuses', 'is_protected')) {
            return;
        }

        DB::table('order_statuses')
            ->whereIn('slug', ['pagado', 'cancelado'])
            ->update([
                'is_protected' => false,
                'updated_at' => now(),
            ]);
    }

    private function normalizeOrderStatuses($now): void
    {
        if (!Schema::hasTable('order_statuses')) {
            return;
        }

        $canceladoId = $this->normalizeStatusPair(
            primarySlug: 'cancelado',
            legacySlug: 'cancelled',
            name: 'Cancelado',
            sortOrder: 7,
            now: $now,
        );

        $this->normalizeStatusPair(
            primarySlug: 'pendiente',
            legacySlug: 'pending',
            name: 'Pendiente',
            sortOrder: 1,
            now: $now,
        );

        $pagado = DB::table('order_statuses')->where('slug', 'pagado')->first();
        if ($pagado) {
            DB::table('order_statuses')->where('id', $pagado->id)->update([
                'name' => 'Pagado',
                'sort_order' => 5,
                'is_active' => true,
                'updated_at' => $now,
            ]);
        } else {
            DB::table('order_statuses')->insert([
                'name' => 'Pagado',
                'slug' => 'pagado',
                'color' => '#059669',
                'sort_order' => 5,
                'is_active' => true,
                'created_at' => $now,
                'updated_at' => $now,
            ]);
        }

        if (Schema::hasColumn('order_statuses', 'is_protected')) {
            DB::table('order_statuses')
                ->whereIn('slug', ['pagado', 'cancelado'])
                ->update(['is_protected' => true, 'updated_at' => $now]);
        }

        if ($canceladoId) {
            DB::table('order_statuses')
                ->where('slug', 'cancelled')
                ->where('id', '!=', $canceladoId)
                ->delete();
        }
    }

    private function normalizeStatusPair(string $primarySlug, string $legacySlug, string $name, int $sortOrder, $now): ?int
    {
        $primary = DB::table('order_statuses')->where('slug', $primarySlug)->first();
        $legacy = DB::table('order_statuses')->where('slug', $legacySlug)->first();

        if (!$primary && $legacy) {
            DB::table('order_statuses')->where('id', $legacy->id)->update([
                'slug' => $primarySlug,
                'name' => $name,
                'sort_order' => $sortOrder,
                'is_active' => true,
                'updated_at' => $now,
            ]);

            $primary = DB::table('order_statuses')->where('id', $legacy->id)->first();
            $legacy = null;
        }

        if (!$primary) {
            $primaryId = DB::table('order_statuses')->insertGetId([
                'name' => $name,
                'slug' => $primarySlug,
                'color' => null,
                'sort_order' => $sortOrder,
                'is_active' => true,
                'created_at' => $now,
                'updated_at' => $now,
            ]);
            $primary = (object) ['id' => $primaryId];
        }

        if ($legacy && $legacy->id !== $primary->id) {
            $this->reassignOrdersStatus((int) $legacy->id, (int) $primary->id, $now);
            DB::table('order_statuses')->where('id', $legacy->id)->delete();
        }

        DB::table('order_statuses')->where('id', $primary->id)->update([
            'name' => $name,
            'sort_order' => $sortOrder,
            'is_active' => true,
            'updated_at' => $now,
        ]);

        return (int) $primary->id;
    }

    private function reassignOrdersStatus(int $fromStatusId, int $toStatusId, $now): void
    {
        if (!Schema::hasTable('orders') || !Schema::hasColumn('orders', 'order_status_id')) {
            return;
        }

        DB::table('orders')
            ->where('order_status_id', $fromStatusId)
            ->update([
                'order_status_id' => $toStatusId,
                'updated_at' => $now,
            ]);
    }

    private function normalizeLimaGeography($now): void
    {
        if (!Schema::hasTable('provinces') || !Schema::hasTable('districts')) {
            return;
        }

        $limaProvinciaId = $this->upsertProvince('Lima Provincia', 'LIMAPROV', ['Lima', 'Lima Provincia'], $now);
        $limaMetroId = $this->upsertProvince('Lima Metropolitana', 'LIMAMETRO', ['Lima Metropolitana'], $now);

        if ($limaProvinciaId === null || $limaMetroId === null) {
            return;
        }

        $metroDistricts = [
            'Lima Cercado', 'Miraflores', 'San Isidro', 'Surquillo', 'Barranco', 'Chorrillos',
            'San Borja', 'Santiago de Surco', 'La Molina', 'Ate', 'Santa Anita', 'El Agustino',
            'San Luis', 'Breña', 'Jesús María', 'Lince', 'Magdalena del Mar', 'Pueblo Libre',
            'San Miguel', 'La Victoria', 'Rímac', 'San Martín de Porres', 'Los Olivos',
            'Independencia', 'Comas', 'Carabayllo', 'Puente Piedra', 'Ancón', 'Santa Rosa',
            'Lurigancho-Chosica', 'San Juan de Lurigancho', 'San Juan de Miraflores',
            'Villa María del Triunfo', 'Villa El Salvador', 'Pachacamac', 'Lurín',
            'Punta Hermosa', 'Punta Negra', 'Santa María del Mar', 'Pucusana', 'San Bartolo',
            'Cieneguilla', 'Chaclacayo',
        ];

        $limaProvinciaDistricts = [
            'Huaral', 'Huacho', 'Barranca', 'Cañete', 'San Vicente de Cañete',
            'Huarochirí', 'Matucana', 'Yauyos', 'Oyon', 'Sayán',
        ];

        $this->assignDistrictsToProvince($limaMetroId, $metroDistricts, $now);
        $this->assignDistrictsToProvince($limaProvinciaId, $limaProvinciaDistricts, $now);

        $callaoId = DB::table('provinces')->where('name', 'Callao')->value('id');
        if ($callaoId) {
            $this->assignDistrictsToProvince((int) $callaoId, [
                'Callao', 'Bellavista', 'Carmen de La Legua Reynoso', 'La Perla', 'La Punta',
                'Mi Perú', 'Ventanilla',
            ], $now);
        }
    }

    private function upsertProvince(string $name, string $code, array $aliases, $now): ?int
    {
        $province = DB::table('provinces')
            ->where('code', $code)
            ->orWhereIn('name', $aliases)
            ->first();

        if ($province) {
            DB::table('provinces')->where('id', $province->id)->update([
                'name' => $name,
                'code' => $code,
                'is_active' => true,
                'updated_at' => $now,
            ]);

            return (int) $province->id;
        }

        return (int) DB::table('provinces')->insertGetId([
            'name' => $name,
            'code' => $code,
            'is_active' => true,
            'created_at' => $now,
            'updated_at' => $now,
        ]);
    }

    private function assignDistrictsToProvince(int $provinceId, array $districtNames, $now): void
    {
        foreach ($districtNames as $districtName) {
            $district = DB::table('districts')
                ->where('name', $districtName)
                ->orderByRaw('CASE WHEN province_id = ? THEN 0 ELSE 1 END', [$provinceId])
                ->first();

            if ($district) {
                DB::table('districts')->where('id', $district->id)->update([
                    'province_id' => $provinceId,
                    'is_active' => true,
                    'code' => $district->code ?: $this->districtCode($provinceId, $districtName),
                    'updated_at' => $now,
                ]);
                continue;
            }

            DB::table('districts')->insert([
                'province_id' => $provinceId,
                'name' => $districtName,
                'code' => $this->districtCode($provinceId, $districtName),
                'is_active' => true,
                'created_at' => $now,
                'updated_at' => $now,
            ]);
        }
    }

    private function districtCode(int $provinceId, string $districtName): string
    {
        $rawCode = strtoupper(preg_replace('/\s+/', '', Str::ascii($districtName)));

        return substr($provinceId . '_' . $rawCode, 0, 20);
    }
};
