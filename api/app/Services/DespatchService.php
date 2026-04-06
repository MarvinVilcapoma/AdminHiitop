<?php

namespace App\Services;

use App\Exceptions\DespatchException;
use App\Models\DocumentType;
use App\Models\Order;
use App\Models\Setting;
use DateTime;
use Greenter\Model\Client\Client;
use Greenter\Model\Company\Address;
use Greenter\Model\Company\Company;
use Greenter\Model\Despatch\Despatch;
use Greenter\Model\Despatch\DespatchDetail;
use Greenter\Model\Despatch\Direction;
use Greenter\Model\Despatch\Shipment;
use Greenter\Model\Despatch\Transportist;
use Greenter\See;
use Greenter\Ws\Services\SunatEndpoints;

class DespatchService
{
    private ?array $settings = null;

    private function getSettings(): array
    {
        if ($this->settings !== null) {
            return $this->settings;
        }

        $this->settings = Setting::whereIn('key', [
            'sunat_environment',
            'sunat_beta_ruc', 'sunat_beta_razon_social', 'sunat_beta_nombre_comercial',
            'sunat_beta_ubigueo', 'sunat_beta_departamento', 'sunat_beta_provincia',
            'sunat_beta_distrito', 'sunat_beta_urbanizacion', 'sunat_beta_direccion',
            'sunat_beta_codigo_local', 'sunat_beta_sol_user', 'sunat_beta_sol_pass',
            'sunat_beta_certificate_pem',
            'sunat_prod_ruc', 'sunat_prod_razon_social', 'sunat_prod_nombre_comercial',
            'sunat_prod_ubigueo', 'sunat_prod_departamento', 'sunat_prod_provincia',
            'sunat_prod_distrito', 'sunat_prod_urbanizacion', 'sunat_prod_direccion',
            'sunat_prod_codigo_local', 'sunat_prod_sol_user', 'sunat_prod_sol_pass',
            'sunat_prod_certificate_pem',
            'sunat_ruc', 'sunat_razon_social', 'sunat_nombre_comercial',
            'sunat_ubigueo', 'sunat_departamento', 'sunat_provincia',
            'sunat_distrito', 'sunat_urbanizacion', 'sunat_direccion', 'sunat_codigo_local',
            'sunat_sol_user', 'sunat_sol_pass', 'sunat_certificate_pem',
            'guide_series_default',
        ])->pluck('value', 'key')->toArray();

        return $this->settings;
    }

    public function defaultSeries(): string
    {
        $s = $this->getSettings();
        $series = trim((string) ($s['guide_series_default'] ?? 'T001'));

        return $series !== '' ? $series : 'T001';
    }

    private function setting(string $suffix): string
    {
        $s = $this->getSettings();
        $env = $s['sunat_environment'] ?? 'beta';
        $prefix = ($env === 'produccion') ? 'sunat_prod_' : 'sunat_beta_';

        return (string) ($s["{$prefix}{$suffix}"] ?? $s["sunat_{$suffix}"] ?? '');
    }

    private function buildSee(): See
    {
        $certPem = $this->setting('certificate_pem');
        if (trim($certPem) === '') {
            throw new DespatchException('No hay certificado digital configurado para emitir guías de remisión.');
        }

        $s = $this->getSettings();
        $env = $s['sunat_environment'] ?? 'beta';

        $see = new See();
        $see->setCertificate($certPem);
        $see->setService($env === 'produccion' ? SunatEndpoints::GUIA_PRODUCCION : SunatEndpoints::GUIA_BETA);
        $see->setClaveSOL(
            $this->setting('ruc'),
            $this->setting('sol_user'),
            $this->setting('sol_pass')
        );

        return $see;
    }

