## Purpose

Garantizar que lo que el usuario comunica se conserva tal cual, sin alteraciones, como base verificable sobre la que se apoyan todas las interpretaciones posteriores.

## ADDED Requirements

### Requirement: Captura cruda inmutable
El sistema MUST guardar cada comunicación del usuario como una captura con el texto literal recibido, sin limpieza, resumen ni interpretación, y MUST NOT modificarla después de guardada.

#### Scenario: Se guarda el texto literal
- **WHEN** el usuario entrega una nota o una transcripción
- **THEN** el sistema guarda una captura cuyo contenido es idéntico al recibido

#### Scenario: Una captura no se edita
- **WHEN** el usuario corrige un dato derivado de una captura
- **THEN** la captura permanece intacta y la corrección se registra en el registro afectado

### Requirement: Identidad y metadatos de la captura
Cada captura MUST tener un identificador único y estable, la fecha y hora en que se comunicó, y su origen (texto o voz transcrita).

#### Scenario: Metadatos presentes
- **WHEN** se guarda una captura
- **THEN** incluye identificador, fecha y hora de comunicación y origen

#### Scenario: Fecha del acontecimiento distinta de la de comunicación
- **WHEN** la captura menciona algo ocurrido otro día
- **THEN** la fecha de comunicación de la captura no se altera

### Requirement: Transcripción sin interpretación
Cuando el origen es voz, el sistema MUST conservar la transcripción literal como original y MUST NOT eliminar el audio hasta confirmar que la transcripción fue guardada.

#### Scenario: Audio borrado tras guardar
- **WHEN** la transcripción se ha guardado correctamente
- **THEN** el audio puede eliminarse y la transcripción queda como original

#### Scenario: Fallo al guardar
- **WHEN** la transcripción no se puede guardar
- **THEN** el audio se conserva y la captura se marca como pendiente

### Requirement: Recuperación independiente del agente
Las capturas MUST poder leerse sin depender del agente ni del índice derivado.

#### Scenario: Lectura directa
- **WHEN** se abre un fichero de captura sin ninguna herramienta de ORUMI
- **THEN** su contenido y metadatos son legibles como texto

### Requirement: Metadatos opcionales
Una captura MAY incluir la fecha del acontecimiento, una etiqueta o proyecto, y contexto automático de hora y dispositivo. El sistema MUST NOT registrar ubicación ni monitorizar la actividad del usuario, y MUST tratar los metadatos aportados por el usuario como información dicha por él.

#### Scenario: Fecha indicada por el usuario
- **WHEN** el usuario indica que la captura trata de un día anterior
- **THEN** la captura guarda esa fecha del acontecimiento y la fecha de comunicación no cambia

#### Scenario: Sin metadatos
- **WHEN** el usuario entrega solo el contenido
- **THEN** la captura es válida con el contexto automático de hora y dispositivo

#### Scenario: Sin ubicación
- **WHEN** se guarda cualquier captura
- **THEN** no contiene datos de ubicación

