## MODIFIED Requirements

### Requirement: Acceso restringido
El sistema MUST exigir una sesión válida del proveedor de identidad de la plataforma en toda operación, MUST admitir únicamente cuentas cuyo correo verificado figure en la lista configurada, MUST limitar los intentos fallidos y MUST NOT revelar datos ni crear capturas sin una sesión autorizada.

#### Scenario: Sin sesión o con sesión inválida
- **WHEN** llega una petición sin sesión válida o con una sesión caducada
- **THEN** se rechaza sin crear nada y la página pide iniciar sesión conservando las notas pendientes

#### Scenario: Cuenta no autorizada
- **WHEN** la sesión es válida pero el correo no figura en la lista permitida
- **THEN** se rechaza con un error de acceso denegado, sin crear nada

#### Scenario: Cuenta autorizada
- **WHEN** la sesión pertenece a un correo verificado de la lista
- **THEN** la operación se permite

#### Scenario: Intentos repetidos
- **WHEN** hay muchos intentos fallidos seguidos desde el mismo origen
- **THEN** se bloquean temporalmente nuevos intentos

#### Scenario: Proveedor de identidad no disponible
- **WHEN** el proveedor de identidad no responde
- **THEN** el servicio responde con un error temporal, no cuenta como intento fallido y la nota sigue guardada en el dispositivo

### Requirement: Sesión en la página
La página MUST permitir iniciar y cerrar sesión con Google, MUST renovar la sesión antes de que caduque mientras el token de refresco sea válido y MUST NOT perder ninguna nota por caducidad de sesión.

#### Scenario: Renovación
- **WHEN** el token de acceso está a punto de caducar y hay token de refresco
- **THEN** la página lo renueva sin intervención del usuario

#### Scenario: Sesión caducada con notas pendientes
- **WHEN** no se puede renovar la sesión
- **THEN** la página pide iniciar sesión, conserva las notas y las envía tras iniciar sesión
