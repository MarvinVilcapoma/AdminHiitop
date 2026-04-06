<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Illuminate\Database\Eloquent\SoftDeletes;

class ShippingAgency extends Model
{
    use SoftDeletes;
    protected $fillable = ['name', 'code', 'shipping_rate', 'is_active'];

    protected $casts = [
        'shipping_rate' => 'decimal:2',
        'is_active' => 'boolean',
    ];

    public function orders(): HasMany
    {
        return $this->hasMany(Order::class, 'shipping_agency_id');
    }
}
