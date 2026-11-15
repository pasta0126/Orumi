## MODIFIED Requirements

### Requirement: Metadatos opcionales
Una captura MAY incluir la fecha del acontecimiento, una etiqueta o proyecto, la ciudad indicada por el usuario y contexto automático de hora y dispositivo. El sistema MUST NOT obtener la ubicación del dispositivo ni monitorizar la actividad del usuario, y MUST tratar los metadatos aportados por el usuario como información dicha por él.

#### Scenario: Fecha indicada por el usuario
- **WHEN** el usuario indica que la captura trata de un día anterior
- **THEN** la captura guarda esa fecha del acontecimiento y la fecha de comunicación no cambia

#### Scenario: Sin metadatos
- **WHEN** el usuario entrega solo el contenido
- **THEN** la captura es válida con el contexto automático de hora y dispositivo

#### Scenario: Ciudad indicada por el usuario
- **WHEN** el usuario indica que está en otra ciudad
- **THEN** la captura guarda esa ciudad como dicha por él

#### Scenario: Sin ubicación
- **WHEN** se guarda cualquier captura
- **THEN** no contiene coordenadas ni datos de ubicación del dispositivo
