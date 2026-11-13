# Mira — Contrato de comportamiento del agente

Estado: propuesta de la change `agente-mira`. Describe **qué hace** Mira, no cómo se implementa: es independiente del proveedor de IA, del modelo y del lenguaje (la tecnología se decide aparte, X32). Cualquier implementación debe cumplir este documento y las specs `procesamiento-de-capturas`, `preguntas-de-mira` y `privacidad-del-procesamiento`. Los formatos de ficheros están en `docs/formato-de-ficheros.md`; casos de prueba en `docs/evaluacion-mira.md`.

## 1. Principios

1. **Nunca inventar.** Lo no dicho es `desconocido` o se pregunta; no se fabrican cifras ni se supone que algo se resolvió.
2. **El original no se toca.** Mira lee capturas; no las modifica nunca.
3. **Cobertura completa (X37).** Todo lo que el usuario cuenta genera registro, también lo cotidiano. Mira no decide qué importa; filtrar es cosa del panel.
4. **Lo del usuario manda (X30).** Lo que el usuario corrige o completa es válido y no se pisa.
5. **Trazable.** Cada registro cita su fragmento, su certeza, su versión de Mira y su lote.
6. **Pocas preguntas, estructurales (X13, X36).**
7. **Fallo cerrado en privacidad (X38).**

Tono en todo texto que Mira escribe (títulos, descripciones, preguntas): cercano, sereno y neutro; sin entusiasmo, valoraciones ni consejos no solicitados.

## 2. Ciclo de una ejecución (tareas 1.1)

Una ejecución puede ser **periódica** (por defecto de noche) o **a petición** (`ejecucion: lote | peticion` en el lote).

1. **Comprobar privacidad (§7).** Si no se cumplen las condiciones, se detiene sin enviar nada, deja las capturas pendientes y lo anota en un lote con `problemas`.
2. **Seleccionar capturas:** las que no figuran en ningún lote, en **orden cronológico** de `comunicada_el` (así las continuaciones y contradicciones se resuelven en orden). Una captura ya procesada solo se vuelve a procesar por petición expresa o por una nueva versión de Mira que el usuario autorice.
3. **Preparar la entrada** de cada captura (§7.1): texto, fecha de comunicación, metadatos del usuario, claves existentes de esa captura y registros candidatos relacionados.
4. **Extraer** (§3): dividir en fragmentos, producir registros, relaciones, conflictos y candidatas a preguntas.
5. **Validar** (§5) cada fichero antes de guardarlo.
6. **Escribir** registros y preguntas.
7. **Anotar el lote** con la captura **solo si todos sus ficheros se guardaron**.
8. **Repetir** con la siguiente captura. Un fallo en una captura no impide procesar las demás.

### Recuperación ante fallos

- Una captura figura en un lote **únicamente cuando todos sus registros están guardados**; si no, sigue pendiente.
- Si Mira se interrumpe tras guardar registros pero antes de anotar el lote, al reintentar **reclama las claves de extracción existentes** (§4) y no duplica: la idempotencia es la recuperación.
- Los ficheros se escriben completos (se prepara el contenido, se valida y se guarda); nunca queda un fichero a medias dado por bueno. Un fichero que no pasa la validación no se guarda.
- Todo problema se anota en el lote (`problemas`: captura, motivo) y no detiene el resto.

## 3. Reglas de extracción por campo (tarea 1.2)

La salida de cada registro sigue `docs/formato-de-ficheros.md`. Reglas de contenido:

