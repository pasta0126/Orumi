## Context

ORUMI lleva en uso real desde 2026-11-13. Los ficheros son la fuente de verdad y PostgreSQL es un índice derivado y regenerable (X3, X41). X20–X23 fijaron cómo deben ser los patrones (evidencia mínima, revisión semanal, confirmar/matizar/descartar, solo frecuencia y evidencia) pero se aplazaron a «una change de análisis». Esta change las implementa en su versión mínima, acotada a **hipótesis que el usuario declara**, y añade el clima como contexto externo.

Hipótesis iniciales del usuario (se guardan como datos, no como código):

| Factor | Efecto | Retardo |
|---|---|---|
| Larga exposición al sol (> 1,5 h al exterior en día soleado o despejado, según el usuario) | Cansancio físico y siestas largas | 0–1 día |
| UV alto con luz fuerte | Dolor de cabeza | 0 |
| Lluvia | Relajación | 0 |
| Socialización | Agotamiento, y después más soledad o introspección | 0 y 1–2 días |

## Goals / Non-Goals

**Goals:** clima fiable y trazable por día y hora; contrastar hipótesis ya declaradas con honestidad (incluida la refutación); ningún dato original ni registro modificado.
**Non-Goals:** descubrimiento libre de patrones, causalidad, consejos, datos de salud o sueño de otros dispositivos, notificaciones, clima en el momento de la captura para el modelo.

## Decisions

