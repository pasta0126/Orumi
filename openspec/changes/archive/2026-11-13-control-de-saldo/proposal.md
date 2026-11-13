## Why

La IA se contrató con créditos prepago y **sin recarga automática** (el saldo es el tope de gasto): cuando se acaben los 5 US$, la API dejará de responder y, con el lote nocturno, nadie se enteraría hasta darse cuenta de que las notas no se procesan. Mira debe detectar el saldo agotado, dejarlo claro, **avisar antes** de que ocurra con una estimación del gasto, y retomar sola cuando el usuario recargue.

## What Changes

- Mira **clasifica los errores del proveedor** (saldo agotado o problema de facturación, clave no válida, otros) y, ante un saldo agotado o una clave inválida, **detiene la ejecución**: no reintenta las demás capturas, las deja pendientes, anota en el lote un problema claro con lo que hay que hacer y termina con un código de salida propio.
- Cada lote registra el **uso** (tokens de entrada y salida) y un **coste estimado** con los precios de la configuración (nunca inventados: sin precios, el coste queda desconocido).
- Un **presupuesto configurable** (saldo comprado, fecha de inicio y porcentaje de aviso, **90 % por defecto**): cuando el gasto estimado lo alcanza, Mira avisa en el lote y en la consola. La estimación se calcula con los tokens exactos que devuelve cada respuesta y los precios oficiales; avisa, pero no detiene nada: solo el proveedor detiene.
- Nuevo comando `estado` (capturas pendientes, último lote, gasto estimado frente al presupuesto, avisos) y **un aviso breve en `capturar` y `validar`**, que es lo que el usuario ejecuta a diario.
- Tras recargar, la siguiente ejecución retoma sin intervención.
- También detiene la ejecución un **límite de gasto** del proveedor (el mensual que se configura en la consola o el tope del nivel de uso).
- Fuera de alcance: notificaciones por correo o móvil (dependen del panel o la app) y consultar el saldo real: la API de Claude no lo expone (el uso y el coste solo están en una API de administración que no está disponible para cuentas individuales).

## Capabilities

### New Capabilities
- `control-del-saldo`: detección de saldo agotado o clave inválida, uso y coste estimado por lote, presupuesto con aviso previo y visibilidad del estado.

### Modified Capabilities
<!-- Ninguna: se añade comportamiento nuevo sin cambiar el de las specs vigentes. -->

## Impact

- Spec nueva `control-del-saldo`; cambios en `Orumi.Agente` (proveedor, procesador, configuración), `Orumi.Cli` (`estado` y avisos) y el formato del lote (campos de uso y de avisos, opcionales).
- Configuración del servidor: precios y presupuesto en `orumi.yaml`.
- Sin dependencias nuevas ni gasto.
