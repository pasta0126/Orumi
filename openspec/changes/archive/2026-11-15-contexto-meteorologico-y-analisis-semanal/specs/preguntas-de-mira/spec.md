## MODIFIED Requirements

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

## ADDED Requirements

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