| Campo | Regla |
|-------|-------|
| `fuentes.fragmento` | Rango exacto del texto del que sale el registro; `cita` es el inicio literal. Una captura se divide en tantos registros como hechos distintos contenga. Un mismo hecho no se parte en dos. |
| `clase` | `actividad` (algo que hace), `evento` (algo que ocurre o una reunión), `idea`, `decision`, `problema`. `otro` solo si ninguna encaja. |
| `titulo` / cuerpo | Neutros, descriptivos, fieles. Sin valoraciones ni añadidos. |
| `fecha` | Fecha o referencia explícita → `dicho`. Expresión relativa («ayer», «la semana pasada», «el lunes») → se resuelve contra `comunicada_el`, queda `interpretado` y se conserva `expresion`. Metadato `fecha_acontecimiento` del usuario → `dicho`. Sin ninguna referencia temporal → el día de la comunicación, `interpretado`. Una imprecisión se guarda como rango, nunca como un día exacto inventado. |
| `hora` | Hora explícita → `valor`, `dicho`. «Sobre las…» → `aproximada: true`. Solo una franja («por la mañana») → solo `expresion`, **sin `valor`**. Nunca se deduce una hora. |
| `duracion` | Solo se escribe `minutos` si el usuario dio una cifra. Algo vago («un rato», «toda la tarde») → solo `expresion`. Ausente → `desconocido`. |
| `personas` | Los nombrados. No se deducen parentescos ni roles que el usuario no dijo. |
| `entidades` | Organizaciones, clientes, sistemas o productos nombrados, con nombre normalizado de erratas. `tipo` solo si está claro. Una entidad deducida va `interpretado`. Entidad = a qué se refiere el registro; herramienta = con qué se hace algo. |
| `herramientas`, `lugares`, `proyectos` | `dicho` si los nombra; `interpretado` si los deduce. |
| `acciones`, `resultados`, `problemas`, `ideas` | Solo lo dicho. Si no se dice cómo acabó algo, `resultados: desconocido`; **nunca se supone que se resolvió**. Si se dice que hubo acuerdos sin detallarlos, se registra que los hubo y que no se indican. |
| `contexto` | Solo lo dicho. **No se generan motivos ni causas no dichas.** |
| `relaciones` | `dicho` si el texto las conecta de forma explícita («ya que…», «mientras…»); `interpretado` si se deducen por cercanía. |
| `multidia`, `entradas` | Actividad que el usuario deja abierta («aún no he terminado», «mañana sigo») → un único registro con `multidia.estado: abierto`. |
| Erratas | En los campos estructurados se normalizan («gullon» → «Gullón»); la captura conserva el texto literal. Una errata dudosa se conserva tal cual. |

**Cobertura completa:** comida, asuntos domésticos y cualquier otro hecho cotidiano generan registro igual que el trabajo. Si una captura no contiene hechos registrables, no se crea registro, la captura queda intacta y el lote lo indica.

## 4. Claves, protegidos, aportaciones y conflictos (tarea 1.3)

**Reclamo de claves.** Al procesar una captura que ya tiene registros, Mira recibe sus claves de extracción. Cada elemento extraído **reclama** una clave existente o recibe una nueva; las claves no reclamadas **se conservan** y Mira nunca elimina registros. La clave se asigna una vez y nunca se recalcula.

**Campos protegidos (X30).** Un campo es protegido si tiene certeza `confirmado` o alguna entrada de `historial` de origen `usuario`. Mira no lo modifica, aunque su nueva interpretación sea distinta; puede añadir información a su alrededor y dejar una pregunta pendiente. El resto de campos se actualizan, con entrada de `historial` (`origen: mira`) y `procedencia.actualizado_*`. Una **nueva versión de Mira** actualiza solo lo no protegido, con historial.

**Rechazos.** Una interpretación que figura en `rechazos` no se vuelve a proponer sin evidencia nueva sustancial.

**Aportaciones.** Si una captura amplía un registro existente de forma clara (p. ej. continúa o cierra una actividad abierta), se añade como `fuente` (`rol: ampliacion`) y, si procede, como entrada fechada; no se crea un registro nuevo.

**Coincidencia dudosa.** Si no está claro que se hable de lo mismo, los registros **no se fusionan** y se crea una pregunta pendiente.

**Contradicciones.** Si una captura da otro valor para lo que parece el mismo hecho, se conservan ambos registros, se marca `conflictos` en los dos (simétrico), no se elige ganador y se crea una pregunta pendiente.

## 5. Validación antes de escribir (tarea 1.4)

Todo fichero se comprueba antes de guardarse. Si falla, no se guarda; se reintenta una vez o se anota en el lote.

- Ficheros: nombre `<id>.md`, ruta según el id, YAML válido (textos con `:` entre comillas; instantes dentro de `{}` entre comillas).
- Obligatorios presentes; `clase` y `tipo` de entidad válidos; primera fuente con `rol: origen`.
- **Fragmentos:** `desde < hasta` dentro del cuerpo de la captura y `cita` prefijo del fragmento.
- `comunicada_el` igual a la de la captura de origen; `clave_extraccion` única y coherente con la captura de origen y la clase.
- **Certeza:** valor válido en cada entrada; todo campo con valor tiene certeza (salvo `desconocido`); un campo no lleva certeza y está `desconocido`; en `entidades`, certeza por campo o por elemento, nunca ambas.
- `hora`: formato `HH:MM`, aproximada solo con `valor`, certeza propia, solo en acontecimientos de un día.
- Referencias existentes (capturas, registros, preguntas, relaciones); conflictos simétricos y con pregunta; wikilinks válidos; cuerpo con enlace a la captura de origen.
- `multidia` coherente (cerrado con fin, abierto sin fin, con entradas).
- Un campo rechazado no conserva el valor rechazado; un campo corregido por el usuario queda `confirmado`; `historial` en orden cronológico.
- La captura procesada tiene su `huella` correcta.

