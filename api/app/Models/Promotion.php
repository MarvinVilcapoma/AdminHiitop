<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Illuminate\Database\Eloquent\SoftDeletes;

class Promotion extends Model
{
    use SoftDeletes;
    protected $fillable = ['name', 'description', 'is_active', 'fixed_price'];

    protected $casts = [
        'is_active'   => 'boolean',
        'fixed_price' => 'decimal:2',
    ];

    public function items(): HasMany
    {
        return $this->hasMany(PromotionItem::class);
    }

    /** Computed total price based on items (if no fixed_price set) */
    public function getTotalPriceAttribute(): float
    {
        if ($this->fixed_price !== null) {
            return (float) $this->fixed_price;
        }
        return $this->items->sum(function ($item) {
            $price = $item->unit_price ?? optional($item->product)->base_price ?? 0;
            return $price * $item->quantity;
        });
    }
}
