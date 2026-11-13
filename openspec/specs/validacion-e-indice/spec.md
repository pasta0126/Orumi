# validacion-e-indice Specification

## Purpose
Dar herramientas de línea de comandos para comprobar que los ficheros de ORUMI cumplen el formato y para regenerar el índice derivado, sin modificar nunca los ficheros.

## Requirements

### Requirement: Validar ficheros
El sistema MUST ofrecer un comando que compruebe todos los ficheros de `orumi/` contra las reglas del formato y del contrato de Mira, informe por fichero de cada incumplimiento y MUST NOT modificar ningún fichero.

#### Scenario: Datos correctos
- **WHEN** se validan ficheros que cumplen el formato
- **THEN** el informe indica que no hay errores

#### Scenario: Captura alterada
- **WHEN** el cuerpo de una captura no coincide con su huella
- **THEN** el informe lo señala con el identificador de la captura

#### Scenario: Referencia rota
- **WHEN** un registro cita una captura o un registro que no existe
- **THEN** el informe lo señala y no se corrige

### Requirement: Regenerar el índice
El sistema MUST ofrecer un comando que reconstruya el índice derivado por completo a partir de los ficheros, produciendo siempre el mismo índice con los mismos ficheros, y MUST NOT abortar por un fichero dañado: lo omite y lo lista en el informe.

#### Scenario: Índice borrado
- **WHEN** se elimina el índice y se ejecuta el comando
- **THEN** se regenera sin pérdida de información

#### Scenario: Fichero ilegible
- **WHEN** un fichero no se puede leer
- **THEN** se omite, se lista en el informe y el resto se indexa

#### Scenario: Sustitución segura
- **WHEN** el índice nuevo se ha construido correctamente
- **THEN** sustituye al vigente solo entonces; si falla, el vigente se conserva

### Requirement: Procesamiento independiente del índice
Los comandos de captura, procesamiento y validación MUST funcionar usando solo los ficheros de `orumi/`, sin requerir que el índice esté disponible.

#### Scenario: Índice no disponible
- **WHEN** la base de datos del índice no responde
- **THEN** se pueden crear capturas, procesarlas y validar los ficheros, y solo la regeneración y las consultas del índice quedan afectadas

### Requirement: Consultas del índice
El índice generado MUST permitir las consultas definidas para el índice derivado: por fecha, hora, tipo, certeza, relación, proyecto, entidad y preguntas pendientes.

#### Scenario: Consulta por entidad
- **WHEN** se consulta por una entidad escrita con o sin acentos ni mayúsculas
- **THEN** se obtienen los registros que la mencionan
