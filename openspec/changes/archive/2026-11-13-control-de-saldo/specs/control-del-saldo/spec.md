## Purpose

Garantizar que el usuario se entera de que Mira no puede procesar por falta de saldo o por una clave inválida, que recibe un aviso previo cuando el gasto estimado se acerca al saldo comprado, y que el procesamiento se reanuda solo cuando el problema se resuelve.

## ADDED Requirements

### Requirement: Saldo agotado o clave inválida detienen la ejecución
Cuando el proveedor indique que no hay saldo suficiente, que hay un problema de facturación, que se ha alcanzado un límite de gasto o que la clave no es válida, Mira MUST detener la ejecución sin reintentar las demás capturas, MUST dejar las capturas pendientes, MUST registrar en el lote un problema que explique la causa y qué hacer, y MUST terminar con un código de salida distinto del de éxito y del de otros errores.

#### Scenario: Saldo agotado
- **WHEN** el proveedor responde que el saldo es insuficiente
- **THEN** la ejecución se detiene, las capturas siguen pendientes y el lote indica «saldo agotado: recarga créditos en la consola del proveedor»

#### Scenario: Límite de gasto alcanzado
- **WHEN** el proveedor responde que se ha alcanzado un límite de gasto (el configurado por el usuario o el del nivel de uso)
- **THEN** la ejecución se detiene, las capturas siguen pendientes y el lote indica «límite de gasto alcanzado» y, si el proveedor lo informa, cuándo se recupera el acceso

#### Scenario: Clave no válida
- **WHEN** el proveedor rechaza la clave de API
- **THEN** la ejecución se detiene, las capturas siguen pendientes y el lote indica que la clave no es válida, sin reproducir la clave

#### Scenario: Otro error
- **WHEN** el proveedor devuelve un error que no es de saldo ni de clave
- **THEN** solo esa captura queda pendiente y se continúa con las demás

### Requirement: Reanudación tras recargar
Después de resolver el problema, la siguiente ejecución MUST procesar las capturas pendientes sin intervención adicional.

#### Scenario: Recarga de créditos
- **WHEN** el usuario recarga el saldo y se ejecuta el siguiente lote
- **THEN** se procesan las capturas que habían quedado pendientes

### Requirement: Uso y coste estimado por lote
Cada lote MUST registrar los tokens de entrada y de salida consumidos por captura y MUST calcular un coste estimado con los precios de la configuración; si no hay precios configurados, el coste MUST quedar como desconocido y no se inventa.

#### Scenario: Con precios
- **WHEN** se procesa una captura y hay precios configurados
- **THEN** el lote muestra sus tokens y un coste estimado

#### Scenario: Sin precios
- **WHEN** no hay precios configurados
- **THEN** el lote muestra los tokens y el coste como desconocido

### Requirement: Presupuesto y aviso previo
Con un presupuesto configurado (saldo comprado, fecha de inicio y porcentaje de aviso, por defecto el 90 %), Mira MUST sumar el coste estimado de los lotes desde esa fecha y MUST avisar cuando alcance el porcentaje de aviso y cuando lo supere por completo; el aviso MUST NOT detener el procesamiento, porque la estimación puede diferir del saldo real.

#### Scenario: Se alcanza el umbral
- **WHEN** el gasto estimado llega al 90 % del saldo configurado
- **THEN** el lote y el estado muestran un aviso con el gasto estimado, el saldo y lo que queda aproximado

#### Scenario: Se supera el saldo estimado
- **WHEN** el gasto estimado supera el saldo configurado
- **THEN** se muestra el aviso y el procesamiento continúa hasta que lo detenga el proveedor

### Requirement: Estado y avisos visibles al usuario
El sistema MUST ofrecer un comando de estado que muestre las capturas pendientes, el resultado del último lote, el gasto estimado frente al presupuesto y los avisos vigentes, y MUST mostrar un aviso breve, sin bloquear, al ejecutar los comandos de captura y de validación cuando haya un aviso vigente; los avisos MUST NOT contener contenido de las notas ni la clave de API.

#### Scenario: Aviso al capturar
- **WHEN** el último lote terminó por saldo agotado y el usuario crea una captura
- **THEN** la captura se crea con normalidad y se muestra un aviso de que no se procesará hasta recargar

#### Scenario: Aviso resuelto
- **WHEN** un lote posterior termina sin problemas de saldo
- **THEN** el aviso de saldo agotado deja de mostrarse

#### Scenario: Consulta del estado
- **WHEN** el usuario pide el estado
- **THEN** ve las capturas pendientes, el último lote, el gasto estimado y los avisos
