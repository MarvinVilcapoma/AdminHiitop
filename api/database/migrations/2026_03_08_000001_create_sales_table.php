<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('sales', function (Blueprint $table) {
            $table->id();
            $table->string('tipo_movimiento')->default('venta');
            $table->string('tipo_documento')->nullable();
            $table->string('numero_documento')->nullable();
            $table->date('fecha_emision')->nullable();
            $table->string('numero_serie')->nullable()->comment('Ej: B003-32');
            $table->string('prefijo_serie')->nullable()->comment('Ej: B003');
            $table->string('tracking_number')->nullable();
            $table->dateTime('fecha_hora_venta')->nullable();
            $table->string('sucursal')->nullable();
            $table->string('vendedor')->nullable();
            $table->string('nombre_cliente')->nullable();
            $table->string('cliente_ruc', 20)->nullable();
            $table->string('email_cliente')->nullable();
            $table->string('cliente_direccion')->nullable();
            $table->string('cliente_distrito')->nullable();
            $table->string('cliente_provincia')->nullable();
            $table->string('cliente_departamento')->nullable();
            $table->string('lista_precio')->nullable();
            $table->string('tipo_entrega')->nullable();
            $table->string('moneda', 10)->default('PEN');
            $table->decimal('venta_total_neta', 12, 2)->default(0);
            $table->decimal('total_impuestos', 12, 2)->default(0);
            $table->decimal('venta_total_bruta', 12, 2)->default(0);
            $table->decimal('descuento_neto', 12, 2)->default(0);
            $table->decimal('descuento_bruto', 12, 2)->default(0);
            $table->string('import_source')->nullable()->comment('bsale, manual, etc.');
            $table->string('import_batch')->nullable();
            $table->timestamps();

            $table->index(['fecha_hora_venta']);
            $table->index(['numero_serie']);
            $table->index(['sucursal']);
            $table->index(['vendedor']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('sales');
    }
};
