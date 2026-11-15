## ADDED Requirements

### Requirement: El repaso se somete a las mismas garantías
La capa del repaso que usa el modelo MUST enviar solo los registros con huecos y el texto mínimo necesario para formular la pregunta, MUST usar únicamente el proveedor y las garantías ya exigidas a Mira, MUST NOT enviar clima, audio, ubicación ni datos de otros destinos y MUST dejar constancia del proveedor y del modelo. Si no puede asegurar las garantías, MUST NOT enviar nada y MUST mantener solo la capa determinista, avisando.

#### Scenario: Repaso con garantías vigentes
- **WHEN** las garantías están vigentes
- **THEN** solo salen los registros con huecos y el repaso registra proveedor y modelo

#### Scenario: Sin garantías
- **WHEN** la garantía de privacidad no está vigente
- **THEN** no se envía nada al proveedor y se ejecuta solo la capa determinista
