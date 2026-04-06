<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::table('orders', function (Blueprint $table) {
            if (!Schema::hasColumn('orders', 'guide_transfer_reason_code')) {
                $table->string('guide_transfer_reason_code', 4)->nullable()->after('document_number');
            }
            if (!Schema::hasColumn('orders', 'guide_transfer_reason_description')) {
                $table->string('guide_transfer_reason_description')->nullable()->after('guide_transfer_reason_code');
            }
            if (!Schema::hasColumn('orders', 'guide_transfer_mode')) {
                $table->string('guide_transfer_mode', 2)->nullable()->after('guide_transfer_reason_description');
            }
            if (!Schema::hasColumn('orders', 'guide_transfer_date')) {
                $table->date('guide_transfer_date')->nullable()->after('guide_transfer_mode');
            }
            if (!Schema::hasColumn('orders', 'guide_total_weight')) {
                $table->decimal('guide_total_weight', 12, 3)->nullable()->after('guide_transfer_date');
            }
            if (!Schema::hasColumn('orders', 'guide_weight_unit')) {
                $table->string('guide_weight_unit', 3)->nullable()->after('guide_total_weight');
            }
            if (!Schema::hasColumn('orders', 'guide_package_count')) {
                $table->unsignedInteger('guide_package_count')->nullable()->after('guide_weight_unit');
            }
            if (!Schema::hasColumn('orders', 'guide_origin_ubigeo')) {
                $table->string('guide_origin_ubigeo', 6)->nullable()->after('guide_package_count');
            }
            if (!Schema::hasColumn('orders', 'guide_origin_address')) {
                $table->string('guide_origin_address')->nullable()->after('guide_origin_ubigeo');
            }
            if (!Schema::hasColumn('orders', 'guide_destination_ubigeo')) {
                $table->string('guide_destination_ubigeo', 6)->nullable()->after('guide_origin_address');
            }
            if (!Schema::hasColumn('orders', 'guide_destination_address')) {
                $table->string('guide_destination_address')->nullable()->after('guide_destination_ubigeo');
            }
            if (!Schema::hasColumn('orders', 'guide_recipient_doc_type')) {
                $table->string('guide_recipient_doc_type', 2)->nullable()->after('guide_destination_address');
            }
            if (!Schema::hasColumn('orders', 'guide_recipient_doc_number')) {
                $table->string('guide_recipient_doc_number', 20)->nullable()->after('guide_recipient_doc_type');
            }
            if (!Schema::hasColumn('orders', 'guide_recipient_name')) {
                $table->string('guide_recipient_name')->nullable()->after('guide_recipient_doc_number');
            }
            if (!Schema::hasColumn('orders', 'guide_carrier_doc_type')) {
                $table->string('guide_carrier_doc_type', 2)->nullable()->after('guide_recipient_name');
            }
            if (!Schema::hasColumn('orders', 'guide_carrier_doc_number')) {
                $table->string('guide_carrier_doc_number', 20)->nullable()->after('guide_carrier_doc_type');
            }
            if (!Schema::hasColumn('orders', 'guide_carrier_name')) {
                $table->string('guide_carrier_name')->nullable()->after('guide_carrier_doc_number');
            }
            if (!Schema::hasColumn('orders', 'guide_vehicle_plate')) {
                $table->string('guide_vehicle_plate', 20)->nullable()->after('guide_carrier_name');
            }
            if (!Schema::hasColumn('orders', 'guide_driver_doc_type')) {
                $table->string('guide_driver_doc_type', 2)->nullable()->after('guide_vehicle_plate');
            }
            if (!Schema::hasColumn('orders', 'guide_driver_doc_number')) {
                $table->string('guide_driver_doc_number', 20)->nullable()->after('guide_driver_doc_type');
            }
            if (!Schema::hasColumn('orders', 'guide_driver_name')) {
                $table->string('guide_driver_name')->nullable()->after('guide_driver_doc_number');
            }
            if (!Schema::hasColumn('orders', 'guide_driver_license')) {
                $table->string('guide_driver_license', 30)->nullable()->after('guide_driver_name');
            }
            if (!Schema::hasColumn('orders', 'guide_transport_certificate')) {
                $table->string('guide_transport_certificate', 60)->nullable()->after('guide_driver_license');
            }

            if (!Schema::hasColumn('orders', 'guide_series')) {
                $table->string('guide_series', 10)->nullable()->after('guide_transport_certificate');
            }
            if (!Schema::hasColumn('orders', 'guide_correlativo')) {
                $table->unsignedInteger('guide_correlativo')->nullable()->after('guide_series');
            }
            if (!Schema::hasColumn('orders', 'guide_full_number')) {
                $table->string('guide_full_number', 60)->nullable()->after('guide_correlativo');
            }
            if (!Schema::hasColumn('orders', 'guide_status')) {
                $table->string('guide_status', 20)->nullable()->after('guide_full_number');
            }
            if (!Schema::hasColumn('orders', 'guide_sunat_code')) {
                $table->integer('guide_sunat_code')->nullable()->after('guide_status');
            }
            if (!Schema::hasColumn('orders', 'guide_sunat_description')) {
                $table->text('guide_sunat_description')->nullable()->after('guide_sunat_code');
            }
            if (!Schema::hasColumn('orders', 'guide_xml_content')) {
                $table->longText('guide_xml_content')->nullable()->after('guide_sunat_description');
            }
            if (!Schema::hasColumn('orders', 'guide_cdr_content')) {
                $table->longText('guide_cdr_content')->nullable()->after('guide_xml_content');
            }
            if (!Schema::hasColumn('orders', 'guide_sent_at')) {
                $table->dateTime('guide_sent_at')->nullable()->after('guide_cdr_content');
            }
        });
    }

    public function down(): void
    {
        Schema::table('orders', function (Blueprint $table) {
            $columns = [
                'guide_transfer_reason_code',
                'guide_transfer_reason_description',
                'guide_transfer_mode',
                'guide_transfer_date',
                'guide_total_weight',
                'guide_weight_unit',
                'guide_package_count',
                'guide_origin_ubigeo',
                'guide_origin_address',
                'guide_destination_ubigeo',
                'guide_destination_address',
                'guide_recipient_doc_type',
                'guide_recipient_doc_number',
                'guide_recipient_name',
                'guide_carrier_doc_type',
                'guide_carrier_doc_number',
                'guide_carrier_name',
                'guide_vehicle_plate',
                'guide_driver_doc_type',
                'guide_driver_doc_number',
                'guide_driver_name',
                'guide_driver_license',
                'guide_transport_certificate',
                'guide_series',
                'guide_correlativo',
                'guide_full_number',
                'guide_status',
                'guide_sunat_code',
                'guide_sunat_description',
                'guide_xml_content',
                'guide_cdr_content',
                'guide_sent_at',
            ];

            foreach ($columns as $column) {
                if (Schema::hasColumn('orders', $column)) {
                    $table->dropColumn($column);
                }
            }
        });
    }
};
