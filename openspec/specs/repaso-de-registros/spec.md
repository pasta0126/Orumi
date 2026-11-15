# repaso-de-registros Specification

## Purpose
TBD - created by archiving change mira-pregunta-mas-y-repasa. Update Purpose after archive.

## Requirements

### Requirement: Repaso de los registros existentes
Mira MUST repasar periódicamente los registros ya existentes con datos desconocidos, dudosos o poco detallados y MUST formular las preguntas que falten, sin alterar ningún registro. El repaso MUST ejecutarse cada noche dentro del lote y a petición del usuario (terminal y panel), MUST tomar el bloqueo de Mira y MUST NOT interrumpir ni bloquear el registro de notas nuevas.

#### Scenario: Repaso nocturno
- **WHEN** se ejecuta el lote nocturno
- **THEN** Mira repasa los registros con huecos y crea las preguntas que falten

#### Scenario: Repaso a petición
- **WHEN** el usuario lanza el repaso desde la terminal o el panel
- **THEN** se ejecuta el mismo repaso y se informa de cuántas preguntas se han creado

#### Scenario: Registro sin huecos
- **WHEN** un registro no tiene datos desconocidos ni dudosos
- **THEN** el repaso no crea ninguna pregunta sobre él

### Requirement: Dos capas con la misma validación
El repaso MUST tener una capa determinista, que pregunta por los campos estructurales desconocidos sin usar el modelo, y una capa con el modelo, que señala huecos de contenido. Toda pregunta, venga de la capa que venga, MUST referirse a un registro y a un campo existentes y corregibles, MUST NOT duplicar una pregunta pendiente, cerrada o equivalente a una ya cerrada «no lo recuerdo» o «no relevante», MUST NOT contener consejos ni valoraciones y MUST descartarse si no cumple; lo que descarte MUST quedar contado.

#### Scenario: Campo desconocido
- **WHEN** un registro tiene la duración desconocida y no hay pregunta sobre ella
- **THEN** el repaso crea la pregunta de duración sin llamar al modelo

#### Scenario: Pregunta ya cerrada
- **WHEN** el usuario cerró antes esa misma pregunta como «no lo recuerdo» o «no relevante»
- **THEN** el repaso no la vuelve a crear

#### Scenario: Salida del modelo inválida
- **WHEN** el modelo propone una pregunta sobre un registro o campo inexistente
- **THEN** se descarta, se cuenta y no se escribe nada de ella

### Requirement: Tope de creación por repaso
Para que la primera pasada sobre el histórico no inunde al usuario, cada repaso MUST crear como máximo un número configurable de preguntas nuevas (200 por defecto), eligiendo las de mayor valor; las demás MUST quedar para el siguiente repaso, sin perderse ni cerrarse.

#### Scenario: Muchos huecos
- **WHEN** hay más candidatas que el tope de creación
- **THEN** se crean las de mayor valor y el resto queda para la noche siguiente

### Requirement: Constancia y coste del repaso
Cada repaso MUST dejar constancia en su propio fichero (`analisis/repaso/`) de cuántos registros revisó, cuántas preguntas creó y descartó y, si usó el modelo, su proveedor, modelo, uso y coste, y MUST respetar el control de saldo.

#### Scenario: Presupuesto agotado
- **WHEN** el presupuesto configurado se ha agotado
- **THEN** el repaso omite la capa con modelo, mantiene la determinista y lo anota
