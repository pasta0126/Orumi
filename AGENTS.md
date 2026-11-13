# AGENTS.md — ORUMI

Instrucciones para cualquier agente de IA que trabaje en este repositorio.

## Qué es ORUMI

Ecosistema personal de captura, organización, comprensión y análisis de experiencias cotidianas mediante IA. Idea central: **ORUMI escucha, comprende y conecta.** Es una memoria personal estructurada, no un diario, ni un gestor de tareas, ni un sistema de vigilancia.

Tres componentes con responsabilidades separadas:

1. **Captura** — graba voz (o texto), transcribe, limpia y entrega un documento ordenado. Producto independiente.
2. **Agente de seguimiento** — núcleo del sistema. Interpreta documentos, los descompone en registros, los relaciona con el historial, formula preguntas y detecta patrones.
3. **Panel web** — consulta, corrección, respuesta a preguntas pendientes, relaciones, análisis y consulta en lenguaje natural.

ORUMI es autónomo: sus ficheros viven en su propio directorio y, por ahora, **no se integra con ningún otro software** (ni Atlas ni otros repositorios del fundador; ver X34 en `docs/decisiones.md`).

## Metodología: SDD con OpenSpec

Trabajamos con Spec-Driven Development. **No se escribe código sin una change aprobada en OpenSpec.**

- Specs vigentes: `openspec/specs/`. Changes en curso: `openspec/changes/`.
- Flujo: `/opsx:propose "idea"` → revisar proposal/specs/design/tasks → `/opsx:apply` → `/opsx:archive`.
- Las decisiones de producto y de arquitectura se registran en specs o en el `design.md` de la change, no solo en el chat.
- Ante una cuestión abierta, se propone una recomendación y se deja constancia; no se asume en silencio.

## Estado actual

- Fase: **MVP implementado y desplegado** en el servidor (change `mvp-mira`, archivada 2026-11-13): `capturar`, `validar`, `procesar` (con cliente simulado; el real de Anthropic está escrito pero sin ejercitar), `reindexar` y `consultar`. IA contratada (Anthropic, Haiku 5.5) y lote nocturno activo en el servidor desde el 2026-11-13; el cliente real ya se ejercitó con datos ficticios. Pendiente: primeras notas reales y revisión de su calidad. Stack decidido en la change `tecnologias-mvp` (detalle en `docs/tecnologias.md`); despliegue vía el repo `infra` del servidor (`docs/decisiones.md` X43): **.NET**, **PostgreSQL** (base y rol propios en la instancia compartida del servidor), contenedor Docker con temporizador, proveedor de IA **Anthropic** (Claude Haiku 5.5 por defecto), contratación aplazada hasta necesitar la API. No introducir otros frameworks, bases de datos ni formatos sin una change que lo decida y justifique.
- Orden previsto: (1) modelo de registro y procedencia (change `modelo-de-registro`, archivada el 2026-11-13), (2) flujo del agente (Mira, change `agente-mira`, archivada), (3) elección de tecnologías (change `tecnologias-mvp`), (4) implementación del MVP (change `mvp-mira`, archivada), (5) primera ejecución real con la IA contratada, (6) captura por voz, API y panel.

## Criterios de coste y reutilización

- Minimizar coste, idealmente gratis. Antes de elegir un servicio, comparar precios y capas gratuitas.
- Reutilizar software propio del fundador solo si no implica integración con otros sistemas (X34): como librería o idea, no como dependencia en ejecución.
- Preferir el ecosistema Google y similares cuando reduzcan coste o fricción.
- Dudas y decisiones pendientes: `docs/decisiones.md` (documento vivo; mantenerlo al día).

## Principios de producto (innegociables)

1. Capturar debe ser más fácil que organizar; el usuario no categoriza.
2. **El original es irremplazable**: nunca se sustituye por interpretaciones; siempre recuperable.
3. Comprender > clasificar: qué, cuándo, contexto, acciones, problemas, resultados y relaciones.
4. Preguntar es parte de comprender, pero con moderación: preguntas selectivas, asíncronas, que admiten «no lo recuerdo», no bloquean el registro y no se repiten.
5. Observar antes de recomendar: ningún patrón ni recomendación a partir de evidencias aisladas.
6. El usuario controla: puede consultar, corregir, fusionar, separar, eliminar y rechazar conclusiones.
7. Los componentes son independientes y colaboran; no se diseña un monolito.

## Reglas para el agente de seguimiento

- **Nunca inventar datos.** Lo desconocido se marca como desconocido o se pregunta. No fabricar cifras.
- Distinguir siempre cinco categorías de información, sin mezclarlas:
  1. dicho directamente por el usuario,
  2. confirmado mediante aclaración,
  3. interpretación razonable no verificada,
  4. hipótesis derivada de varios registros,
  5. recomendación del sistema.
- Todo patrón debe enlazar con los registros que lo sustentan (trazabilidad).
- Un documento puede generar varios registros; evitar duplicados; conservar el vínculo con la fuente.
- Los datos personales deben seguir siendo accesibles con independencia del agente.
- Tono: cercano, sereno, curioso; sin entusiasmo excesivo, paternalismo ni consejos no solicitados.

## Reglas para trabajar en el repo

- Priorizar coherencia conceptual, simplicidad de uso, fiabilidad de los registros y utilidad de los descubrimientos. No añadir funcionalidades solo porque sean posibles.
- Objetivo inicial: resolver una necesidad personal real de forma sólida, no construir un producto comercial.
- Idioma de documentación y specs: español.
- No hacer commit ni push sin que se pida.
- La privacidad es prioritaria: no enviar datos del usuario a servicios externos sin decisión explícita registrada en una spec.

## Cuestiones abiertas

Ver §15 del briefing original (captura, agente, historial, análisis, panel, repositorio). Se resuelven mediante changes de OpenSpec, a partir de necesidades reales y no de preferencias tecnológicas.
