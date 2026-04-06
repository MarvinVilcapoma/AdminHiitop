<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\HasMany;

class Province extends Model
{
    protected $fillable = ['name', 'code', 'is_active'];

    protected $casts = [
        'is_active' => 'boolean',
    ];

    public function districts(): HasMany
    {
        return $this->hasMany(District::class, 'province_id');
    }

    public function customers(): HasMany
    {
        return $this->hasMany(Customer::class, 'province_id');
    }

    public function orders(): HasMany
    {
        return $this->hasMany(Order::class, 'province_id');
    }
}