## 6. Preguntas (tareas 2.1 y 2.2)

**Qué se pregunta.** Solo datos estructurales de un registro: fecha, hora, duración, personas, lugar, proyecto, y estado o resultado de lo que queda abierto. **Nunca el contenido sustantivo** de lo hablado o decidido (qué acuerdos hubo, por qué se decidió algo).

**Candidatas y valor.**

| Valor | Cuándo |
|-------|--------|
| `alta` | Conflictos y coincidencias dudosas; asuntos que quedan abiertos (¿se resolvió?). |
| `media` | Fecha, hora, duración, personas, lugar o proyecto de un hecho relevante. |
| `baja` | Datos accesorios (p. ej. proyecto de una idea suelta). |

**Máximo por revisión.** Tras cada ejecución hay como mucho **N preguntas pendientes presentadas** (N configurable; **por defecto 5**), ordenadas por valor. Una candidata de valor mayor puede desplazar a una pendiente de valor menor, que pasa a `cerrada_por_limite`; las candidatas de menor valor no se crean. Las descartadas pueden volver a surgir en otra ejecución si el dato sigue ausente y la pregunta no se cerró como «no lo recuerdo» o «no relevante».

**Cierre.** `respondida` (el dato pasa a `confirmado`, la respuesta es la evidencia), `no_recuerda`, `no_relevante` (ambas: no se repite ni se formulan equivalentes sobre ese dato) o `cerrada_por_limite` (también si el registro ya cubre el valor esperado, X19).

**Formulación.** Una pregunta, un dato. Autocontenida: nombra el registro por su título, de modo que se entienda sin abrir nada. Sin juicio ni consejo. Tono sereno y curioso.

| Válida | No válida |
|--------|-----------|
| «¿Quedó resuelta la incidencia de los registros de Gullón?» | «¿Qué problemas había en los registros de Gullón?» (contenido) |
| «¿Cuánto duró la reunión de seguimiento con GH?» | «¿A qué acuerdos llegasteis con Laura?» (contenido sustantivo) |
| «Hablas de una llamada con Marta «ayer» y de otra «la semana pasada»: ¿son la misma?» | «¿Por qué decidiste no automatizarlo?» (motivo) |
| «¿La idea de lanzar el script con cron pertenece a algún proyecto?» | «Deberías automatizar el script, ¿lo harás?» (consejo) |

## 7. Privacidad (tareas 3.1–3.3)

### 7.1 Inventario

| Sale hacia el proveedor de IA | Nunca sale |
|-------------------------------|------------|
| Texto de la captura que se procesa | Audio |
| Fecha de comunicación y metadatos que dio el usuario (fecha del acontecimiento, etiqueta) | Ubicación o cualquier monitorización |
| Claves de extracción de esa captura | Otras capturas no relacionadas |
| Registros candidatos relacionados, **solo** con: `id`, `clase`, `titulo`, `fecha`, `personas`, `entidades`, `proyectos`, `multidia.estado` (ventana reciente configurable, por defecto 14 días, y solo si comparten personas, entidades, proyectos o una actividad abierta) | Ficheros fuera de `orumi/`, otros sistemas, credenciales, datos de otras personas del usuario |
| | Historial, preguntas y respuestas del usuario, salvo que sea imprescindible para no pisar una corrección |

Los textos que Mira genera con ayuda del proveedor se guardan solo en `orumi/`. No hay ningún otro destino de datos del usuario.

### 7.2 Condiciones al proveedor y fallo cerrado

Mira solo usa un proveedor cuyas condiciones **impidan emplear los datos del usuario para entrenar o mejorar sus productos** (en la práctica, el nivel de pago; el gratuito de algunos proveedores no lo garantiza, X9). La comprobación se hace **antes de cada ejecución**. Si el servicio configurado no lo garantiza, o no puede comprobarse:

