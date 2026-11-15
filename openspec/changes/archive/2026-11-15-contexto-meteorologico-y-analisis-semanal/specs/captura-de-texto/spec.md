## MODIFIED Requirements

### Requirement: Metadatos opcionales
El sistema MUST permitir indicar la fecha del acontecimiento, una etiqueta y la ciudad donde ocurrió, y los guardará como información dicha por el usuario. El sistema MUST NOT obtener la ubicación del dispositivo ni deducirla; la ciudad solo procede de lo que el usuario indique.

#### Scenario: Fecha del acontecimiento
- **WHEN** el usuario indica que la nota trata de un día anterior
- **THEN** la captura guarda esa fecha y la de comunicación no cambia

#### Scenario: Ciudad indicada
- **WHEN** el usuario indica la ciudad
- **THEN** la captura la guarda como dicha por el usuario

#### Scenario: Sin ciudad
- **WHEN** el usuario no indica ciudad
- **THEN** la captura no guarda ciudad y se interpreta como la ciudad por defecto de la configuración
