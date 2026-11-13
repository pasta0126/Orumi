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

- Fase: **en uso real** desde el 2026-11-13. Todo lo siguiente está implementado, desplegado en el servidor y archivado como change de OpenSpec (decisiones X1–X57 en `docs/decisiones.md`):
  - **Captura:** por terminal (`orumi capturar`) y por **web instalable con dictado opcional** (servicio `orumi-web`, solo crea capturas).
  - **Agente Mira:** `procesar` con la API de Anthropic (**Sonnet 5.5**, por defecto en **lotes con 50 % de descuento**; `--inmediato` para verlo al momento), `validar`, `reindexar`, `consultar`, `estado`, control de saldo y de privacidad, y robustez ante la salida del modelo (X55). Lote nocturno activo.
  - **Control del usuario:** `responder`, `corregir` y `rechazar` (por terminal y desde el panel), con historial de origen `usuario`; Mira respeta lo corregido.
  - **Panel web con API** (servicio `orumi-panel`): ver por día, buscar, ficha con trazabilidad, responder, corregir y rechazar. Las capturas se montan **en solo lectura**. Acceso con el login de Google de la plataforma y lista de correos; limitación de peticiones, CORS con lista cerrada y cabeceras de endurecimiento (X54, X56).
  - **Diseño del front** compartido entre captura y panel (X57): sistema visual propio, iconos SVG propios, sin emojis, movimiento respetuoso.
- Stack decidido en la change `tecnologias-mvp` (detalle en `docs/tecnologias.md`): **.NET**, **PostgreSQL** como índice derivado (base y rol propios en la instancia compartida), contenedores Docker en el servidor vía el repo `infra` (`docs/decisiones.md` X43), proveedor de IA **Anthropic**. Interfaces web en HTML, CSS y JS sin dependencias, con CSP estricta (sin estilos ni scripts en línea). No introducir otros frameworks, bases de datos, bibliotecas de interfaz ni formatos sin una change que lo decida y justifique.
- **Pendiente** (lista viva en `docs/decisiones.md`, «Pendientes con fecha»): probar el dictado de la web; educar a Mira con casos reales (preguntas solo estructurales, X36); comparar Haiku y Sonnet con ≥ 2 semanas de notas (desde el 2026-11-27); fusionar, separar, eliminar y deshacer; capa conversacional (servidor MCP acotado y consulta en lenguaje natural; **no** un agente de propósito general ni bot de Telegram, descartado por ahora); análisis y patrones cuando haya datos; renovar la garantía de privacidad antes del 2027-05-12.
- Los detalles de infraestructura y despliegue son privados (`docs/privado/`, no se versiona) y viven en el repo `infra` del servidor.

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
