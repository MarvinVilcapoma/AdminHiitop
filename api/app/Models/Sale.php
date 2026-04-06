<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class Sale extends Model
{
    protected $fillable = [
        'movement_type', 'document_type_label', 'document_number',
        'issue_date', 'series_number', 'series_prefix', 'tracking_number',
        'sale_datetime', 'branch', 'seller',
        'customer_name', 'customer_tax_id', 'customer_email',
        'customer_address', 'customer_district', 'customer_province', 'customer_department',
        'price_list', 'delivery_type', 'currency',
        'total_net', 'total_tax', 'total_gross',
        'discount_net', 'discount_gross',
        'import_source', 'import_batch', 'user_id',
    ];

    protected $casts = [
        'issue_date'   => 'date',
        'sale_datetime' => 'datetime',
        'total_net'    => 'decimal:2',
        'total_tax'    => 'decimal:2',
        'total_gross'  => 'decimal:2',
        'discount_net' => 'decimal:2',
        'discount_gross' => 'decimal:2',
    ];

    public function items(): HasMany
    {
        return $this->hasMany(SaleItem::class);
    }

    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }
}
