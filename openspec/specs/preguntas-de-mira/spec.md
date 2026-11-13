# preguntas-de-mira Specification

## Purpose
Definir cuándo y cómo Mira formula, ordena y cierra preguntas para completar los registros sin interrumpir al usuario ni convertir el registro en un cuestionario.

## Requirements

### Requirement: Preguntas pendientes y asíncronas
Mira MUST formular las preguntas como pendientes, asociadas a un registro y a un campo, y MUST NOT interrumpir al usuario ni bloquear el registro mientras no se respondan.

#### Scenario: Pregunta sin respuesta
- **WHEN** el usuario no responde una pregunta
- **THEN** el registro sigue siendo válido y consultable

#### Scenario: Pregunta ligada al dato
- **WHEN** Mira crea una pregunta
- **THEN** indica el registro y el campo que pretende completar

### Requirement: Máximo por revisión y orden por valor
Mira MUST limitar el número de preguntas pendientes que presenta en cada revisión a un máximo configurable, ordenadas por valor, y MUST descartar las candidatas de menor valor cuando se supere.

#### Scenario: Más candidatas que el máximo
- **WHEN** hay más preguntas candidatas que el máximo configurado
- **THEN** solo se crean las de mayor valor

#### Scenario: Valor de una pregunta
- **WHEN** Mira prioriza
- **THEN** da más valor a conflictos y a datos que dejan abierto un asunto (por ejemplo, si una incidencia se resolvió) que a datos accesorios

### Requirement: Cierre y no repetición
Una pregunta MUST cerrarse como respondida, «no lo recuerdo», «no relevante» o por límite, y Mira MUST NOT repetirla ni formular otras equivalentes tras un cierre «no lo recuerdo» o «no relevante».

#### Scenario: Respuesta
- **WHEN** el usuario responde
- **THEN** el dato del registro pasa a confirmado y la respuesta queda como evidencia

#### Scenario: No relevante
- **WHEN** el usuario cierra la pregunta como no relevante
- **THEN** no se repite ni se formulan otras parecidas sobre ese dato

### Requirement: Solo datos estructurales
Mira MUST preguntar únicamente por datos estructurales del registro (fecha, hora, duración, personas, lugar, proyecto, estado o resultado) y MUST NOT preguntar por el contenido sustantivo de lo hablado o decidido.

#### Scenario: Reunión con acuerdos sin detallar
- **WHEN** la captura dice que se llegó a acuerdos sin indicarlos
- **THEN** Mira no pregunta cuáles fueron

### Requirement: Tono
La formulación MUST ser cercana, serena y neutra, y MUST NOT incluir consejos, valoraciones ni recomendaciones no solicitadas.

#### Scenario: Pregunta sin consejo
- **WHEN** Mira pregunta por el estado de una incidencia
- **THEN** la pregunta no sugiere qué hacer ni valora lo ocurrido
