# contexto-meteorologico Specification

## Purpose
TBD - created by archiving change contexto-meteorologico-y-analisis-semanal. Update Purpose after archive.

## Requirements

### Requirement: Clima como contexto derivado
Mira MUST obtener, para cada día y ciudad en que existan registros, temperatura, humedad relativa, precipitación y luminosidad (índice UV, radiación solar, nubosidad y horas de sol) con resolución horaria y resumen diario, y MUST guardarlos en ficheros propios con indicación de fuente, ubicación aproximada usada y fecha de obtención. MUST NOT escribirlos en las capturas ni modificar registros, y MUST poder regenerarlos.

#### Scenario: Día con registros
- **WHEN** existen registros de un día sin clima guardado
- **THEN** Mira obtiene y guarda el clima de ese día y ciudad sin tocar capturas ni registros

#### Scenario: Dato que el servicio no ofrece
- **WHEN** el servicio no devuelve una variable (por ejemplo el índice UV de un día antiguo)
- **THEN** esa variable queda como desconocida y nunca se estima ni se rellena

#### Scenario: Regeneración
- **WHEN** se borran los ficheros de clima
- **THEN** pueden volver a generarse desde el servicio sin pérdida de nada original

### Requirement: Ciudad de cada nota y ciudad por defecto
Cada captura MUST pertenecer a una ciudad: la que el usuario indique en ella o, si no indica ninguna, la ciudad por defecto de la configuración (Montbau, Barcelona). El clima de cada registro MUST corresponder a la ciudad de su captura. Las coordenadas MUST proceder de la configuración, de las ciudades que ORUMI haya aprendido o de la geocodificación del nombre que escribió el usuario, MUST guardarse redondeadas a unos 10 km y MUST NOT obtenerse del dispositivo. Una ciudad que no se conoce MUST ubicarse buscando solo su nombre: si la tiene clara (un único candidato o uno que domina claramente) Mira MUST usarla ya para obtener el clima y MUST preguntar después para confirmarla; si tiene dudas MUST preguntar y MUST NOT consultar el clima de esa nota hasta tener respuesta. Si el usuario corrige, Mira MUST corregir la ciudad y volver a obtener el clima del lugar correcto.

#### Scenario: Sin ciudad indicada
- **WHEN** una captura no indica ciudad
- **THEN** su clima es el de la ciudad por defecto

#### Scenario: Otra ciudad conocida
- **WHEN** el usuario indica que estuvo en una ciudad conocida
- **THEN** el clima de ese día para esa nota se obtiene de esa ciudad

#### Scenario: Ciudad desconocida y clara
- **WHEN** el usuario indica una ciudad que ORUMI no conoce y su nombre identifica claramente un solo lugar
- **THEN** Mira la ubica, obtiene su clima sin esperar y pregunta al usuario si es correcta

#### Scenario: Ciudad desconocida y dudosa
- **WHEN** el nombre podría corresponder a varios lugares o no se encuentra
- **THEN** Mira pregunta cuál es, con las opciones que haya, y no consulta el clima de esa nota hasta tener respuesta

#### Scenario: Corrección del usuario
- **WHEN** el usuario responde que la ciudad no es la ubicada y dice cuál es
- **THEN** Mira sustituye la ubicación y vuelve a obtener el clima de los días afectados

#### Scenario: Cierre sin responder
- **WHEN** el usuario cierra la pregunta de ciudad sin contestar
- **THEN** Mira no insiste y la nota sigue sin clima de esa ciudad

#### Scenario: Texto que menciona otra ciudad
- **WHEN** una nota sin ciudad menciona una ciudad conocida distinta de la de por defecto
- **THEN** Mira pregunta si estaba allí, sin cambiar la ciudad por sí sola, y si el usuario lo confirma solo esa nota pasa a esa ciudad

#### Scenario: Día con varias ciudades
- **WHEN** las notas de un mismo día pertenecen a ciudades distintas
- **THEN** se guarda y se usa el clima de cada ciudad por separado, y cada registro se une al de su ciudad

### Requirement: El clima no bloquea ni condiciona el procesamiento
La obtención del clima MUST NOT impedir ni retrasar el procesamiento de capturas, y los datos meteorológicos MUST NOT enviarse al modelo al interpretar capturas, para no sesgar los registros.

#### Scenario: Servicio caído
- **WHEN** el servicio meteorológico no responde
- **THEN** las capturas se procesan igual, el día queda pendiente de clima y se reintenta en la siguiente ejecución

#### Scenario: Procesar una captura
- **WHEN** Mira interpreta una captura
- **THEN** el modelo no recibe datos meteorológicos

### Requirement: Alcance honesto del dato
El clima describe las condiciones de la ciudad de la nota, no la exposición real del usuario. El sistema MUST NOT presentar el clima de un día como exposición personal, y toda conclusión que dependa de la exposición MUST apoyarse en lo que el usuario haya registrado (por ejemplo, haber estado fuera y cuánto tiempo).

#### Scenario: Día soleado sin salir
- **WHEN** un día tuvo UV alto pero los registros no indican que el usuario estuviera al aire libre
- **THEN** el análisis no cuenta ese día como exposición al sol

### Requirement: Umbrales ajustables
Los umbrales que convierten el clima en factores (UV alto, lluvia, día soleado o despejado) MUST estar en la configuración con valores iniciales justificados y MUST poder ajustarse; la revisión semanal MAY proponer ajustes como sugerencia, y MUST NOT cambiarlos por sí sola.

#### Scenario: Ajuste propuesto
- **WHEN** los datos sugieren que un umbral discrimina mal
- **THEN** la revisión lo indica como sugerencia y el umbral no cambia hasta que el usuario lo modifique
