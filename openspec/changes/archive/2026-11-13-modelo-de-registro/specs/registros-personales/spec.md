## Purpose

Definir la unidad de memoria de ORUMI —el registro personal— con su identidad, certeza, procedencia y relaciones, de forma que toda interpretación sea trazable, corregible y distinguible de los hechos.

## ADDED Requirements

### Requirement: Un fichero por registro
Cada registro personal (evento, actividad, idea, decisión, problema u otro tipo significativo) MUST ser un fichero propio con metadatos estructurados y descripción, y MUST enlazar a la captura de la que procede.

#### Scenario: Una captura genera varios registros
- **WHEN** una captura contiene una actividad, una idea y una decisión
- **THEN** existen tres registros distintos, cada uno enlazado a esa captura

#### Scenario: Fragmento de origen
- **WHEN** un registro procede de una parte de una captura con varios temas
- **THEN** el registro referencia ese fragmento y la captura permanece entera

#### Scenario: Registro incompleto
- **WHEN** falta un dato como la duración o la herramienta
- **THEN** el registro se crea igualmente y el dato figura como desconocido

### Requirement: Campos del registro
Cada registro MUST incluir identificador, tipo, fecha del acontecimiento (o su imprecisión), fecha de comunicación, captura de origen y descripción, y MAY incluir duración, contexto, personas, lugares, proyectos, acciones, resultados, problemas, ideas y relaciones.

#### Scenario: Fecha imprecisa
- **WHEN** el usuario dice «la semana pasada»
- **THEN** el registro conserva la expresión original y un rango estimado marcado como interpretación no verificada, sin inventar una fecha exacta

#### Scenario: Corrección del rango
- **WHEN** el usuario indica la fecha real
- **THEN** el rango se sustituye, pasa a confirmado y la expresión original se conserva

#### Scenario: Campos opcionales ausentes
- **WHEN** un registro no tiene personas ni lugares
- **THEN** es válido sin esos campos

### Requirement: Certeza explícita
Cada dato relevante de un registro MUST declarar su certeza en una de cinco categorías: dicho por el usuario, confirmado mediante aclaración, interpretación no verificada, hipótesis derivada de varios registros o recomendación del sistema. Las categorías MUST NOT mezclarse.

#### Scenario: Dato interpretado
- **WHEN** Mira deduce que una actividad fue técnica sin que el usuario lo dijera
- **THEN** ese dato queda marcado como interpretación no verificada

#### Scenario: Confirmación del usuario
- **WHEN** el usuario responde a una aclaración
- **THEN** el dato afectado pasa a confirmado y se conserva la respuesta como evidencia

### Requirement: Nada inventado
El sistema MUST NOT rellenar datos no expresados ni confirmados, ni fabricar cifras; lo ausente MUST marcarse como desconocido.

#### Scenario: Duración no indicada
- **WHEN** la captura no menciona cuánto duró una actividad
- **THEN** la duración queda como desconocida y no se estima sin marcarla como interpretación

### Requirement: Procedencia y versión del agente
Todo registro generado por el agente MUST indicar qué versión de Mira, modelo y criterios lo produjeron, y el reprocesado de una captura MUST NOT duplicar registros ya existentes.

#### Scenario: Reprocesado
- **WHEN** se vuelve a procesar una captura ya procesada
- **THEN** los registros existentes se actualizan o se mantienen y no aparecen duplicados

#### Scenario: Cambio de versión
- **WHEN** una nueva versión de Mira regenera un registro
- **THEN** el registro indica la nueva versión y se conserva el historial de la anterior

### Requirement: Las aportaciones del usuario se respetan al reprocesar
Lo que el usuario corrige o completa MUST considerarse válido y acertado. Mira MAY adaptarlo o adecuarlo y MAY preguntar si necesita más información, pero MUST NOT eliminarlo ni sustituirlo por su propia interpretación; un reprocesado MUST actualizar solo lo no tocado por el usuario.

#### Scenario: Reprocesado con datos corregidos
- **WHEN** Mira regenera un registro que el usuario había corregido
- **THEN** conserva los valores del usuario, actualiza únicamente el resto y, si lo necesita, deja una pregunta pendiente

