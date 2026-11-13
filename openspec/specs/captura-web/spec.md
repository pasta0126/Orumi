# captura-web Specification

## Purpose
Permitir crear capturas desde cualquier dispositivo con un navegador, escribiendo o dictando, sin terminal, conservando las garantías de inmutabilidad y trazabilidad de las capturas.

## Requirements

### Requirement: Crear una captura desde la web
El sistema MUST permitir enviar un texto desde la página de captura y crear con él una captura inmutable idéntica, en formato y validaciones, a la creada por línea de comandos, devolviendo su identificador.

#### Scenario: Nota escrita
- **WHEN** el usuario escribe una nota y la envía
- **THEN** se crea una captura con origen texto y la página muestra su identificador

#### Scenario: Nota dictada
- **WHEN** el usuario dicta una nota, la revisa y la envía
- **THEN** se crea una captura con origen voz con el texto tal como quedó tras la revisión

#### Scenario: Metadatos opcionales
- **WHEN** el usuario indica una fecha del acontecimiento o una etiqueta
- **THEN** se guardan en la captura; si la fecha no es válida, no se crea nada y se informa

#### Scenario: Texto vacío
- **WHEN** el texto está vacío o solo tiene espacios
- **THEN** no se crea ninguna captura

### Requirement: Acceso restringido
El sistema MUST exigir una sesión válida del proveedor de identidad de la plataforma en toda operación, MUST admitir únicamente cuentas cuyo correo verificado figure en la lista configurada, MUST limitar los intentos fallidos y MUST NOT revelar datos ni crear capturas sin una sesión autorizada.

#### Scenario: Sin sesión o con sesión inválida
- **WHEN** llega una petición sin sesión válida o con una sesión caducada
- **THEN** se rechaza sin crear nada y la página pide iniciar sesión conservando las notas pendientes

#### Scenario: Cuenta no autorizada
- **WHEN** la sesión es válida pero el correo no figura en la lista permitida
- **THEN** se rechaza con un error de acceso denegado, sin crear nada

#### Scenario: Cuenta autorizada
- **WHEN** la sesión pertenece a un correo verificado de la lista
- **THEN** la operación se permite

#### Scenario: Intentos repetidos
- **WHEN** hay muchos intentos fallidos seguidos desde el mismo origen
- **THEN** se bloquean temporalmente nuevos intentos

#### Scenario: Proveedor de identidad no disponible
- **WHEN** el proveedor de identidad no responde
- **THEN** el servicio responde con un error temporal, no cuenta como intento fallido y la nota sigue guardada en el dispositivo

### Requirement: No perder notas sin conexión
La página MUST conservar en el dispositivo toda nota no confirmada por el servidor y reenviarla cuando haya conexión, sin crear duplicados.

#### Scenario: Sin red al enviar
- **WHEN** el usuario envía una nota sin conexión
- **THEN** la nota queda guardada en el dispositivo marcada como pendiente de envío y se envía al recuperarse la conexión

#### Scenario: Reenvío tras un corte
- **WHEN** el servidor creó la captura pero la respuesta no llegó
- **THEN** el reenvío no crea una segunda captura

### Requirement: Dictado opcional y transparente
La página MUST ofrecer el dictado como opción, MUST permitir escribir sin él, MUST avisar de que el audio lo procesa el proveedor del navegador y MUST seguir funcionando para escribir si el navegador no admite dictado.

#### Scenario: Navegador sin dictado
- **WHEN** el navegador no admite reconocimiento de voz
- **THEN** el botón de dictado no se ofrece y se puede escribir la nota

### Requirement: Alcance mínimo
El servicio MUST limitarse a crear capturas: no MUST leer, modificar ni borrar capturas, registros ni preguntas existentes, y MUST escribir solo en el directorio de capturas.

#### Scenario: Operación no soportada
- **WHEN** se intenta leer o modificar una captura existente a través del servicio
- **THEN** la operación no existe y no se modifica nada

### Requirement: Sesión en la página
La página MUST permitir iniciar y cerrar sesión con Google, MUST renovar la sesión antes de que caduque mientras el token de refresco sea válido y MUST NOT perder ninguna nota por caducidad de sesión.

#### Scenario: Renovación
- **WHEN** el token de acceso está a punto de caducar y hay token de refresco
- **THEN** la página lo renueva sin intervención del usuario

#### Scenario: Sesión caducada con notas pendientes
- **WHEN** no se puede renovar la sesión
- **THEN** la página pide iniciar sesión, conserva las notas y las envía tras iniciar sesión
