<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * Pedidos - tabla principal según Excel
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('orders', function (Blueprint $table) {
            $table->id();
            $table->string('order_number')->unique()->nullable();
            $table->date('order_date');
            $table->foreignId('order_status_id')->constrained()->cascadeOnDelete();
            $table->foreignId('shipping_agency_id')->nullable()->constrained()->nullOnDelete();
            $table->foreignId('purchase_type_id')->nullable()->constrained()->nullOnDelete();
            $table->text('observations')->nullable();
            $table->string('phone', 30)->nullable();
            $table->foreignId('customer_id')->nullable()->constrained()->nullOnDelete();
            // Denormalizado para histórico (provincia, distrito, dirección)
            $table->string('customer_name')->nullable();
            $table->string('dni', 20)->nullable();
            $table->foreignId('province_id')->nullable()->constrained()->nullOnDelete();
            $table->foreignId('district_id')->nullable()->constrained()->nullOnDelete();
            $table->string('address')->nullable();
            $table->decimal('delivery_cost', 12, 2)->default(0);
            $table->decimal('total', 12, 2)->default(0);
            // Comprobante
            $table->foreignId('document_type_id')->nullable()->constrained()->nullOnDelete();
            $table->string('document_number', 50)->nullable();
            $table->string('customer_email')->nullable();
            $table->boolean('needs_receipt')->default(true);
            $table->foreignId('user_id')->nullable()->constrained()->nullOnDelete();
            $table->timestamps();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('orders');
    }
};
