## ADDED Requirements

### Requirement: Crear una captura de texto desde la web
El sistema MUST permitir enviar un texto escrito desde la página de captura y crear con él una captura inmutable idéntica, en formato y validaciones, a la creada por línea de comandos, devolviendo su identificador. Toda captura creada desde la web MUST tener origen texto.

#### Scenario: Nota escrita
- **WHEN** el usuario escribe una nota (incluido el texto introducido con el voz a texto del teclado del dispositivo) y la envía
- **THEN** se crea una captura con origen texto y la página muestra su identificador

#### Scenario: Metadatos opcionales
- **WHEN** el usuario indica una fecha del acontecimiento o una etiqueta
- **THEN** se guardan en la captura; si la fecha no es válida, no se crea nada y se informa

#### Scenario: Texto vacío
- **WHEN** el texto está vacío o solo tiene espacios
- **THEN** no se crea ninguna captura

#### Scenario: Indicador de voz heredado
- **WHEN** llega una petición que aún incluye el indicador de voz (página antigua en caché)
- **THEN** se ignora y la captura se crea con origen texto

## REMOVED Requirements

### Requirement: Crear una captura desde la web
**Reason**: sustituido por «Crear una captura de texto desde la web», sin el escenario de nota dictada.
**Migration**: ninguna; las capturas web existentes con origen voz permanecen intactas.

### Requirement: Dictado opcional y transparente
**Reason**: el dictado del navegador no funciona de forma fiable y es innecesario: el teclado del teléfono ofrece voz a texto nativo. Retirarlo elimina además el único envío de audio a terceros (Google o Apple).
**Migration**: escribir la nota, o usar el voz a texto del teclado del dispositivo. No hay datos que migrar.
