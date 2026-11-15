## ADDED Requirements

### Requirement: Descubrimientos y revisión de hipótesis
El panel MUST ofrecer una sección de descubrimientos con la última revisión semanal y los contrastes de hipótesis con su evidencia enlazada, MUST permitir declarar, confirmar, matizar y descartar, y MUST someter estas operaciones a las mismas garantías de acceso, límites y convivencia con Mira que el resto de escrituras.

#### Scenario: Ver la revisión
- **WHEN** el usuario abre descubrimientos
- **THEN** ve la última revisión semanal, o el progreso de datos si aún está en observación

#### Scenario: Seguir la evidencia
- **WHEN** el usuario abre un contraste
- **THEN** navega a los registros que lo sustentan

#### Scenario: Revisar desde el panel
- **WHEN** el usuario confirma, matiza o descarta un contraste
- **THEN** el cambio queda en el historial con origen usuario

#### Scenario: Mira procesando
- **WHEN** Mira está procesando
- **THEN** la escritura se rechaza con conflicto, como el resto de escrituras