- no se envía ningún dato,
- las capturas siguen pendientes,
- el lote anota el problema y el proveedor configurado.

### 7.3 Constancia

Cada lote indica el proveedor y el modelo usados. Cambiar de proveedor requiere que cumpla 7.2 y queda registrado en los lotes siguientes.

## 8. Fuera de alcance

Detección de patrones, hipótesis e informes (change posterior, X20–X23); transcripción de voz; API de entrada; app; panel; notificación de preguntas pendientes; elección de proveedor, modelo, lenguaje y planificador (X32).

## 9. Notas de implementación del MVP (change `mvp-mira`)

Decisiones de detalle tomadas al implementar, coherentes con las specs:

- **Fragmento localizado por el código.** El proveedor devuelve el texto literal del fragmento y Mira calcula su posición (caracteres Unicode) y la cita; si no está en la captura, la captura no se procesa.
- **Solo se citan registros ofrecidos.** `amplia`, `duda`, `en_conflicto_con` y las relaciones solo pueden apuntar a los registros que se enviaron al proveedor; cualquier otra referencia se rechaza.
- **Preguntas filtradas por campo.** Solo se admiten fecha, hora, duración, personas, lugares, proyectos, entidades, herramientas, estado/resultado y coincidencia. Por `resultados` solo se pregunta en actividades y problemas (¿se resolvió?); en un evento o una decisión el resultado es contenido y no se pregunta. Los conflictos y las coincidencias dudosas generan siempre su pregunta automática, de valor alta.
- **No relevante desalienta lo equivalente:** mismo campo y misma clase de registro. «No lo recuerdo» o una respuesta cierran el dato para ese registro.
- **Un conflicto nunca queda sin pregunta:** si el cupo no permite crearla, la captura no se procesa.
- **Reprocesado.** Es siempre explícito (`--reprocesar CAPTURA`). Si el proveedor no reclama la clave, Mira reconoce el registro por su fragmento de origen y su clase; nunca pierde un dato conocido por recibir «desconocido».
- **Recuperación.** Los registros que una ejecución interrumpida dejó guardados sin lote se adoptan como creados en el siguiente lote.
- **El lote indica el proveedor y el modelo** (`proveedor`, `agente.modelo`) y los problemas sin reproducir contenido.

## 10. Control del saldo y del gasto (change `control-de-saldo`)

- **Errores del proveedor que cortan la ejecución:** saldo agotado o problema de facturación (402, `billing_error` o un mensaje de saldo insuficiente), límite de gasto (429 con `enforced_spend_limit_reached`, o 400 «You have reached your specified API usage limits») y clave inválida (401). Mira no reintenta las demás capturas, las deja pendientes, escribe un lote con `problemas[].tipo` y una explicación de qué hacer, y termina con código **7** (saldo o límite) u **8** (clave). Cualquier otro error solo deja pendiente esa captura. Tras recargar, la siguiente ejecución retoma sola: la captura no está en ningún lote.
- **Uso y coste estimado:** cada respuesta informa de los tokens de entrada y salida; el lote guarda el uso por captura y el total de la ejecución (aunque la salida no se pudiera guardar, porque la llamada ya se cobra). Los precios salen de la configuración (`precios`); sin ellos el coste es `desconocido`.
- **Presupuesto** (`presupuesto.saldo_usd`, `desde`, `aviso_porcentaje`, 90 por defecto): se suma el coste estimado de los lotes desde esa fecha; al llegar al porcentaje y al superar el saldo hay aviso en el lote y en la consola. **Solo avisa**: la estimación no detiene nada, porque no incluye lo que gaste el *playground* de la consola ni otras claves.
- **Dónde se ve:** `orumi estado` (pendientes, último lote, gasto estimado, avisos) y una línea `⚠ Mira: …` por la salida de error al ejecutar `capturar` o `validar`. Se deriva de los lotes, sin estado propio, y desaparece cuando un lote posterior termina bien.
- **Limitaciones:** la API de Claude no expone el saldo real a cuentas individuales (el uso y el coste solo están en una API de administración que requiere una organización y una clave de administrador, y que no devuelve el saldo restante); no hay avisos por correo o móvil hasta que exista el panel o la app.

## 11. Procesamiento diferido por lotes (change `procesar-con-descuento-por-lotes`)

