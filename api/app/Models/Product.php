<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\BelongsToMany;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Illuminate\Database\Eloquent\SoftDeletes;

class Product extends Model
{
    use SoftDeletes;
    protected $fillable = [
        'name', 'sku', 'product_type_id', 'collection_id',
        'description', 'base_price', 'unit_cost', 'is_active',
    ];

    protected $casts = [
        'base_price' => 'decimal:2',
        'unit_cost' => 'decimal:2',
        'is_active' => 'boolean',
    ];

    protected $appends = ['total_stock'];

    public function getTotalStockAttribute(): int
    {
        return (int) ($this->stocks_sum_quantity ?? 0);
    }

    public function productType(): BelongsTo
    {
        return $this->belongsTo(ProductType::class, 'product_type_id');
    }

    public function collection(): BelongsTo
    {
        return $this->belongsTo(Collection::class, 'collection_id');
    }

    public function stocks(): HasMany
    {
        return $this->hasMany(Stock::class);
    }

    public function orderItems(): HasMany
    {
        return $this->hasMany(OrderItem::class);
    }

    /** Colores pre-configurados para este producto */
    public function colors(): BelongsToMany
    {
        return $this->belongsToMany(Color::class, 'product_color')
                    ->withPivot('sort_order')
                    ->orderByPivot('sort_order');
    }
}
