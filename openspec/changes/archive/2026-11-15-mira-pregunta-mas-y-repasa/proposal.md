## Why

Mira recaba poca información y pregunta poco: el usuario lo ha comprobado con sus notas reales y prefiere que pregunte de todo antes que no pregunte. Hoy el *prompt* le pide «pocas» preguntas, solo estructurales y nunca sobre el contenido (X36), hay un máximo de 5 pendientes (X13, X37) y solo pregunta en el momento de procesar cada nota, sin volver sobre lo que ya tiene registrado con datos desconocidos o poco detallados.

## What Changes

- **Qué pregunta (cambia X36):** además de los datos estructurales (fecha, hora, duración, personas, lugar, proyecto, estado), Mira pregunta por el **contenido que falta**: el porqué, el resultado, lo que quedó pendiente, las personas implicadas y los detalles que la nota deja abiertos. Sigue sin inventar nada: lo no dicho es desconocido y se pregunta.
- **Sin tope de pendientes (cambia X13 y X37):** el máximo de 5 deja de aplicarse por defecto. Sigue siendo configurable (`maximo_pendientes`) para quien quiera limitarlo, y las preguntas se ordenan por valor. Para que la primera pasada sobre el histórico no inunde, el **repaso** crea como máximo un número de preguntas nuevas por ejecución (configurable).
- **Repaso (nuevo):** cada noche, dentro del lote, Mira revisa los **registros que ya existen** con datos desconocidos, dudosos o poco detallados y formula las preguntas que falten. Tiene dos capas: una **determinista** (campos estructurales desconocidos, sin modelo) y otra **con el modelo** (huecos de contenido), ambas con la misma validación. `orumi repasar` y un botón en el panel lo lanzan a petición.
- **Respuestas rápidas (nuevo, pedido por el usuario):** las preguntas con respuesta cerrada llevan **botones con la respuesta ya fijada** (coincidencia de registros: «Es el mismo», «Relacionados», «Son distintos»; conflictos: un botón por cada valor candidato; confirmaciones como la ciudad o el contraste de hipótesis: «Sí» y «No»; estados como «resuelto» o «pendiente»). Un clic responde, sin escribir ni recibir avisos del tipo «responde misma o distinta». «Es el mismo» puede fusionar los registros, con la tumba reversible de X58 y un «Deshacer» inmediato. Los cierres «No lo recuerdo», «No es relevante» y «No sucedió» siguen siempre disponibles.
- **Se mantiene:** nunca inventar datos; preguntas asíncronas que no bloquean; cierres «no lo recuerdo», «no es relevante» y «no sucedió», sin repetirse; el aprendizaje por clase baja la prioridad de lo que el usuario cierra como no relevante (no lo prohíbe); sin consejos ni valoraciones; las preguntas de Noa siguen aparte, con su cupo.

## Capabilities

### New Capabilities
- `repaso-de-registros`: revisión periódica y a petición de los registros existentes para formular las preguntas que falten.
- `respuestas-rapidas`: botones con la respuesta cerrada ya fijada en las preguntas que tienen un conjunto de respuestas posible.

### Modified Capabilities
- `preguntas-de-mira`: deja de limitarse a datos estructurales y a un máximo de 5; el máximo pasa a ser opcional y se añade el tope de creación por repaso.
- `privacidad-del-procesamiento`: el repaso es otro envío al proveedor y se somete a las mismas garantías, mínimo y fallo cerrado.
- `panel-web`: botón y estado del repaso y botones de respuesta rápida en las preguntas.
- `respuestas-y-correcciones`: responder con una opción fijada y, en las coincidencias, fusionar o relacionar en un paso.

## Impact

- Código: *prompt* y límites de `Procesador`, nuevo repaso en `Orumi.Agente` (capas determinista y con modelo), comando `orumi repasar`, integración en el lote nocturno, endpoint y botón del panel. Sin dependencias, bases de datos ni formatos nuevos: las preguntas usan el formato actual (el contenido se pregunta con los campos `contexto`, `resultados`, `acciones`, `problemas` e `ideas`, ya corregibles).
- Coste de IA: una llamada adicional por noche (por lotes si es posible) sobre los registros con huecos; se mide en la primera ejecución y entra en el control de saldo.
- Documentos: `docs/decisiones.md` (nueva decisión que reinterpreta X36, X13 y X37), `AGENTS.md` y las specs vigentes al archivar.
- **Riesgo asumido:** más preguntas pueden cansar. Se mitiga con el orden por valor, los cierres sin penalización y el aprendizaje por clase.
