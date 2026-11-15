## ADDED Requirements

### Requirement: Responder con una opción y fusionar en un paso
Responder a una pregunta con una de sus opciones MUST producir exactamente el mismo efecto que escribir esa respuesta, con la misma validación, el mismo bloqueo frente a Mira y la misma auditoría. En una coincidencia dudosa, «Es el mismo» MUST fusionar los registros (conservando el más antiguo como destino y dejando la tumba reversible), «Relacionados» MUST relacionarlos sin fusionar y «Son distintos» MUST dejarlos separados y no volver a preguntarlo. Tras una fusión por respuesta rápida, el usuario MUST poder deshacerla durante un minuto, devolviendo todos los ficheros tocados a su estado anterior y sin pisar ningún cambio posterior. Responder a una pregunta de ciudad o de contraste MUST cerrarla con la respuesta sin intentar corregir ningún campo del registro.

#### Scenario: Es el mismo
- **WHEN** el usuario pulsa «Es el mismo» en una coincidencia dudosa
- **THEN** los registros se fusionan y se ofrece «Deshacer»

#### Scenario: Deshacer la fusión
- **WHEN** el usuario pulsa «Deshacer» justo después
- **THEN** los registros vuelven a estar separados como antes

#### Scenario: Son distintos
- **WHEN** el usuario pulsa «Son distintos»
- **THEN** los registros quedan separados y Mira no vuelve a preguntar por esa pareja

#### Scenario: Responder una pregunta de ciudad o de contraste
- **WHEN** el usuario responde «Sí» a una pregunta de ciudad o de contraste de hipótesis
- **THEN** la pregunta se cierra como respondida, el registro no cambia y Mira aplica la respuesta en su siguiente ejecución

#### Scenario: Algo cambió tras la fusión
- **WHEN** el usuario pulsa «Deshacer» pero alguno de los ficheros ha cambiado desde entonces
- **THEN** no se deshace nada y se le avisa
