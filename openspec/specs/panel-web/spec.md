# panel-web Specification

## Purpose
Consultar, buscar, responder y corregir los registros de ORUMI desde una web con API, con control total del usuario y sin poner en riesgo los originales.

## Requirements

### Requirement: Toda petición autenticada
El sistema MUST exigir una sesión válida del proveedor de identidad de la plataforma en toda petición a la API (solo la carcasa estática y la configuración pública están exentas), MUST admitir únicamente cuentas de la lista configurada y MUST indicar a navegadores e intermediarios que no almacenen en caché ninguna respuesta con datos.

#### Scenario: Sin sesión o cuenta no autorizada
- **WHEN** llega una petición sin sesión válida o de una cuenta no permitida
- **THEN** se rechaza sin revelar ningún dato

#### Scenario: Petición sin credenciales a un endpoint de datos
- **WHEN** se invoca cualquier endpoint de datos sin sesión
- **THEN** responde 401 sin datos y sin indicar el motivo

#### Scenario: Respuestas con datos
- **WHEN** el sistema responde con datos personales
- **THEN** la respuesta indica que no debe almacenarse en caché

### Requirement: Origen restringido de la página
El sistema MUST aceptar peticiones entre orígenes únicamente desde la lista cerrada de orígenes configurada, MUST responder con el origen exacto (nunca un comodín), MUST resolver las peticiones previas sin autenticar y sin devolver datos, y MUST rechazar las escrituras cuyo origen no sea el propio ni uno de la lista.

#### Scenario: Origen permitido
- **WHEN** la página del panel hace una petición con su origen en la lista
- **THEN** la API responde con ese origen exacto en las cabeceras de control de acceso

#### Scenario: Origen no permitido
- **WHEN** otro origen hace una petición
- **THEN** la respuesta no incluye cabeceras de control de acceso y las escrituras se rechazan con 403

#### Scenario: Petición previa
- **WHEN** el navegador envía una petición previa desde un origen permitido
- **THEN** se responde sin datos y sin exigir sesión

### Requirement: Ver registros por día
El sistema MUST listar los registros de un día o rango, con título, clase, hora y certeza de la fecha, y permitir navegar entre días.

#### Scenario: Día con registros
- **WHEN** el usuario abre un día
- **THEN** ve sus registros ordenados por hora (los de hora desconocida al final) con su título, clase y estado de certeza

#### Scenario: Día sin registros
- **WHEN** el día no tiene registros
- **THEN** se indica que no hay ninguno

### Requirement: Ficha de registro con trazabilidad
El sistema MUST mostrar de un registro cada dato con su certeza, el fragmento exacto de la nota del que sale, su historial de cambios, sus relaciones y conflictos, y el texto completo de la captura de origen.

#### Scenario: Ficha
- **WHEN** el usuario abre un registro
- **THEN** ve cada campo con su certeza, la cita de la nota, el historial y los enlaces a registros relacionados

#### Scenario: Datos desconocidos
- **WHEN** un campo es desconocido
- **THEN** se muestra como desconocido, nunca como vacío ni con un valor supuesto

### Requirement: Búsqueda
El sistema MUST permitir buscar registros por texto, rango de fechas, entidad, proyecto, clase y certeza, combinando filtros.

#### Scenario: Búsqueda combinada
- **WHEN** el usuario filtra por entidad y rango de fechas
- **THEN** solo ve los registros que cumplen ambos criterios

#### Scenario: Sin coincidencias
- **WHEN** ningún registro cumple los filtros
- **THEN** se indica que no hay resultados

### Requirement: Responder preguntas
El sistema MUST listar las preguntas pendientes con su contexto (registro, campo, valor actual y registros implicados) y permitir responderlas con las mismas reglas que la línea de comandos: valor, no lo recuerdo, no es relevante, misma o distinta y registro destino en un conflicto.

#### Scenario: Responder con un valor
- **WHEN** el usuario responde una pregunta con un valor válido
- **THEN** el dato queda confirmado, la pregunta cerrada y el cambio aparece en el historial

#### Scenario: Valor no válido
- **WHEN** el valor no es válido
- **THEN** no se modifica nada y se explica el formato esperado

### Requirement: Corregir y rechazar desde la ficha
El sistema MUST permitir corregir un dato o rechazar una interpretación de Mira desde la ficha, con las mismas reglas, validaciones y trazabilidad que la línea de comandos.

#### Scenario: Corrección
- **WHEN** el usuario corrige un dato
- **THEN** queda confirmado y el valor anterior queda en el historial

#### Scenario: Rechazo de lo que no es una interpretación
- **WHEN** el usuario intenta rechazar un dato que no es una interpretación de Mira
- **THEN** no se modifica nada y se informa

### Requirement: Los originales son intocables
El sistema MUST ofrecer las capturas únicamente en lectura: no MUST existir ninguna operación que cree, modifique o borre una captura a través del panel, y el servicio MUST NOT tener permiso de escritura sobre el directorio de capturas.

#### Scenario: Intento de modificar una captura
- **WHEN** se intenta modificar o borrar una captura mediante la API
- **THEN** la operación no existe y no se modifica nada

### Requirement: Integridad y convivencia con Mira
Toda operación de escritura MUST validar el resultado antes de escribir, escribir de forma atómica y rechazarse con un aviso de reintentar si Mira está procesando.

