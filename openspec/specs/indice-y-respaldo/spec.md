# indice-y-respaldo Specification

## Purpose
Permitir consultas estructuradas sobre el historial sin convertir la base de consulta en fuente de verdad, y mantener los datos de ORUMI aislados del repositorio personal vedlvm.

## Requirements

### Requirement: Los ficheros son la fuente de verdad
Los ficheros de `orumi/` MUST ser la fuente de verdad del historial; cualquier índice o base de consulta MUST ser derivado y reconstruible solo a partir de ellos.

#### Scenario: Reconstrucción
- **WHEN** se elimina el índice derivado
- **THEN** puede regenerarse por completo desde los ficheros sin pérdida de información

### Requirement: Ediciones propagadas a los ficheros
Toda modificación hecha desde el panel o desde el agente MUST persistirse en los ficheros correspondientes.

#### Scenario: Corrección desde el panel
- **WHEN** el usuario corrige un registro en el panel
- **THEN** el fichero del registro se actualiza y el índice se refresca a partir de él

### Requirement: Consultas estructuradas
El índice derivado MUST permitir consultar registros por fecha, hora, tipo, certeza, relación, proyecto, entidad y estado de las preguntas pendientes.

#### Scenario: Consulta por certeza
- **WHEN** se piden los registros con datos solo interpretados
- **THEN** se devuelven únicamente los registros que contienen datos con esa certeza

#### Scenario: Consulta por entidad
- **WHEN** se piden los registros relacionados con una entidad, escrita con o sin acentos ni mayúsculas
- **THEN** se devuelven todos los registros que la mencionan, con su certeza

#### Scenario: Consulta por hora
- **WHEN** se piden los registros de una franja horaria de un día
- **THEN** se devuelven los registros con hora dentro de esa franja y no los que carecen de hora

### Requirement: Separación de datos
Los datos de ORUMI MUST residir en `orumi/`, separados de las notas personales de vedlvm, y MUST NOT copiarse ni publicarse en vedlvm ni en su remoto.

#### Scenario: Contenido ajeno
- **WHEN** se escribe un registro, una captura o un informe
- **THEN** se guarda en `orumi/` y no en el árbol de notas de vedlvm

#### Scenario: Sin salida hacia vedlvm
- **WHEN** Mira genera un informe de análisis o revisión
- **THEN** no se crea ninguna copia en vedlvm
