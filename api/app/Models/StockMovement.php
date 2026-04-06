<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class StockMovement extends Model
{
    protected $fillable = [
        'stock_id', 'product_id', 'warehouse_id', 'color_id', 'size',
        'movement_type', 'quantity_before', 'quantity_change', 'quantity_after',
        'reason', 'reference', 'user_id',
    ];

    protected $casts = [
        'quantity_before'  => 'integer',
        'quantity_change'  => 'integer',
        'quantity_after'   => 'integer',
    ];

    public function stock(): BelongsTo     { return $this->belongsTo(Stock::class); }
    public function product(): BelongsTo   { return $this->belongsTo(Product::class); }
    public function warehouse(): BelongsTo { return $this->belongsTo(Warehouse::class); }
    public function color(): BelongsTo     { return $this->belongsTo(Color::class); }
    public function user(): BelongsTo      { return $this->belongsTo(User::class); }
}
