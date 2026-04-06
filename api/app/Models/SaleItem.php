<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class SaleItem extends Model
{
    protected $fillable = [
        'sale_id', 'product_type', 'sku', 'product_name',
        'variant', 'other_attributes', 'brand', 'pack_detail',
        'list_price', 'unit_net_price', 'unit_gross_price', 'quantity',
        'total_net', 'total_tax', 'total_gross',
        'discount_name', 'discount_net', 'discount_gross', 'discount_pct',
        'unit_cost_net', 'total_cost_net', 'margin', 'margin_pct',
    ];

    protected $casts = [
        'list_price'      => 'decimal:2',
        'unit_net_price'  => 'decimal:2',
        'unit_gross_price'=> 'decimal:2',
        'quantity'        => 'decimal:2',
        'total_net'       => 'decimal:2',
        'total_tax'       => 'decimal:2',
        'total_gross'     => 'decimal:2',
        'discount_net'    => 'decimal:2',
        'discount_gross'  => 'decimal:2',
        'discount_pct'    => 'decimal:4',
        'unit_cost_net'   => 'decimal:2',
        'total_cost_net'  => 'decimal:2',
        'margin'          => 'decimal:2',
        'margin_pct'      => 'decimal:4',
    ];

    public function sale(): BelongsTo
    {
        return $this->belongsTo(Sale::class);
    }
}
