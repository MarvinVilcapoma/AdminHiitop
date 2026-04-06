<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Illuminate\Database\Eloquent\SoftDeletes;

class OrderStatus extends Model
{
    use SoftDeletes;
    protected $fillable = ['name', 'slug', 'color', 'icon', 'sort_order', 'is_active', 'is_protected'];

    protected $casts = [
        'is_active' => 'boolean',
        'is_protected' => 'boolean',
    ];

    /** Pedidos con este estado */
    public function orders(): HasMany
    {
        return $this->hasMany(Order::class, 'order_status_id');
    }
}
