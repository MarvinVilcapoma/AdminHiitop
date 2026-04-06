<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;

class UnitMeasure extends Model
{
    use SoftDeletes;

    protected $fillable = [
        'name',
        'code',
        'sunat_code',
        'is_active',
    ];

    protected $casts = [
        'is_active' => 'boolean',
    ];
}
