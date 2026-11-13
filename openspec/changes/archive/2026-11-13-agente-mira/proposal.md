## Why

El modelo de registro ya está definido y probado a mano (changes archivadas `modelo-de-registro` y `ajustes-modelo-registro`), pero falta el componente que lo produce: Mira, el agente de seguimiento. La prueba con una nota real demostró que el trabajo de Mira es posible y útil, pero hoy está en la cabeza de quien lo hizo. Hay que fijar qué hace Mira con cada captura, cómo reprocesa sin pisar lo que el usuario corrigió, qué pregunta y qué datos salen a terceros, antes de elegir cualquier tecnología.

## What Changes

- Define el **procesamiento de capturas**: ejecución por lotes (nocturno) y a petición; extracción de cero o más registros por captura; cobertura completa (se registra todo lo contado, también lo cotidiano); reprocesado idempotente que respeta las aportaciones del usuario; aportaciones y contradicciones; lotes recuperables y bitácora.
- Define las **preguntas de Mira**: pendientes, nunca interrumpen, ordenadas por valor con un máximo por revisión, centradas en datos estructurales, sin repetirse tras «no lo recuerdo» o «no relevante».
- Define la **privacidad del procesamiento**: qué se envía al proveedor de IA, condiciones que debe cumplir y qué hace Mira si no se cumplen (D17, X29).
- Documenta el contrato de comportamiento en `docs/agente-mira.md` y un conjunto de casos de evaluación.
- No elige lenguaje, framework, proveedor ni modelo concretos, ni diseña la API, la app, el panel, la detección de patrones o los informes.

## Capabilities

### New Capabilities
- `procesamiento-de-capturas`: cómo Mira convierte capturas en registros, de forma completa, idempotente y recuperable.
- `preguntas-de-mira`: cuándo y cómo formula y cierra preguntas.
- `privacidad-del-procesamiento`: qué datos pueden salir hacia el proveedor de IA y con qué garantías.

### Modified Capabilities
<!-- Ninguna: los requisitos de registros-personales ya cubren el modelo; esta change define el comportamiento del agente que los aplica. -->

## Impact

- Nuevas specs en `openspec/specs/` al archivar.
- Nuevo `docs/agente-mira.md` y casos de evaluación en `docs/`.
- Condiciona la change de tecnologías (X32), que deberá elegir proveedor y ejecución conforme a esta privacidad.
- Sin código ni dependencias nuevas.
