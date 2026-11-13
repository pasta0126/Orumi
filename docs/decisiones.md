# ORUMI — Registro de decisiones y dudas abiertas

Documento vivo. Aquí se aglutinan todas las dudas; se resuelven una a una. Al cerrar una, pasa a "Decididas" con su motivo y, si afecta a comportamiento, se refleja en una change/spec de OpenSpec.

## Criterios rectores (decididos)

| # | Decisión | Motivo |
|---|----------|--------|
| C1 | **Coste mínimo, idealmente gratis.** Se compara precio antes de elegir cualquier servicio. | Proyecto personal. |
| C2 | **Reutilizar software propio** ya desarrollado, aunque haya que ajustarlo. | Ahorro y coherencia. |
| C3 | **Preferir ecosistema Google** y similares cuando minimicen coste o fricción. | Ecosistema integrado. |
| C4 | **Proveedor de IA (Claude / Codex / otros): por decidir** comparando planes y precios. | Ver D1. |
| C5 | Stack previsto: app **Android con Flutter**; STT con IA; API; BD; dashboard web. Sujeto a revisión. | Idea del fundador. |

## Flujo conceptual propuesto

`Flutter (grabar) → STT → LLM (limpiar/ordenar) → API → fichero → agente (eventos/acciones) → API → BD → dashboard (consultar/corregir) → agente (patrones)`

## Dudas abiertas (agrupadas)

### A. Coste y proveedores
- **D1. Acceso a IA.** Las suscripciones de chat (ChatGPT/Claude) no dan acceso programático; la API es de pago por uso. Comparar: API de pago, niveles gratuitos (p. ej. Gemini), modelos locales, o agente manual con CLI. Medir coste real estimado (volumen de audio/texto/día).
- **D2. STT.** Opciones a comparar por coste/calidad en español: servicio de Google, Whisper local o API, reconocimiento en dispositivo.
- **D3. Hosting de API y BD.** Reutilizar infraestructura propia o capa gratuita de un proveedor.

### B. Datos y fuente de verdad
- **D4. Inventario de software propio reutilizable.** *Revisado (ver sección «Inventario»).* Pendiente revisar otros proyectos (Kanban JSON, monitor de logs, Postgres `platform-*`).
- **D5. Fuente de verdad.** Propuesta: ficheros mandan, BD es índice reconstruible. Edición desde el dashboard debe volver al fichero.
- **D6. Conservar el original.** Audio y transcripción cruda se guardan antes de cualquier LLM, para poder reprocesar.
- **D7. Formato del registro** y de los ficheros (modelo de registro, procedencia, categorías de certeza).

### C. Agente
- **D8. Cuándo corre:** tras cada subida, por lotes (p. ej. nocturno) o a petición.
- **D9. Nombre del agente.**
- **D10. Idempotencia y reprocesado:** reprocesar un fichero no duplica eventos; registrar versión de agente/prompt/modelo usada.
- **D11. Preguntas:** cuándo inmediatas vs. pendientes, prioridad, dónde se responden (app móvil, dashboard, notificación).
- **D12. Contradicciones, eventos multi-día, fechas imprecisas, cuándo un registro está "completo".**
- **D13. Patrones:** evidencia mínima, frecuencia de presentación, confirmación/descarte de hipótesis. No aplicable hasta acumular historial.

### D. Captura (app Flutter)
- **D14. Offline:** cola local de grabaciones y sincronización posterior.
- **D15. Entrada de texto** en la primera versión (propuesta: sí, es barato y permite probar el agente antes).
- **D16. Grabaciones multi-tema y metadatos que las acompañan.**

### E. Seguridad y privacidad
- **D17. Autenticación de la API** y qué datos salen a terceros (STT/LLM). Registrar la decisión en una spec.

### F. Panel web
- **D18. Pantalla principal, visualización de preguntas pendientes, mecanismos de corrección, peso de la conversación directa.**

### G. Estrategia de construcción
- **D19. Orden.** Propuesta: validar primero el agente y el modelo de registro con texto/audio manual y ficheros; después app y dashboard.

## Inventario de infraestructura

Los detalles de la infraestructura propia (servidor, dominios, servicios y huecos de seguridad detectados) están en `docs/privado/infraestructura.md`, **fuera de git** porque el repositorio puede ser público. Resumen no sensible:

- El servidor de despliegue es una máquina pequeña propia con Docker y proxy inverso con TLS.
- **Atlas** y **vedlvm** quedan fuera de ORUMI (X34); solo se aprovecha la filosofía ficheros + git (D5/D6).
- Huecos a resolver (G1–G9): audio fuera de git, no usar el cifrado de Atlas para ORUMI, tokens con alcance, separación de datos, índice derivado, concurrencia, cómputo limitado, y endurecimiento de acceso. Resueltos o aceptados en X1–X15.

