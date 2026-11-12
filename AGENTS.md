# AGENTS.md — ORUMI

Instrucciones para cualquier agente de IA que trabaje en este repositorio.

## Qué es ORUMI

Ecosistema personal de captura, organización, comprensión y análisis de experiencias cotidianas mediante IA. Idea central: **ORUMI escucha, comprende y conecta.** Es una memoria personal estructurada, no un diario, ni un gestor de tareas, ni un sistema de vigilancia.

Tres componentes con responsabilidades separadas:

1. **Captura** — graba voz (o texto), transcribe, limpia y entrega un documento ordenado. Producto independiente.
2. **Agente de seguimiento** — núcleo del sistema. Interpreta documentos, los descompone en registros, los relaciona con el historial, formula preguntas y detecta patrones.
3. **Panel web** — consulta, corrección, respuesta a preguntas pendientes, relaciones, análisis y consulta en lenguaje natural.

Se reutilizará un repositorio de ficheros ya existente del fundador como almacén (pendiente de estudiar sus capacidades).

## Metodología: SDD con OpenSpec

Trabajamos con Spec-Driven Development. **No se escribe código sin una change aprobada en OpenSpec.**

- Specs vigentes: `openspec/specs/`. Changes en curso: `openspec/changes/`.
- Flujo: `/opsx:propose "idea"` → revisar proposal/specs/design/tasks → `/opsx:apply` → `/opsx:archive`.
- Las decisiones de producto y de arquitectura se registran en specs o en el `design.md` de la change, no solo en el chat.
- Ante una cuestión abierta, se propone una recomendación y se deja constancia; no se asume en silencio.

## Estado actual

- Fase: definición. **No hay tecnología ni arquitectura elegidas.** No introducir frameworks, bases de datos ni formatos sin una change que lo decida y justifique.
- Orden previsto: (1) estudiar el repositorio de ficheros existente, (2) modelo de registro y procedencia, (3) flujo del agente, (4) captura y panel.

## Criterios de coste y reutilización

- Minimizar coste, idealmente gratis. Antes de elegir un servicio, comparar precios y capas gratuitas.
- Reutilizar software propio del fundador siempre que sea viable, aunque requiera ajustes.
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
