# ORUMI — Índice derivado: esquema lógico y regeneración

Estado: **propuesta** de la change `modelo-de-registro` (tareas 3.1 y 3.3). **No fija motor** (SQLite, Postgres u otro: change posterior, X3/X32). Los ficheros de `orumi/` son la fuente de verdad; el índice es desechable.

## 1. Esquema lógico (tarea 3.1)

Cada entidad es una tabla o colección con estos campos. Todos salen del frontmatter (ver `docs/formato-de-ficheros.md`); la columna `ruta` apunta al fichero de origen.

| Entidad | Campos |
|---------|--------|
| `captura` | `id`, `ruta`, `comunicada_el`, `origen`, `fecha_acontecimiento`, `etiqueta`, `dispositivo`, `huella_ok` |
| `registro` | `id`, `ruta`, `clase`, `titulo`, `comunicada_el`, `fecha_expresion`, `fecha_desde`, `fecha_hasta`, `hora_valor`, `hora_aproximada`, `hora_expresion`, `duracion_minutos`, `multidia_estado`, `multidia_fin`, `clave_extraccion`, `version_agente`, `modelo`, `generado_el`, `actualizado_el` |
| `registro_dato` | `registro_id`, `campo`, `valor`, `certeza` (una fila por elemento de `personas`, `lugares`, `proyectos`, `herramientas`, `acciones`, `resultados`, `problemas`, `ideas`, `contexto`) |
| `registro_entidad` | `registro_id`, `nombre`, `nombre_normalizado`, `tipo`, `certeza` (una fila por entidad) |
| `registro_certeza` | `registro_id`, `campo`, `certeza` (una fila por entrada del mapa `certeza`) |
| `fuente` | `registro_id`, `captura_id`, `rol`, `fragmento_desde`, `fragmento_hasta` |
| `entrada` | `registro_id`, `fecha`, `captura_id`, `resumen` |
| `relacion` | `origen_id`, `destino_id`, `tipo`, `certeza`, `evidencia` (lista de ids) |
| `conflicto` | `registro_id`, `con_registro_id`, `campo`, `pregunta_id`, `estado` |
| `rechazo` | `registro_id`, `campo`, `valor`, `rechazada_el` |
| `historial` | `registro_id`, `fecha`, `origen`, `campo`, `pregunta_id`, `captura_id` |
| `pregunta` | `id`, `registro_id`, `campo`, `estado`, `valor`, `creada_el`, `cerrada_el` |
| `lote` | `id`, `ejecutado_el`, `ejecucion`, `version_agente`, `modelo` |
| `lote_captura` | `lote_id`, `captura_id`, `registro_id`, `accion` (`creado` / `actualizado`) |

### Consultas que debe permitir (spec `indice-y-respaldo`)

| Consulta | Se resuelve con |
|----------|-----------------|
| Por fecha | `registro.fecha_desde/hasta` (solapamiento de rangos) |
| Por hora | `registro.hora_valor` dentro de la franja pedida; los registros sin `hora_valor` (incluidas las franjas como «por la mañana») no aparecen |
| Por tipo | `registro.clase` |
| Por certeza | `registro_certeza.certeza` |
| Por relación | `relacion`, `conflicto` |
| Por proyecto | `registro_dato` con `campo = proyectos` |
| Por entidad | `registro_entidad.nombre_normalizado` |
| Preguntas pendientes | `pregunta.estado = pendiente`, ordenadas por `valor` |

Además: capturas aún no procesadas = `captura` sin fila en `lote_captura`; conflictos abiertos = `conflicto.estado = abierto`.

### Reglas

- `nombre_normalizado` es el nombre de la entidad en minúsculas y sin acentos; la consulta por entidad compara con él, de modo que «Gullón», «gullon» y «GULLÓN» coinciden. No hay catálogo ni alias: dos nombres distintos son dos entidades.
- El índice **no guarda nada que no esté en un fichero**. Ningún campo se calcula con información externa.
- Las preguntas y los conflictos del índice son de solo lectura: se responden y resuelven editando el fichero (que luego se reindexa).
- Los umbrales de patrones (X20) y los informes llegarán con la change de análisis; no están en este esquema.

## 2. Regeneración (tarea 3.3)

El índice se puede borrar (`orumi/.indice/`) y reconstruir sin pérdida.

**Cuándo:** (a) tras cada lote de Mira, (b) tras cada edición desde el panel, de forma incremental sobre los ficheros cambiados, (c) completa y a petición, y (d) siempre que se detecte que falta o está dañado.

**Procedimiento completo:**

1. Crear un índice nuevo y vacío (no se toca el vigente hasta el final).
2. Recorrer en este orden `capturas/`, `registros/`, `preguntas/` y `lotes/`; `informes/` cuando exista su formato. Ignorar `entrada/` y `.indice/`.
3. Para cada fichero: leer el frontmatter, comprobar que `<nombre> = <id>.md` y que la ruta corresponde al id, y volcar sus filas.
4. En capturas, comprobar la `huella` con el cuerpo y guardar `huella_ok`.
5. Tras el volcado, **comprobar referencias**: capturas, registros, preguntas y fragmentos citados existen; los conflictos son simétricos; las claves de extracción no se repiten; todo campo con valor tiene certeza (salvo `desconocido`).
6. Todo problema se anota en un informe de regeneración y **no aborta**: un fichero ilegible se omite y se lista; las referencias rotas se marcan, no se corrigen.
7. Si hay diferencias graves (capturas con huella incorrecta, ids duplicados), la sustitución del índice vigente no se hace sin aviso al usuario.
8. Sustituir el índice vigente por el nuevo y registrar recuento por entidad.

**Garantías:** la regeneración es de solo lectura sobre `orumi/`; dos ejecuciones sobre los mismos ficheros dan el mismo índice; el índice nunca corrige un fichero.

**Verificación sugerida:** `docs/ejemplos/orumi/` debe regenerarse en un índice con 5 capturas, 7 registros, 4 preguntas y 5 lotes, con un conflicto abierto (dos filas en `conflicto`, una por cada lado) y una pregunta pendiente.