    private function buildCompany(): Company
    {
        $address = (new Address())
            ->setUbigueo($this->setting('ubigueo') ?: '150101')
            ->setDepartamento($this->setting('departamento') ?: 'LIMA')
            ->setProvincia($this->setting('provincia') ?: 'LIMA')
            ->setDistrito($this->setting('distrito') ?: 'LIMA')
            ->setUrbanizacion($this->setting('urbanizacion') ?: '-')
            ->setDireccion($this->setting('direccion') ?: '-')
            ->setCodLocal($this->setting('codigo_local') ?: '0000');

        return (new Company())
            ->setRuc($this->setting('ruc'))
            ->setRazonSocial($this->setting('razon_social'))
            ->setNombreComercial($this->setting('nombre_comercial'))
            ->setAddress($address);
    }

    private function recipientFromOrder(Order $order): Client
    {
        $docType = (string) ($order->guide_recipient_doc_type ?: '-');
        if ($docType === '-' && $order->dni) {
            $docType = '1';
        }

        $docNumber = (string) ($order->guide_recipient_doc_number ?: '-');
        if ($docNumber === '-' && $order->dni) {
            $docNumber = (string) $order->dni;
        }

        $name = (string) ($order->guide_recipient_name ?: '');
        if ($name === '') {
            $name = (string) ($order->customer_name ?: 'CLIENTE VARIOS');
        }

        return (new Client())
            ->setTipoDoc($docType)
            ->setNumDoc($docNumber)
            ->setRznSocial($name);
    }

    private function transferReasonDescription(string $code): string
    {
        return match ($code) {
            '01' => 'VENTA',
            '02' => 'COMPRA',
            '04' => 'TRASLADO ENTRE ESTABLECIMIENTOS',
            '08' => 'IMPORTACIÓN',
            '09' => 'EXPORTACIÓN',
            default => 'OTROS',
        };
    }

    private function buildShipment(Order $order): Shipment
    {
        $transferCode = (string) ($order->guide_transfer_reason_code ?: '01');
        $transferMode = (string) ($order->guide_transfer_mode ?: '02');
        $transferDate = $order->guide_transfer_date
            ? new DateTime($order->guide_transfer_date->format('Y-m-d'))
            : new DateTime();

        $shipment = (new Shipment())
            ->setCodTraslado($transferCode)
            ->setDesTraslado((string) ($order->guide_transfer_reason_description ?: $this->transferReasonDescription($transferCode)))
            ->setIndTransbordo(false)
            ->setPesoTotal((float) ($order->guide_total_weight ?? 0))
            ->setUndPesoTotal((string) ($order->guide_weight_unit ?: 'KGM'))
            ->setNumBultos($order->guide_package_count ? (int) $order->guide_package_count : null)
            ->setModTraslado($transferMode)
            ->setFecTraslado($transferDate)
            ->setPartida(new Direction((string) $order->guide_origin_ubigeo, (string) $order->guide_origin_address))
            ->setLlegada(new Direction((string) $order->guide_destination_ubigeo, (string) $order->guide_destination_address));

        $transportist = $this->buildTransportist($order, $transferMode);
        if ($transportist) {
            $shipment->setTransportista($transportist);
        }

        return $shipment;
    }

    private function buildTransportist(Order $order, string $transferMode): ?Transportist
    {
        [$docType, $docNumber, $name] = $this->transportistIdentity($order, $transferMode);

        $hasTransportistData = $docType !== ''
            || $docNumber !== ''
            || $name !== ''
            || !empty($order->guide_vehicle_plate)
            || !empty($order->guide_driver_doc_number);

        if (!$hasTransportistData) {
            return null;
        }

        return (new Transportist())
            ->setTipoDoc($this->nullableString($docType))
            ->setNumDoc($this->nullableString($docNumber))
            ->setRznSocial($this->nullableString($name))
            ->setPlaca($this->nullableString($order->guide_vehicle_plate))
            ->setChoferTipoDoc($this->nullableString($order->guide_driver_doc_type))
            ->setChoferDoc($this->nullableString($order->guide_driver_doc_number));
    }

