<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Illuminate\Database\Eloquent\SoftDeletes;

class DocumentType extends Model
{
    use SoftDeletes;
    protected $fillable = ['name', 'code', 'is_active', 'is_protected'];

    protected $casts = [
        'is_active' => 'boolean',
        'is_protected' => 'boolean',
    ];

    public function orders(): HasMany
    {
        return $this->hasMany(Order::class, 'document_type_id');
    }
}
