## ADDED Requirements

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
Cada captura MUST pertenecer a una ciudad: la que el usuario indique en ella o, si no indica ninguna, la ciudad por defecto de la configuración (Montbau, Barcelona). El clima de cada registro MUST corresponder a la ciudad de su captura. Las coordenadas MUST proceder de la configuración o de una tabla de ciudades conocida, MUST guardarse redondeadas a unos 10 km y MUST NOT obtenerse del dispositivo. Si una ciudad indicada no se reconoce, Mira MUST preguntar al usuario en lugar de suponer, y MUST NOT consultar el clima de esa nota hasta resolverlo.

#### Scenario: Sin ciudad indicada
- **WHEN** una captura no indica ciudad
- **THEN** su clima es el de la ciudad por defecto

#### Scenario: Otra ciudad
- **WHEN** el usuario indica que estuvo en otra ciudad
- **THEN** el clima de ese día para esa nota se obtiene de esa ciudad

#### Scenario: Ciudad no reconocida
- **WHEN** la ciudad indicada no está en la tabla ni se resuelve con certeza
- **THEN** Mira pregunta qué ciudad es y no consulta el clima de esa nota hasta tener respuesta

#### Scenario: Texto que menciona otra ciudad
- **WHEN** el texto de una captura menciona una ciudad distinta de la de la captura
- **THEN** Mira puede preguntar en qué ciudad estaba, sin cambiar la ciudad por sí sola

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
