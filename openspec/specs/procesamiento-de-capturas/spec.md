# procesamiento-de-capturas Specification

## Purpose
Definir cómo Mira convierte las capturas del usuario en registros de forma completa, trazable, idempotente y recuperable, sin inventar datos ni pisar lo que el usuario corrigió.

## Requirements

### Requirement: Ejecución por lotes y a petición
Mira MUST procesar las capturas que aún no figuren en ningún lote, de forma periódica (por defecto de noche) y a petición del usuario, y MUST NOT reprocesar una captura ya procesada salvo petición explícita.

#### Scenario: Lote nocturno
- **WHEN** se ejecuta el lote periódico y existen capturas sin procesar
- **THEN** Mira las procesa todas y deja constancia en un lote

#### Scenario: Ejecución a petición
- **WHEN** el usuario pide procesar ahora
- **THEN** Mira procesa las capturas pendientes sin esperar al lote periódico

#### Scenario: Captura ya procesada
- **WHEN** una captura ya figura en un lote
- **THEN** no se vuelve a procesar salvo que el usuario o una nueva versión de Mira lo pida expresamente

### Requirement: Cobertura completa
Mira MUST crear registros para todo lo que el usuario cuente, incluido lo cotidiano que no es de trabajo, y MUST NOT descartar contenido en silencio ni decidir qué es relevante por el usuario.

#### Scenario: Nota mezclada
- **WHEN** una captura mezcla trabajo, comida y asuntos domésticos
- **THEN** cada hecho relevante genera su registro sin filtrar por ámbito

#### Scenario: Captura sin contenido registrable
- **WHEN** una captura no contiene hechos que registrar
- **THEN** no se crea ningún registro, la captura queda intacta y el lote lo indica

### Requirement: Extracción fiel y con certeza
Al extraer, Mira MUST dividir la captura en registros con su fragmento de origen, MUST marcar la certeza de cada dato y MUST NOT inventar datos: lo dicho literalmente es «dicho», lo deducido es «interpretación no verificada» y lo ausente queda «desconocido».

#### Scenario: Fecha relativa
- **WHEN** la captura dice «ayer» o «la semana pasada»
- **THEN** Mira resuelve el día o el rango a partir de la fecha de comunicación y lo marca como interpretación

#### Scenario: Hora imprecisa
- **WHEN** la captura dice «por la mañana»
- **THEN** Mira conserva la expresión y no inventa una hora concreta

#### Scenario: Dato no mencionado
- **WHEN** la captura no indica la duración de una actividad
- **THEN** la duración queda desconocida

#### Scenario: Consecuencia no escrita
- **WHEN** la captura dice que hubo acuerdos sin detallarlos
- **THEN** Mira registra que los hubo y que no se indican, sin suponerlos

### Requirement: Validación antes de escribir
Mira MUST comprobar que cada fichero que produce cumple el formato definido (campos obligatorios, certeza válida, referencias existentes, fragmentos dentro del texto) antes de guardarlo.

#### Scenario: Salida no válida
- **WHEN** un registro generado no cumple el formato
- **THEN** no se guarda, se reintenta o se anota el problema en el lote, y la captura no se da por procesada

### Requirement: Reprocesado idempotente que respeta al usuario
Al reprocesar una captura, Mira MUST reclamar las claves de extracción existentes en lugar de crear duplicados, MUST NOT eliminar registros, y MUST NOT modificar los campos confirmados o corregidos por el usuario; el resto se actualiza dejando constancia en el historial y en la versión de Mira usada.

#### Scenario: Reprocesado sin cambios del usuario
- **WHEN** se reprocesa una captura cuyos registros no han sido tocados
- **THEN** los registros se actualizan o se mantienen y no aparecen duplicados

#### Scenario: Reprocesado con correcciones
- **WHEN** el usuario había corregido un campo
- **THEN** el campo conserva el valor del usuario y Mira solo actualiza lo no tocado

#### Scenario: Registro que la nueva pasada no reclama
- **WHEN** una nueva pasada no produce un registro existente
- **THEN** el registro se mantiene tal cual

### Requirement: Aportaciones y contradicciones
Cuando una captura amplíe un registro existente, Mira MUST añadirla como nueva fuente o entrada; cuando la coincidencia sea dudosa MUST NOT fusionar y MUST dejar una pregunta pendiente; y cuando contradiga datos existentes MUST conservar ambas versiones, marcar el conflicto y no elegir ganador.

#### Scenario: Ampliación clara
- **WHEN** una captura continúa una actividad abierta
- **THEN** se añade una entrada fechada al mismo registro

#### Scenario: Coincidencia dudosa
- **WHEN** no está claro si dos capturas hablan de lo mismo
- **THEN** se mantienen separadas y se crea una pregunta pendiente

#### Scenario: Contradicción
- **WHEN** una captura da una fecha distinta para lo que parece el mismo hecho
- **THEN** ambos registros se marcan en conflicto y se crea una pregunta pendiente

### Requirement: Capturas intactas
Mira MUST NOT modificar ninguna captura.

#### Scenario: Procesar no altera la captura
- **WHEN** Mira procesa una captura
- **THEN** su contenido y su huella permanecen idénticos

### Requirement: Lotes recuperables y bitácora
Cada ejecución MUST dejar un lote con la versión de Mira, el modelo, las capturas procesadas, los registros y preguntas creados o actualizados y los problemas; un fallo MUST NOT dejar capturas marcadas como procesadas sin sus registros.

#### Scenario: Fallo a mitad de lote
- **WHEN** el procesamiento falla tras procesar parte de las capturas
- **THEN** las ya completadas quedan registradas en el lote, las demás siguen pendientes y pueden reintentarse

#### Scenario: Consulta de lo ocurrido
- **WHEN** el usuario consulta un lote
- **THEN** ve qué capturas se procesaron, qué se creó o actualizó y qué problemas hubo
