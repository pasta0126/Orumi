## Context

El modelo ya prevé correcciones del usuario (certeza `confirmado`, `historial` con origen `usuario`, `rechazos`, estados de pregunta `respondida`, `no_recuerda`, `no_relevante`) y Mira respeta lo corregido (X30, X31): un campo es protegido si tiene certeza `confirmado` o alguna entrada de historial de origen usuario. Los ficheros se escriben con `FrontmatterCanonico` y de forma atómica, y el `Validador` comprueba el resultado. Falta el lado de las herramientas. No hay panel ni app, y las respuestas deben ser deterministas y baratas: sin llamar a un modelo.

## Goals / Non-Goals

**Goals:**
- Responder, corregir y rechazar desde la línea de comandos con trazabilidad completa.
- Que Mira respete lo hecho (sin cambios en Mira: ya lo hace).
- Cero riesgo de corromper datos: validar antes de escribir, escritura atómica, rechazar lo ambiguo.

**Non-Goals:**
- Fusionar, separar o eliminar registros.
- Interpretar texto libre con un modelo.
- Interfaz web o por voz.

## Decisions

1. **Valores deterministas por campo.** Una función de análisis por campo: `fecha` (`AAAA-MM-DD` o `AAAA-MM-DD..AAAA-MM-DD`), `hora` (`HH:MM`), `duracion` (minutos `90`, `90m`, `1h30`, `2h`; el texto escrito se conserva como expresión), listas (`personas`, `lugares`, `proyectos`, `herramientas`, `acciones`, `ideas`, `problemas`: separadas por comas), `entidades` (nombres separados por comas, con `tipo` opcional `nombre:tipo`), y texto libre (`titulo`, `contexto`, `resultados`). Lo que no encaja con el campo se rechaza y no se modifica nada. *Alternativa descartada:* un modelo que interprete frases libres; añade coste, latencia y la posibilidad de interpretar mal una corrección del usuario.
2. **Campos corregibles:** `titulo`, `fecha`, `hora`, `duracion`, `personas`, `entidades`, `lugares`, `proyectos`, `herramientas`, `contexto`, `resultados`, `acciones`, `ideas`, `problemas`. No lo son los identificadores, las fuentes, la procedencia, la clave de extracción ni las capturas.
3. **Operación sobre el árbol del fichero.** Se lee el registro, se modifica el campo y la certeza (`confirmado`), se añade la entrada de historial (`origen: usuario`, `anterior`, `nuevo`, `pregunta` si procede) y se reescribe con `FrontmatterCanonico`. La corrección de `fecha` sustituye el rango y conserva la expresión original (escenario ya especificado). Una corrección de un campo con certeza por elemento (entidades) deja cada elemento `confirmado`.
4. **Validar antes de escribir.** Se construye el almacén propuesto y se pasa el `Validador` sobre los ficheros tocados; si falla, no se escribe nada y se muestra el motivo. Escritura atómica (temporal + renombrado), como el resto del sistema.
5. **Exclusión mutua con Mira** mediante el mismo bloqueo de ejecución: si Mira procesa, la operación se rechaza con un mensaje de reintentar (código de salida propio).
6. **Respuesta como evidencia.** La pregunta pasa a `respondida` con `respuesta: {texto, respondida_el}` (el texto literal que escribió el usuario) y `cerrada_el`. `--no-recuerdo` y `--no-relevante` la cierran con esos estados, sin tocar el registro.
7. **Conflictos y coincidencias.** Al responder una pregunta de conflicto hay que indicar el registro al que corresponde el valor (`--registro`, por defecto el de la pregunta): se aplica a ese registro y el conflicto pasa a `resuelto` en ambos. La otra versión no se cambia (el usuario puede corregirla). Una pregunta de coincidencia se contesta con `distinta` (se cierra, nada cambia) o `misma` (se añade una relación `relacionado_con` confirmada entre los dos registros; la fusión queda fuera de alcance y se avisa).
8. **Rechazar** mueve el valor interpretado a `rechazos` (con `certeza_original`) y el campo a `desconocido`, con historial `usuario`; solo procede si la certeza era `interpretado` o `hipotesis`. Si no, informa y no hace nada.
9. **Contexto en `consultar pendientes`:** por cada pregunta, registro, campo, valor actual y registros implicados, leyendo los ficheros (sin depender del índice).
10. **Índice:** las operaciones no regeneran el índice; se muestra el aviso y el lote nocturno lo hace. *Alternativa:* regenerarlo siempre; acopla los comandos a la base de datos.

## Risks / Trade-offs

- **Un valor mal escrito se acepta como válido** (p. ej., una fecha equivocada pero bien formada) → es una corrección del usuario y se asume acertada (X30); queda el valor anterior en el historial.
- **No hay «deshacer»** → el historial conserva el valor anterior y se puede volver a corregir; un comando de deshacer queda fuera de alcance.
- **Resolver un conflicto no cambia la otra versión** → se avisa y se indica cómo corregirla.
- **Respuestas largas en texto libre** → solo se admiten donde el campo es texto (`resultados`, `contexto`); en el resto se pide el formato esperado.
- **Dos personas o procesos escribiendo a la vez** → el bloqueo y la escritura atómica lo evitan en este despliegue (un solo usuario).
