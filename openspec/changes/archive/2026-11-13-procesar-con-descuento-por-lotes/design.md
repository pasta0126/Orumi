## Context

Mira procesa nota a nota con la API normal (`procesamiento-de-capturas`, `control-del-saldo`). La API de lotes de Anthropic (Message Batches) cobra el 50 % de las tarifas; un lote admite hasta 100.000 peticiones o 256 MB, la mayoría termina en menos de una hora y expira a las 24 horas. Cada petición lleva un `custom_id` de 1 a 64 caracteres alfanuméricos y guiones (los identificadores de captura caben). Los resultados son `succeeded`, `errored`, `canceled` o `expired`; solo se cobran los correctos. Los resultados se conservan 29 días y la función **no admite retención cero** (según su documentación). Los lotes pueden pasarse ligeramente de un límite de gasto. El SDK de C# ofrece `Messages.Batches.Create`, `Retrieve` y los resultados.

## Goals / Non-Goals

**Goals:**
- Reducir a la mitad el coste por nota sin cambiar la calidad ni las garantías de validación.
- Que nunca se pierda ni se envíe dos veces una captura, ni siquiera tras un reinicio.

**Non-Goals:**
- Cambiar el contrato de extracción, el modelo o el formato de los registros.
- Caché de prompts (no aplica con un prompt corto) ni otros ahorros.
- Procesar en tiempo real con descuento (no existe).

## Decisions

1. **Dos fases en cada ejecución: recoger y enviar.** Primero se consultan los envíos abiertos y se recogen los terminados; después se envían las pendientes que no estén en ningún envío abierto; y se espera (hasta el máximo) a que termine el envío nuevo para recogerlo en la misma ejecución. Lo que no termine se recoge en la siguiente.
2. **El estado vive en ficheros, no en memoria.** Un nuevo tipo `envio` (`orumi/envios/AAAA/MM/envio-AAAAMMDD-HHMM.md`) guarda: identificador del lote del proveedor, fecha, modelo, modo, capturas incluidas (con, por cada una, los identificadores de los registros que se ofrecieron al proveedor y las claves existentes) y estado `abierto` o `recogido` (con el identificador del lote de Mira). Es el único fichero del sistema que se actualiza al cambiar de estado, junto a las preguntas. Se escribe **justo después de crear el lote en el proveedor**; si el proceso muere entre ambas cosas, el lote del proveedor queda huérfano (coste solo si termina), y es el único hueco: se acepta y se anota. *Alternativa descartada:* consultar al proveedor por los lotes abiertos al empezar; la API lista lotes, pero ligar capturas y ofrecidos exige guardarlos de todos modos.
3. **Los ofrecidos se guardan en el envío.** La regla «solo se citan registros ofrecidos al proveedor» se comprueba al recoger contra lo ofrecido al enviar, aunque el almacén haya cambiado entre medias. Los registros que el usuario haya cambiado durante la espera siguen protegidos por las reglas de siempre al aplicar el resultado.
4. **Misma tubería de aplicación.** Un resultado correcto se convierte en la misma `RespuestaExtraccion` que devuelve el modo inmediato y pasa por el mismo código de validación, claves, protegidos y preguntas; solo cambia cómo llega la respuesta. El consumo (`usage`) se contabiliza también para salidas inválidas.
5. **Interfaz de proveedor por lotes** (`IProveedorPorLotes`: enviar, consultar, recoger) separada de la inmediata, con una implementación simulada para las pruebas y la de Anthropic. El cliente no envía parámetros de muestreo.
6. **Modos.** Configuración `procesamiento.modo` (`lotes` por defecto en el lote nocturno, `inmediato`) y `--inmediato` en la línea de comandos para la ejecución a petición. El lote de Mira indica el modo.
7. **Espera máxima de 90 minutos** (configurable), sondeando cada minuto. La unidad de systemd pasa a tener un tiempo máximo mayor (por defecto un servicio `oneshot` no tiene límite, pero se fija uno de unas 2 horas para no quedar colgado).
8. **Coste con factor de lote** (`precios.factor_lote`, 0,5 por defecto en modo lotes). Los errores de saldo, límite o clave siguen cortando: al crear el lote (se rechaza el envío y no se crea el fichero) o al recoger.
9. **Privacidad.** El procesamiento diferido pasa por las mismas comprobaciones previas. Se deja constancia de que la API de lotes **retiene 29 días** y no admite retención cero; las Condiciones Comerciales siguen prohibiendo entrenar con el contenido, que es lo que exige X38. Se anota en `docs/decisiones.md` y `security/orumi.md`.
10. **Primer uso real con un lote mínimo** (una captura ficticia, céntimos) antes de dejarlo en el temporizador.

## Risks / Trade-offs

- **Un lote tarda más de lo esperado o expira (24 h)** → las capturas quedan pendientes y se reenvían en la siguiente ejecución; sin pérdidas.
- **Hueco entre crear el lote y escribir el envío** → se escribe inmediatamente después y se prueba la recuperación; un lote huérfano solo cuesta si termina.
- **Los datos se retienen 29 días en el proveedor** → equivalente a los 30 días de la API normal; se documenta y se mantiene la garantía de no entrenamiento.
- **La nota tarda más en procesarse** (hasta una hora más que antes) → irrelevante de noche; para verlo ya existe el modo inmediato.
- **Un lote puede pasarse ligeramente de un límite de gasto** → con saldo prepago el tope sigue siendo el saldo; se anota.
- **Más complejidad** (un fichero de estado y dos fases) → acotada por pruebas con proveedor simulado y una prueba real mínima.
