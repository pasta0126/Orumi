# perfil-personal Specification

## Purpose
TBD - created by archiving change perfil-personal. Update Purpose after archive.

## Requirements

### Requirement: Ejecución periódica con datos suficientes y a petición
Noa MUST generar una nueva versión del perfil de forma automática cada dos semanas (configurable a mensual) solo si, desde la versión anterior, hay información nueva suficiente (umbral configurable de registros nuevos), MUST dejar constancia cuando omita una ejecución por falta de datos y MUST poder generarse también a petición explícita del usuario. MUST NOT ejecutarse dentro del procesamiento de Mira.

#### Scenario: Ejecución periódica con datos nuevos
- **WHEN** llega el momento programado y hay información nueva suficiente
- **THEN** Noa genera una nueva versión del perfil

#### Scenario: Sin información nueva suficiente
- **WHEN** llega el momento programado pero no hay información nueva suficiente
- **THEN** no se genera versión y se anota la omisión y el motivo

#### Scenario: A petición
- **WHEN** el usuario pide una versión fuera del calendario
- **THEN** Noa la genera si hay datos suficientes en conjunto

### Requirement: Etapa de observación y niveles de evidencia
Noa MUST NOT generar el perfil hasta que hayan pasado al menos cuatro semanas desde el primer registro (configurable), e informará del progreso de datos. Cada afirmación MUST pertenecer a un nivel de evidencia explícito: *patrón* (≥ 3 registros en ≥ 2 semanas distintas, X20), *indicio* (2 registros) o *conjetura* (un solo registro, o una deducción no respaldada por repetición). Las conjeturas MUST presentarse en un apartado propio, marcadas como de baja confianza, y MUST NOT usarse como base de un patrón ni de una recomendación.

#### Scenario: Antes de cuatro semanas
- **WHEN** aún no han pasado cuatro semanas de registros
- **THEN** Noa no genera perfil e indica los datos disponibles y los que faltan

#### Scenario: Deducción de un solo registro
- **WHEN** una deducción se apoya en un solo registro
- **THEN** figura como conjetura de baja confianza, separada de patrones e indicios

#### Scenario: Recomendación
- **WHEN** Noa formula una recomendación
- **THEN** se apoya solo en patrones o indicios, nunca en conjeturas

### Requirement: Certeza y trazabilidad de cada rasgo
Cada rasgo del perfil MUST declarar una de las cinco categorías de certeza (dicho por el usuario, confirmado, interpretación no verificada, hipótesis derivada de varios registros o recomendación del sistema), MUST enlazar con los registros que lo sustentan y MUST NOT mezclar las categorías. Las recomendaciones MUST presentarse separadas de lo observado.

#### Scenario: Rasgo con evidencia
- **WHEN** el usuario abre un rasgo
- **THEN** ve su categoría de certeza y los registros que lo sustentan

#### Scenario: Recomendación
- **WHEN** el perfil incluye una forma de comunicarse
- **THEN** se marca como recomendación del sistema y se distingue de los rasgos observados

#### Scenario: Hipótesis ya contrastadas
- **WHEN** el apartado de energía y ánimo se apoya en una hipótesis del usuario
- **THEN** solo se usa si el contraste ha sido confirmado por el usuario, y se enlaza con él

### Requirement: Alcance del perfil
El perfil MUST ser lo más completo que permita la información, como lo elaboraría un perfilista profesional o un atacante de ingeniería social que revisara las notas y los registros: personalidad y carácter, estilo de comunicación, motivaciones, valores, miedos y preocupaciones, hábitos y rutinas, energía y ánimo, relaciones y su dinámica desde el punto de vista del usuario, situación personal, familiar, laboral, económica y de salud, entorno y lugares, y cualquier otra deducción, indicio o suposición razonable, sensible o no. Además MUST incluir un apartado de superficie de ingeniería social: qué puede deducir un tercero, qué palancas de persuasión funcionarían con el usuario, sus puntos débiles y cómo protegerse. MUST incluir recomendaciones sobre cómo comunicarse él con otros y cómo conviene comunicarse con él. Todo MUST derivarse solo de lo que ORUMI ya conserva.

#### Scenario: Deducción sobre un tema sensible
- **WHEN** los registros permiten deducir algo sobre la salud, la economía o la vida personal del usuario
- **THEN** Noa lo incluye con su nivel de evidencia y su categoría de certeza, nunca como un hecho

#### Scenario: Superficie de ingeniería social
- **WHEN** se genera el perfil
- **THEN** incluye qué podría deducir un tercero y qué palancas de persuasión funcionarían, como información para que el usuario se proteja

#### Scenario: Apartado sin datos
- **WHEN** un apartado no tiene ni conjeturas
- **THEN** se omite y se indica que no hay datos, sin rellenarlo

#### Scenario: Sin fuentes externas
- **WHEN** Noa elabora el perfil
- **THEN** no consulta internet ni otras fuentes: solo lo que ORUMI ya conserva

### Requirement: Solo el perfil del usuario
Noa MUST elaborar únicamente el perfil del propio usuario y MUST NOT crear perfiles de terceras personas ni inferir rasgos, estados o conductas de ellas, aunque el usuario pida «todo» sobre sí mismo. Las demás personas, en particular los menores, MUST aparecer solo como contexto de las interacciones del usuario, descritas desde su punto de vista.

#### Scenario: Interacción con otra persona
- **WHEN** los registros muestran que el usuario se agota tras ciertas reuniones
- **THEN** el perfil describe cómo le afecta a él, sin atribuir rasgos a las otras personas

