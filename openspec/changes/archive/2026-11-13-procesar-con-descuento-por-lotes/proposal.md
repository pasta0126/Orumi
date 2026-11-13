## Why

Mira procesa cada nota con la API normal, a precio completo, aunque el lote nocturno no tiene ninguna prisa. La API de lotes de Anthropic (Message Batches) cobra el **50 %** de las tarifas, y según su documentación la mayoría de los lotes termina en menos de una hora (24 horas como máximo). Con Sonnet 5.5 el coste baja de ~0,02 a ~0,01 US$ por nota con la misma calidad, y los 5 US$ de saldo duplican su alcance.

## What Changes

- El lote nocturno **envía las capturas pendientes como un único lote al proveedor y recoge los resultados cuando terminan**, en lugar de llamar nota a nota. Los resultados pasan por exactamente la misma validación, claves, protegidos, conflictos y preguntas de siempre.
- Cada envío se registra en un nuevo fichero `envio` dentro de `orumi/`, de modo que **una captura nunca se envía dos veces**, un envío no terminado se recoge en la siguiente ejecución y nada se pierde si el servidor se reinicia.
- La ejecución espera hasta un tiempo máximo configurable (90 minutos por defecto) a que termine el lote y, si no ha terminado, lo deja abierto para la siguiente ejecución.
- La **ejecución a petición** conserva un modo inmediato (`--inmediato`) a precio completo, para cuando se quiere el resultado ya.
- El coste estimado aplica el descuento de lotes (configurable) y los errores de saldo, límite o clave siguen cortando la ejecución.
- `orumi estado` muestra los envíos abiertos.
- No cambia el contrato de extracción, el formato de los registros ni el modelo.

## Capabilities

### New Capabilities
- `procesamiento-diferido`: envío de las capturas pendientes como lote al proveedor, recogida de resultados, idempotencia y recuperación, modo inmediato y coste con descuento.

### Modified Capabilities
<!-- Ninguna: la validación y el resto del procesamiento no cambian. -->

## Impact

- Spec nueva y un fichero nuevo del formato (`envio`), con su validación.
- Cambios en `Orumi.Agente` (proveedor por lotes, procesador en dos fases), `Orumi.Cli` y la unidad systemd (tiempo máximo del servicio).
- **Privacidad:** la API de lotes retiene los datos 29 días y no admite retención cero; las Condiciones Comerciales siguen prohibiendo entrenar con ellos. Hay que dejarlo anotado.
- Configuración: modo de procesamiento, espera máxima y factor de lote.
