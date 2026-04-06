<?php

namespace Database\Seeders;

use App\Models\InvoiceSeries;
use Illuminate\Database\Seeder;

class InvoiceSeriesSeeder extends Seeder
{
    public function run(): void
    {
        $series = [
            ['doc_type' => '01', 'serie' => 'F001', 'next_number' => 1, 'is_active' => true],
            ['doc_type' => '03', 'serie' => 'B001', 'next_number' => 1, 'is_active' => true],
            ['doc_type' => '07', 'serie' => 'FC01', 'next_number' => 1, 'is_active' => true],
            ['doc_type' => '08', 'serie' => 'BC01', 'next_number' => 1, 'is_active' => true],
        ];

        foreach ($series as $s) {
            InvoiceSeries::firstOrCreate(
                ['serie' => $s['serie']],
                ['doc_type' => $s['doc_type'], 'next_number' => $s['next_number'], 'is_active' => $s['is_active']]
            );
        }
    }
}
