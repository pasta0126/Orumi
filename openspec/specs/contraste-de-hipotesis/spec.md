# contraste-de-hipotesis Specification

## Purpose
TBD - created by archiving change contexto-meteorologico-y-analisis-semanal. Update Purpose after archive.

## Requirements

### Requirement: Hipótesis declaradas por el usuario
El usuario MUST poder declarar hipótesis propias con un factor, un efecto y, opcionalmente, un retardo en días, y el sistema MUST conservar su formulación literal con certeza «dicho por el usuario» y estado «por verificar». Mira MUST NOT tratar una hipótesis declarada como un hecho ni crear hipótesis propias en esta change.

#### Scenario: Declarar una hipótesis
- **WHEN** el usuario declara que un índice UV alto con luz fuerte le provoca dolor de cabeza
- **THEN** se guarda con su formulación literal, su factor y su efecto, en estado por verificar

#### Scenario: Retardo
- **WHEN** el usuario declara que tras socializar se siente más solitario o introspectivo en los dos días siguientes
- **THEN** la hipótesis guarda el retardo y el análisis busca el efecto en esa ventana

### Requirement: Etiquetado solo de lo dicho
Para contrastar, Mira MUST etiquetar en los registros la presencia de cada factor y efecto basándose únicamente en lo que el usuario dijo, MUST guardar las etiquetas como derivadas y regenerables con certeza «interpretación no verificada», y MUST NOT inferir un estado no mencionado. Una ausencia solo MUST considerarse establecida si el usuario la dijo o la confirmó respondiendo «no sucedió».

#### Scenario: Efecto no mencionado
- **WHEN** un registro no menciona cansancio ni siestas
- **THEN** no se etiqueta como ausencia de cansancio: el día queda sin dato para ese efecto, y Mira puede preguntar

#### Scenario: Ausencia confirmada
- **WHEN** el usuario cierra la pregunta de un efecto como «no sucedió»
- **THEN** ese día cuenta como ausencia confirmada del efecto

#### Scenario: Corrección del usuario
- **WHEN** el usuario corrige una etiqueta
- **THEN** la corrección prevalece y se respeta en análisis posteriores

### Requirement: Definiciones de los factores del usuario
El factor «larga exposición al sol» MUST darse solo cuando el usuario indique más de 1,5 horas al exterior en un día soleado o despejado según el clima de su ciudad en esas horas; si indica haber estado fuera sin duración, el día queda sin dato y Mira puede preguntar. El factor «socialización» MUST darse solo cuando el usuario indique que socializó; el trabajo ordinario (por defecto de 08:30 a 18:30 en días laborables, configurable) MUST NOT contarse como socialización. Sus efectos son el cansancio físico posterior y, en la socialización, el agotamiento y después más soledad o introspección.

#### Scenario: Exposición suficiente
- **WHEN** el usuario indica tres horas al exterior en un día despejado
- **THEN** el día cuenta como larga exposición al sol

#### Scenario: Exposición corta o sin duración
- **WHEN** el usuario indica una hora al exterior, o que estuvo fuera sin decir cuánto
- **THEN** no cuenta como larga exposición; en el segundo caso queda sin dato

#### Scenario: Jornada de trabajo
- **WHEN** el día solo contiene la jornada laboral
- **THEN** no cuenta como socialización

#### Scenario: Socialización indicada
- **WHEN** el usuario indica que quedó con amigos
- **THEN** el día cuenta como socialización y se busca el efecto en la ventana de retardo

### Requirement: Evidencia mínima y contraste en código
Una hipótesis MUST considerarse contrastable solo con ≥ 3 registros del efecto en ≥ 2 semanas distintas (umbrales configurables, X20). La comparación entre días con y sin factor MUST calcularse con código determinista, no con el modelo, y MUST resolverse como *apoyada*, *sin apoyo* o *evidencia insuficiente*, sin afirmar causalidad.

#### Scenario: Pocos datos
- **WHEN** no se alcanza la evidencia mínima
- **THEN** el resultado es «evidencia insuficiente» con el recuento actual, sin conclusión

#### Scenario: Hipótesis sin apoyo
- **WHEN** los datos suficientes no muestran diferencia entre días con y sin factor
- **THEN** se informa «sin apoyo» con la misma claridad que un resultado favorable

#### Scenario: Sin datos del clima
- **WHEN** faltan datos meteorológicos de los días implicados
- **THEN** esos días se excluyen y el resultado indica cuántos se excluyeron

#### Scenario: Días sin dato del efecto
- **WHEN** un día con factor no tiene efecto mencionado ni ausencia confirmada
- **THEN** no cuenta como ausencia, se indica cuántos días quedan sin dato y Mira puede preguntar por ellos

### Requirement: Análisis semanal trazable y sobrio
Mira MUST generar una revisión semanal con un contraste por hipótesis, cada uno con los registros y días que lo sustentan, MUST mostrar solo frecuencia observada y evidencia (X23), y MUST NOT dar recomendaciones ni consejos. Durante la etapa de observación (~4 semanas desde el primer registro) MUST informar solo del progreso de datos.

#### Scenario: Etapa de observación
- **WHEN** no han pasado cuatro semanas de registros
- **THEN** la revisión indica cuántos datos hay y cuántos faltan, sin contrastes

#### Scenario: Trazabilidad
- **WHEN** el usuario abre un contraste
- **THEN** ve los registros y días que lo sustentan y los datos del clima usados

#### Scenario: Sin consejos
- **WHEN** una hipótesis resulta apoyada
- **THEN** el texto describe lo observado y no sugiere qué hacer

### Requirement: Revisión por el usuario
El usuario MUST poder confirmar, matizar o descartar un contraste. Lo descartado MUST NOT volver a proponerse salvo evidencia nueva sustancial, y lo matizado MUST reescribirse con su comentario, que prevalece sobre la interpretación de Mira.

#### Scenario: Descartar
- **WHEN** el usuario descarta un contraste
- **THEN** no vuelve a mostrarse mientras no haya evidencia nueva sustancial

#### Scenario: Matizar
- **WHEN** el usuario matiza un contraste con un comentario
- **THEN** se conserva su comentario, con origen usuario, junto al historial