## Investigación de costes D1/D2 (2026-11-12)

Fuentes: páginas oficiales de Gemini (precios) leídas directamente; el resto de cifras salen de blogs de terceros y **no están verificadas**. Revisar antes de comprometerse.

**Volumen supuesto (a validar con uso real):** ~10 capturas/día, ~1M tokens de entrada y ~1M de salida al mes entre limpieza y agente.

| Opción | Precio (por 1M tokens, entrada / salida) | Coste estimado/mes | Notas |
|--------|------------------------------------------|--------------------|-------|
| Gemini 2.5 Flash-Lite | 0,10 $ / 0,40 $ (audio 0,30 $) | < 1 $ | Oficial. Más barato. |
| Gemini 3.5 Flash-Lite | 0,30 $ / 2,50 $ (audio 0,30 $) | ~3 $ | Oficial. 2.5 Flash enruta a este el 1-dic-2026. |
| Claude Haiku 5.5 | 0,10 $ / 0,50 $ (batch -50 %) | < 1 $ | Terceros, sin verificar. |
| Claude Sonnet 5.5 | 2 $ / 10 $ (batch -50 %) | ~12 $ | Terceros, sin verificar. Para análisis de patrones. |

**Conclusiones:**
1. **El coste de IA es marginal** (céntimos a pocos euros al mes) con este volumen. Lo gratis no compensa.
2. **Privacidad pesa más que el precio.** En el nivel gratuito de Gemini *las peticiones se usan para mejorar productos de Google* (oficial); en el de pago no. Para datos de vida personal, recomendación: **nivel de pago, no gratuito.**
3. **Suscripciones (Claude Pro/Max) con `claude -p` en el servidor:** las fuentes se contradicen sobre si el uso automatizado cuenta contra la suscripción o va a tarifa API (cambio de junio 2026, posiblemente pausado). **Sin verificar**; comprobar en la documentación oficial de Claude Code antes de apoyarse en ello. A este volumen la API es más predecible.
4. **STT:** Google Cloud Speech-to-Text ~0,016 $/min estándar y ~60 min gratis/mes (sin verificar); OpenAI transcribe ~0,003 $/min (sin verificar); Whisper local es gratis pero lento en la RPi 4. **Gemini acepta audio directamente** (audio a 0,30 $/M en 3.5 Flash-Lite, oficial), lo que permitiría transcribir con el mismo proveedor y una sola factura. Habría que probar la calidad en español y mantener la transcripción como paso aparte y literal (X5).
5. El reconocimiento en el dispositivo (Android) es gratis pero de calidad y duración limitadas; sirve de respaldo, no como base.

**Propuesta provisional (a decidir):** Gemini de pago para transcribir y limpiar (Flash-Lite), y un modelo más capaz (Sonnet o Gemini Flash) solo para el análisis de patrones por lotes. Reevaluar tras probar la calidad con tus propias notas.

## Orden sugerido de resolución

1. D4 revisado → decidir G1–G9 (sobre todo G2, G4 y G6), que condicionan D3, D5 y D7.
2. D1/D2 (precios y proveedores) → condiciona D8.
3. D5–D7 (modelo de datos) → primera change de OpenSpec.
4. Resto según se necesite.

## Decididas

Además de los criterios rectores (C1–C5):

