## Purpose

Dar al usuario control total sobre sus registros (deshacer, eliminar, restaurar, fusionar y separar) sin poner en riesgo las notas originales y sin que Mira deshaga lo hecho.

## ADDED Requirements

### Requirement: Deshacer la última corrección
El sistema MUST permitir revertir la última corrección o rechazo del usuario sobre un campo, restaurando el valor y la certeza anteriores y anotando la reversión en el historial; MUST rechazar la operación si no hay nada que deshacer.

#### Scenario: Deshacer una corrección
- **WHEN** el usuario deshace la última corrección de un campo
- **THEN** el campo recupera su valor y certeza anteriores y el historial anota la reversión con su origen usuario

#### Scenario: Sin cambios del usuario
- **WHEN** el campo no tiene correcciones del usuario pendientes de deshacer
- **THEN** no se modifica nada y se informa

#### Scenario: Deshacer un rechazo
- **WHEN** el usuario deshace el rechazo de una interpretación
- **THEN** el valor vuelve al campo con su certeza original y se retira de los rechazos

### Requirement: Eliminar a la papelera y restaurar
El sistema MUST permitir eliminar un registro dejándolo en estado eliminado (reversible) sin borrar su fichero ni su clave, MUST excluirlo de consultas, días, búsquedas y preguntas, MUST cerrar sus preguntas pendientes y resolver sus conflictos abiertos, MUST NOT modificar nunca las capturas y MUST permitir restaurarlo.

#### Scenario: Eliminar
- **WHEN** el usuario elimina un registro
- **THEN** el registro queda marcado como eliminado, desaparece de las vistas de datos y sus preguntas pendientes se cierran como no relevantes

#### Scenario: Restaurar
- **WHEN** el usuario restaura un registro eliminado
- **THEN** vuelve a estar activo con su contenido anterior

#### Scenario: Registro con conflictos
- **WHEN** se elimina un registro que tenía un conflicto abierto con otro
- **THEN** el conflicto queda resuelto en el otro registro

### Requirement: Fusionar registros
El sistema MUST permitir fusionar un registro origen en un destino, incorporando las fuentes, entradas y relaciones del origen y los datos que el destino no tenía, conservando el valor del destino cuando ambos difieren y anotando el otro en el historial; el origen MUST quedar como tumba fusionada que apunta al destino.

#### Scenario: Fusión
- **WHEN** el usuario fusiona un registro en otro
- **THEN** el destino contiene las fuentes y entradas de ambos, el origen queda marcado como fusionado en el destino y deja de aparecer en las vistas

#### Scenario: Valores distintos
- **WHEN** ambos registros tienen valores distintos para un campo
- **THEN** se conserva el del destino y el del origen queda anotado en el historial

#### Scenario: Fusión no válida
- **WHEN** el origen y el destino son el mismo registro, o alguno no está activo
- **THEN** no se modifica nada y se informa

### Requirement: Separar una captura de un registro
El sistema MUST permitir separar de un registro una captura que figura como ampliación, creando con ella un registro nuevo, retirándola de las fuentes y entradas del original, y MUST listar los campos que esa captura aportó al original para que el usuario los revise; MUST rechazar separar la fuente de origen del registro.

#### Scenario: Separar una ampliación
- **WHEN** el usuario separa una captura ampliada de un registro
- **THEN** existe un registro nuevo con esa captura como origen y el original ya no la lista como fuente

#### Scenario: Fuente de origen
- **WHEN** el usuario intenta separar la captura de origen del registro
- **THEN** no se modifica nada y se informa

### Requirement: Mira respeta lo eliminado, fusionado y separado
Un reprocesado de Mira MUST NOT recrear ni reactivar un registro eliminado o fusionado, MUST NOT mezclar sus datos en otros registros y MUST NOT volver a ampliar un registro con una captura que se separó de él.

#### Scenario: Reprocesar tras eliminar
- **WHEN** se reprocesa la captura de un registro eliminado y el modelo vuelve a extraer el mismo hecho
- **THEN** no se crea ningún registro nuevo ni se reactiva el eliminado

#### Scenario: Reprocesar tras separar
- **WHEN** se reprocesa una captura separada de un registro y el modelo vuelve a proponer ampliarlo
- **THEN** esa ampliación se ignora y el registro separado se conserva

### Requirement: Integridad de las operaciones de gestión
Toda operación MUST validar el resultado antes de escribir, escribir de forma atómica y entera (todos los ficheros o ninguno), no ejecutarse mientras Mira procesa, registrarse en el historial de los registros afectados con origen usuario y fallar con un mensaje claro si algo no existe o no es aplicable.

#### Scenario: Mira procesando
- **WHEN** hay una ejecución de Mira en curso
- **THEN** la operación se rechaza con aviso de reintentar, sin modificar nada

#### Scenario: Resultado inválido
- **WHEN** la operación dejaría algún fichero inválido
- **THEN** no se escribe ningún fichero

### Requirement: Papelera y vistas
El índice de consultas y el panel MUST ignorar los registros no activos en las vistas de datos, y el panel MUST ofrecer una papelera con los registros eliminados y la opción de restaurarlos.

#### Scenario: Papelera
- **WHEN** el usuario abre la papelera del panel
- **THEN** ve los registros eliminados con la fecha de eliminación y puede restaurarlos
