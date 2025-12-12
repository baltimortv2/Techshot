---
inclusion: always
---
# Techshot Project Memory
Здесь агент записывает важные архитектурные решения и исключения из правил.

## Конфигурация проекта
- **Unity версия**: 6000.4.0a5 (Unity 6.4 Alpha)
- **Render Pipeline**: URP 17.4.0
- **Rendering Mode**: Deferred (m_RenderingMode: 3)
- **Graphics API**: Direct3D12 с поддержкой Inline Ray Tracing

## Ключевые файлы
- **URP Renderer**: `Assets/TechShot/Settings/Ultra-high PC.asset`
- **URP Pipeline Asset**: `Assets/TechShot/Settings/PC_RPAsset.asset`
- **Volume Profile**: `Assets/TechShot/Settings/TechShotVolume.asset`
- **Главная сцена**: `Assets/TechShot/Scenes/SampleScene.unity`

## Установленные Renderer Features
1. **RayTracedShadowPass** - Ray Traced Shadows
   - Путь: `Assets/RayTracingShadows/CustomPass/RayTracedShadowPass.cs`
   - Статус: ✅ Работает
   - RenderPassEvent: AfterRenderingDeferredLights
   - **Настройки**: Все настройки через Volume (`RayTracedShadowsVolume`)
   - В Feature только ресурсы: Material, ComputeShader, debugLogs

2. **ScreenSpaceGlobalIlluminationURP** (SSGI) - из пакета jiaozi158
   - Пакет: `Packages/com.jiaozi158.unityssgiurp` (локальный)
   - Статус: ✅ Работает
   - **Настройки**: Все настройки через Volume (`ScreenSpaceGlobalIlluminationVolume`)
   - В Feature только технические параметры: Shader, ReflectionProbes, HQUpscaling

## Volume Components (TechShotVolume.asset)

### RayTracedShadowsVolume
- Путь: `Assets/RayTracingShadows/Runtime/RayTracedShadowsVolume.cs`
- **General**: enable, disableStandardShadows
- **Quality**: shadowIntensity, shadowSpread
- **Sampling**: useFixedSampleCount, sampleCount
- **Occluder**: useOccluderDistance, occluderDistanceScale
- **Distance**: shadowMaxDistance, shadowReceiverMaxDistance, shadowDistanceFade
- **Denoise**: enableDenoise, denoiseStrength, denoiseMinBlend

### ScreenSpaceGlobalIlluminationVolume
- **Quality**: quality, sampleCount, maxRaySteps
- **Thickness**: thicknessMode, depthBufferThickness
- **Resolution**: fullResolutionSS, resolutionScaleSS
- **Denoise**: denoiseSS, denoiseIntensitySS, denoiserRadiusSS, denoiserAlgorithmSS, secondDenoiserPassSS
- **Ray Miss**: rayMiss (Sky, Reflection, Both, Nothing)
- **Intensity**: indirectDiffuseLightingMultiplier

## RayTracedShadowsLight Component
- Путь: `Assets/RayTracingShadows/Runtime/RayTracedShadowsLight.cs`
- Добавляется на Light для использования с RTS
- Автоматически отключает стандартные тени когда RTS активен
- `RayTracedShadowsLight.MainLight` - статический доступ к главному directional light
- `RayTracedShadowsVolume.GetRTSLights()` - получить все lights с RTS
- `RayTracedShadowsVolume.GetPrimaryRTSLight()` - получить главный directional light

## Архитектура управления эффектами
```
Volume Profile (TechShotVolume.asset)
├── RayTracedShadowsVolume (настройки RTS)
├── ScreenSpaceGlobalIlluminationVolume (настройки SSGI)
└── Другие Volume Overrides...

Renderer Feature (Ultra-high PC.asset)
├── RayTracedShadowPass (только ресурсы)
└── ScreenSpaceGlobalIlluminationURP (только технические параметры)

Scene Lights
├── Directional Light + RayTracedShadowsLight
├── Point Light + RayTracedShadowsLight
└── Spot Light + RayTracedShadowsLight
```

## Порядок Renderer Features
1. RayTracedShadowPass - выполняется после Deferred Lights
2. ScreenSpaceGlobalIlluminationURP - выполняется после Skybox

## Интеграция RTS + SSGI
- Оба эффекта используют GBuffer (Deferred path)
- RTS добавляет ray-traced тени от Directional и Spot lights
- SSGI добавляет непрямое освещение (bounced light)
- RTS поддерживает: Directional Light, Spot Light
- Point lights НЕ поддерживаются RTS (используют стандартные shadow maps)

## RayTracedShadowsLight - Логика переключения теней
- `UseRayTracedShadows = true` + RTS Volume активен → `Light.shadows = None`
- `UseRayTracedShadows = false` → `Light.shadows = CachedShadowType` (восстановление)
- `disableStandardShadows` в Volume контролирует глобальное отключение
- `_cachedShadowType` сохраняет тип теней (Soft/Hard) для восстановления
- `_autoCacheShadowType` автоматически кеширует тип при включении компонента

## Оптимизации RTS
- Один общий RTAS для всех источников света (избегает ошибок "too many instances")
- RTAS строится один раз за кадр, переиспользуется для всех lights
- Per-light temporal data для корректного denoising каждого источника

## Удалённые компоненты
- ❌ `TechShotSSGIFeature` - удалён
- ❌ `RtsDebugInfo.cs` - удалён
- ❌ Настройки качества из RTS Feature - перенесены в Volume

## MCP Unity Bridge
- Установлен и работает через `Assets/MCP/`
