## 1. Decisión y verificación del servicio

- [x] 1.1 Verificar con las condiciones y la documentación oficiales de Open-Meteo: uso gratuito sin clave, variables horarias y diarias disponibles (incluido UV) en previsión con `past_days` e histórico, límites y no reutilización de datos enviados; si no cumple, elegir alternativa y registrarlo
- [x] 1.2 Registrar en `docs/decisiones.md` y `docs/tecnologias.md`: X60 (servicio), X61 (privacidad del servicio meteorológico), X62 (ciudad indicada por el usuario: reinterpreta X25), X63 (preguntas de contraste: amplía X36), X64 (aprendizaje de Mira) y las definiciones del usuario (exposición > 1,5 h, trabajo 08:30–18:30, Montbau por defecto); fuente, condiciones y fecha en la configuración

## 2. Ciudad en las capturas

- [x] 2.1 Campo opcional `ciudad` en la captura (formato, validación, `docs/formato-de-ficheros.md`); las capturas existentes siguen válidas; origen «dicho por el usuario»
- [x] 2.2 Terminal (`orumi capturar --ciudad`) y web (campo opcional que recuerda la última ciudad solo en el dispositivo, sin geolocalización del navegador); la API de captura web lo acepta y valida
- [x] 2.3 Configuración: ciudad por defecto (Montbau, Barcelona), tabla de ciudades conocidas (inicialmente Barcelona y Segur de Calafell) con coordenadas redondeadas; ciudades nuevas confirmadas por el usuario se añaden a la tabla, zona horaria, horario laboral (08:30–18:30, días laborables)
- [x] 2.4 Pruebas: sin ciudad → por defecto; ciudad indicada se guarda; el original sigue inmutable y verificado; sin ubicación del dispositivo

## 3. Contexto meteorológico

- [x] 3.1 Cliente del servicio de clima (.NET, sin dependencias nuevas, tiempo de espera y reintento acotado, petición solo con coordenadas redondeadas, fechas y variables, sin cabeceras identificativas)
- [x] 3.2 Formato `clima/AAAA/MM/AAAA-MM-DD-<ciudad>.yaml`, escritura atómica, variables desconocidas explícitas; documentarlo en `docs/formato-de-ficheros.md`
- [x] 3.3 Comando `orumi clima` idempotente: solo día y ciudad con registros y sin clima, refresco de los recientes, relleno histórico inicial; ciudad no reconocida → pregunta y sin consulta; el fallo no bloquea `procesar`
- [x] 3.4 Índice: tablas `clima_dia` y `clima_hora` en `reindexar`, con consulta por fecha y ciudad
- [x] 3.5 Pruebas: petición mínima (sin datos personales), variable ausente → desconocida, servicio caído, ciudad no reconocida, día con dos ciudades, el modelo no recibe clima

## 4. Cierre «no sucedió», preguntas de contraste y aprendizaje

- [x] 4.1 Nuevo cierre «no sucedió» (formato de preguntas, `orumi responder`/`cerrar`, API y panel); efecto: ausencia confirmada, nunca se repite
- [x] 4.2 Clase de pregunta (campo + tema) guardada en cada pregunta y cierre; migración que deja válidas las preguntas existentes
- [x] 4.3a Preguntas de contraste: huecos deterministas (efecto sin dato en los días del contraste, equilibrando los grupos; duración de estar fuera ausente), pocas (≤ 3 por pasada y dentro del máximo de Mira), neutras, sin repetir un (concepto, día); solo tras la etapa de observación
- [ ] 4.3b Pregunta de ciudad: ciudad indicada que no se reconoce (no se consulta su clima hasta confirmarla) y texto que menciona otra ciudad. **Pendiente de decisión:** para ubicar una ciudad nueva hace falta geolocalizar su nombre (Open-Meteo tiene un servicio de geocodificación; solo saldría el nombre de la ciudad) y guardar las ciudades confirmadas en un fichero propio, porque la configuración es de solo lectura
- [x] 4.4 Almacén `aprendizajes/apr-*.yaml` y motor: ≥ 3 cierres «no relevante» en ≥ 2 días → prioridad baja de la clase; una respuesta con datos la restablece; la clase nunca se prohíbe; desactivación del usuario prevalece
- [x] 4.5 Aplicar los aprendizajes: orden y selección de candidatas (determinista) y preferencias en el prompt de interpretación, sin incluir textos de cierres; el prompt no permite inventar
- [x] 4.6 `orumi aprendizajes listar|desactivar|activar|borrar` y su equivalente en el panel
- [x] 4.7 Pruebas: umbral de aprendizaje, restablecimiento, «no sucedió» no enseña a callar, desactivación respetada, la clase nunca queda en cero, cierre equivalente sobre el mismo dato no se repite

