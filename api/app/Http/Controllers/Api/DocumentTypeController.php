<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\DocumentType;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Schema;
use Illuminate\Validation\ValidationException;

class DocumentTypeController extends Controller
{
    private const PROTECTED_CODES = ['BOLETA', 'FACTURA', 'TICKET', 'NOTA_CRED', 'NOTA_VENTA', 'GUIA_REMISION'];

    public function index(Request $request): JsonResponse
    {
        $query = DocumentType::query()->orderBy('name');

        if ($request->filled('search')) {
            $search = trim((string) $request->search);
            $query->where(function ($q) use ($search) {
                $q->where('name', 'like', '%' . $search . '%')
                    ->orWhere('code', 'like', '%' . $search . '%');
            });
        }

        if ($request->boolean('active_only')) {
            $query->where('is_active', true);
        }
        $items = $request->has('per_page')
            ? $query->paginate((int) $request->get('per_page', 15))
            : $query->get();
        return response()->json($items);
    }

    public function store(Request $request): JsonResponse
    {
        $data = $request->validate([
            'name' => 'required|string|max:255',
            'code' => 'required|string|max:50|unique:document_types,code',
            'is_active' => 'nullable|boolean',
        ]);

        $data['code'] = mb_strtoupper(trim((string) $data['code']));

        if (in_array($data['code'], self::PROTECTED_CODES, true) && Schema::hasColumn('document_types', 'is_protected')) {
            $data['is_protected'] = true;
        }

        $item = DocumentType::create($data);
        return response()->json($item, 201);
    }

    public function show(DocumentType $documentType): JsonResponse
    {
        return response()->json($documentType);
    }

    public function update(Request $request, DocumentType $documentType): JsonResponse
    {
        if ($this->isProtectedDocumentType($documentType)) {
            throw ValidationException::withMessages([
                'document_type' => 'Este tipo de comprobante está protegido y no se puede editar.',
            ]);
        }

        $data = $request->validate([
            'name' => 'sometimes|string|max:255',
            'code' => 'sometimes|string|max:50|unique:document_types,code,' . $documentType->id,
            'is_active' => 'nullable|boolean',
        ]);

        if (array_key_exists('code', $data)) {
            $data['code'] = mb_strtoupper(trim((string) $data['code']));
        }

        $documentType->update($data);
        return response()->json($documentType);
    }

    public function destroy(DocumentType $documentType): JsonResponse
    {
        if ($this->isProtectedDocumentType($documentType)) {
            throw ValidationException::withMessages([
                'document_type' => 'Este tipo de comprobante está protegido y no se puede eliminar.',
            ]);
        }

        if ($documentType->orders()->exists()) {
            throw ValidationException::withMessages([
                'document_type' => 'No se puede eliminar un tipo de comprobante que ya está en uso.',
            ]);
        }

        $documentType->delete();
        return response()->json(null, 204);
    }

    private function isProtectedDocumentType(DocumentType $documentType): bool
    {
        if (Schema::hasColumn('document_types', 'is_protected') && (bool) $documentType->is_protected) {
            return true;
        }

        return in_array(mb_strtoupper(trim((string) $documentType->code)), self::PROTECTED_CODES, true);
    }
}
