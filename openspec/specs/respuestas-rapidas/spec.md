# respuestas-rapidas Specification

## Purpose
TBD - created by archiving change mira-pregunta-mas-y-repasa. Update Purpose after archive.

## Requirements

### Requirement: Opciones cerradas en las preguntas que las admiten
Una pregunta cuyo conjunto de respuestas posibles es cerrado MUST llevar sus **opciones** (etiqueta y valor ya fijado) y el panel MUST mostrarlas como botones que responden con un solo clic. Las opciones MUST ser: «Es el mismo», «Relacionados» y «Son distintos» en las coincidencias dudosas; un botón por cada valor candidato en los conflictos; «Sí» y «No» en las confirmaciones (ciudad, contraste de hipótesis, presencia de un factor); y los estados posibles cuando la pregunta es por un estado. Las preguntas abiertas MUST NOT llevar opciones inventadas.

#### Scenario: Coincidencia dudosa
- **WHEN** Mira pregunta si dos registros son el mismo
- **THEN** la pregunta muestra los botones «Es el mismo», «Relacionados» y «Son distintos» y el usuario no tiene que escribir nada

#### Scenario: Conflicto entre valores
- **WHEN** dos registros tienen valores distintos para el mismo dato
- **THEN** la pregunta muestra un botón por cada valor, que responde con ese valor y su registro

#### Scenario: Confirmación sí o no
- **WHEN** la pregunta es una confirmación, como si la ciudad deducida es correcta
- **THEN** muestra «Sí» y «No»; «No» pide la correcta si hace falta

#### Scenario: Pregunta abierta
- **WHEN** la respuesta no pertenece a un conjunto cerrado
- **THEN** no hay botones de opción, solo el campo de texto

### Requirement: Las opciones se derivan de la pregunta y se validan
Toda opción MUST poder aplicarse con el mecanismo de respuesta existente: su valor MUST ser aceptado por el campo de la pregunta y MUST NOT sugerir una respuesta que el usuario no haya dado ni contener consejos. Las opciones MUST derivarse siempre del contenido de la pregunta (no se guardan), de modo que las preguntas ya existentes las tengan y no puedan quedar desfasadas. Una opción inválida MUST descartarse y la pregunta quedar abierta. Los cierres «No lo recuerdo», «No es relevante» y «No sucedió» MUST estar siempre disponibles junto a las opciones.

#### Scenario: Opción no válida
- **WHEN** una pregunta propone una opción cuyo valor el campo no acepta
- **THEN** se descarta la opción y la pregunta se muestra abierta

#### Scenario: Cierres siempre presentes
- **WHEN** una pregunta tiene opciones
- **THEN** los tres cierres siguen disponibles
