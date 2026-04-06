<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::table('promotion_items', function (Blueprint $table) {
            // Make product_id nullable (items may now reference product types instead)
            $table->foreignId('product_type_id')
                ->nullable()
                ->after('product_id')
                ->constrained('product_types')
                ->nullOnDelete();

            // Make product_id nullable for backward compat
            $table->foreignId('product_id')
                ->nullable()
                ->change();
        });
    }

    public function down(): void
    {
        Schema::table('promotion_items', function (Blueprint $table) {
            $table->dropForeign(['product_type_id']);
            $table->dropColumn('product_type_id');
        });
    }
};
