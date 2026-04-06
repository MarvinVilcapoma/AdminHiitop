<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * Importaciones de ventas desde sistemas externos (ej. Bsale, Defontana).
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('sale_imports', function (Blueprint $table) {
            $table->id();
            $table->string('import_batch')->index();    // UUID del lote de importación
            $table->string('movement_type', 50)->nullable();
            $table->string('document_type', 80)->nullable();
            $table->string('document_number', 50)->nullable();
            $table->date('issue_date')->nullable();
            $table->string('series_number', 50)->nullable();
            $table->string('series_prefix', 20)->nullable();
            $table->string('tracking_number', 100)->nullable();
            $table->dateTime('sale_datetime')->nullable();
            $table->string('branch', 100)->nullable();
            $table->string('seller', 100)->nullable();
            $table->string('customer_name', 200)->nullable();
            $table->string('customer_ruc', 20)->nullable();
            $table->string('customer_email', 150)->nullable();
            $table->string('customer_address', 255)->nullable();
            $table->string('customer_district', 100)->nullable();
            $table->string('customer_province', 100)->nullable();
            $table->string('customer_department', 100)->nullable();
            $table->string('price_list', 100)->nullable();
            $table->string('delivery_type', 100)->nullable();
            $table->string('currency', 10)->nullable();
            $table->string('product_category', 100)->nullable();
            $table->string('sku', 80)->nullable();
            $table->string('product_name', 255)->nullable();
            $table->string('variant', 100)->nullable();
            $table->string('other_attributes', 255)->nullable();
            $table->string('brand', 100)->nullable();
            $table->string('pack_detail', 255)->nullable();
            $table->decimal('list_price', 12, 2)->nullable();
            $table->decimal('unit_net_price', 12, 2)->nullable();
            $table->decimal('unit_gross_price', 12, 2)->nullable();
            $table->integer('quantity')->nullable();
            $table->decimal('total_net', 12, 2)->nullable();
            $table->decimal('total_tax', 12, 2)->nullable();
            $table->decimal('total_gross', 12, 2)->nullable();
            $table->string('discount_name', 100)->nullable();
            $table->decimal('discount_net', 12, 2)->nullable();
            $table->decimal('discount_gross', 12, 2)->nullable();
            $table->string('discount_pct', 20)->nullable();
            $table->decimal('unit_cost_net', 12, 2)->nullable();
            $table->decimal('total_cost_net', 12, 2)->nullable();
            $table->decimal('margin', 12, 2)->nullable();
            $table->string('margin_pct', 20)->nullable();
            $table->foreignId('imported_by')->nullable()->constrained('users')->nullOnDelete();
            $table->timestamps();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('sale_imports');
    }
};
