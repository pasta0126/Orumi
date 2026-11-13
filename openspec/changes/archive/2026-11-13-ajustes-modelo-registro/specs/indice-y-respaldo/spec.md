## MODIFIED Requirements

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
