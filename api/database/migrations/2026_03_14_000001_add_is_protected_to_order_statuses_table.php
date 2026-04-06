<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::table('order_statuses', function (Blueprint $table) {
            if (!Schema::hasColumn('order_statuses', 'is_protected')) {
                $table->boolean('is_protected')->default(false)->after('is_active');
            }
        });
    }

    public function down(): void
    {
        Schema::table('order_statuses', function (Blueprint $table) {
            if (Schema::hasColumn('order_statuses', 'is_protected')) {
                $table->dropColumn('is_protected');
            }
        });
    }
};
