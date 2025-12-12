# Requirements Document

## Introduction

Система Ray Traced Shadows (RTS) в проекте TechShot не отбрасывает тени от Spot источников света, хотя код содержит поддержку для них. Directional lights работают корректно, Point lights не поддерживаются (это ожидаемое поведение). Необходимо диагностировать и исправить проблему с Spot lights.

## Glossary

- **RTS (Ray Traced Shadows)**: Система трассировки лучей для создания реалистичных теней в Unity URP
- **Spot Light**: Прожекторный источник света с конусообразной областью освещения
- **Directional Light**: Направленный источник света (имитирует солнце)
- **Point Light**: Точечный источник света (не поддерживается RTS)
- **RTAS (Ray Tracing Acceleration Structure)**: Структура ускорения для трассировки лучей
- **Volume Component**: Компонент настроек в Unity Volume Profile
- **RayTracedShadowsLight**: Компонент на источнике света для управления RTS
- **Compute Shader**: Шейдер для вычислений на GPU

## Requirements

### Requirement 1

**User Story:** Как разработчик, я хочу, чтобы Spot lights отбрасывали ray-traced тени, чтобы все поддерживаемые типы источников света работали корректно.

#### Acceptance Criteria

1. WHEN a Spot Light has RayTracedShadowsLight component with UseRayTracedShadows enabled AND RTS Volume is active THEN the system SHALL generate ray-traced shadows from that Spot Light
2. WHEN a Spot Light is within range of a surface THEN the system SHALL cast shadows only within the spot cone angle
3. WHEN a Spot Light shadow is computed THEN the system SHALL apply correct attenuation based on distance and cone angle
4. WHEN multiple Spot Lights are active THEN the system SHALL process shadows for each light independently
5. WHEN a Spot Light is outside its range THEN the system SHALL not compute shadows for surfaces beyond that range

### Requirement 2

**User Story:** Как разработчик, я хочу диагностировать, почему Spot lights не работают, чтобы найти корневую причину проблемы.

#### Acceptance Criteria

1. WHEN the system processes lights THEN the system SHALL correctly identify Spot lights as supported light types
2. WHEN light parameters are passed to compute shader THEN the system SHALL include all required Spot light parameters (position, range, angles)
3. WHEN the compute shader executes THEN the system SHALL correctly branch to Spot light code path when g_LightType equals 1
4. WHEN debugging is enabled THEN the system SHALL log which lights are being processed and their types
5. WHEN a Spot Light is processed THEN the system SHALL verify that light direction, position, and cone angles are correctly transformed to world space

### Requirement 3

**User Story:** Как разработчик, я хочу, чтобы система корректно фильтровала типы источников света, чтобы только поддерживаемые типы обрабатывались RTS.

#### Acceptance Criteria

1. WHEN the system collects RTS lights THEN the system SHALL include Directional lights in the processing list
2. WHEN the system collects RTS lights THEN the system SHALL include Spot lights in the processing list
3. WHEN the system collects RTS lights THEN the system SHALL exclude Point lights from the processing list
4. WHEN a light has RayTracedShadowsLight component with UseRayTracedShadows disabled THEN the system SHALL exclude that light from RTS processing
5. WHEN RTS Volume is inactive THEN the system SHALL not process any lights for ray-traced shadows

### Requirement 4

**User Story:** Как разработчик, я хочу проверить корректность передачи параметров Spot light в compute shader, чтобы убедиться, что все данные доступны для вычислений.

#### Acceptance Criteria

1. WHEN a Spot Light is processed THEN the system SHALL set g_LightType to 1 in the compute shader
2. WHEN a Spot Light is processed THEN the system SHALL pass light position in world space coordinates
3. WHEN a Spot Light is processed THEN the system SHALL pass light range value
4. WHEN a Spot Light is processed THEN the system SHALL pass cosine of outer spot angle (spotAngle/2)
5. WHEN a Spot Light is processed THEN the system SHALL pass cosine of inner spot angle for smooth falloff
6. WHEN a Spot Light is processed THEN the system SHALL pass light forward direction vector
