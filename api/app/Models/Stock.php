<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\SoftDeletes;

class Stock extends Model
{
    use SoftDeletes;
    protected $fillable = ['product_id', 'warehouse_id', 'color_id', 'size', 'quantity', 'reserved'];

    protected $casts = [
        'quantity' => 'integer',
        'reserved' => 'integer',
    ];

    protected $appends = ['available'];

    public function product(): BelongsTo
    {
        return $this->belongsTo(Product::class);
    }

    public function warehouse(): BelongsTo
    {
        return $this->belongsTo(Warehouse::class);
    }

    public function color(): BelongsTo
    {
        return $this->belongsTo(Color::class);
    }

    /** Cantidad disponible (quantity - reserved) */
    public function getAvailableAttribute(): int
    {
        return max(0, $this->quantity - $this->reserved);
    }
}
