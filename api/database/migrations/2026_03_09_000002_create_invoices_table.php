<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * Comprobantes electrónicos (facturas, boletas, notas de crédito)
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('invoices', function (Blueprint $table) {
            $table->id();

            // Relación con pedido (puede ser nulo si se emite de forma independiente)
            $table->foreignId('order_id')->nullable()->constrained()->nullOnDelete();

            // Serie (FK a invoice_series)
            $table->foreignId('invoice_series_id')->constrained('invoice_series');

            // Tipo de comprobante SUNAT
            $table->string('doc_type', 2);        // 01=Factura, 03=Boleta, 07=NC-Fact, 08=NC-Bol
            $table->string('serie', 10);           // F001, B001, FC01
            $table->unsignedInteger('correlativo');
            // 20123456789-01-F001-00000001
            $table->string('full_number', 60)->unique();

            // Estado del comprobante
            $table->string('status', 20)->default('draft');
            // draft → pending → accepted | rejected | exception | error

            // Datos del receptor (snapshot al momento de emisión)
            $table->string('customer_doc_type', 2)->nullable();  // 1=DNI, 6=RUC, 4=CE, 7=PAS, -=sin doc
            $table->string('customer_doc_number', 20)->nullable();
            $table->string('customer_name')->nullable();

            // Moneda y forma de pago
            $table->string('currency', 3)->default('PEN');
            $table->string('form_of_payment', 20)->default('contado'); // contado, credito

            // Importes (calculados sin/con IGV según configuración)
            $table->decimal('mto_oper_gravadas', 12, 2)->default(0); // Base imponible
            $table->decimal('mto_igv', 12, 2)->default(0);
            $table->decimal('valor_venta', 12, 2)->default(0);       // = mto_oper_gravadas
            $table->decimal('sub_total', 12, 2)->default(0);          // = mto_imp_venta
            $table->decimal('mto_imp_venta', 12, 2)->default(0);      // Total a pagar

            // Respuesta de SUNAT
            $table->integer('sunat_code')->nullable();
            $table->text('sunat_description')->nullable();
            $table->json('sunat_notes')->nullable();

            // XML firmado y CDR (zip en base64)
            $table->longText('xml_content')->nullable();
            $table->text('cdr_content')->nullable();

            // Datos específicos de Nota de Crédito
            $table->string('note_motive', 2)->nullable();       // 01=Anulación, 02=Anulación por error, etc.
            $table->string('note_motive_desc')->nullable();
            $table->string('ref_doc_type', 2)->nullable();      // Tipo doc referenciado
            $table->string('ref_doc_number')->nullable();        // Nro. comprobante referenciado
            $table->date('ref_doc_date')->nullable();

            $table->text('observations')->nullable();
            $table->timestamp('issued_at');
            $table->timestamps();
            $table->softDeletes();

            // Usuario que emitió
            $table->foreignId('user_id')->nullable()->constrained()->nullOnDelete();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('invoices');
    }
};
