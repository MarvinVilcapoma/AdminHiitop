<?php

namespace Database\Seeders;

use App\Models\Setting;
use Illuminate\Database\Seeder;

/**
 * Siembra las claves de configuración SUNAT (vacías para producción,
 * con valores de prueba para el entorno beta de SUNAT).
 *
 * PARA MODO BETA: usa las credenciales de prueba de SUNAT (MODDATOS/moddatos)
 * y el certificado de prueba de Greenter.
 *
 * IMPORTANTE: reemplaza sunat_certificate_pem con el contenido de tu certificado
 * PEM real antes de pasar a producción.
 */
class SunatSettingsSeeder extends Seeder
{
    public function run(): void
    {
        $settings = [
            // ── Empresa emisora ────────────────────────────────────────────────
            ['key' => 'sunat_ruc',              'value' => '20000000001',    'label' => 'RUC del emisor',              'type' => 'string',  'group' => 'sunat'],
            ['key' => 'sunat_razon_social',     'value' => 'EMPRESA DEMO SAC', 'label' => 'Razón Social',              'type' => 'string',  'group' => 'sunat'],
            ['key' => 'sunat_nombre_comercial', 'value' => 'DEMO',           'label' => 'Nombre Comercial',            'type' => 'string',  'group' => 'sunat'],

            // ── Dirección fiscal ───────────────────────────────────────────────
            ['key' => 'sunat_ubigueo',          'value' => '150101',         'label' => 'Ubigeo',                      'type' => 'string',  'group' => 'sunat'],
            ['key' => 'sunat_departamento',     'value' => 'LIMA',           'label' => 'Departamento',                'type' => 'string',  'group' => 'sunat'],
            ['key' => 'sunat_provincia',        'value' => 'LIMA',           'label' => 'Provincia',                   'type' => 'string',  'group' => 'sunat'],
            ['key' => 'sunat_distrito',         'value' => 'LIMA',           'label' => 'Distrito',                    'type' => 'string',  'group' => 'sunat'],
            ['key' => 'sunat_urbanizacion',     'value' => '-',              'label' => 'Urbanización',                'type' => 'string',  'group' => 'sunat'],
            ['key' => 'sunat_direccion',        'value' => 'Av. Demo 123',   'label' => 'Dirección',                   'type' => 'string',  'group' => 'sunat'],
            ['key' => 'sunat_codigo_local',     'value' => '0000',           'label' => 'Código de establecimiento',   'type' => 'string',  'group' => 'sunat'],

            // ── Clave SOL (beta por defecto) ───────────────────────────────────
            ['key' => 'sunat_sol_user',         'value' => 'MODDATOS',       'label' => 'Usuario SOL',                 'type' => 'string',  'group' => 'sunat'],
            ['key' => 'sunat_sol_pass',         'value' => 'moddatos',       'label' => 'Contraseña SOL',              'type' => 'string',  'group' => 'sunat'],

            // ── Ambiente: beta | production ───────────────────────────────────
            ['key' => 'sunat_environment',      'value' => 'beta',           'label' => 'Ambiente SUNAT',              'type' => 'string',  'group' => 'sunat'],

            // ── Certificado digital PEM ────────────────────────────────────────
            // Pega aquí el contenido completo de tu certificado .pem
            // Para pruebas puedes usar el cert de Greenter:
            // https://raw.githubusercontent.com/thegreenter/xmldsig/master/tests/certificate.pem
            ['key' => 'sunat_certificate_pem',  'value' => '',               'label' => 'Certificado digital (PEM)',   'type' => 'string',  'group' => 'sunat'],
        ];

        foreach ($settings as $s) {
            Setting::firstOrCreate(
                ['key' => $s['key']],
                ['value' => $s['value'], 'label' => $s['label'], 'type' => $s['type'], 'group' => $s['group']]
            );
        }
    }
}
