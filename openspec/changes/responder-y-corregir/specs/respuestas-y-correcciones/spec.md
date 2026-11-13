## Purpose

Dar al usuario herramientas para contestar a las preguntas de Mira y para corregir o rechazar datos de sus registros, con trazabilidad completa y sin que Mira pise después lo que ha aportado.

## ADDED Requirements

### Requirement: Responder una pregunta con un valor
El sistema MUST permitir responder una pregunta pendiente con un valor que se interpreta según el campo por el que se preguntó, y MUST aplicarlo al registro como dato confirmado, registrar el cambio en el historial con origen usuario y enlazar la pregunta, guardar la respuesta como evidencia y cerrar la pregunta como respondida.

#### Scenario: Duración
- **WHEN** el usuario responde con «1h30» a una pregunta sobre la duración
- **THEN** el registro guarda la duración de 90 minutos como confirmada, el historial lo anota con la pregunta y esta queda respondida

#### Scenario: Fecha
- **WHEN** el usuario responde con una fecha a una pregunta sobre la fecha
- **THEN** el rango de fechas se sustituye por esa fecha, pasa a confirmado y se conserva la expresión original

#### Scenario: Lista de datos
- **WHEN** el usuario responde con varios nombres separados por comas a una pregunta sobre personas o proyectos
- **THEN** el registro guarda la lista como confirmada

#### Scenario: Valor no válido
- **WHEN** el valor no se puede interpretar para ese campo
- **THEN** no se modifica ningún fichero y se explica qué formato se espera

### Requirement: Cerrar una pregunta sin dato
El sistema MUST permitir cerrar una pregunta como «no lo recuerdo» o «no relevante» sin modificar el registro, y esas preguntas MUST NOT volver a formularse.

#### Scenario: No lo recuerdo
- **WHEN** el usuario cierra una pregunta como no lo recuerdo
- **THEN** la pregunta queda cerrada con esa respuesta y el dato del registro no cambia

### Requirement: Corregir un dato
El sistema MUST permitir corregir o completar un dato de un registro, que MUST quedar como confirmado con una entrada de historial de origen usuario con el valor anterior y el nuevo, y MUST NOT ser modificado después por un reprocesado de Mira.

#### Scenario: Corrección
- **WHEN** el usuario corrige el título o las personas de un registro
- **THEN** el registro refleja el nuevo valor como confirmado y el valor anterior queda en el historial

#### Scenario: Campo no corregible
- **WHEN** el usuario intenta corregir un campo que no admite corrección (identificador, fuentes, procedencia)
- **THEN** no se modifica nada y se informa

### Requirement: Rechazar una interpretación
El sistema MUST permitir rechazar el valor interpretado de un campo, guardándolo en los rechazos con su certeza original, dejando el campo como desconocido y anotando el cambio en el historial, y Mira MUST NOT volver a proponerlo sin evidencia nueva.

#### Scenario: Rechazo
- **WHEN** el usuario rechaza una interpretación de un registro
- **THEN** el valor queda en los rechazos, el campo pasa a desconocido y el historial lo anota

#### Scenario: Dato que no es una interpretación
- **WHEN** el campo no tiene un valor interpretado
- **THEN** no se modifica nada y se informa

### Requirement: Resolver conflictos y coincidencias dudosas
Al responder la pregunta de un conflicto indicando el registro al que corresponde el valor, el sistema MUST aplicar el valor a ese registro y marcar el conflicto como resuelto en los registros implicados; al responder la pregunta de una coincidencia dudosa, MUST admitir que son distintas (quedan separadas y relacionadas por nada) o la misma (queda anotada la relación entre ambos registros, sin fusionarlos).

#### Scenario: Conflicto resuelto
- **WHEN** el usuario responde a una pregunta de conflicto con el valor correcto y el registro al que corresponde
- **THEN** ese registro recibe el valor como confirmado y el conflicto figura como resuelto en ambos

#### Scenario: Coincidencia dudosa distinta
- **WHEN** el usuario responde que dos registros son distintos
- **THEN** ambos se mantienen sin cambios y la pregunta se cierra

### Requirement: Integridad de las operaciones
Toda operación MUST validar el resultado antes de escribirlo, escribir de forma atómica, no modificar nunca una captura, no ejecutarse mientras Mira procesa y fallar con un mensaje claro si el identificador no existe o la pregunta ya está cerrada.

#### Scenario: Identificador inexistente
- **WHEN** se indica una pregunta o un registro que no existe
- **THEN** no se modifica nada y se informa

#### Scenario: Mira está procesando
- **WHEN** hay una ejecución de Mira en curso
- **THEN** la operación se rechaza con el aviso de reintentar más tarde, sin modificar nada

### Requirement: Contexto para contestar
La consulta de preguntas pendientes MUST mostrar, junto a cada pregunta, el registro y el campo afectados, el valor actual y los registros implicados en un conflicto o una coincidencia.

#### Scenario: Pregunta con contexto
- **WHEN** el usuario consulta las preguntas pendientes
- **THEN** cada una indica su registro, su campo, el valor actual y, si procede, el otro registro
