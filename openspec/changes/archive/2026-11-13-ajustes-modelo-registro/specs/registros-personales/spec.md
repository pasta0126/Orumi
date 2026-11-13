## MODIFIED Requirements

### Requirement: Campos del registro
Cada registro MUST incluir identificador, tipo, fecha del acontecimiento (o su imprecisión), fecha de comunicación, captura de origen y descripción, y MAY incluir hora, duración, contexto, personas, entidades, lugares, proyectos, acciones, resultados, problemas, ideas y relaciones.

#### Scenario: Fecha imprecisa
- **WHEN** el usuario dice «la semana pasada»
- **THEN** el registro conserva la expresión original y un rango estimado marcado como interpretación no verificada, sin inventar una fecha exacta

#### Scenario: Corrección del rango
- **WHEN** el usuario indica la fecha real
- **THEN** el rango se sustituye, pasa a confirmado y la expresión original se conserva

#### Scenario: Campos opcionales ausentes
- **WHEN** un registro no tiene personas, entidades ni lugares
- **THEN** es válido sin esos campos

#### Scenario: Registro anterior a los campos nuevos
- **WHEN** un registro existente no tiene hora ni entidades
- **THEN** sigue siendo válido sin modificarlo

## ADDED Requirements

### Requirement: Entidades mencionadas
Un registro MAY incluir las entidades nombradas por el usuario (organizaciones, clientes, proveedores, sistemas o productos), cada una con su nombre, un tipo opcional y su certeza, y el sistema MUST NOT añadir entidades que el usuario no haya mencionado ni que no se deduzcan razonablemente marcadas como interpretación.

#### Scenario: Entidad dicha por el usuario
- **WHEN** el usuario menciona un cliente o un sistema por su nombre
- **THEN** el registro lo incluye como entidad con certeza dicho

#### Scenario: Entidad deducida
- **WHEN** Mira deduce una entidad que el usuario no nombró
- **THEN** queda marcada como interpretación no verificada

#### Scenario: Nombre con errata
- **WHEN** el usuario escribe un nombre con errata evidente
- **THEN** la entidad se guarda con el nombre normalizado y la captura original permanece intacta

#### Scenario: Corrección de una entidad
- **WHEN** el usuario corrige el nombre de una entidad
- **THEN** el registro refleja el nombre corregido con certeza confirmado, el valor anterior queda en el historial y un reprocesado no lo sustituye

### Requirement: Hora del acontecimiento
Un registro MAY incluir la hora del acontecimiento con su propia certeza, independiente de la de la fecha, indicando si es aproximada, y MUST NOT inferir una hora exacta a partir de una franja imprecisa.

#### Scenario: Hora aproximada
- **WHEN** el usuario dice «sobre las 9.30»
- **THEN** el registro guarda la expresión original, la hora 09:30 marcada como aproximada y con certeza dicho

#### Scenario: Franja sin hora
- **WHEN** el usuario dice solo «por la mañana»
- **THEN** el registro conserva la expresión y no guarda ninguna hora concreta

#### Scenario: Certezas distintas
- **WHEN** el día de un registro es interpretado y la hora fue dicha por el usuario
- **THEN** cada uno conserva su certeza sin mezclarlas

### Requirement: Preguntas orientadas a completar datos del registro
Las preguntas del agente MUST orientarse a completar datos estructurales de un registro (fecha, hora, duración, personas, lugar, proyecto, estado o resultado) y MUST NOT solicitar el contenido sustantivo de lo hablado o decidido salvo que el usuario lo haya aportado. El usuario MUST poder cerrar una pregunta como no relevante, y una pregunta cerrada así MUST NOT repetirse ni formularse en términos parecidos.

#### Scenario: Pregunta sobre un dato estructural
- **WHEN** una captura menciona una incidencia sin indicar si se resolvió
- **THEN** el agente puede preguntar si quedó resuelta

#### Scenario: Contenido sustantivo no solicitado
- **WHEN** una captura dice que se llegó a acuerdos en una reunión sin detallarlos
- **THEN** el agente no pregunta cuáles fueron los acuerdos y el dato queda como no indicado

#### Scenario: Pregunta no relevante
- **WHEN** el usuario cierra una pregunta como no relevante
- **THEN** la pregunta no se repite ni se formulan otras equivalentes sobre ese dato
