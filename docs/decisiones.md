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
- **Atlas** (gestor de ficheros `.md`, sin BD) y **vedlvm** (repo de contenido) son reutilizables; la filosofía ficheros + git coincide con D5/D6.
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
| X1 | **Acceso del agente a los ficheros:** ambas vías válidas, por SSH directo al servidor o por la API de Atlas. Se elegirá según el caso; por defecto la más simple. | 2026-11-12 | G3 (parcial), G5 |
| X2 | **Datos de ORUMI en `orumi/`** (separados de las notas personales). **Los análisis y las revisiones generan además un duplicado `.md` que se copia a vedlvm como backup.** | 2026-11-12 | G4 |
| X3 | **Índice derivado y reconstruible** (SQLite o Postgres existente) generado desde los ficheros para consultas estructuradas; los ficheros siguen siendo la fuente de verdad. Atlas queda como visor/editor de ficheros crudos. | 2026-11-12 | G6, D5 |
| X7 | **Entrada de la captura:** API propia de ORUMI como destino final (la usará la app Flutter, con token propio). **En el MVP se empieza dejando ficheros directamente en `orumi/` (SSH/sincronización)** y se añade la API al llegar la app móvil. | 2026-11-12 | entrada de captura |
| X8 | **El agente se llama Mira.** | 2026-11-12 | D9 |
| X9 | **Proveedores de IA (provisional):** Gemini Flash-Lite de pago para transcribir y limpiar; modelo más capaz solo para análisis de patrones por lotes. Precios y calidad se verifican en detalle antes de contratar. | 2026-11-12 | D1, D2 |
| X10 | **Granularidad:** cada captura se guarda cruda (original) y cada registro que extrae Mira (evento, idea, decisión…) es su propio `.md` enlazado a su captura. | 2026-11-12 | D7 |
| X11 | **Certeza** (dicho, confirmado, interpretado, hipótesis, recomendación) como campo en el frontmatter de cada registro. | 2026-11-12 | D7 |
| X12 | **Sin respaldo de ORUMI en vedlvm** (D17): ningún dato de ORUMI sale hacia vedlvm/GitHub. **Sustituye la parte de copia en vedlvm de X2.** El respaldo de `orumi/` queda por definir (ver nota). | 2026-11-12 | D17, X2 |
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
| X4 | **Agente por lotes nocturno + a petición.** Las preguntas se acumulan para revisarlas cuando el usuario quiera. | 2026-11-12 | D8 |
| X5 | **El audio se borra tras transcribirlo.** La transcripción cruda se conserva siempre y pasa a ser el «original» del sistema. | 2026-11-12 | D6, G1 |
| X6 | **Orden de construcción:** texto (entrada propia de ORUMI, no el inbox de Atlas) + agente + modelo de registro primero; voz (Flutter) y dashboard después. | 2026-11-12 | D15, D19 |

Notas abiertas derivadas:
- **X20–X23 (patrones):** se implementarán en una change posterior de análisis (no en `modelo-de-registro`). El modelo de registro ya admite hipótesis con evidencia enlazada y rechazo de interpretaciones.
- **X12, respaldo de `orumi/` (abierta):** al descartar vedlvm, hace falta decidir cómo se respalda `orumi/` (p. ej. repo git propio con remoto privado o copia local en otro disco/equipo). Hasta entonces el único respaldo es el disco del servidor.
- **X5 y principio 3.2:** al borrar el audio, la transcripción cruda es el original. Mitigación acordada de facto: no pasarla por ningún LLM antes de guardarla (el STT no «limpia»), y verificar que la transcripción se guardó antes de borrar. Si la calidad del STT en español resulta insuficiente en las pruebas, se reabre (p. ej. con caducidad de N días).
- **X2, ubicación de `orumi/`:** confirmado 2026-11-12: ORUMI tiene sus propios ficheros, en un directorio/repo propio en el servidor, distinto de vedlvm. **No se usa el `/api/inbox` de Atlas.** El agente crea los ficheros en `orumi/` y después, por SSH, genera la copia `.md` en vedlvm (backup).
- **X2, alcance del duplicado:** «análisis y revisiones» = informes de patrones y revisiones del agente. Falta decidir si los registros/eventos individuales también se duplican y cómo se evita que el duplicado diverja del original (se propone: el duplicado es de solo lectura/generado, nunca se edita).
- **X1:** si el agente usa la API de Atlas sigue haciendo falta un token con permiso de lectura (G3).
- **G2** (no usar el cifrado de Atlas para ORUMI) se da por aceptado al validar el punto 2 de la propuesta; el duplicado en vedlvm queda en claro.
