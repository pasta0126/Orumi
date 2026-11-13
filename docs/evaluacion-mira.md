# Mira — Casos de evaluación

Estado: propuesta de la change `agente-mira`. Capturas **ficticias** con el resultado esperado de Mira, para revisar a mano una ejecución (real o simulada) contra `docs/agente-mira.md`. No hay métrica automática: se compara caso a caso y se anota lo que falla. Los ejemplos completos en formato de fichero están en `docs/ejemplos/`.

Convención: fecha de comunicación = **lunes 2026-11-16** salvo que se indique. «Cert.» = certeza esperada (`D` dicho, `I` interpretado, `C` confirmado). «→ desc.» = `desconocido`.

## Casos

### C1 · Nota mezclada (cobertura completa)

> Esta mañana he revisado el contrato de Ferrer con Pau. Luego me he tomado un cortado y una tostada. Ya he puesto la lavadora y he sacado al perro.

**Esperado:** 4 registros, todos con su fragmento: actividad «Revisar el contrato de Ferrer con Pau» (personas Pau D, entidades Ferrer D; fecha 2026-11-16 I), actividad de desayuno, actividad «Poner la lavadora», actividad «Sacar al perro». **No se filtra lo cotidiano.** Ninguna pregunta de alto valor por lo cotidiano.
**Trampa:** descartar «lo no profesional».

### C2 · Fechas y horas relativas

> Ayer sobre las 18 hablé con Rosa por teléfono. El lunes de la semana pasada tuve médico por la mañana.

**Esperado:** evento «Llamada con Rosa»: fecha 2026-11-15 `I` (expresion «ayer»), hora `{valor: "18:00", aproximada: true}` `D`. Evento «Médico»: fecha 2026-11-09 `I` (expresion «el lunes de la semana pasada»), hora `{expresion: por la mañana}` **sin `valor`**.
**Trampa:** inventar una hora para «por la mañana»; marcar la fecha como `dicho`.

### C3 · Dato no mencionado y acuerdos sin detallar

> Hoy reunión con Nuvia sobre el piloto. Hemos llegado a varios acuerdos. Después he estado con la incidencia del servidor de Nuvia.

**Esperado:** evento «Reunión con Nuvia sobre el piloto» con `duracion: desconocido` y un resultado «Hay acuerdos; no se indican» `D`. Problema «Incidencia del servidor de Nuvia» con `resultados: desconocido`.
**Preguntas esperadas:** «¿Quedó resuelta la incidencia del servidor de Nuvia?» (alta); «¿Cuánto duró la reunión con Nuvia?» (media).
**Preguntas prohibidas:** «¿Qué acuerdos tomasteis?», «¿Qué fallaba en el servidor?».
**Trampa:** suponer que la incidencia se resolvió; preguntar el contenido.

### C4 · Contradicción entre capturas

Captura 1 (lunes): «El jueves pasado comí con Carla en el centro.»
Captura 2 (martes): «Ayer me acordé de la comida con Carla, fue el miércoles de la semana pasada.»

**Esperado:** dos registros, cada uno con su fecha (jueves 2026-11-12; miércoles 2026-11-11), `conflictos` simétricos que apuntan a una pregunta pendiente de valor **alta** sobre si es la misma comida y qué día fue. **No se elige ganador ni se fusionan.**
**Trampa:** quedarse con la fecha más reciente.

### C5 · Coincidencia dudosa

Existe el registro «Reunión con Laura sobre facturas» (2026-11-12). Captura: «He vuelto a hablar con Laura de las facturas.»

**Esperado:** no es una ampliación segura: se crea un registro nuevo (**no se fusiona**) y una pregunta pendiente (alta) sobre si es la misma conversación o una nueva.
**Trampa:** fusionarlo en silencio.

### C6 · Continuación y cierre de una actividad

Registro abierto «Pintar el pasillo» (inicio 2026-11-13, `multidia.estado: abierto`). Captura: «Hoy he terminado de pintar el pasillo.»

**Esperado:** **no** se crea registro nuevo; se añade la captura como fuente `ampliacion`, una entrada fechada 2026-11-16 y `multidia.estado: cerrado` con `fin` 2026-11-16 (`I`: «hoy»).
**Trampa:** duplicar el registro.

### C7 · Reprocesado con corrección del usuario

Registro de C3 «Reunión con Nuvia…» con `duracion` confirmada por el usuario en 90 min (`C`) y el nombre de la entidad corregido de «Nuvia» a «Nuvia Labs» (`C`). Se reprocesa la captura con una nueva versión de Mira que extrae `duracion: desconocido` y la entidad «Nuvia».

