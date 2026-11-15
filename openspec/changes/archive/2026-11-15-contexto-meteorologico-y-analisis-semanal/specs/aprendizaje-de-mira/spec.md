## ADDED Requirements

### Requirement: Mira aprende de cómo se cierran sus preguntas
Mira MUST registrar cada cierre de pregunta (respondida, «no lo recuerdo», «no relevante» o «no sucedió») asociado a su clase (campo y tema) y MUST usar ese historial para ajustar qué pregunta y con qué prioridad. El usuario MUST poder cerrar como «no relevante» o «no sucedió» cualquier pregunta sin penalización: cerrar nunca hace que Mira deje de preguntar en general.

#### Scenario: Cierre registrado
- **WHEN** el usuario cierra una pregunta como no relevante
- **THEN** el cierre queda anotado con su clase y su fecha, y esa pregunta concreta no se repite

#### Scenario: Cierres repetidos de una clase
- **WHEN** una clase de pregunta acumula al menos 3 cierres «no relevante» en al menos 2 días distintos
- **THEN** Mira baja la prioridad de esa clase y lo deja anotado como aprendizaje con su evidencia

#### Scenario: Respuesta tras bajar la prioridad
- **WHEN** el usuario responde con datos a una pregunta de una clase de prioridad reducida
- **THEN** Mira restablece la prioridad de esa clase

#### Scenario: No sucedió
- **WHEN** el usuario cierra una pregunta como «no sucedió»
- **THEN** el hecho preguntado queda como ausente confirmado por el usuario y no se vuelve a preguntar por ese día

### Requirement: Lo aprendido es visible, trazable y reversible
Cada regla aprendida MUST guardarse en un fichero propio, legible, con su descripción, su evidencia (los cierres que la sustentan), su fecha y su estado, y MUST poder verse, corregirse, desactivarse o borrarse por el usuario por terminal y desde el panel. Mira MUST NOT aplicar aprendizajes ocultos y MUST NOT dejar de formular preguntas por completo.

#### Scenario: Ver lo aprendido
- **WHEN** el usuario consulta los aprendizajes
- **THEN** ve cada regla con la evidencia que la sustenta

#### Scenario: Revertir un aprendizaje
- **WHEN** el usuario desactiva una regla aprendida
- **THEN** Mira deja de aplicarla desde ese momento y la conserva en el historial

#### Scenario: Aprendizaje sin evidencia suficiente
- **WHEN** hay menos cierres que el mínimo
- **THEN** no se crea regla

### Requirement: Las preferencias aprendidas guían a Mira
Las reglas aprendidas activas MUST aplicarse de forma determinista a la selección y orden de preguntas y MUST incluirse como preferencias del usuario en las instrucciones enviadas al modelo, sin incluir los textos de los cierres más allá de lo necesario. Una preferencia aprendida MUST NOT permitir inventar datos ni contradecir lo dicho por el usuario.

#### Scenario: Selección de preguntas
- **WHEN** hay más candidatas que el máximo por revisión
- **THEN** las de clases con prioridad reducida quedan por detrás de las demás

### Requirement: La evolución de Mira es medible
La revisión semanal MUST incluir un apartado «Qué ha aprendido Mira» con las reglas nuevas o cambiadas y la evolución de preguntas formuladas, respondidas y cerradas como no relevantes o no sucedidas, para que el usuario pueda comprobar si Mira mejora.

#### Scenario: Revisión semanal
- **WHEN** se genera la revisión semanal
- **THEN** incluye los aprendizajes de la semana y las tasas de respuesta y de «no relevante» frente a las semanas anteriores
