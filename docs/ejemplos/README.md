# Ejemplos ficticios del modelo de registro

Datos **inventados** para ilustrar `docs/formato-de-ficheros.md`. El árbol `orumi/` imita el directorio real: 5 capturas, 7 registros, 4 preguntas y 5 lotes de Mira (cronología del 9 al 13 de noviembre de 2026).

## Qué cuenta cada captura

| Captura | Qué pasa |
|---------|----------|
| `cap-20261109-0815-m4k` | Captura **multitema**: actividad, idea y decisión en una sola nota; cada registro cita su fragmento. |
| `cap-20261110-2140-x9d` | Origen **voz** con **metadatos opcionales** (fecha del acontecimiento y etiqueta); actividad abierta y problema. |
| `cap-20261111-1830-p3w` | **Aportación** a un registro existente: cierra la actividad iniciada el 9. |
| `cap-20261112-1905-h8t` | **Fecha imprecisa** («la semana pasada») con rango interpretado. |
| `cap-20261113-0920-c5n` | **Contradicción** de fecha con la anterior. |

## Escenarios de las specs cubiertos (tarea 2.2)

| Escenario | Dónde se ve |
|-----------|-------------|
| Se guarda el texto literal / captura intacta tras una corrección | `huella` de cada captura; el cuerpo no cambia aunque `reg-20261110-q2x1` tenga correcciones |
| Metadatos presentes / fecha del acontecimiento distinta / sin metadatos / sin ubicación | `x9d` (con metadatos), `m4k` (sin ellos); ninguna lleva ubicación |
| Una captura genera varios registros / fragmento de origen | `m4k` → `q2x1`, `r7m4`, `t5k8`; `x9d` → `a3f9`, `d6y2` |
| Registro incompleto / dato desconocido | `t5k8` (`contexto: desconocido`; Mira no pregunta el motivo, por X36), `d6y2` (`duracion: desconocido`) |
| Campos opcionales ausentes / registro mínimo | `r7m4` (sin duración, personas ni lugares) |
| Fecha imprecisa / corrección del rango | `e4n7` (rango interpretado); el rango pasaría a `confirmado` al corregirlo el usuario |
| Dato interpretado / confirmación por el usuario | `fecha: interpretado` en `q2x1`; `duracion: confirmado` tras `preg-20261112-n8j6` (respondida) |
| Nada inventado | `a3f9`: «toda la tarde» sin cifra; `q2x1` antes de preguntar: `desconocido` |
| Procedencia y versión del agente / cambio de versión | `procedencia` en todos; `actualizado_por/_el` en `q2x1` y `e4n7` |
| Reprocesado sin duplicar / aportaciones del usuario respetadas | `q2x1` reprocesado el 12-nov: `p3w` se añade como `ampliacion` y se actualiza `multidia`, sin tocar `duracion` ni `proyectos` |
| Corrección con historial | `q2x1.proyectos` (de `copias-de-seguridad` a `mantenimiento-servidor`, ahora `confirmado`) |
| Rechazo de una interpretación | `t5k8.rechazos` («Posible falta de tiempo») |
| Preguntas pendientes / «no lo recuerdo» | `k2v9` (pendiente), `b7c2` (`no_recuerda`: duración de la conversación con Marta, no se repite) |
| Pregunta «no relevante» | `f4t6` (`no_relevante`): el usuario cierra una pregunta sobre un proyecto que no le importa; no se repite ni se formulan otras parecidas |
| Preguntas sobre datos estructurales, no sobre contenido | todas las preguntas piden duración, estado, fecha o proyecto; ninguna pide qué se habló o decidió (X36) |
| Contradicciones (ambas versiones, marcadas, sin ganador, con pregunta) | `e4n7` ↔ `w1h5` y `preg-20261113-k2v9` |
| Evento multi-día: continuación y cierre | `q2x1` (cerrado, con dos entradas); `a3f9` (abierto) |
| Aportación a registro existente | `p3w` → `q2x1` |
| Coincidencia dudosa (no fusionar hasta la respuesta) | `e4n7` y `w1h5` no se fusionan; queda `k2v9` |
| Hora aproximada | `q2x1.hora`: «sobre las 7.30» → `07:30`, `aproximada: true` |
| Franja sin hora | `w1h5.hora`: «por la tarde», sin `valor` (no se inventa una hora) |
| Certezas distintas (día y hora) | `q2x1`: `fecha: interpretado`, `hora: dicho` |
| Entidad dicha, con nombre normalizado | `e4n7` y `w1h5`: «reformas duero» en la captura → `Reformas Duero` (certeza por campo) |
| Corrección de una entidad | `q2x1`: «duplicati» → `Duplicacy`, `confirmado` (certeza por elemento) y entrada en `historial` |
| Consulta por entidad | «Reformas Duero» devolvería `e4n7` y `w1h5` (ver `docs/indice-derivado.md`) |
| Registro anterior a los campos nuevos | `r7m4`, `t5k8`, `a3f9`, `d6y2`: sin `hora` ni `entidades`, siguen siendo válidos |
| Relaciones trazables | `relaciones` en `r7m4`, `t5k8`, `d6y2` |
| Separación de datos | Todo vive bajo `orumi/`; no hay copias fuera |

**No cubiertos en estos ejemplos** (dependen de changes posteriores): patrones e hipótesis con evidencia (X20–X23), informes, y el fallo al guardar una transcripción (`entrada/` con `.pendiente`).

## Comprobación

Los ejemplos se revisaron con un script desechable (no incluido en el repositorio, para no fijar tecnología) que verificó: huellas de las capturas, posiciones y citas de fragmentos, formato de ids y rutas, certeza válida y presente en cada campo con valor, referencias entre ficheros, simetría de conflictos, coherencia de actividades abiertas/cerradas, rechazos con historial, cronología del historial y que cada captura tenga lote. Resultado: sin errores (también para `hora` y `entidades`: formato `HH:MM`, aproximada con valor, tipos válidos y certeza por campo o por elemento, nunca ambas). También se probó que detecta un cuerpo alterado, una cita incorrecta, un conflicto no simétrico y una actividad abierta con fin.

Las fechas se escriben ISO 8601; los instantes dentro de mapas en línea (`{…}`) van entre comillas, porque algunos lectores YAML fallan con los dos puntos de la hora sin comillas.
