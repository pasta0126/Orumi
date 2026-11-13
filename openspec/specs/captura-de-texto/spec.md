# captura-de-texto Specification

## Purpose
Permitir entregar una nota de texto a ORUMI con un solo comando, creando una captura inmutable con su identidad y su huella, sin necesidad de la app de voz.

## Requirements

### Requirement: Crear una captura desde texto
El sistema MUST permitir crear una captura a partir de texto recibido por argumento, fichero o entrada estándar, guardando el contenido literal con identificador, fecha y hora de comunicación, origen texto, dispositivo y huella del contenido, según el formato vigente.

#### Scenario: Texto por entrada estándar
- **WHEN** el usuario entrega una nota por entrada estándar
- **THEN** se crea una captura cuyo cuerpo es idéntico al texto recibido y cuya huella coincide con él

#### Scenario: Salto de línea y erratas
- **WHEN** el texto contiene erratas, saltos de línea o espacios finales
- **THEN** se conservan tal cual en la captura

### Requirement: Rechazo de texto vacío
El sistema MUST NOT crear una captura si el texto está vacío o solo contiene espacios.

#### Scenario: Nota vacía
- **WHEN** el texto recibido está vacío
- **THEN** no se crea ningún fichero y se informa del motivo

### Requirement: Metadatos opcionales
El sistema MUST permitir indicar la fecha del acontecimiento y una etiqueta, los guardará como información dicha por el usuario, y MUST NOT registrar ubicación.

#### Scenario: Fecha del acontecimiento
- **WHEN** el usuario indica que la nota trata de un día anterior
- **THEN** la captura guarda esa fecha y la de comunicación no cambia

### Requirement: Sin sobrescritura
El sistema MUST NOT sobrescribir ni modificar una captura existente; si el identificador generado ya existe, MUST generar otro.

#### Scenario: Colisión de identificador
- **WHEN** el identificador generado coincide con el de una captura existente
- **THEN** se genera uno distinto y la captura existente permanece intacta

### Requirement: Confirmación tras verificar
El sistema MUST confirmar la creación solo después de escribir el fichero y comprobar que su contenido y su huella son correctos, indicando el identificador y la ruta.

#### Scenario: Fallo de escritura
- **WHEN** el fichero no se puede escribir o verificar
- **THEN** no se confirma, no queda un fichero a medias y se informa del error
