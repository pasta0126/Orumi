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
Una pregunta MUST cerrarse como respondida, «no lo recuerdo», «no relevante», «no sucedió» o por límite, y Mira MUST NOT repetir una pregunta cerrada. Tras un cierre «no lo recuerdo» o «no relevante», Mira MUST NOT formular preguntas equivalentes sobre ese mismo dato; para las de la misma clase sobre otros datos aplica el aprendizaje de Mira, que reduce su prioridad pero no las prohíbe.

#### Scenario: Respuesta
- **WHEN** el usuario responde
- **THEN** el dato del registro pasa a confirmado y la respuesta queda como evidencia

#### Scenario: No relevante
- **WHEN** el usuario cierra la pregunta como no relevante
- **THEN** no se repite ni se formulan otras parecidas sobre ese dato, y el cierre cuenta para el aprendizaje de Mira

#### Scenario: No sucedió
- **WHEN** el usuario cierra la pregunta como «no sucedió»
- **THEN** no se repite y el hecho queda como ausente confirmado por el usuario

### Requirement: Solo datos estructurales
Mira MUST preguntar únicamente por datos estructurales del registro (fecha, hora, duración, personas, lugar, ciudad, proyecto, estado o resultado) o, para contrastar una hipótesis declarada por el usuario, por la presencia o ausencia de un factor o efecto ya declarado, y MUST NOT preguntar por el contenido sustantivo de lo hablado o decidido.

#### Scenario: Reunión con acuerdos sin detallar
- **WHEN** la captura dice que se llegó a acuerdos sin indicarlos
- **THEN** Mira no pregunta cuáles fueron

### Requirement: Tono
La formulación MUST ser cercana, serena y neutra, y MUST NOT incluir consejos, valoraciones ni recomendaciones no solicitadas.

#### Scenario: Pregunta sin consejo
- **WHEN** Mira pregunta por el estado de una incidencia
- **THEN** la pregunta no sugiere qué hacer ni valora lo ocurrido

### Requirement: Preguntas de contraste
Para completar el contraste de una hipótesis, Mira MUST poder preguntar por la presencia de un factor o un efecto declarados por el usuario, solo en días en que el factor esté presente o falte el dato del efecto, solo mientras la hipótesis no tenga evidencia suficiente, y dentro del máximo de preguntas por revisión. Mira MUST NOT sugerir la respuesta ni dar consejos.

#### Scenario: Día de UV alto sin mención de dolor de cabeza
- **WHEN** un día tuvo UV alto, el usuario estuvo al exterior según sus registros y no menciona dolor de cabeza
- **THEN** Mira puede preguntar si tuvo dolor de cabeza ese día

#### Scenario: Hipótesis con evidencia suficiente
- **WHEN** la hipótesis ya alcanzó la evidencia mínima
- **THEN** Mira no formula nuevas preguntas de contraste para ella

#### Scenario: Pregunta de exposición
- **WHEN** un registro indica que el usuario estuvo fuera pero no cuánto
- **THEN** Mira puede preguntar la duración
