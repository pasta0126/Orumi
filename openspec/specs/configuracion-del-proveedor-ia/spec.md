# configuracion-del-proveedor-ia Specification

## Purpose
Hacer el proveedor y el modelo de IA configurables y sustituibles, y convertir la garantía de privacidad exigida en algo declarado, fechado y comprobado antes de enviar cualquier dato.

## Requirements

### Requirement: Proveedor y modelo configurables
El proveedor y el modelo de IA MUST indicarse en la configuración y MUST poder sustituirse sin cambiar el contrato de comportamiento de Mira ni el formato de los ficheros.

#### Scenario: Cambio de proveedor
- **WHEN** el usuario cambia el proveedor o el modelo en la configuración
- **THEN** Mira funciona igual y los lotes siguientes registran el nuevo proveedor y modelo

### Requirement: Garantía de privacidad declarada y vigente
La configuración MUST incluir, por cada proveedor usable, la declaración de que sus condiciones impiden reutilizar los datos para entrenar o mejorar productos, junto con la fecha de verificación y la fuente, y el sistema MUST NOT usar un proveedor sin esa declaración o con una verificación más antigua que el máximo configurable (por defecto 180 días).

#### Scenario: Declaración ausente
- **WHEN** el proveedor configurado no tiene garantía declarada
- **THEN** no se envía ningún dato, las capturas siguen pendientes y el lote lo indica

#### Scenario: Verificación caducada
- **WHEN** la fecha de verificación supera el máximo permitido
- **THEN** no se envía ningún dato hasta que el usuario la renueve y se avisa en el lote

### Requirement: Comprobación antes de cada ejecución
El sistema MUST comprobar la garantía y la presencia de la clave de API antes de cada ejecución, y MUST fallar sin enviar datos si alguna comprobación no se cumple.

#### Scenario: Clave ausente
- **WHEN** no está disponible la clave de API del proveedor
- **THEN** la ejecución termina sin enviar datos y lo indica

### Requirement: Clave de API fuera de los ficheros versionados
La clave de API MUST proporcionarse por el entorno o un fichero de secretos con permisos restringidos y MUST NOT escribirse en la configuración versionada, en los lotes ni en los registros técnicos.

#### Scenario: Revisión de lotes y logs
- **WHEN** se revisan lotes y logs tras una ejecución
- **THEN** no aparece la clave de API
