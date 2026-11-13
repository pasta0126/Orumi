## Context

El formato de registro, el índice y los ejemplos están definidos (`docs/formato-de-ficheros.md`, `docs/indice-derivado.md`, `docs/ejemplos/`) y las specs `capturas-originales`, `registros-personales` e `indice-y-respaldo` están vigentes. Una prueba manual con una nota real (2026-11-13) hizo de Mira a mano y validó el modelo. Decisiones ya tomadas que condicionan esta change: ejecución por lotes y a petición (X4), preguntas pendientes con máximo (X13, X36), conflictos y aportaciones (X16, X24–X26), reprocesado que respeta al usuario (X30), proveedor de IA de pago provisional (X9), despliegue en el servidor propio (X32), autonomía de ORUMI (X34) y sin respaldo (X27).

## Goals / Non-Goals

**Goals:**
- Un contrato de comportamiento de Mira independiente del proveedor, del modelo y del lenguaje.
- Procesamiento completo, idempotente, recuperable y trazable.
- Preguntas útiles y escasas.
- Privacidad verificable: lo que sale, a quién y con qué garantías.

**Non-Goals:**
- Elegir proveedor, modelo, lenguaje o framework (X32).
- Detección de patrones, hipótesis e informes (change posterior de análisis, X20–X23).
- Transcripción de voz, API de entrada, app Flutter y panel.
- Notificar al usuario de preguntas pendientes.

## Decisions

1. **Cobertura completa.** Mira registra todo lo contado, también lo cotidiano. Decidido por el usuario: lo más fiel a «el usuario no categoriza» y Mira nunca decide en silencio qué importa. El filtrado, si se quiere, es del panel o de la consulta. *Alternativas descartadas:* ignorar lo no profesional (decide por el usuario); campo de ámbito (añade clasificación que el usuario no pidió).
2. **Contrato de entrada y salida.** Entrada: texto de la captura, su fecha de comunicación y, solo si hace falta, los registros relacionados y las claves existentes de esa captura. Salida: ficheros de registro y preguntas en el formato vigente. El contrato no depende del modelo, así que el proveedor es intercambiable.
3. **Reclamo de claves en el reprocesado.** Mira recibe las claves existentes de la captura; cada elemento extraído reclama una o recibe una nueva; lo no reclamado se conserva. La clave nunca se recalcula a partir del contenido (decisión 5 de la change de modelo de registro).
4. **Validación antes de escribir.** Mira comprueba la salida contra las reglas del formato (obligatorios, certeza, referencias, fragmentos) antes de guardar. La comprobación manual que hicimos con el script de ejemplos es el modelo de esas reglas. Un fallo no marca la captura como procesada.
5. **Un lote por ejecución con bitácora completa**, como ya define el formato. Una captura solo figura en un lote cuando todos sus registros se guardaron; el resto sigue pendiente y es reintentable.
6. **Valor de las preguntas.** Alto: conflictos y datos que dejan un asunto abierto (¿se resolvió?). Medio: fecha, hora, duración, personas, lugar y proyecto de lo relevante. Bajo: datos accesorios. El máximo por revisión es configurable; por defecto 5. *Alternativa descartada:* sin límite; convierte el registro en cuestionario (X13).
7. **Preguntas estructurales** (X36). Nunca por el contenido de lo hablado o decidido. Un cierre «no relevante» o «no lo recuerdo» también bloquea preguntas equivalentes.
8. **Privacidad con fallo cerrado.** Si el proveedor no garantiza no reutilizar los datos, Mira no procesa. Los únicos datos que salen son el texto de la captura y los registros mínimos relacionados; nunca audio, ubicación ni ficheros fuera de `orumi/`. Cada lote registra proveedor y modelo (D17, X29).
9. **Evaluación con casos conocidos.** La calidad de Mira se verifica con un conjunto de capturas y resultados esperados (los ejemplos ficticios y casos con trampas: contradicción, ambigüedad, lo cotidiano, dato no mencionado, reprocesado con corrección del usuario), revisados a mano. No hay métrica automática en esta etapa.
10. **Documento de comportamiento** `docs/agente-mira.md`: el ciclo del lote, las reglas de extracción por campo, la política de preguntas y la privacidad, para que cualquier implementación futura las cumpla.

## Risks / Trade-offs

- **El modelo inventa o sobreinterpreta** → certeza obligatoria, fragmentos de origen verificables, validación antes de escribir, casos de evaluación y revisión del usuario.
- **Ruido por registrar lo cotidiano** → decisión asumida; el panel o las consultas podrán ocultarlo sin borrarlo.
- **Una nueva versión de Mira cambia registros antiguos** → solo se actualiza lo no protegido, con historial y versión.
- **Coste y límites del proveedor** → volumen estimado bajo (X9); si se supera, el lote es reanudable.
- **Sin respaldo de `orumi/` (X27)** → un fallo de escritura o de disco pierde datos; la escritura se valida y los lotes son recuperables, pero el riesgo está aceptado.
- **Preguntas escasas dejan datos incompletos** → es deliberado (X13, X19): un registro es válido desde el principio.
