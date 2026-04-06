<?php

namespace Database\Seeders;

use App\Models\Color;
use App\Models\District;
use App\Models\DocumentType;
use App\Models\OrderStatus;
use App\Models\Province;
use App\Models\PurchaseType;
use App\Models\ShippingAgency;
use App\Models\Warehouse;
use Illuminate\Database\Seeder;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

class CatalogSeeder extends Seeder
{
    public function run(): void
    {
        $this->orderStatuses();
        $this->shippingAgencies();
        $this->documentTypes();
        $this->purchaseTypes();
        $this->unitMeasures();
        $this->provincesAndDistricts();
        $this->colors();
        $this->warehouses();
    }

    private function orderStatuses(): void
    {
        $items = [
            ['name' => 'Reservado',    'slug' => 'reservado',  'color' => '#6366f1', 'sort_order' => 0, 'is_protected' => false, 'is_active' => true],
            ['name' => 'Pending',      'slug' => 'pending',    'color' => '#f59e0b', 'sort_order' => 1, 'is_protected' => true,  'is_active' => true],
            ['name' => 'En proceso',   'slug' => 'en-proceso', 'color' => '#8b5cf6', 'sort_order' => 2, 'is_protected' => false, 'is_active' => true],
            ['name' => 'En camino',    'slug' => 'en-camino',  'color' => '#3b82f6', 'sort_order' => 3, 'is_protected' => false, 'is_active' => true],
            ['name' => 'Delivered',    'slug' => 'delivered',  'color' => '#10b981', 'sort_order' => 4, 'is_protected' => true,  'is_active' => true],
            ['name' => 'Devuelto',     'slug' => 'devuelto',   'color' => '#f97316', 'sort_order' => 6, 'is_protected' => false, 'is_active' => true],
            ['name' => 'Cancelled',    'slug' => 'cancelled',  'color' => '#ef4444', 'sort_order' => 7, 'is_protected' => true,  'is_active' => true],
        ];
        foreach ($items as $item) {
            $payload = [
                'name' => $item['name'],
                'color' => $item['color'],
                'sort_order' => $item['sort_order'],
                'is_active' => $item['is_active'] ?? true,
            ];

            if (Schema::hasColumn('order_statuses', 'is_protected')) {
                $payload['is_protected'] = $item['is_protected'];
            }

            OrderStatus::updateOrCreate(
                ['slug' => $item['slug']],
                $payload
            );
        }

        $pagadoId = OrderStatus::query()->where('slug', 'pagado')->value('id');
        $deliveredId = OrderStatus::query()->where('slug', 'delivered')->value('id');

        if ($pagadoId) {
            if ($deliveredId && Schema::hasTable('orders') && Schema::hasColumn('orders', 'order_status_id')) {
                DB::table('orders')
                    ->where('order_status_id', $pagadoId)
                    ->update([
                        'order_status_id' => $deliveredId,
                        'updated_at' => now(),
                    ]);
            }

            OrderStatus::query()->where('id', $pagadoId)->update([
                'is_active' => false,
                'is_protected' => false,
            ]);
        }
    }

    private function shippingAgencies(): void
    {
        $agencies = [
            'SHALOM'              => 'Shalom',
            'OLVA_COURIER'        => 'Olva Courier',
            'SERPOST'             => 'Serpost',
            'DINSIDES'            => 'Dinsides',
            'INKA_EXPRESS'        => 'Inka Express',
            'RANSA'               => 'Ransa',
            'DHL'                 => 'DHL Express',
            'FEDEX'               => 'FedEx',
            'UPS'                 => 'UPS',
            'CRUZ_DEL_SUR'        => 'Cruz del Sur Encomiendas',
            'CIVA'                => 'Civa Encomiendas',
            'TEPSA'               => 'Tepsa Encomiendas',
            'FLORES'              => 'Flores Hermanos',
            'AMERICA_EXPRESS'     => 'América Express',
            'JET_PERU'            => 'Jet Perú',
            'GLS'                 => 'GLS Perú',
            'URBANO'              => 'Urbano Express',
            '99MINUTOS'           => '99 Minutos',
            'RAPPI'               => 'Rappi',
            'CHAZKI'              => 'Chazki',
        ];
        foreach ($agencies as $code => $name) {
            ShippingAgency::firstOrCreate(['code' => $code], ['name' => $name]);
        }
    }

