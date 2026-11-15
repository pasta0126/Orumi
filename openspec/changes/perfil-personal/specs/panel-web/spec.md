## ADDED Requirements

### Requirement: Sección de perfil
El panel MUST ofrecer una sección de perfil que permita solicitar su generación, ver su calendario y la última ejecución u omisión,, leer cada rasgo con su certeza y su evidencia enlazada, corregirlo, matizarlo, descartarlo y borrar el perfil entero, exigiendo en todas las operaciones la misma sesión autorizada, los mismos límites y la misma protección que el resto de la API, y MUST NOT servirlo de forma pública ni cachearlo.

#### Scenario: Pedir una versión
- **WHEN** el usuario pide generar el perfil desde el panel
- **THEN** se lanza una única generación y la página muestra el resultado o el progreso de datos

#### Scenario: Revisar un rasgo
- **WHEN** el usuario abre un rasgo
- **THEN** ve su categoría de certeza y puede ir a los registros que lo sustentan

#### Scenario: Sin sesión autorizada
- **WHEN** se solicita el perfil sin una sesión de un correo autorizado
- **THEN** se rechaza sin devolver datos

#### Scenario: Borrar el perfil
- **WHEN** el usuario confirma el borrado
- **THEN** se elimina el perfil y su historial
