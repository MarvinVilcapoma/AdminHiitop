<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Illuminate\Database\Eloquent\SoftDeletes;
use App\Models\Invoice;

class Order extends Model
{
    use SoftDeletes;
    protected $fillable = [
        'order_number', 'order_date', 'order_status_id', 'shipping_agency_id', 'purchase_type_id',
        'warehouse_id',
        'observations', 'phone', 'customer_id', 'customer_name', 'dni', 'province_id', 'district_id', 'address',
        'delivery_cost', 'total', 'document_type_id', 'document_number', 'customer_email', 'needs_receipt', 'user_id',
        'guide_transfer_reason_code', 'guide_transfer_reason_description', 'guide_transfer_mode', 'guide_transfer_date',
        'guide_total_weight', 'guide_weight_unit', 'guide_package_count',
        'guide_origin_ubigeo', 'guide_origin_address', 'guide_destination_ubigeo', 'guide_destination_address',
        'guide_recipient_doc_type', 'guide_recipient_doc_number', 'guide_recipient_name',
        'guide_carrier_doc_type', 'guide_carrier_doc_number', 'guide_carrier_name',
        'guide_vehicle_plate', 'guide_driver_doc_type', 'guide_driver_doc_number', 'guide_driver_name',
        'guide_driver_license', 'guide_transport_certificate',
        'guide_series', 'guide_correlativo', 'guide_full_number', 'guide_status', 'guide_sunat_code',
        'guide_sunat_description', 'guide_xml_content', 'guide_cdr_content', 'guide_sent_at',
    ];

    protected $casts = [
        'order_date' => 'datetime',
        'total' => 'decimal:2',
        'delivery_cost' => 'decimal:2',
        'needs_receipt' => 'boolean',
        'guide_transfer_date' => 'date',
        'guide_total_weight' => 'decimal:3',
        'guide_package_count' => 'integer',
        'guide_correlativo' => 'integer',
        'guide_sunat_code' => 'integer',
        'guide_sent_at' => 'datetime',
    ];

    public function orderStatus(): BelongsTo
    {
        return $this->belongsTo(OrderStatus::class, 'order_status_id');
    }

    public function shippingAgency(): BelongsTo
    {
        return $this->belongsTo(ShippingAgency::class, 'shipping_agency_id');
    }

    public function purchaseType(): BelongsTo
    {
        return $this->belongsTo(PurchaseType::class, 'purchase_type_id');
    }

    public function warehouse(): BelongsTo
    {
        return $this->belongsTo(Warehouse::class, 'warehouse_id');
    }

    public function customer(): BelongsTo
    {
        return $this->belongsTo(Customer::class, 'customer_id');
    }

    public function province(): BelongsTo
    {
        return $this->belongsTo(Province::class, 'province_id');
    }

    public function district(): BelongsTo
    {
        return $this->belongsTo(District::class, 'district_id');
    }

    public function documentType(): BelongsTo
    {
        return $this->belongsTo(DocumentType::class, 'document_type_id');
    }

    public function invoices(): HasMany
    {
        return $this->hasMany(Invoice::class);
    }

    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class, 'user_id');
    }

    public function items(): HasMany
    {
        return $this->hasMany(OrderItem::class)->orderBy('sort_order');
    }
}