## 5. Hipótesis y etiquetado

- [x] 5.1 Formato `hipotesis/hip-*.yaml` (formulación literal, factor, efecto, retardo, estado, historial) y validador; documentar en `docs/formato-de-ficheros.md`
- [x] 5.2 Comando `orumi hipotesis añadir|listar` y carga de las cuatro hipótesis iniciales con su formulación literal y las definiciones del usuario (exposición > 1,5 h en día soleado o despejado → cansancio físico; UV alto → dolor de cabeza; lluvia → relajación; socialización → agotamiento y luego soledad o introspección con retardo)
- [x] 5.3 Paso de etiquetado (en modo inmediato, X65; la API de lotes de X51 queda como mejora de coste; mismas garantías X38): solo texto de registros, solo lo dicho, con cita; duración de exposición y estar fuera; el trabajo no es socialización; guardado derivado y regenerable en `analisis/etiquetas/`; respeta correcciones del usuario
- [x] 5.4 Pruebas del etiquetado con respuestas simuladas: efecto no mencionado → sin dato (no «ausente»); salida malformada rechazada sin escribir

## 6. Análisis semanal

- [x] 6.1 Motor de contraste determinista: unión registro↔clima por fecha, hora y ciudad, ventanas de retardo, denominador con efecto mencionado o ausencia confirmada, evidencia mínima (≥ 3 registros en ≥ 2 semanas), resultado apoyada / sin apoyo / insuficiente
- [x] 6.2 Factores: exposición al sol (> 1,5 h dicha por el usuario y día soleado o despejado según el clima de esas horas), UV alto, lluvia, socialización con retardo
- [x] 6.3 Revisión semanal `analisis/AAAA/semana-AAAA-Www.yaml` con texto por plantilla, trazabilidad a registros y clima, sugerencias de ajuste de umbrales (sin aplicarlas), etapa de observación de 4 semanas y sin recomendaciones
- [x] 6.4 Apartado «Qué ha aprendido Mira» en la revisión: reglas nuevas o cambiadas y tendencia semanal de preguntas formuladas, respondidas, «no relevante» y «no sucedió»
- [x] 6.5 Comando `orumi analizar` y paso semanal del lote nocturno; coste y proveedor constan en el lote
- [x] 6.6 Revisión del usuario: `orumi hipotesis confirmar|matizar|descartar`; lo descartado reaparece solo con evidencia nueva sustancial
- [x] 6.7 Pruebas con datos sintéticos: asociación fuerte → apoyada; ruido → sin apoyo; pocos datos → insuficiente; días sin clima excluidos y contados; días sin dato contados y no tomados por ausencia; exposición de 1 h o sin duración no cuenta; jornada laboral no es socialización; texto sin consejos

## 7. Panel

- [ ] 7.1 API: lectura de la revisión, contrastes (con enlaces a registros) y aprendizajes; operaciones declarar/confirmar/matizar/descartar y gestionar aprendizajes, con las mismas guardias, límites y conflicto 409 frente a Mira; cierre «no sucedió» en preguntas
- [ ] 7.2 Vista «Descubrimientos» (X57: sin emojis, sin scripts ni estilos en línea, texto siempre como texto) con la sección «Qué ha aprendido Mira»; pruebas de reglas de la interfaz

## 8. Documentación y cierre

- [ ] 8.1 Actualizar `docs/agente-mira.md` (patrones fuera de alcance → implementado; preguntas de contraste y aprendizaje), `docs/decisiones.md` (pendientes), AGENTS.md (estado y X25) y README
- [ ] 8.2 Desplegar en el servidor vía `infra` (configuración con la ciudad por defecto, temporizador), ejecutar `orumi clima` con el histórico y revisar la primera semana
- [ ] 8.3 Tras ~4 semanas de datos: primera revisión de contrastes y de la evolución de Mira con el usuario; ajuste de umbrales
