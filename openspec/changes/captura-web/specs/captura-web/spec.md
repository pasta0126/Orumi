## Purpose

Permitir crear capturas desde cualquier dispositivo con un navegador, escribiendo o dictando, sin terminal, conservando las garantías de inmutabilidad y trazabilidad de las capturas.

## ADDED Requirements

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
El sistema MUST exigir un token de acceso en toda operación, MUST limitar los intentos fallidos y MUST NOT revelar datos ni crear capturas sin un token válido.

#### Scenario: Sin token o con token inválido
- **WHEN** llega una petición sin token válido
- **THEN** se rechaza sin crear nada y sin indicar qué falló

#### Scenario: Intentos repetidos
- **WHEN** hay muchos intentos fallidos seguidos desde el mismo origen
- **THEN** se bloquean temporalmente nuevos intentos

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