#### Scenario: Petición de perfilar a otra persona
- **WHEN** se pide el perfil de un tercero
- **THEN** Noa no lo genera

### Requirement: Temas sensibles marcados y sin diagnóstico clínico
Noa MAY deducir y suponer sobre temas sensibles del propio usuario (salud física y mental, situación económica, creencias, vida íntima y similares), MUST marcarlos como sensibles, MUST asignarles siempre categoría de certeza y nivel de evidencia, MUST NOT presentar una deducción como un hecho ni como confirmada si el usuario no la ha confirmado, y MUST NOT emitir diagnósticos clínicos ni etiquetas de trastornos: describe patrones y señales en lenguaje llano, sin nombrar una enfermedad como conclusión.

#### Scenario: Señales de salud
- **WHEN** los registros muestran patrones de cansancio y mal humor repetidos
- **THEN** Noa los describe como patrón con su evidencia, sin atribuir una enfermedad

#### Scenario: Deducción sensible no confirmada
- **WHEN** Noa supone algo sensible
- **THEN** figura como interpretación, indicio o conjetura marcada como sensible, hasta que el usuario la confirme

#### Scenario: Visibilidad
- **WHEN** el perfil contiene apartados sensibles
- **THEN** el usuario puede ocultarlos o descartarlos individualmente

### Requirement: Aislamiento de datos
Noa MUST leer registros, respuestas, correcciones e hipótesis, MUST escribir únicamente en su propio directorio, MUST NOT modificar capturas, registros ni preguntas, y MUST NOT suministrar su resultado a Mira ni a ningún otro componente que condicione cómo se registra.

#### Scenario: Generar el perfil
- **WHEN** Noa genera el perfil
- **THEN** no cambia ningún fichero fuera de su directorio

#### Scenario: Mira procesando
- **WHEN** Mira interpreta una captura
- **THEN** no recibe ningún dato del perfil

### Requirement: Control total del usuario
El usuario MUST poder ver, corregir, matizar, descartar y borrar cada rasgo, y borrar el perfil completo con su historial. Lo dicho o corregido por el usuario MUST prevalecer sobre cualquier interpretación de Noa, y lo descartado MUST NOT volver a proponerse salvo evidencia nueva sustancial.

#### Scenario: Descartar un rasgo
- **WHEN** el usuario descarta un rasgo
- **THEN** no reaparece mientras no haya evidencia nueva sustancial

#### Scenario: Corregir un rasgo
- **WHEN** el usuario corrige un rasgo
- **THEN** su versión pasa a «dicho por el usuario», prevalece y la anterior queda en el historial

#### Scenario: Borrado total
- **WHEN** el usuario borra el perfil
- **THEN** se eliminan los ficheros del perfil y su historial, sin afectar a capturas ni registros

### Requirement: Independencia de Noa
Los datos del perfil MUST ser ficheros legibles, accesibles con independencia de Noa, y regenerables solo a partir de lo que ORUMI ya conserva, sin depender de un índice.

#### Scenario: Sin Noa
- **WHEN** Noa no está disponible
- **THEN** el usuario sigue pudiendo leer sus ficheros de perfil

### Requirement: Preguntas de Noa
Noa MAY formular preguntas al usuario para completar o contrastar el perfil, solo cuando la respuesta pueda convertir una conjetura o un indicio en un patrón, resolver una contradicción o completar un apartado sin datos. Las preguntas de Noa MUST declarar a Noa como agente, MUST ser asíncronas y no bloquear nada, MUST admitir los mismos cierres que las de Mira (respondida, no lo recuerdo, no relevante, no sucedió), MUST NOT repetirse tras un cierre y MUST tener un máximo configurable por ejecución (por defecto 3). Versarán sobre el propio usuario (cómo se siente, qué prefiere, qué le ayuda o le desgasta), MUST NOT preguntar por rasgos o estados de terceras personas ni sugerir la respuesta ni dar consejos, y MUST NOT formularse antes de la etapa de observación.

#### Scenario: Pregunta que cierra una duda
- **WHEN** una conjetura sobre cómo le afecta una situación al usuario tiene un solo registro de apoyo
- **THEN** Noa puede preguntarle directamente si eso es así, sin sugerir la respuesta

#### Scenario: Pregunta sobre otra persona
- **WHEN** Noa quiere entender una relación del usuario
- **THEN** pregunta por lo que el usuario siente o hace, no por rasgos de la otra persona

#### Scenario: Cierre sin dato
- **WHEN** el usuario cierra una pregunta de Noa como no relevante
- **THEN** no se repite y cuenta para el aprendizaje de las preguntas de Noa

#### Scenario: Respuesta del usuario
- **WHEN** el usuario responde una pregunta de Noa
- **THEN** la respuesta queda como dicha por el usuario, prevalece sobre las interpretaciones de Noa y se usa en las siguientes versiones del perfil

### Requirement: Las preguntas y respuestas de Noa son de Noa
Las preguntas de Noa y sus respuestas MUST guardarse en el espacio de Noa y MUST NOT ser leídas por Mira ni mezclarse con las preguntas de Mira, de modo que no condicionen cómo se registran las notas. Que el usuario responda desde el panel MUST escribirse como acción del usuario en ese espacio.

#### Scenario: Mira procesando
- **WHEN** Mira procesa capturas y planifica sus preguntas
- **THEN** no ve ni cuenta las preguntas ni las respuestas de Noa

#### Scenario: Responder desde el panel
- **WHEN** el usuario responde una pregunta de Noa en el panel
- **THEN** la respuesta se guarda en el espacio de Noa y no cambia ningún registro ni captura