**Esperado:** `duracion` y `entidades` **conservan los valores del usuario**; lo no protegido se actualiza (versión de Mira y `historial` con `origen: mira`); el registro sigue existiendo aunque la nueva pasada no lo reclamara; sin duplicados.
**Trampa:** pisar la corrección; eliminar o duplicar registros.

### C8 · Captura sin contenido registrable

> Probando, probando. ¿Se oye bien?

**Esperado:** ningún registro, captura intacta, lote con la captura y nota «sin hechos registrables». Ninguna pregunta.
**Trampa:** forzar un registro.

### C9 · Privacidad: proveedor sin garantías

El proveedor configurado no garantiza que no reutilice los datos.

**Esperado:** **no se envía nada**, las capturas siguen pendientes y el lote anota el problema y el proveedor.
**Trampa:** procesar de todos modos.

### C10 · Exceso de preguntas

Una nota con ocho datos estructurales ausentes, uno de ellos un conflicto.

**Esperado:** como máximo 5 preguntas pendientes, la del conflicto (alta) entre ellas; las de menor valor no se crean. Si ya había 5 pendientes de valor bajo, la del conflicto desplaza a una, que pasa a `cerrada_por_limite`.
**Trampa:** crear las ocho.

### C11 · Pregunta cerrada como «no relevante»

El usuario cierra «¿A qué proyecto pertenece la idea del cron?» como no relevante. Una captura posterior vuelve a mencionar una idea sin proyecto.

**Esperado:** no se repite la pregunta ni se formula otra equivalente sobre el proyecto de esa clase de ideas.

### C12 · Metadatos del usuario

Captura con `fecha_acontecimiento: 2026-11-08` y `etiqueta: casa`: «Monté la estantería del salón toda la tarde.»

**Esperado:** fecha 2026-11-08 `D` (metadato del usuario), `duracion: {expresion: toda la tarde}` sin `minutos`, `proyectos: [casa]` `D`, `lugares: [salón]` `D`.

## Revisión contra las specs (tarea 4.2)

| Spec · requisito | Casos |
|------------------|-------|
| `procesamiento-de-capturas` · Ejecución por lotes y a petición | C8, C9 (ejecución y pendientes) |
| · Cobertura completa | C1, C8 |
| · Extracción fiel y con certeza | C2, C3, C12 |
| · Validación antes de escribir | (revisión de formato en cada caso; ver §5 del contrato) |
| · Reprocesado idempotente que respeta al usuario | C7 |
| · Aportaciones y contradicciones | C4, C5, C6 |
| · Capturas intactas | todos (la `huella` no cambia) |
| · Lotes recuperables y bitácora | C8, C9 (lote con problemas) |
| `preguntas-de-mira` · Pendientes y asíncronas | C3, C4 |
| · Máximo por revisión y orden por valor | C10 |
| · Cierre y no repetición | C11 |
| · Solo datos estructurales | C3 |
| · Tono | todas las preguntas: sin consejo ni juicio |
| `privacidad-del-procesamiento` · Mínimo necesario | revisión de entrada (§7.1 del contrato) |
| · Garantías del proveedor | C9 |
| · Constancia de lo enviado | C9 (lote con proveedor) |

**Pendiente de cubrir** (dependen de la implementación): la recuperación ante un fallo a mitad de lote y la validación previa a escribir solo se pueden comprobar con una ejecución real; se revisan en la primera.

## Cómo revisar una ejecución (tarea 4.3)

1. **Preparar:** tomar las capturas del caso (o capturas reales en `data/orumi/`, que no se suben al repositorio) y ejecutar Mira sobre ellas.
2. **Comparar caso a caso** con «Esperado». Cada línea es un punto: registros, certeza, fechas y horas, preguntas esperadas y prohibidas.
3. **Comprobar criterios duros** (cualquiera que falle es un error):
   - hay un dato inventado (cifra, hora, resultado, motivo, entidad no mencionada);
   - algo `dicho` que no lo dijo el usuario;
   - un registro sin fragmento válido o sin lote;
   - una captura modificada o con la huella distinta;
   - una corrección del usuario pisada, o un registro eliminado o duplicado en un reprocesado;
   - una pregunta de contenido sustantivo o con consejo;
   - se supera el máximo de preguntas;
   - sale algo del inventario «nunca sale» (§7.1) o se procesa con un proveedor sin garantías.
4. **Anotar** los fallos con el caso y el campo; se corrigen en el contrato (`docs/agente-mira.md`) o en la implementación, y se repite la revisión.
5. **Criterio de aceptación para pasar a uso real:** ningún error duro en C1–C12 y al menos una ejecución real revisada por el usuario sin datos inventados.
