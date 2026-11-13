# procesamiento-diferido Specification

## Purpose
Reducir el coste del procesamiento nocturno enviando las capturas pendientes como un lote diferido al proveedor, sin perder capturas, sin duplicar envíos y sin cambiar la validación ni los resultados.

## Requirements

### Requirement: Envío de las pendientes como un lote
En el modo por lotes, Mira MUST enviar al proveedor las capturas pendientes que no estén ya en un envío abierto como un único lote, y MUST registrar el envío en un fichero propio antes de dar la ejecución por terminada.

#### Scenario: Envío nocturno
- **WHEN** se ejecuta el lote periódico y hay capturas pendientes
- **THEN** se envían como un único lote y queda un fichero de envío con el identificador del lote del proveedor y las capturas incluidas

#### Scenario: Sin pendientes
- **WHEN** no hay capturas pendientes
- **THEN** no se envía nada

### Requirement: Ninguna captura se envía dos veces
Una captura incluida en un envío abierto MUST NOT incluirse en otro envío mientras ese envío no se haya recogido.

#### Scenario: Ejecución repetida antes de que termine
- **WHEN** se ejecuta Mira de nuevo mientras el lote del proveedor sigue en curso
- **THEN** no se vuelven a enviar esas capturas

### Requirement: Recogida de resultados
Mira MUST consultar los envíos abiertos al empezar cada ejecución y, cuando el lote del proveedor haya terminado, MUST procesar cada resultado con la misma validación y las mismas reglas que el procesamiento inmediato, y MUST dar el envío por recogido.

#### Scenario: Lote terminado
- **WHEN** el proveedor informa de que un envío ha terminado
- **THEN** cada resultado correcto genera sus registros, el envío queda recogido y las capturas figuran en el lote de Mira

#### Scenario: Resultado con error
- **WHEN** el proveedor devuelve un error, una cancelación o una expiración para una captura
- **THEN** esa captura queda pendiente, el lote lo anota y las demás se procesan

#### Scenario: Salida no válida
- **WHEN** un resultado no cumple el formato
- **THEN** no se guarda nada de esa captura, queda pendiente y su consumo se contabiliza

### Requirement: Espera acotada y recuperación
Mira MUST esperar a que termine el envío hasta un tiempo máximo configurable y, si no ha terminado, MUST dejarlo abierto para recogerlo en la siguiente ejecución; un reinicio o una interrupción MUST NOT perder ni duplicar un envío.

#### Scenario: El lote tarda más de lo esperado
- **WHEN** se alcanza el tiempo máximo de espera sin que el lote termine
- **THEN** la ejecución termina, el envío sigue abierto y la siguiente ejecución lo recoge

#### Scenario: Interrupción tras enviar
- **WHEN** la ejecución se interrumpe después de crear el lote en el proveedor
- **THEN** la siguiente ejecución encuentra el envío y lo recoge sin volver a enviar las capturas

### Requirement: Modo inmediato
Mira MUST permitir procesar a petición sin diferir, con la API normal, y MUST dejar claro en el lote qué modo se usó.

#### Scenario: Procesar ahora
- **WHEN** el usuario pide procesar de forma inmediata
- **THEN** se procesan las pendientes una a una con el resultado en el momento

### Requirement: Coste con descuento
El coste estimado de un lote diferido MUST aplicar el factor de descuento configurado, y los errores de saldo agotado, límite de gasto o clave inválida MUST seguir deteniendo la ejecución.

#### Scenario: Coste estimado
- **WHEN** se recoge un envío y hay precios y factor de lote configurados
- **THEN** el coste estimado del lote aplica el factor

#### Scenario: Saldo agotado al enviar
- **WHEN** el proveedor rechaza el envío por saldo agotado
- **THEN** no se crea el envío, las capturas siguen pendientes y se aplica el aviso de saldo

### Requirement: Privacidad del procesamiento diferido
El procesamiento diferido MUST cumplir las condiciones de privacidad del procesamiento inmediato y MUST dejar constancia en el lote de que el proveedor retiene los datos del lote durante su periodo de conservación.

#### Scenario: Constancia
- **WHEN** se recoge un envío
- **THEN** el lote indica el modo diferido y el proveedor
