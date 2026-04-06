<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Setting;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

class SettingController extends Controller
{
    /** GET /settings  – returns all settings as { key: { value, label, type, group } } */
    public function index(): JsonResponse
    {
        $settings = Setting::all()->keyBy('key')->map(fn ($s) => [
            'value'  => $s->casted_value,
            'raw'    => $s->value,
            'label'  => $s->label,
            'type'   => $s->type,
            'group'  => $s->group,
        ]);
        return response()->json($settings);
    }

    /** PATCH /settings  – bulk update { key => value } */
    public function update(Request $request): JsonResponse
    {
        $data = $request->validate([
            'settings'        => 'required|array',
            'settings.*.key'  => 'required|string|exists:settings,key',
            'settings.*.value'=> 'nullable',
        ]);

        foreach ($data['settings'] as $item) {
            Setting::where('key', $item['key'])->update(['value' => $item['value']]);
        }

        return $this->index();
    }

    /** PUT /settings/{key}  – update a single setting */
    public function updateKey(Request $request, string $key): JsonResponse
    {
        $setting = Setting::findOrFail($key);
        $request->validate(['value' => 'nullable']);
        $setting->update(['value' => $request->value]);
        return response()->json([
            'value' => $setting->fresh()->casted_value,
            'raw'   => $request->value,
        ]);
    }
}
