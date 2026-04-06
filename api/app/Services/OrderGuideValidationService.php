<?php

namespace App\Services;

use App\Models\DocumentType;
use App\Models\Order;
use Illuminate\Validation\ValidationException;

class OrderGuideValidationService
{
    public function validate(array $data, ?Order $order = null): void
    {
        $documentTypeId = $this->payloadValue($data, $order, 'document_type_id');
        if (!$this->isGuideDocumentType($documentTypeId)) {
            return;
        }

        $errors = [];

        $baseRequiredFields = [
            'guide_transfer_reason_code' => 'Selecciona el motivo de traslado para la guía de remisión.',
            'guide_transfer_mode' => 'Selecciona la modalidad de traslado para la guía de remisión.',
            'guide_transfer_date' => 'La fecha de traslado es obligatoria para la guía de remisión.',
            'guide_total_weight' => 'El peso total es obligatorio para la guía de remisión.',
            'guide_origin_ubigeo' => 'El ubigeo de partida es obligatorio para la guía de remisión.',
            'guide_origin_address' => 'La dirección de partida es obligatoria para la guía de remisión.',
            'guide_destination_ubigeo' => 'El ubigeo de llegada es obligatorio para la guía de remisión.',
            'guide_destination_address' => 'La dirección de llegada es obligatoria para la guía de remisión.',
            'guide_recipient_doc_type' => 'El tipo de documento del destinatario es obligatorio para la guía de remisión.',
            'guide_recipient_doc_number' => 'El número de documento del destinatario es obligatorio para la guía de remisión.',
            'guide_recipient_name' => 'El nombre/razón social del destinatario es obligatorio para la guía de remisión.',
        ];

        $this->appendMissingFields($errors, $baseRequiredFields, $data, $order);

        $transferMode = (string) ($this->payloadValue($data, $order, 'guide_transfer_mode') ?? '');
        $modeRequiredFields = $this->guideTransportModeRequiredFields($transferMode);
        $this->appendMissingFields($errors, $modeRequiredFields, $data, $order);

        if (!empty($errors)) {
            throw ValidationException::withMessages($errors);
        }
    }

    private function guideTransportModeRequiredFields(string $transferMode): array
    {
        if ($transferMode === '01') {
            return [
                'guide_carrier_doc_type' => 'Para traslado público, el tipo de documento del transportista es obligatorio.',
                'guide_carrier_doc_number' => 'Para traslado público, el número de documento del transportista es obligatorio.',
                'guide_carrier_name' => 'Para traslado público, la razón social del transportista es obligatoria.',
            ];
        }

        if ($transferMode === '02') {
            return [
                'guide_vehicle_plate' => 'Para traslado privado, la placa del vehículo es obligatoria.',
                'guide_driver_doc_type' => 'Para traslado privado, el tipo de documento del conductor es obligatorio.',
                'guide_driver_doc_number' => 'Para traslado privado, el documento del conductor es obligatorio.',
            ];
        }

        return [];
    }

    private function appendMissingFields(array &$errors, array $requiredFields, array $data, ?Order $order): void
    {
        foreach ($requiredFields as $field => $message) {
            $value = $this->payloadValue($data, $order, $field);
            if ($value === null || $value === '') {
                $errors[$field] = $message;
            }
        }
    }

    private function payloadValue(array $data, ?Order $order, string $field): mixed
    {
        if (array_key_exists($field, $data)) {
            return $data[$field];
        }

        return $order?->{$field};
    }

    private function isGuideDocumentType(mixed $documentTypeId): bool
    {
        if (!$documentTypeId) {
            return false;
        }

        $code = DocumentType::query()->where('id', (int) $documentTypeId)->value('code');

        return mb_strtoupper((string) $code) === 'GUIA_REMISION';
    }
}
