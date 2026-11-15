## ADDED Requirements

### Requirement: Lanzar y ver el repaso desde el panel
El panel MUST permitir al usuario lanzar un repaso de sus registros y MUST mostrar si hay uno en curso y el resultado del último (registros revisados y preguntas creadas), con las mismas garantías de acceso, limitación de peticiones y bloqueo frente a Mira que el resto de escrituras. Si Mira está procesando, MUST responder que está ocupada sin cambiar nada.

#### Scenario: Lanzar un repaso
- **WHEN** el usuario pulsa «Repasar ahora»
- **THEN** el repaso se ejecuta en segundo plano y las preguntas nuevas aparecen en «Preguntas»

#### Scenario: Mira ocupada
- **WHEN** Mira está procesando o ya hay un repaso en curso
- **THEN** el panel lo indica y no lanza otro

### Requirement: Botones de respuesta rápida en las preguntas
El panel MUST mostrar, en cada pregunta con opciones, un botón por opción que responde con un clic, junto a los cierres «No lo recuerdo», «No es relevante» y «No sucedió», y MUST seguir ofreciendo el campo de texto cuando la pregunta es abierta. Tras una respuesta rápida que fusiona registros, MUST ofrecer «Deshacer» durante unos segundos. Los botones MUST tener nombre accesible y respetar el movimiento reducido.

#### Scenario: Respuesta con un clic
- **WHEN** el usuario pulsa una opción
- **THEN** la pregunta se responde y desaparece de la lista, sin avisos de formato

#### Scenario: Pregunta abierta
- **WHEN** la pregunta no tiene opciones
- **THEN** solo se muestra el campo de texto y los cierres