- **Modo por lotes** (`procesamiento.modo: lotes` en la configuración, por defecto en el lote nocturno): cada ejecución (1) recoge los envíos abiertos que el proveedor ya haya terminado, (2) envía como **un único lote** las pendientes que no estén en un envío abierto, (3) espera hasta `espera_maxima_minutos` (90 por defecto), consultando cada `intervalo_segundos`, y (4) escribe el lote de Mira y marca los envíos como recogidos. Lo que no termine queda abierto y se recoge en la siguiente ejecución.
- **Mismas reglas:** cada resultado correcto pasa por la misma validación, claves, protegidos, aportaciones, conflictos y preguntas que el modo inmediato. Un resultado con error, cancelado, expirado, ausente o con formato inválido deja pendiente solo esa captura; el consumo de las llamadas ya hechas se contabiliza igualmente.
- **Recuperación:** el envío se escribe justo después de crear el lote en el proveedor. Una interrupción tras enviar se recoge sin reenviar; una interrupción antes de escribir el lote de Mira se recoge otra vez sin duplicar (los registros guardados se reclaman por fragmento y clase y se adoptan). Único hueco: si el proceso muere entre crear el lote en el proveedor y escribir el fichero, el lote queda huérfano (solo cuesta si termina) y la captura se reenvía.
- **Saldo, límite y clave** siguen cortando la ejecución, al enviar o al recoger; un error de saldo al recoger deja el envío abierto.
- **Coste:** `precios.factor_lote` (0,5 por defecto) se aplica al estimar el coste del lote diferido.
- **Modo inmediato:** `orumi procesar --inmediato si` procesa nota a nota con la API normal y a precio completo, para ver el resultado en el momento.
- **Privacidad:** igual que el modo inmediato (garantía declarada y vigente), con una salvedad: la API de lotes retiene los datos del lote **29 días** y no admite retención cero; las Condiciones Comerciales siguen prohibiendo entrenar con ellos.

## 12. Respuestas y correcciones del usuario (change `responder-y-corregir`)

- `orumi responder PREGUNTA VALOR`: aplica el valor al campo por el que se preguntó (confirmado, con historial de origen `usuario` y enlace a la pregunta), guarda el texto literal como `respuesta` y cierra la pregunta como `respondida`. `--no-recuerdo` / `--no-relevante` la cierran sin tocar el registro (y no se repite). En un conflicto, `--registro ID` indica a qué registro corresponde el valor y el conflicto queda `resuelto` en ambos; en una coincidencia dudosa, `misma` (relación `relacionado_con` confirmada, sin fusionar) o `distinta`.
- `orumi corregir REGISTRO CAMPO VALOR` y `orumi rechazar REGISTRO CAMPO` (solo interpretaciones de Mira: pasan a `rechazos` y el campo queda `desconocido`).
- Los valores se interpretan con reglas deterministas, sin modelo: fecha `AAAA-MM-DD[..AAAA-MM-DD]`, hora `HH:MM`, duración `90`, `90m`, `2h`, `1h30`, listas separadas por comas, entidades `Nombre[:tipo]`. Lo que no encaja se rechaza sin modificar nada.
- Se valida el resultado antes de escribir (si aparece un error nuevo no se escribe nada), la escritura es atómica, no se tocan capturas y no se ejecuta mientras Mira procesa (código de salida 3). Mira respeta lo corregido en los reprocesados (X30).
- `orumi consultar pendientes` lee los ficheros y muestra, por pregunta, el registro, el campo, el valor actual y el otro registro implicado.

## 13. Robustez ante la salida del modelo (primeras notas reales, 2026-11-13)

- **Razonamiento interno desactivado** (`thinking` desactivado) y límite de salida de 16 000 tokens: con Sonnet 5.5 el razonamiento oculto consumía el límite de 8192 y cortaba el JSON en las notas largas (`stop_reason: max_tokens`). Una salida cortada deja la captura pendiente con el motivo «la salida del modelo se cortó por el límite de tokens» y **su consumo se contabiliza** (también el de cualquier salida inservible, que antes no se sumaba).
- **Certeza omitida:** si el modelo da un valor sin certeza, se anota `interpretado` (la más débil; nunca se supone `dicho`), en lugar de rechazar el registro entero. Mismo criterio que para relaciones y estados.
- **Depuración:** con `ORUMI_DEPURAR_DIR` definido, la salida cruda no legible se vuelca en ese directorio. Está apagado por defecto: esa salida contiene texto del usuario y nunca se registra.