    private function transportistIdentity(Order $order, string $transferMode): array
    {
        if ($transferMode === '01') {
            return [
                (string) ($order->guide_carrier_doc_type ?: ''),
                (string) ($order->guide_carrier_doc_number ?: ''),
                (string) ($order->guide_carrier_name ?: ''),
            ];
        }

        return [
            '6',
            (string) $this->setting('ruc'),
            (string) $this->setting('razon_social'),
        ];
    }

    private function nullableString(mixed $value): ?string
    {
        $text = trim((string) ($value ?? ''));

        return $text === '' ? null : $text;
    }

    private function buildDetails(Order $order): array
    {
        $details = [];

        foreach ($order->items as $item) {
            $sku = optional($item->product)->sku ?: ('ITEM' . $item->id);
            $description = $item->product_description
                ?: optional($item->product)->name
                ?: 'Producto';

            $details[] = (new DespatchDetail())
                ->setCodigo((string) $sku)
                ->setDescripcion((string) $description)
                ->setUnidad('NIU')
                ->setCantidad((float) ($item->quantity ?? 0));
        }

        return $details;
    }

    public function buildDocument(Order $order): Despatch
    {
        if (!$order->guide_series || !$order->guide_correlativo) {
            throw new DespatchException('No se encontró serie/correlativo para la guía de remisión.');
        }

        return (new Despatch())
            ->setTipoDoc('09')
            ->setSerie((string) $order->guide_series)
            ->setCorrelativo((string) $order->guide_correlativo)
            ->setFechaEmision(new DateTime())
            ->setCompany($this->buildCompany())
            ->setDestinatario($this->recipientFromOrder($order))
            ->setEnvio($this->buildShipment($order))
            ->setObservacion($order->observations ?: null)
            ->setDetails($this->buildDetails($order));
    }

    public function sendToSunat(Order $order): array
    {
        $order->loadMissing(['items.product', 'documentType']);

        if (!$order->document_type_id) {
            throw new DespatchException('El pedido no tiene tipo de documento asignado.');
        }

        $docCode = DocumentType::query()->where('id', $order->document_type_id)->value('code');
        if (mb_strtoupper((string) $docCode) !== 'GUIA_REMISION') {
            throw new DespatchException('Este pedido no está configurado como Guía de Remisión.');
        }

        $see = $this->buildSee();
        $doc = $this->buildDocument($order);

        $result = $see->send($doc);
        $xml = $see->getFactory()->getLastXml();

        if (!$result->isSuccess()) {
            $err = $result->getError();
            $code = $err?->getCode() ?? 'CONN';
            $msg = $err?->getMessage() ?? 'Error de conexión con SUNAT para guía de remisión';

            $order->update([
                'guide_status' => 'error',
                'guide_sunat_code' => is_numeric($code) ? (int) $code : null,
                'guide_sunat_description' => "[{$code}] {$msg}",
                'guide_xml_content' => $xml ?? null,
            ]);

            return [
                'success' => false,
                'status' => 'error',
                'error' => "[{$code}] {$msg}",
            ];
        }

        $cdrZip = $result->getCdrZip();
        $cdr = $result->getCdrResponse();
        $code = (int) $cdr->getCode();

        $status = match (true) {
            $code === 0 => 'accepted',
            $code >= 2000 && $code <= 3999 => 'rejected',
            default => 'exception',
        };

        $order->update([
            'guide_status' => $status,
            'guide_sunat_code' => $code,
            'guide_sunat_description' => $cdr->getDescription(),
            'guide_xml_content' => $xml ?? null,
            'guide_cdr_content' => base64_encode((string) $cdrZip),
            'guide_sent_at' => now(),
        ]);

        return [
            'success' => $code === 0,
            'status' => $status,
            'code' => $code,
            'description' => $cdr->getDescription(),
            'notes' => $cdr->getNotes(),
        ];
    }
}
