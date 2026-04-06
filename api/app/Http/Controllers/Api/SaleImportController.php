<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\SaleImport;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Carbon;
use Illuminate\Support\Str;

class SaleImportController extends Controller
{
    /**
     * Listar lotes de importación (agrupado por batch).
     */
    public function index(Request $request): JsonResponse
    {
        $batches = SaleImport::select('import_batch', \Illuminate\Support\Facades\DB::raw('COUNT(*) as rows'), \Illuminate\Support\Facades\DB::raw('SUM(total_gross) as total'), \Illuminate\Support\Facades\DB::raw('MIN(issue_date) as date_from'), \Illuminate\Support\Facades\DB::raw('MAX(issue_date) as date_to'), \Illuminate\Support\Facades\DB::raw('MAX(created_at) as imported_at'))
            ->groupBy('import_batch')
            ->orderByDesc('imported_at')
            ->paginate(15);

        return response()->json($batches);
    }

    /**
     * Ver detalle de un lote específico.
     */
    public function show(Request $request, string $batch): JsonResponse
    {
        $query = SaleImport::where('import_batch', $batch)->orderBy('sale_datetime');
        $items = $query->paginate((int) $request->get('per_page', 50));
        return response()->json($items);
    }

    /**
     * Importar filas TSV (tab-separated) pegadas desde Excel.
     * El frontend envía: { rows: [[campo1, campo2, ...], [...]] }
     * La primera fila debe ser la cabecera.
     */
    public function import(Request $request): JsonResponse
    {
        $request->validate([
            'rows' => 'required|array|min:2',
        ]);

        $allRows = $request->rows;
        $headers = array_map('trim', $allRows[0]);
        $dataRows = array_slice($allRows, 1);

        // Mapa de cabeceras → campo del modelo
        $headerMap = [
            'Tipo Movimiento'               => 'movement_type',
            'Tipo de Documento'             => 'document_type',
            'Numero Documento'              => 'document_number',
            'Fecha de Emisión'              => 'issue_date',
            'Número de serie'               => 'series_number',
            'Prefijo del Número de Serie'   => 'series_prefix',
            'Tracking Number'               => 'tracking_number',
            'Fecha y Hora Venta'            => 'sale_datetime',
            'Sucursal'                      => 'branch',
            'Vendedor'                      => 'seller',
            'Nombre Cliente'                => 'customer_name',
            'Cliente RUC'                   => 'customer_ruc',
            'Email Cliente'                 => 'customer_email',
            'Cliente Dirección'             => 'customer_address',
            'Cliente Distrito'              => 'customer_district',
            'Cliente Provincia'             => 'customer_province',
            'Cliente Departamento'          => 'customer_department',
            'Lista de Precio'               => 'price_list',
            'Tipo de Entrega'               => 'delivery_type',
            'Moneda'                        => 'currency',
            'Tipo de Producto / Servicio'   => 'product_category',
            'SKU'                           => 'sku',
            'Producto / Servicio'           => 'product_name',
            'Variante'                      => 'variant',
            'Otros Atributos'               => 'other_attributes',
            'Marca'                         => 'brand',
            'Detalle de Productos/Servicios Pack/Promo' => 'pack_detail',
            'Precio de Lista'               => 'list_price',
            'Precio Neto Unitario'          => 'unit_net_price',
            'Precio Bruto Unitario'         => 'unit_gross_price',
            'Cantidad'                      => 'quantity',
            'Venta Total Neta'              => 'total_net',
            'Total Impuestos'               => 'total_tax',
            'Venta Total Bruta'             => 'total_gross',
            'Nombre de dcto'                => 'discount_name',
            'Descuento Neto'                => 'discount_net',
            'Descuento Bruto'               => 'discount_gross',
            '% Descuento'                   => 'discount_pct',
            'Costo Neto Unitario'           => 'unit_cost_net',
            'Costo Total Neto'              => 'total_cost_net',
            'Margen'                        => 'margin',
            '% Margen'                      => 'margin_pct',
        ];

        $batch = (string) Str::uuid();
        $userId = auth()->id();
        $records = [];

        foreach ($dataRows as $row) {
            if (count($row) !== count($headers)) continue;
            $mapped = array_combine($headers, $row);
            $record = ['import_batch' => $batch, 'imported_by' => $userId];

            foreach ($headerMap as $headerKey => $fieldName) {
                $val = trim($mapped[$headerKey] ?? '');
                if ($val === '' || $val === '-') {
                    $record[$fieldName] = null;
                    continue;
                }

                // Parsear fechas peruanas: dd/mm/yyyy o dd/mm/yyyy hh:mm:ss
                if (in_array($fieldName, ['issue_date', 'sale_datetime']) && $val !== '') {
                    try {
                        $record[$fieldName] = Carbon::createFromFormat(
                            str_contains($val, ':') ? 'd/m/Y H:i:s' : 'd/m/Y',
                            $val
                        )->toDateTimeString();
                    } catch (\Exception) {
                        $record[$fieldName] = null;
                    }
                    continue;
                }

                // Parsear numéricos
                if (in_array($fieldName, ['list_price','unit_net_price','unit_gross_price','quantity','total_net','total_tax','total_gross','discount_net','discount_gross','unit_cost_net','total_cost_net','margin'])) {
                    $record[$fieldName] = is_numeric(str_replace(',', '.', $val)) ? (float) str_replace(',', '.', $val) : null;
                    continue;
                }

                $record[$fieldName] = $val;
            }

            $records[] = $record;
        }

        $now = now()->toDateTimeString();
        foreach ($records as &$r) {
            $r['created_at'] = $now;
            $r['updated_at'] = $now;
        }

        // Insertar en chunks para no superar límites
        foreach (array_chunk($records, 100) as $chunk) {
            SaleImport::insert($chunk);
        }

        return response()->json([
            'message'      => 'Importación exitosa.',
            'import_batch' => $batch,
            'rows_imported'=> count($records),
        ], 201);
    }

    /**
     * Eliminar un lote completo.
     */
    public function destroyBatch(string $batch): JsonResponse
    {
        $deleted = SaleImport::where('import_batch', $batch)->delete();
        return response()->json(['deleted' => $deleted]);
    }

    /**
     * Resumen de ventas importadas (para dashboard).
     */
    public function summary(Request $request): JsonResponse
    {
        $from = $request->get('from');
        $to   = $request->get('to');

        $query = SaleImport::query();
        if ($from) $query->whereDate('issue_date', '>=', $from);
        if ($to)   $query->whereDate('issue_date', '<=', $to);

        $totalGross  = $query->sum('total_gross');
        $totalNet    = $query->sum('total_net');
        $totalItems  = $query->sum('quantity');
        $uniqueDocs  = $query->distinct('document_number')->count('document_number');

        return response()->json(compact('totalGross', 'totalNet', 'totalItems', 'uniqueDocs'));
    }
}