| # | Decisión | Fecha | Resuelve |
|---|----------|-------|----------|
| X1 | **Acceso del agente a los ficheros:** ~~por SSH o por la API de Atlas~~ → solo acceso directo al servidor (X34 descarta Atlas). | 2026-11-12 | G3 (parcial), G5 |
| X2 | **Datos de ORUMI en `orumi/`** (separados de las notas personales). *(La parte de duplicado en vedlvm quedó sustituida por X12.)* | 2026-11-12 | G4 |
| X3 | **Índice derivado y reconstruible** (SQLite o Postgres existente) generado desde los ficheros para consultas estructuradas; los ficheros siguen siendo la fuente de verdad. ~~Atlas como visor/editor de ficheros crudos~~ (descartado por X34). | 2026-11-12 | G6, D5 |
| X7 | **Entrada de la captura:** API propia de ORUMI como destino final (la usará la app Flutter, con token propio). **En el MVP se empieza dejando ficheros directamente en `orumi/` (SSH/sincronización)** y se añade la API al llegar la app móvil. | 2026-11-12 | entrada de captura |
| X8 | **El agente se llama Mira.** | 2026-11-12 | D9 |
| X9 | **Proveedores de IA (provisional):** Gemini Flash-Lite de pago para transcribir y limpiar; modelo más capaz solo para análisis de patrones por lotes. Precios y calidad se verifican en detalle antes de contratar. | 2026-11-12 | D1, D2 |
| X10 | **Granularidad:** cada captura se guarda cruda (original) y cada registro que extrae Mira (evento, idea, decisión…) es su propio `.md` enlazado a su captura. | 2026-11-12 | D7 |
| X11 | **Certeza** (dicho, confirmado, interpretado, hipótesis, recomendación) como campo en el frontmatter de cada registro. | 2026-11-12 | D7 |
| X12 | **Sin respaldo de ORUMI en vedlvm** (D17): ningún dato de ORUMI sale hacia vedlvm/GitHub. **Sustituye la parte de copia en vedlvm de X2.** El respaldo de `orumi/` se resuelve en X27. | 2026-11-12 | D17, X2 |
| X13 | **Preguntas de Mira:** siempre pendientes (nunca interrumpen), ordenadas por valor y con un máximo N por revisión. | 2026-11-12 | D11 |
| X14 | **App Android offline:** cola local y sincronización posterior; el audio local solo se borra tras confirmación del servidor. | 2026-11-12 | D14 |
| X15 | **Pantalla principal del panel:** cronología reciente + preguntas pendientes; descubrimientos en sección aparte; sin métricas de productividad. | 2026-11-12 | D18 |
| X16 | **Contradicciones:** Mira conserva ambas versiones, marca el conflicto en los dos registros, no elige ganador y lo deja como pregunta pendiente. | 2026-11-12 | D12 |
| X17 | **Eventos multi-día:** un único registro con inicio, fin, estado (abierto/cerrado) y entradas fechadas. | 2026-11-12 | D12 |
| X18 | **Fechas imprecisas:** se guarda la expresión original y un rango estimado marcado como interpretación, corregible por el usuario. | 2026-11-12 | D12 |
| X19 | **Completitud:** un registro es válido desde el principio y no hay estado «completo»; Mira deja de preguntar por saturación (valor cubierto, «no recuerdo» o límite de preguntas). | 2026-11-12 | D12 |
| X20 | **Evidencia mínima de un patrón:** ≥3 registros en ≥2 semanas distintas. Los umbrales son configuración ajustable, no parte fija del modelo. | 2026-11-12 | D13 |
| X21 | **Presentación de descubrimientos:** revisión semanal, sin interrumpir, con pocos patrones, en la sección de descubrimientos del panel; solo activa tras la etapa de observación (~4 semanas). | 2026-11-12 | D13 |
| X22 | **Revisión de hipótesis:** el usuario puede confirmar, matizar o descartar. Lo descartado no se vuelve a proponer salvo evidencia nueva sustancial; lo matizado se reescribe con su comentario. | 2026-11-12 | D13 |
| X23 | **Indicadores:** solo frecuencia observada y evidencia enlazada. Duración e impacto únicamente si el usuario los expresó; sin puntuaciones ni métricas de productividad. | 2026-11-12 | D13 |
| X24 | **Capturas con varios temas:** la captura se guarda entera e inmutable; Mira la divide en registros durante el lote, y cada registro referencia el fragmento de origen. El usuario puede fusionar o separar después. | 2026-11-12 | D16 |
| X25 | **Metadatos opcionales de una captura:** fecha del acontecimiento (si no es hoy), etiqueta o proyecto, y contexto automático (hora y dispositivo, sin ubicación ni monitorización). Los aportados por el usuario cuentan como «dicho por el usuario». | 2026-11-12 | D16 |
| X26 | **Aportación a registros existentes:** una captura nueva que amplía algo ya registrado se añade como nueva fuente/entrada al mismo registro; ante la duda, Mira pregunta. | 2026-11-12 | D16 |
| X27 | **Sin respaldo de `orumi/`:** los ficheros viven solo en el servidor. Riesgo aceptado conscientemente: si falla el disco, se pierde todo (incluidas las transcripciones, que son el único original). Reabrible si cambia la tolerancia al riesgo. | 2026-11-13 | X12 |
| X28 | **Audio y STT (confirma X5):** se mantiene el borrado del audio tras transcribir y verificar que la transcripción se guardó. Solo se reabre si las pruebas de STT en español salen insuficientes. | 2026-11-13 | X5 |
| X29 | **Autenticación y datos a terceros (D17):** token propio por cliente (app, panel) con alcance mínimo; en el MVP por ficheros no hay API que autenticar. Solo salen a terceros los datos que Mira envía al proveedor de IA de pago (X9), y esto se registra en una spec antes de implementar la API. | 2026-11-13 | D17 |
| X30 | **Reprocesado y aportaciones del usuario (D10):** lo que el usuario corrige o completa se asume válido y acertado. Mira lo tiene en cuenta, puede preguntar si necesita más información y puede adaptarlo o adecuarlo (p. ej. formato, relaciones), pero **nunca lo elimina ni lo sustituye por su propia interpretación.** Un reprocesado actualiza solo lo no tocado por el usuario. | 2026-11-13 | D10 |
| X31 | **Correcciones en el panel (D18):** mismo criterio que X30. Cualquier campo es corregible en línea, queda en el historial, pasa a «confirmado» y lo dicho por el usuario prevalece sobre cualquier interpretación de Mira. | 2026-11-13 | D18 |
| X32 | **Despliegue (D3):** la aplicación se despliega en el servidor propio por SSH, usando su infraestructura compartida. Las tecnologías concretas (API, índice, panel) quedan pendientes de definir en una change posterior. | 2026-11-13 | D3 |
| X33 | **Formato de ficheros (propuesto, pendiente de tu revisión):** ids `cap-`/`reg-`/`preg-`/`lote-` con fecha y sufijo aleatorio; clave de extracción `captura#f<n>:<clase>:<k>` asignada una vez y nunca recalculada; fragmentos por posición en el cuerpo inmutable; `huella` SHA-256 en las capturas; bitácora de lotes de Mira (las capturas no llevan estado); directorios `AAAA/MM`; certeza por campo. Detalle en `docs/formato-de-ficheros.md`, ejemplos en `docs/ejemplos/` e índice en `docs/indice-derivado.md`. | 2026-11-13 | D7, D10 |
| X34 | **ORUMI es autónomo:** no se integra con ningún otro software (Atlas, vedlvm u otros) por el momento. Sus ficheros, su índice, su agente y su panel son propios. Se revisará solo si surge una necesidad real. | 2026-11-13 | X1, X3, D4 |
| X4 | **Agente por lotes nocturno + a petición.** Las preguntas se acumulan para revisarlas cuando el usuario quiera. | 2026-11-12 | D8 |
| X5 | **El audio se borra tras transcribirlo.** La transcripción cruda se conserva siempre y pasa a ser el «original» del sistema. | 2026-11-12 | D6, G1 |
| X6 | **Orden de construcción:** texto (entrada propia de ORUMI, no el inbox de Atlas) + agente + modelo de registro primero; voz (Flutter) y dashboard después. | 2026-11-12 | D15, D19 |