#### Scenario: Mira procesando
- **WHEN** el usuario intenta responder o corregir mientras Mira procesa
- **THEN** se rechaza con un error temporal, sin modificar nada, y el panel pide reintentar

### Requirement: Datos siempre al día
El panel MUST leer los ficheros como fuente de verdad, de modo que cualquier cambio (del panel, de la línea de comandos o de Mira) sea visible sin esperar a regenerar el índice.

#### Scenario: Corrección por línea de comandos
- **WHEN** se corrige un registro con la línea de comandos
- **THEN** el panel muestra el cambio en su siguiente consulta

### Requirement: Limitación de peticiones
El sistema MUST limitar las peticiones por IP, por usuario autenticado y de forma global dentro de ventanas de tiempo deslizantes, con límites más estrictos para las escrituras y para los intentos de acceso fallidos, MUST responder 429 con `Retry-After` al superarlos y MUST NOT dejar que las peticiones rechazadas consuman recursos del proveedor de identidad.

#### Scenario: Exceso desde una IP
- **WHEN** una IP supera el máximo de peticiones por minuto
- **THEN** se rechazan las siguientes con 429 e indicación de cuándo reintentar, sin afectar a otras IP

#### Scenario: Exceso de escrituras de un usuario
- **WHEN** un usuario autenticado supera el máximo de escrituras por minuto
- **THEN** se rechazan las siguientes escrituras con 429 y las lecturas siguen disponibles dentro de su propio límite

#### Scenario: Fallos de acceso repetidos
- **WHEN** hay varios intentos de acceso fallidos desde una IP
- **THEN** esa IP queda bloqueada temporalmente, también para peticiones válidas

#### Scenario: Tope global
- **WHEN** el total de peticiones supera el tope global
- **THEN** el servicio responde 429 para proteger el servidor

### Requirement: Límites de tamaño y de resultados
El sistema MUST rechazar cuerpos de petición mayores que un máximo, limitar la longitud de los textos de búsqueda y de valores, acotar el número de resultados por página y validar el formato de los identificadores antes de usarlos.

#### Scenario: Cuerpo demasiado grande
- **WHEN** una petición supera el tamaño máximo
- **THEN** se rechaza con 413 sin procesarla

#### Scenario: Identificador con formato inválido
- **WHEN** el identificador no tiene el formato esperado
- **THEN** se responde 400 o 404 sin tocar ningún fichero

### Requirement: Endurecimiento de la superficie
El sistema MUST enviar cabeceras de seguridad (transporte seguro estricto, política de contenido sin scripts en línea, sin incrustación, sin referente, sin sniffing y permisos restringidos), MUST rechazar escrituras cuyo origen no sea el propio panel, MUST exigir `application/json` en las escrituras, MUST devolver errores genéricos sin trazas y MUST registrar una auditoría de escrituras (operación, identificador y resultado) sin contenido de las notas ni valores.

#### Scenario: Escritura desde otro origen
- **WHEN** una escritura llega con un `Origin` distinto del propio panel
- **THEN** se rechaza con 403

#### Scenario: Error interno
- **WHEN** ocurre un error inesperado
- **THEN** la respuesta es genérica y no revela rutas ni trazas

#### Scenario: Auditoría
- **WHEN** se realiza una escritura
- **THEN** queda registrada la operación, el identificador afectado y el resultado, sin valores

### Requirement: Descubrimientos y revisión de hipótesis
El panel MUST ofrecer una sección de descubrimientos con la última revisión semanal y los contrastes de hipótesis con su evidencia enlazada, MUST permitir declarar, confirmar, matizar y descartar, y MUST someter estas operaciones a las mismas garantías de acceso, límites y convivencia con Mira que el resto de escrituras.

#### Scenario: Ver la revisión
- **WHEN** el usuario abre descubrimientos
- **THEN** ve la última revisión semanal, o el progreso de datos si aún está en observación

#### Scenario: Seguir la evidencia
- **WHEN** el usuario abre un contraste
- **THEN** navega a los registros que lo sustentan

#### Scenario: Revisar desde el panel
- **WHEN** el usuario confirma, matiza o descarta un contraste
- **THEN** el cambio queda en el historial con origen usuario

#### Scenario: Mira procesando
- **WHEN** Mira está procesando
- **THEN** la escritura se rechaza con conflicto, como el resto de escrituras

### Requirement: Sección de perfil
El panel MUST ofrecer una sección de perfil que permita solicitar su generación, ver su calendario y la última ejecución u omisión,, leer cada rasgo con su certeza y su evidencia enlazada, corregirlo, matizarlo, descartarlo y borrar el perfil entero, exigiendo en todas las operaciones la misma sesión autorizada, los mismos límites y la misma protección que el resto de la API, y MUST NOT servirlo de forma pública ni cachearlo.

#### Scenario: Pedir una versión
- **WHEN** el usuario pide generar el perfil desde el panel
- **THEN** se lanza una única generación y la página muestra el resultado o el progreso de datos

#### Scenario: Revisar un rasgo
- **WHEN** el usuario abre un rasgo
- **THEN** ve su categoría de certeza y puede ir a los registros que lo sustentan

#### Scenario: Sin sesión autorizada
- **WHEN** se solicita el perfil sin una sesión de un correo autorizado
- **THEN** se rechaza sin devolver datos

#### Scenario: Borrar el perfil
- **WHEN** el usuario confirma el borrado
- **THEN** se elimina el perfil y su historial

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
