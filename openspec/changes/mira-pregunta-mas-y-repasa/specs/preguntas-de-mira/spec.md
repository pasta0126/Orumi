## MODIFIED Requirements

### Requirement: Máximo por revisión y orden por valor
Mira MUST ordenar por valor las preguntas pendientes que presenta. El máximo de preguntas pendientes MUST ser opcional y configurable: **por defecto no hay máximo**. Si se configura y se supera, Mira MUST descartar las candidatas de menor valor.

#### Scenario: Sin máximo configurado
- **WHEN** no se ha configurado ningún máximo y hay muchas preguntas candidatas
- **THEN** se crean todas las válidas, ordenadas por valor

#### Scenario: Más candidatas que el máximo
- **WHEN** el usuario ha configurado un máximo y hay más preguntas candidatas que ese máximo
- **THEN** solo se crean las de mayor valor

#### Scenario: Valor de una pregunta
- **WHEN** Mira prioriza
- **THEN** da más valor a conflictos y a datos que dejan abierto un asunto (por ejemplo, si una incidencia se resolvió) que a datos accesorios

### Requirement: Datos estructurales y contenido que falta
Mira MUST preguntar por los datos estructurales del registro (fecha, hora, duración, personas, lugar, ciudad, proyecto, estado o resultado) y también por el **contenido que la nota deja abierto o sin detallar** (el porqué, el resultado, lo que quedó pendiente, las personas implicadas), usando los campos del registro que admiten corrección. Mira MUST NOT inventar ni suponer el contenido que pregunta, MUST NOT sugerir la respuesta y, para contrastar una hipótesis declarada por el usuario, MUST seguir preguntando solo por la presencia o ausencia de un factor o efecto ya declarado.

#### Scenario: Reunión con acuerdos sin detallar
- **WHEN** la captura dice que se llegó a acuerdos sin indicarlos
- **THEN** Mira puede preguntar cuáles fueron, y mientras no responda el campo queda desconocido

#### Scenario: Contenido ya dicho
- **WHEN** la nota ya indica el motivo o el resultado
- **THEN** Mira no pregunta por ese dato

#### Scenario: La respuesta rellena el campo
- **WHEN** el usuario responde a una pregunta de contenido
- **THEN** el campo del registro se completa con su texto y pasa a confirmado

## REMOVED Requirements

### Requirement: Solo datos estructurales
**Reason**: el usuario prefiere que Mira pregunte de todo antes que no pregunte, y recabar más información exige poder preguntar también por el contenido.
**Migration**: sustituido por «Datos estructurales y contenido que falta». El cierre «no relevante» y el aprendizaje por clase siguen reduciendo la prioridad de lo que no interesa.
