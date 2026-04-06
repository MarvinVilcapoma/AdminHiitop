<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * Rename Spanish column names to English in sales and sale_items tables.
 * Also adds user_id to sales for tracking who created each sale.
 */
return new class extends Migration
{
    public function up(): void
    {
        // ── sales ──
        Schema::table('sales', function (Blueprint $table) {
            $table->renameColumn('tipo_movimiento',    'movement_type');
            $table->renameColumn('tipo_documento',     'document_type_label');
            $table->renameColumn('numero_documento',   'document_number');
            $table->renameColumn('fecha_emision',      'issue_date');
            $table->renameColumn('numero_serie',       'series_number');
            $table->renameColumn('prefijo_serie',      'series_prefix');
            $table->renameColumn('fecha_hora_venta',   'sale_datetime');
            $table->renameColumn('sucursal',           'branch');
            $table->renameColumn('vendedor',           'seller');
            $table->renameColumn('nombre_cliente',     'customer_name');
            $table->renameColumn('cliente_ruc',        'customer_tax_id');
            $table->renameColumn('email_cliente',      'customer_email');
            $table->renameColumn('cliente_direccion',  'customer_address');
            $table->renameColumn('cliente_distrito',   'customer_district');
            $table->renameColumn('cliente_provincia',  'customer_province');
            $table->renameColumn('cliente_departamento', 'customer_department');
            $table->renameColumn('lista_precio',       'price_list');
            $table->renameColumn('tipo_entrega',       'delivery_type');
            $table->renameColumn('moneda',             'currency');
            $table->renameColumn('venta_total_neta',   'total_net');
            $table->renameColumn('total_impuestos',    'total_tax');
            $table->renameColumn('venta_total_bruta',  'total_gross');
            $table->renameColumn('descuento_neto',     'discount_net');
            $table->renameColumn('descuento_bruto',    'discount_gross');
        });

        Schema::table('sales', function (Blueprint $table) {
            $table->foreignId('user_id')->nullable()->constrained()->nullOnDelete()->after('import_batch');
        });

        // ── sale_items ──
        Schema::table('sale_items', function (Blueprint $table) {
            $table->renameColumn('tipo_producto_servicio', 'product_type');
            $table->renameColumn('producto_servicio',      'product_name');
            $table->renameColumn('variante',               'variant');
            $table->renameColumn('otros_atributos',        'other_attributes');
            $table->renameColumn('marca',                  'brand');
            $table->renameColumn('detalle_pack',           'pack_detail');
            $table->renameColumn('precio_lista',           'list_price');
            $table->renameColumn('precio_neto_unitario',   'unit_net_price');
            $table->renameColumn('precio_bruto_unitario',  'unit_gross_price');
            $table->renameColumn('cantidad',               'quantity');
            $table->renameColumn('venta_total_neta',       'total_net');
            $table->renameColumn('total_impuestos',        'total_tax');
            $table->renameColumn('venta_total_bruta',      'total_gross');
            $table->renameColumn('nombre_descuento',       'discount_name');
            $table->renameColumn('descuento_neto',         'discount_net');
            $table->renameColumn('descuento_bruto',        'discount_gross');
            $table->renameColumn('pct_descuento',          'discount_pct');
            $table->renameColumn('costo_neto_unitario',    'unit_cost_net');
            $table->renameColumn('costo_total_neto',       'total_cost_net');
            $table->renameColumn('margen',                 'margin');
            $table->renameColumn('pct_margen',             'margin_pct');
        });
    }

    public function down(): void
    {
        Schema::table('sales', function (Blueprint $table) {
            $table->dropColumn('user_id');
            $table->renameColumn('movement_type',     'tipo_movimiento');
            $table->renameColumn('document_type_label','tipo_documento');
            $table->renameColumn('document_number',   'numero_documento');
            $table->renameColumn('issue_date',        'fecha_emision');
            $table->renameColumn('series_number',     'numero_serie');
            $table->renameColumn('series_prefix',     'prefijo_serie');
            $table->renameColumn('sale_datetime',     'fecha_hora_venta');
            $table->renameColumn('branch',            'sucursal');
            $table->renameColumn('seller',            'vendedor');
            $table->renameColumn('customer_name',     'nombre_cliente');
            $table->renameColumn('customer_tax_id',   'cliente_ruc');
            $table->renameColumn('customer_email',    'email_cliente');
            $table->renameColumn('customer_address',  'cliente_direccion');
            $table->renameColumn('customer_district', 'cliente_distrito');
            $table->renameColumn('customer_province', 'cliente_provincia');
            $table->renameColumn('customer_department','cliente_departamento');
            $table->renameColumn('price_list',        'lista_precio');
            $table->renameColumn('delivery_type',     'tipo_entrega');
            $table->renameColumn('currency',          'moneda');
            $table->renameColumn('total_net',         'venta_total_neta');
            $table->renameColumn('total_tax',         'total_impuestos');
            $table->renameColumn('total_gross',       'venta_total_bruta');
            $table->renameColumn('discount_net',      'descuento_neto');
            $table->renameColumn('discount_gross',    'descuento_bruto');
        });

        Schema::table('sale_items', function (Blueprint $table) {
            $table->renameColumn('product_type',    'tipo_producto_servicio');
            $table->renameColumn('product_name',    'producto_servicio');
            $table->renameColumn('variant',         'variante');
            $table->renameColumn('other_attributes','otros_atributos');
            $table->renameColumn('brand',           'marca');
            $table->renameColumn('pack_detail',     'detalle_pack');
            $table->renameColumn('list_price',      'precio_lista');
            $table->renameColumn('unit_net_price',  'precio_neto_unitario');
            $table->renameColumn('unit_gross_price','precio_bruto_unitario');
            $table->renameColumn('quantity',        'cantidad');
            $table->renameColumn('total_net',       'venta_total_neta');
            $table->renameColumn('total_tax',       'total_impuestos');
            $table->renameColumn('total_gross',     'venta_total_bruta');
            $table->renameColumn('discount_name',   'nombre_descuento');
            $table->renameColumn('discount_net',    'descuento_neto');
            $table->renameColumn('discount_gross',  'descuento_bruto');
            $table->renameColumn('discount_pct',    'pct_descuento');
            $table->renameColumn('unit_cost_net',   'costo_neto_unitario');
            $table->renameColumn('total_cost_net',  'costo_total_neto');
            $table->renameColumn('margin',          'margen');
            $table->renameColumn('margin_pct',      'pct_margen');
        });
    }
};