1. **Servicio: Open-Meteo** (X60, a confirmar en la primera tarea). Gratuito para uso no comercial, sin clave ni cuenta (cumple el criterio de coste y reduce secretos), ofrece variables horarias (`temperature_2m`, `relative_humidity_2m`, `precipitation`, `cloud_cover`, `shortwave_radiation`, `sunshine_duration`, `uv_index`) y diarias (`uv_index_max`, `sunrise`, `sunset`, `daylight_duration`), con histórico. Alternativas: AEMET (clave personal, solo España, sin UV horario fácil), OpenWeather (cuenta y clave, límites), Meteostat (sin UV). *Riesgo:* el histórico puede no incluir UV; la API de previsión admite `past_days` (hasta ~3 meses) que sí lo incluye. Primera tarea: verificar variables y condiciones reales y dejarlas en la configuración con fecha y fuente; lo no disponible queda «desconocido» y nunca se estima.
2. **Luminosidad = UV + radiación + nubosidad + horas de sol.** No existe «lux» en servicios gratuitos. Se documenta y se usa el UV para «luz fuerte».
3. **Ciudad por nota, con Montbau (Barcelona) por defecto** (sustituye la ubicación única). La captura gana el campo opcional `ciudad`, dicho por el usuario (web: campo de texto que recuerda la última como sugerencia editable en el dispositivo; terminal: `--ciudad`). Sin él, ciudad por defecto de la configuración. Las coordenadas salen de la configuración (ciudad por defecto) o de una tabla pequeña de ciudades conocidas (configuración); una ciudad no reconocida genera una pregunta a Mira, no una suposición, y esa nota queda sin clima hasta resolverla. No se usa la geolocalización del navegador ni del dispositivo. Coordenadas redondeadas a 1 decimal (~10 km). Zona horaria `Europe/Madrid` por defecto, ajustable por ciudad. Es compatible con la inmutabilidad: la ciudad se fija al crear la captura; corregirla se hace sobre los registros derivados (X30), nunca sobre el original. Un día con varias ciudades se resuelve por registro.
4. **Almacenamiento:** `clima/AAAA/MM/AAAA-MM-DD-<ciudad>.yaml` con `fuente`, `ubicacion`, `obtenido`, `horas` (24 entradas) y `dia`; escritura atómica; regenerable. Índice: tabla `clima_dia` y `clima_hora` en el esquema derivado.
5. **Obtención:** `orumi clima` (idempotente) pide solo los días con registros y sin clima; se ejecuta antes del análisis en el lote nocturno. Los días recientes se refrescan una vez (los datos de las últimas horas cambian). Un fallo nunca bloquea `procesar` (el clima no es requisito del procesamiento).
6. **El modelo no recibe el clima al procesar capturas.** Si el modelo viera «lluvia», podría inventar «estaba relajado». La unión registro↔clima se hace al analizar, por fecha (y hora si el registro la tiene; si no, resumen diario).
7. **Hipótesis:** ficheros `hipotesis/hip-*.yaml` con formulación literal, `factor` (`clima:uv>=6`, `clima:precipitacion>=1mm`, `registro:socializacion`…), `efecto` (concepto de registro), `retardo_dias`, `estado` y historial. Los factores de clima usan umbrales configurables con valores iniciales míos: UV ≥ 6 («alto» en la escala de la OMS), lluvia ≥ 1 mm/día y «soleado o despejado» = nubosidad ≤ 30 % y horas de sol ≥ 60 % de las horas de luz. El propietario delega los valores en Mira y se ajustarán con el uso: la revisión puede **sugerir** ajustes (p. ej. «el umbral de UV discrimina mejor en 5») pero nunca los cambia sola.
8. **Etiquetado con el modelo, cálculo con código.** Un paso por lotes (mismo mecanismo que X51, mismo proveedor, mismas garantías X38) lee solo el texto de los registros del periodo y devuelve, por registro y concepto, presente/no mencionado, con cita. Se guarda en `analisis/etiquetas/` (derivado, regenerable, certeza «interpretado»). La comparación (proporción de días con efecto entre días con y sin factor, con ventana de retardo) y la decisión *apoyada / sin apoyo / insuficiente* son código determinista y testeable. Criterio inicial: diferencia de proporciones ≥ 0,25 con intervalo de Wilson de los grupos sin solaparse; se ajusta con datos reales (configuración).
9. **Días sin dato ≠ días sin efecto, y Mira los pide.** Un día con el factor presente y sin mención del efecto queda «sin dato». Mira puede preguntar (pregunta de contraste, clase nueva que amplía X36) y el usuario responde o cierra como «no sucedió» (ausencia confirmada, cuenta en el denominador), «no relevante» o «no lo recuerdo». Se pregunta solo mientras la hipótesis tenga evidencia insuficiente, solo días con factor presente, dentro del máximo de 5 por revisión y con tono neutro, sin sugerir la respuesta. Evita el sesgo de anotar solo lo que ocurre.
10. **Definiciones de los factores del usuario.** «Larga exposición al sol»: > 1,5 h al exterior (el usuario lo dice) en un día soleado o despejado según el clima de su ciudad en esas horas; sin duración dicha, el día queda sin dato y Mira puede preguntar. «Socialización»: solo si el usuario lo indica; el trabajo (08:30–18:30 en días laborables, configurable) es solo trabajo y no cuenta. Efectos: cansancio físico posterior (retardo 0–1 día); agotamiento y luego soledad o introspección (retardo 0 y 1–2 días).
11. **Informe:** `analisis/AAAA/semana-AAAA-Www.yaml` (máquina) con texto generado por plantilla en español (sin modelo, para no embellecer ni sugerir); etapa de observación de 4 semanas configurable (X21). Revisión (X22) con `orumi hipotesis confirmar|matizar|descartar` y las mismas operaciones en el panel; lo descartado se recuerda con la huella de la evidencia y solo reaparece con evidencia nueva sustancial (≥ el doble de registros del efecto).
12. **Privacidad (X61):** el servicio meteorológico es un segundo destino: solo coordenadas redondeadas, fechas y variables. Nunca texto ni registros ni identidad. `docs/tecnologias.md` y `orumi.yaml` registran fuente, condiciones y fecha. La petición va sin cabeceras de identificación (cliente genérico).
13. **Panel:** sección «Descubrimientos» con la revisión semanal, contraste por hipótesis con enlaces a los registros, y acciones de revisión. Misma guardia, límites y bloqueo frente a Mira que el resto de escrituras (X56). El front respeta X57 (sin emojis, sin scripts ni estilos en línea).

