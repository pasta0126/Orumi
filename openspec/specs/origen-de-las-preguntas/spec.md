# origen-de-las-preguntas Specification

## Purpose
TBD - created by archiving change perfil-personal. Update Purpose after archive.

## Requirements

### Requirement: Toda pregunta declara qué agente la formuló
Cada pregunta MUST indicar el agente que la formuló (Mira o Noa) y el panel y la terminal MUST mostrarlo. Las preguntas que ya existen, sin agente escrito, MUST entenderse como de Mira.

#### Scenario: Pregunta de Mira
- **WHEN** Mira formula una pregunta sobre un registro
- **THEN** la pregunta declara a Mira como agente

#### Scenario: Pregunta de Noa
- **WHEN** Noa formula una pregunta
- **THEN** la pregunta declara a Noa como agente y se ve como tal

#### Scenario: Pregunta anterior
- **WHEN** existe una pregunta sin agente escrito
- **THEN** se muestra como de Mira sin modificar el fichero

### Requirement: Avatar y nombre de cada agente en las preguntas
El panel MUST mostrar en cada pregunta el avatar y el nombre del agente que la formuló, y MUST agrupar las preguntas pendientes en bloques por agente con una cabecera (avatar, nombre y número de preguntas). Los avatares MUST ser propios (SVG, sin emojis ni emoticonos), distinguibles sin depender solo del color, con nombre accesible, y MUST funcionar en tema claro y oscuro y respetar la preferencia de movimiento reducido.

#### Scenario: Bloques por agente
- **WHEN** hay preguntas pendientes de Mira y de Noa
- **THEN** el panel las muestra en dos bloques, cada uno con el avatar y el nombre de su agente

#### Scenario: Cada tarjeta lleva su avatar
- **WHEN** el usuario ve una pregunta
- **THEN** la tarjeta muestra el avatar y el nombre del agente que la formuló

#### Scenario: Accesibilidad
- **WHEN** un lector de pantalla lee una pregunta
- **THEN** anuncia el nombre del agente, no solo su avatar ni su color

#### Scenario: Solo hay un agente
- **WHEN** solo hay preguntas de un agente
- **THEN** se muestra un único bloque con su cabecera

### Requirement: Filtrar por agente
El panel y la terminal MUST permitir ver solo las preguntas de un agente.

#### Scenario: Filtro
- **WHEN** el usuario filtra por Noa
- **THEN** solo ve las preguntas de Noa

### Requirement: Cada agente con su propio cupo y su propio aprendizaje
Cada agente MUST tener su propio máximo de preguntas pendientes, sin quitar cupo al otro, y el aprendizaje a partir de los cierres del usuario MUST aplicarse por agente: lo que el usuario cierra como no relevante de uno no cambia lo que pregunta el otro.

#### Scenario: Cupos independientes
- **WHEN** Mira ya tiene su máximo de preguntas pendientes
- **THEN** Noa puede seguir formulando las suyas hasta su propio máximo

#### Scenario: Aprendizaje por agente
- **WHEN** el usuario cierra como no relevante varias preguntas de Noa de una misma clase
- **THEN** solo cambia la prioridad de esa clase de Noa, no la de Mira