    private function documentTypes(): void
    {
        $items = [
            ['code' => 'BOLETA', 'name' => 'Boleta de Venta'],
            ['code' => 'FACTURA', 'name' => 'Factura'],
            ['code' => 'TICKET', 'name' => 'Ticket / Nota de venta'],
            ['code' => 'NOTA_CRED', 'name' => 'Nota de Crédito'],
            ['code' => 'NOTA_VENTA', 'name' => 'Nota de Venta'],
            ['code' => 'GUIA_REMISION', 'name' => 'Guía de Remisión'],
        ];

        foreach ($items as $item) {
            $payload = [
                'name' => $item['name'],
                'is_active' => true,
            ];

            if (Schema::hasColumn('document_types', 'is_protected')) {
                $payload['is_protected'] = true;
            }

            DocumentType::updateOrCreate(
                ['code' => $item['code']],
                $payload
            );
        }
    }

    private function purchaseTypes(): void
    {
        $types = ['CANCELÓ CLIENTE', 'COMPROBADO', 'CONTRAENTREGA', 'PREVENTA', 'CONSIGNACIÓN'];
        foreach ($types as $name) {
            PurchaseType::firstOrCreate(
                ['slug' => \Illuminate\Support\Str::slug($name)],
                ['name' => $name]
            );
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  DEPARTAMENTOS Y DISTRITOS DEL PERÚ (25 departamentos)
    // ─────────────────────────────────────────────────────────────
    private function provincesAndDistricts(): void
    {
        $data = [
            'Amazonas' => [
                'Chachapoyas','Bagua Grande','Bagua','Lamud','Luya','Camporredondo','Leimebamba',
                'La Jalca','Lonya Grande','Mendoza','Molinopampa','Rodríguez de Mendoza',
                'San Nicolás','Tingo','Vista Alegre',
            ],
            'Áncash' => [
                'Huaraz','Chimbote','Nuevo Chimbote','Casma','Huari','Recuay','Caraz','Yungay',
                'Carhuaz','Corongo','Pomabamba','Sihuas','Ocros','Aija','Bolognesi',
                'Antonio Raymondi','Asunción','Carlos Fermín Fitzcarrald','Huarmey','Lacabamba',
                'Llanganuco','Mariscal Luzuriaga','Pallasca','Santa','Independencia',
            ],
            'Apurímac' => [
                'Abancay','Andahuaylas','Antabamba','Aymaraes','Cotabambas','Chincheros',
                'Grau','Chuquibambilla','Challhuahuacho','Haquira','Tambobamba','Progreso',
                'Pampachiri','Pacucha','Talavera','San Jerónimo de Tunan',
            ],
            'Arequipa' => [
                'Arequipa','Cayma','Cerro Colorado','Socabaya','José Luis Bustamante y Rivero',
                'Mariano Melgar','Miraflores','Paucarpata','Sachaca','Alto Selva Alegre',
                'Hunter','Jacobo Hunter','La Joya','Tiabaya','Uchumayo','Yanahuara',
                'Camaná','Caravelí','Castilla','Caylloma','Chivay','Condesuyos','Islay',
                'La Unión','Mollendo','Mejía','Pedregal',
            ],
            'Ayacucho' => [
                'Huamanga','Huanta','San Miguel','La Mar','Lucanas','Parinacochas',
                'Páucar del Sara Sara','Sucre','Víctor Fajardo','Vilcas Huamán',
                'Cangallo','Huancasancos','Jesús Nazareno','Acos Vinchos','Quinua',
                'San Juan Bautista','Carmen Alto','Andrés Avelino Cáceres Dorregaray',
            ],
            'Cajamarca' => [
                'Cajamarca','Cajabamba','Celendín','Chota','Contumazá','Cutervo',
                'Hualgayoc','Jaén','San Ignacio','San Marcos','San Miguel','San Pablo',
                'Santa Cruz','Baños del Inca','Jesús','La Asunción','Llacanora',
                'Los Baños del Inca','Magdalena','Namora',
            ],
            'Callao' => [
                'Callao','Bellavista','Carmen de La Legua Reynoso','La Perla','La Punta',
                'Mi Perú','Ventanilla',
            ],
            'Cusco' => [
                'Cusco','Wanchaq','Santiago','San Sebastián','San Jerónimo','Saylla',
                'Acomayo','Anta','Calca','Canas','Canchis','Chumbivilcas','Espinar',
                'La Convención','Paruro','Paucartambo','Quispicanchi','Urubamba',
                'Sicuani','Quillabamba','Pisac','Ollantaytambo','Machu Picchu',
            ],
            'Huancavelica' => [
                'Huancavelica','Acobamba','Angaraes','Castrovirreyna','Churcampa',
                'Huaytará','Tayacaja','Pampas','Lircay','Ascensión','Izcuchaca',
                'Congalla','Colcabamba',
            ],
            'Huánuco' => [
                'Huánuco','Amarilis','Pillco Marca','Ambo','Dos de Mayo','Huacaybamba',
                'Huamalíes','Leoncio Prado','Marañón','Pachitea','Puerto Inca',
                'Lauricocha','Tingo María','La Unión','Huacrachuco','Llata',
            ],
            'Ica' => [
                'Ica','Chincha Alta','Pisco','Nasca','Palpa','Subtanjalla','La Tinguiña',
                'Los Aquijes','Ocucaje','Pachacútec','Parcona','Pueblo Nuevo',
                'San Juan Bautista','Santiago','Salas','Tate','Yauca del Rosario',
            ],
            'Junín' => [
                'Huancayo','El Tambo','Chilca','Concepción','Chanchamayo','Jauja',
                'Junín','Satipo','Tarma','Yauli','La Oroya','La Merced','Mazamari',
                'Pichanaqui','San Ramón','Chupaca','Junín','Carhuacallanga',
            ],
            'La Libertad' => [
                'Trujillo','Víctor Larco Herrera','El Porvenir','Florencia de Mora',
                'Huanchaco','La Esperanza','Laredo','Moche','Poroto','Salaverry',
                'Simbal','Ascope','Bolívar','Chepén','Gran Chimú','Julcán',
                'Otuzco','Pacasmayo','Pataz','Sánchez Carrión','Santiago de Chuco',
                'Virú','Pacanguilla','Paiján',
            ],
            'Lambayeque' => [
                'Chiclayo','José Leonardo Ortiz','La Victoria','Pimentel','Reque',
                'Santa Rosa','Monsefú','Eten','Puerto Eten','Tumán','Pomalca',
                'Cayaltí','Zaña','Ferreñafe','Lambayeque','Illimo','Jayanca',
                'Mochumí','Motupe','Olmos','Pacora','Salas','San José','Túcume',
            ],
            'Lima' => [
                'Lima Cercado','Miraflores','San Isidro','Surquillo','Barranco',
                'Chorrillos','San Borja','Santiago de Surco','La Molina','Ate',
                'Santa Anita','El Agustino','San Luis','Breña','Jesús María',
                'Lince','Magdalena del Mar','Pueblo Libre','San Miguel',
                'La Victoria','Rímac','San Martín de Porres','Los Olivos',
                'Independencia','Comas','Carabayllo','Puente Piedra','Ancón',
                'Santa Rosa','Callao','Ventanilla','Mi Perú','Bellavista',
                'La Perla','Lurigancho-Chosica','San Juan de Lurigancho',
                'San Juan de Miraflores','Villa María del Triunfo','Villa El Salvador',
                'Pachacamac','Lurín','Punta Hermosa','Punta Negra','Santa María del Mar',
                'Pucusana','San Bartolo','Cieneguilla','Chaclacayo',
                'Huaral','Huacho','Barranca','Cañete','San Vicente de Cañete',
                'Huarochirí','Matucana','Yauyos','Oyon','Sayán',
            ],
            'Loreto' => [
                'Iquitos','Punchana','Belén','San Juan Bautista','Nauta',
                'Requena','Yurimaguas','Caballococha','Contamana','Andoas',
                'Caballo Cocha','Indiana','Barranca','Lagunas','Orellana',
            ],
            'Madre de Dios' => [
                'Puerto Maldonado','Tambopata','Inambari','Las Piedras','Laberinto',
                'Manu','Fitzcarrald','Huepetuhe','Iberia','Iñapari','Tahuamanu',
            ],
            'Moquegua' => [
                'Moquegua','Ilo','Omate','Coalaque','Carumas','Cuchumbaya',
                'Puquina','Samegua','Torata','El Algarrobal','Pacocha','San Cristóbal',
            ],
            'Pasco' => [
                'Cerro de Pasco','Chaupimarca','Simon Bolívar','Tinyahuarco',
                'Yanacancha','Oxapampa','Pozuzo','Chontabamba','Huancabamba',
                'Puerto Bermúdez','Constitución','Villa Rica',
            ],
            'Piura' => [
                'Piura','Castilla','Catacaos','Cura Mori','El Tallán','La Arena',
                'La Unión','Tambogrande','Veintiséis de Octubre','Ayabaca',
                'Huancabamba','Morropón','Sullana','Salitral','Querecotillo',
                'Lancones','Marcavelica','Paita','Talara','Máncora','Los Órganos',
                'Lobitos','Sechura','Chulucanas','Sondor','Huarmaca',
            ],
            'Puno' => [
                'Puno','Juliaca','Ilave','Azángaro','Carabaya','Chucuito',
                'El Collao','Huancané','Lampa','Melgar','Moho','San Antonio de Putina',
                'San Román','Sandia','Yunguyo','Ayaviri','Macusani','Desaguadero',
                'Juli','Acora','Capachica','Coata',
            ],
            'San Martín' => [
                'Tarapoto','Moyobamba','Juanjuí','Picota','Bellavista',
                'El Dorado','Huallaga','Lamas','Mariscal Cáceres','Rioja',
                'San Martín','Tocache','Banda de Shilcayo','Morales',
                'Nueva Cajamarca','Soritor','Saposoa',
            ],
            'Tacna' => [
                'Tacna','Alto de la Alianza','Ciudad Nueva','Inclán','Pocollay',
                'Sama','Coronel Gregorio Albarracín Lanchipa','Candarave',
                'Jorge Basadre','Torata','Pachia','Palca','Tarata',
            ],
            'Tumbes' => [
                'Tumbes','Corrales','La Cruz','Pampas de Hospital','San Jacinto',
                'San Juan de la Virgen','Zarumilla','Aguas Verdes','Matapalo',
                'Papayal','Zorritos','Caleta La Cruz',
            ],
            'Ucayali' => [
                'Pucallpa','Manantay','Yarinacocha','Campo Verde','Iparía',
                'Masisea','Nueva Requena','Curimaná','Aguaytía','Padre Abad',
                'Atalaya','Raymondi','Sepahua','Coronel Portillo',
            ],
        ];

        foreach ($data as $deptName => $districts) {
            $rawDeptCode = strtoupper(preg_replace('/\s+/', '', \Illuminate\Support\Str::ascii($deptName)));
            $code = substr($rawDeptCode, 0, 10);
            $province = Province::firstOrCreate(
                ['code' => $code],
                ['name' => $deptName, 'is_active' => true]
            );
            foreach ($districts as $districtName) {
                // Código: máx 20 chars, sin espacios
                $rawCode = strtoupper(preg_replace('/\s+/', '', \Illuminate\Support\Str::ascii($districtName)));
                $distCode = substr($province->id . '_' . $rawCode, 0, 20);
                District::firstOrCreate(
                    ['province_id' => $province->id, 'code' => $distCode],
                    ['name' => $districtName, 'is_active' => true]
                );
            }
        }
    }

    private function colors(): void
    {
        $items = [
            // Neutros
            ['name' => 'Negro',           'hex_code' => '#000000', 'slug' => 'negro'],
            ['name' => 'Blanco',          'hex_code' => '#ffffff', 'slug' => 'blanco'],
            ['name' => 'Gris claro',      'hex_code' => '#d1d5db', 'slug' => 'gris-claro'],
            ['name' => 'Gris',            'hex_code' => '#6b7280', 'slug' => 'gris'],
            ['name' => 'Gris oscuro',     'hex_code' => '#374151', 'slug' => 'gris-oscuro'],
            ['name' => 'Charcoal',        'hex_code' => '#36454f', 'slug' => 'charcoal'],
            ['name' => 'Hielo',           'hex_code' => '#e0f7fa', 'slug' => 'hielo'],
            ['name' => 'Hueso',           'hex_code' => '#f5f0e8', 'slug' => 'hueso'],
            ['name' => 'Crema',           'hex_code' => '#fffdd0', 'slug' => 'crema'],
            ['name' => 'Beige',           'hex_code' => '#f5f5dc', 'slug' => 'beige'],
            // Cálidos
            ['name' => 'Rojo',            'hex_code' => '#ef4444', 'slug' => 'rojo'],
            ['name' => 'Rojo oscuro',     'hex_code' => '#991b1b', 'slug' => 'rojo-oscuro'],
            ['name' => 'Burdeos',         'hex_code' => '#800020', 'slug' => 'burdeos'],
            ['name' => 'Naranja',         'hex_code' => '#f97316', 'slug' => 'naranja'],
            ['name' => 'Naranja quemado', 'hex_code' => '#c2410c', 'slug' => 'naranja-quemado'],
            ['name' => 'Amarillo',        'hex_code' => '#fde047', 'slug' => 'amarillo'],
            ['name' => 'Mostaza',         'hex_code' => '#ca8a04', 'slug' => 'mostaza'],
            ['name' => 'Camel',           'hex_code' => '#c19a6b', 'slug' => 'camel'],
            ['name' => 'Marrón',          'hex_code' => '#92400e', 'slug' => 'marron'],
            ['name' => 'Chocolate',       'hex_code' => '#5c3317', 'slug' => 'chocolate'],
            ['name' => 'Terracota',       'hex_code' => '#c0715e', 'slug' => 'terracota'],
            ['name' => 'Salmón',          'hex_code' => '#fa8072', 'slug' => 'salmon'],
            ['name' => 'Coral',           'hex_code' => '#ff6b6b', 'slug' => 'coral'],
            ['name' => 'Rosa',            'hex_code' => '#f9a8d4', 'slug' => 'rosa'],
            ['name' => 'Rosa chicle',     'hex_code' => '#ec4899', 'slug' => 'rosa-chicle'],
            ['name' => 'Fucsia',          'hex_code' => '#d946ef', 'slug' => 'fucsia'],
            // Fríos
            ['name' => 'Azul claro',      'hex_code' => '#93c5fd', 'slug' => 'azul-claro'],
            ['name' => 'Azul',            'hex_code' => '#3b82f6', 'slug' => 'azul'],
            ['name' => 'Azul marino',     'hex_code' => '#1e3a5f', 'slug' => 'azul-marino'],
            ['name' => 'Azul rey',        'hex_code' => '#2563eb', 'slug' => 'azul-rey'],
            ['name' => 'Celeste',         'hex_code' => '#7dd3fc', 'slug' => 'celeste'],
            ['name' => 'Turquesa',        'hex_code' => '#14b8a6', 'slug' => 'turquesa'],
            ['name' => 'Verde menta',     'hex_code' => '#a7f3d0', 'slug' => 'verde-menta'],
            ['name' => 'Verde claro',     'hex_code' => '#86efac', 'slug' => 'verde-claro'],
            ['name' => 'Verde',           'hex_code' => '#22c55e', 'slug' => 'verde'],
            ['name' => 'Verde botella',   'hex_code' => '#15803d', 'slug' => 'verde-botella'],
            ['name' => 'Verde militar',   'hex_code' => '#4b5320', 'slug' => 'verde-militar'],
            ['name' => 'Verde olivo',     'hex_code' => '#808000', 'slug' => 'verde-olivo'],
            // Violeta / especiales
            ['name' => 'Lavanda',         'hex_code' => '#c4b5fd', 'slug' => 'lavanda'],
            ['name' => 'Lila',            'hex_code' => '#a78bfa', 'slug' => 'lila'],
            ['name' => 'Morado',          'hex_code' => '#7c3aed', 'slug' => 'morado'],
            ['name' => 'Violeta',         'hex_code' => '#6d28d9', 'slug' => 'violeta'],
            ['name' => 'Índigo',          'hex_code' => '#4338ca', 'slug' => 'indigo'],
            ['name' => 'Plomo',           'hex_code' => '#9ca3af', 'slug' => 'plomo'],
            ['name' => 'Kaki',            'hex_code' => '#c0a882', 'slug' => 'kaki'],
            ['name' => 'Dorado',          'hex_code' => '#d4af37', 'slug' => 'dorado'],
            ['name' => 'Plateado',        'hex_code' => '#c0c0c0', 'slug' => 'plateado'],
        ];
        foreach ($items as $item) {
            Color::firstOrCreate(['slug' => $item['slug']], $item);
        }
    }

    private function warehouses(): void
    {
        $items = [
            ['name' => 'Tienda Física',    'code' => 'TIENDA_FISICA', 'type' => 'store',     'city' => 'Lima', 'is_active' => true, 'is_pos' => true],
        ];
        foreach ($items as $item) {
            Warehouse::firstOrCreate(['code' => $item['code']], $item);
        }
    }

    private function unitMeasures(): void
    {
        if (!Schema::hasTable('unit_measures')) {
            return;
        }

        $items = [
            ['name' => 'Unidad',   'code' => 'NIU', 'sunat_code' => 'NIU'],
            ['name' => 'Kilogramo','code' => 'KGM', 'sunat_code' => 'KGM'],
            ['name' => 'Litro',    'code' => 'LTR', 'sunat_code' => 'LTR'],
            ['name' => 'Metro',    'code' => 'MTR', 'sunat_code' => 'MTR'],
            ['name' => 'Caja',     'code' => 'BX',  'sunat_code' => 'BX'],
            ['name' => 'Par',      'code' => 'PR',  'sunat_code' => 'PR'],
        ];

        foreach ($items as $item) {
            \App\Models\UnitMeasure::updateOrCreate(
                ['code' => $item['code']],
                [
                    'name' => $item['name'],
                    'sunat_code' => $item['sunat_code'],
                    'is_active' => true,
                ]
            );
        }
    }
}