14. **Aprendizaje de Mira (exigencia del propietario: «exijo una evolución de Mira»).** Principio: aprender **de forma visible, medida y reversible**, nunca opaca.
    - *Señal:* cada pregunta se cierra como respondida, «no lo recuerdo», «no relevante» o «no sucedió» (nuevo; también en terminal y panel). Se guarda la **clase** de la pregunta (campo + tema, p. ej. `duracion:reunion`, `efecto:dolor-de-cabeza`) y el cierre.
    - *Regla:* ≥ 3 cierres «no relevante» de la misma clase en ≥ 2 días distintos → regla aprendida «prioridad baja» para esa clase (umbrales configurables, como X20). Una respuesta con datos restablece la prioridad. **Nunca se prohíbe una clase** (el usuario prefiere poder cerrar a que Mira deje de preguntar). Los «no sucedió» no enseñan a callar: son datos.
    - *Almacén:* `aprendizajes/apr-*.yaml` (descripción legible, clase, evidencia = ids de preguntas, fecha, estado activo/desactivado, historial). Derivado de las preguntas cerradas y regenerable, salvo las desactivaciones del usuario, que prevalecen (X30).
    - *Aplicación:* (a) determinista: ordenación de candidatas y selección del máximo de 5 (X37); (b) en el prompt como «preferencias del usuario» (solo la descripción de las reglas activas, sin los textos de los cierres), con la regla de que no autorizan inventar ni contradecir lo dicho.
    - *Medida:* la revisión semanal añade «Qué ha aprendido Mira»: reglas nuevas/cambiadas y tendencia semanal de preguntas formuladas, respondidas, «no relevante», «no sucedió» y tasa de respuesta. Si la tasa de «no relevante» no baja tras varias semanas, se anota para `docs/decisiones.md` (educar a Mira, X36).
    - *Alcance:* el aprendizaje de esta change es sobre **qué preguntar y con qué prioridad**. Aprender a extraer mejor registros (prompt de interpretación) sigue siendo «educar a Mira con casos reales», con ajuste manual del prompt.
    - *Control:* ver, corregir, desactivar y borrar reglas por terminal (`orumi aprendizajes`) y desde el panel.

## Risks / Trade-offs

- **Pocos datos al principio:** con ~10 notas diarias, 4 semanas son escasas para hipótesis con efectos poco frecuentes (migraña). Es esperado que muchas salgan «insuficiente»; no se rebajan los umbrales.
- **Confusión entre factores** (verano + más vida social + más sol): no se controla; los contrastes se presentan como asociación, nunca causa. Declarado en el informe.
- **Sesgo de anotación:** mitigado con las preguntas de contraste y «no sucedió» (decisión 9); si el usuario cierra casi todo como «no relevante», habrá pocos días con dato y el resultado será «insuficiente».
- **Aprendizaje que empeore el contraste:** si Mira baja la prioridad de preguntas de contraste porque el usuario las cierra, las hipótesis quedan con menos datos. Por eso la clase nunca se prohíbe y la revisión lo señala.
- **Dato de ciudad no indicado:** si el usuario olvida indicarla estando fuera, el clima será el de Montbau. Mitigación: pregunta si el texto menciona otra ciudad; el compromiso del usuario lo cubre el resto.
- **Dependencia del servicio gratuito:** si desaparece o cambia condiciones, los ficheros ya guardados siguen siendo válidos; se sustituye la fuente sin tocar el resto.
- **Tamaño de la change:** abarca clima, hipótesis, análisis y panel. Si se prefiere, el apartado 5 (panel) y la revisión en panel pueden pasar a una change posterior sin afectar al resto.

## Resolved Questions

1. Ubicación: Montbau, Barcelona por defecto; el usuario indica la ciudad cuando esté en otra.
2. Trabajo: trabajo (≈ 08:30–18:30 en días laborables); socialización solo si el usuario la indica; sin subtipos.
3. Umbrales de clima: a criterio de Mira, ajustables con el uso (decisión 7).
4. Larga exposición al sol: > 1,5 h al exterior en día soleado o despejado; efecto: cansancio físico posterior.

5. Estancias: el usuario indicará la ciudad en cada nota siempre que no sea Barcelona; no hay declaración de estancia (desde/hasta).
6. Ciudades conocidas: Barcelona (por defecto, Montbau) y Segur de Calafell. Cualquier otra (a menudo desplazamientos puntuales por los partidos de sus hijos, sobre todo en fin de semana) se resuelve **preguntando**: Mira no asume ciudad ni la deduce por el día de la semana. Cada ciudad confirmada por el usuario se añade a la tabla de conocidas (con sus coordenadas redondeadas) y deja de preguntarse por ella.

## Open Questions

Ninguna pendiente.
