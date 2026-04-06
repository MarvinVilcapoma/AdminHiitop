<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class SaleImport extends Model
{
    protected $fillable = [
        'import_batch', 'movement_type', 'document_type', 'document_number',
        'issue_date', 'series_number', 'series_prefix', 'tracking_number',
        'sale_datetime', 'branch', 'seller', 'customer_name', 'customer_ruc',
        'customer_email', 'customer_address', 'customer_district', 'customer_province',
        'customer_department', 'price_list', 'delivery_type', 'currency',
        'product_category', 'sku', 'product_name', 'variant', 'other_attributes',
        'brand', 'pack_detail', 'list_price', 'unit_net_price', 'unit_gross_price',
        'quantity', 'total_net', 'total_tax', 'total_gross', 'discount_name',
        'discount_net', 'discount_gross', 'discount_pct', 'unit_cost_net',
        'total_cost_net', 'margin', 'margin_pct', 'imported_by',
    ];

    protected $casts = [
        'issue_date'      => 'date',
        'sale_datetime'   => 'datetime',
        'list_price'      => 'decimal:2',
        'unit_net_price'  => 'decimal:2',
        'unit_gross_price'=> 'decimal:2',
        'total_net'       => 'decimal:2',
        'total_tax'       => 'decimal:2',
        'total_gross'     => 'decimal:2',
        'discount_net'    => 'decimal:2',
        'discount_gross'  => 'decimal:2',
        'unit_cost_net'   => 'decimal:2',
        'total_cost_net'  => 'decimal:2',
        'margin'          => 'decimal:2',
    ];

    public function importedBy(): BelongsTo
    {
        return $this->belongsTo(User::class, 'imported_by');
    }
}