#### Scenario: Dato del usuario que choca con una interpretación
- **WHEN** una interpretación de Mira difiere de un dato aportado por el usuario
- **THEN** prevalece el dato del usuario y la interpretación no sustituye ni oculta ese dato

### Requirement: Control del usuario e historial de correcciones
El usuario MUST poder corregir, completar, fusionar, separar o eliminar registros, y cada cambio MUST quedar en un historial de correcciones sin alterar la captura original.

#### Scenario: Corrección
- **WHEN** el usuario corrige un dato
- **THEN** el registro refleja el valor corregido, el dato pasa a certeza confirmado y el valor anterior queda en el historial

#### Scenario: Rechazo de una interpretación
- **WHEN** el usuario rechaza una interpretación o hipótesis
- **THEN** queda marcada como rechazada y no se vuelve a proponer sin nueva evidencia

### Requirement: Relaciones trazables
Las relaciones entre registros MUST ser explícitas y, cuando sean hipótesis o patrones, MUST enlazar con los registros que las sustentan.

#### Scenario: Patrón con evidencia
- **WHEN** se propone un patrón recurrente
- **THEN** enumera los registros que lo respaldan y su certeza es hipótesis

### Requirement: Preguntas pendientes
Las aclaraciones solicitadas por el agente MUST asociarse al registro afectado, MAY quedar sin responder, y MUST NOT repetirse si el usuario indicó que no recuerda.

#### Scenario: Respuesta «no lo recuerdo»
- **WHEN** el usuario responde que no recuerda un dato
- **THEN** la pregunta se cierra y el dato permanece desconocido

### Requirement: Contradicciones
Cuando una captura contradiga datos de otra, el sistema MUST conservar ambas versiones, marcar el conflicto en los registros afectados, MUST NOT elegir una como ganadora y MUST registrar una pregunta pendiente.

#### Scenario: Dato contradictorio
- **WHEN** una captura nueva da un valor distinto para un dato ya registrado
- **THEN** ambos valores permanecen visibles, el conflicto queda marcado y se crea una pregunta pendiente

#### Scenario: Sin respuesta
- **WHEN** el usuario no resuelve el conflicto
- **THEN** ambas versiones siguen marcadas como en conflicto

### Requirement: Eventos de varios días
Una actividad que se extienda varios días MUST representarse como un único registro con inicio, fin (si se conoce), estado abierto o cerrado y entradas fechadas.

#### Scenario: Continuación
- **WHEN** una captura posterior continúa una actividad abierta
- **THEN** se añade una entrada fechada al mismo registro y no se crea uno nuevo

#### Scenario: Cierre
- **WHEN** el usuario indica que la actividad terminó
- **THEN** el registro pasa a cerrado con su fecha de fin

### Requirement: Completitud no bloqueante
Un registro MUST ser válido desde su creación y el sistema MUST NOT exigir un estado «completo». El agente MUST dejar de formular preguntas sobre un registro cuando se haya cubierto el valor esperado, el usuario indique que no recuerda o se alcance el límite de preguntas.

#### Scenario: Registro mínimo
- **WHEN** se crea un registro con solo descripción y fecha
- **THEN** es válido y consultable

#### Scenario: Fin de preguntas
- **WHEN** se alcanza el límite de preguntas para un registro
- **THEN** no se generan más aclaraciones sobre él salvo nueva información

### Requirement: Aportaciones a registros existentes
Cuando una captura nueva amplíe un registro existente, el sistema MUST añadirla como nueva fuente o entrada de ese registro en lugar de crear uno nuevo, y cuando exista duda MUST registrar una pregunta pendiente.

#### Scenario: Ampliación clara
- **WHEN** una captura aporta más información sobre una actividad ya registrada
- **THEN** el registro existente incorpora la nueva captura como fuente y no se duplica

#### Scenario: Coincidencia dudosa
- **WHEN** no está claro si la captura se refiere a un registro existente
- **THEN** se crea una pregunta pendiente y no se fusionan hasta la respuesta

