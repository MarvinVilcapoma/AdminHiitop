<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\HasMany;

class InvoiceSeries extends Model
{
    protected $fillable = ['doc_type', 'serie', 'next_number', 'is_active'];

    protected $casts = [
        'is_active'   => 'boolean',
        'next_number' => 'integer',
    ];

    public function invoices(): HasMany
    {
        return $this->hasMany(Invoice::class);
    }

    /** Toma el siguiente correlativo de forma atómica y lo incrementa */
    public function takeNextNumber(): int
    {
        $number = $this->next_number;
        $this->increment('next_number');
        return $number;
    }

    /** Genera el número completo de comprobante */
    public function buildFullNumber(string $ruc, int $correlativo): string
    {
        return sprintf('%s-%s-%s-%08d', $ruc, $this->doc_type, $this->serie, $correlativo);
    }
}