Notas abiertas derivadas:
- **X20–X23 (patrones):** se implementarán en una change posterior de análisis (no en `modelo-de-registro`). El modelo de registro ya admite hipótesis con evidencia enlazada y rechazo de interpretaciones.
- **X5 y principio 3.2:** al borrar el audio, la transcripción cruda es el original. Mitigación acordada de facto: no pasarla por ningún LLM antes de guardarla (el STT no «limpia»), y verificar que la transcripción se guardó antes de borrar. Si la calidad del STT en español resulta insuficiente en las pruebas, se reabre (p. ej. con caducidad de N días). Ver X28.
- **X2, ubicación de `orumi/`:** confirmado 2026-11-12: ORUMI tiene sus propios ficheros, en un directorio/repo propio en el servidor, distinto de vedlvm. **No se usa el `/api/inbox` de Atlas.** El agente crea los ficheros en `orumi/`; no se copia nada a vedlvm (X12).
- **G2** (no usar el cifrado de Atlas para ORUMI) aceptado, y queda sin objeto: ORUMI no usa Atlas (X34).
- **D4 (aplazada):** otros proyectos propios (Kanban JSON, monitor de logs, Postgres `platform-*`) solo se miran como referencia o librería, sin integrarlos (X34); se retoma al elegir tecnologías (X32).
- **Prueba con nota real (2026-11-13):** una nota del usuario se convirtió a mano (haciendo de Mira) en 1 captura, 14 registros, 3 preguntas y 1 lote, en `data/orumi/` (fuera de git). El usuario revisó el resultado y lo dio por correcto y adecuado. Hallazgos para futuras changes: (1) los textos con `:` van entre comillas (ya en el formato); (2) falta un campo para entidades (clientes, empresas, sistemas) consultable; (3) `fecha` no guarda la hora; (4) pendiente decidir si Mira debe ignorar o guardar contenido cotidiano no profesional (comida, etc.). Sin probar aún: corrección del usuario sobre registros existentes (X30).
