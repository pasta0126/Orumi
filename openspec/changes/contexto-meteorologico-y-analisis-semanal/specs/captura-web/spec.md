## ADDED Requirements

### Requirement: Indicar la ciudad en la web
La página de captura MUST ofrecer un campo opcional de ciudad, MUST recordar la última ciudad indicada solo en el dispositivo como sugerencia editable, y MUST NOT usar la geolocalización del navegador.

#### Scenario: Ciudad indicada
- **WHEN** el usuario escribe una ciudad y envía la nota
- **THEN** la captura guarda esa ciudad

#### Scenario: Sin geolocalización
- **WHEN** el usuario no indica ciudad
- **THEN** la página no solicita permiso de ubicación y la captura no guarda ciudad
