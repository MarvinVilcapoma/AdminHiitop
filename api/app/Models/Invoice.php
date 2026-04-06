<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\SoftDeletes;

class Invoice extends Model
{
    use SoftDeletes;

    protected $fillable = [
        'order_id', 'invoice_series_id', 'doc_type', 'serie', 'correlativo', 'full_number',
        'status', 'customer_doc_type', 'customer_doc_number', 'customer_name',
        'currency', 'form_of_payment', 'payment_method_id',
        'mto_oper_gravadas', 'mto_igv', 'valor_venta', 'sub_total', 'mto_imp_venta',
        'sunat_code', 'sunat_description', 'sunat_notes', 'xml_content', 'cdr_content',
        'note_motive', 'note_motive_desc', 'ref_doc_type', 'ref_doc_number', 'ref_doc_date',
        'observations', 'issued_at', 'user_id',
    ];

    protected $casts = [
        'mto_oper_gravadas' => 'decimal:2',
        'mto_igv'           => 'decimal:2',
        'valor_venta'       => 'decimal:2',
        'sub_total'         => 'decimal:2',
        'mto_imp_venta'     => 'decimal:2',
        'sunat_notes'       => 'array',
        'issued_at'         => 'datetime',
        'ref_doc_date'      => 'date',
    ];

    /** Nombre legible del tipo de comprobante */
    public function getDocTypeLabelAttribute(): string
    {
        return match ($this->doc_type) {
            '01' => 'Factura',
            '03' => 'Boleta de Venta',
            '07' => 'Nota de Crédito (Factura)',
            '08' => 'Nota de Crédito (Boleta)',
            default => 'Comprobante',
        };
    }

    /** Nombre legible del estado */
    public function getStatusLabelAttribute(): string
    {
        return match ($this->status) {
            'draft'     => 'Borrador',
            'pending'   => 'Enviando',
            'accepted'  => 'Aceptado',
            'rejected'  => 'Rechazado',
            'exception' => 'Excepción',
            'error'     => 'Error',
            'cancelled' => 'Anulado',
            default     => ucfirst($this->status),
        };
    }

    public function order(): BelongsTo
    {
        return $this->belongsTo(Order::class);
    }

    public function paymentMethod(): BelongsTo
    {
        return $this->belongsTo(PaymentMethod::class);
    }

    public function invoiceSeries(): BelongsTo
    {
        return $this->belongsTo(InvoiceSeries::class, 'invoice_series_id');
    }

    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }
}
