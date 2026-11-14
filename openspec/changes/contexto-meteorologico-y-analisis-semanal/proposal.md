## Why

El usuario ha detectado con el tiempo que el clima y la vida social le afectan: mucho sol le provoca cansancio y siestas largas, un índice UV alto con luz fuerte le da dolor de cabeza, la lluvia le relaja y socializar le agota, tras lo cual tiende a la soledad o la introspección. Son **sospechas propias**, no hechos. ORUMI ya acumula registros diarios; falta el dato externo (clima) y la capa de análisis (X20–X23, aplazada) para que Mira pueda **verificarlas o refutarlas con evidencia enlazada**, sin darlas por buenas.

## What Changes

- **Contexto meteorológico:** Mira obtiene de un servicio de clima gratuito y sin clave, por día y por hora, temperatura, humedad, precipitación y luminosidad (índice UV, radiación solar, nubosidad y horas de sol), y lo guarda como fichero derivado por día, nunca dentro del original ni de los registros. Un registro se asocia al clima por su fecha y hora, en el momento del análisis.
- **Ciudad de cada nota:** campo opcional `ciudad` en la captura, indicado por el usuario (nunca por el dispositivo). Sin él, la ciudad por defecto es **Montbau, Barcelona**. El usuario se compromete a indicar la ciudad cuando esté en otra, para que cada nota pertenezca a una localización concreta y se use su clima.
- **Hipótesis del usuario:** el usuario declara sus sospechas (factor, efecto, retardo opcional) y quedan como «dicho por el usuario, por verificar». Mira las contrasta; nunca las convierte en hechos por sí sola.
- **Análisis semanal:** nuevo paso de Mira que etiqueta, a partir del texto de los registros, qué días aparece cada factor y cada efecto (solo lo dicho explícitamente) y calcula, en código y no con el modelo, la comparación entre días con y sin factor. Emite un contraste por hipótesis: *apoyada*, *sin apoyo* o *evidencia insuficiente*, con los registros enlazados. Cubre las cuatro sospechas actuales, incluida la socialización seguida, con retardo, de soledad o introspección. «Larga exposición al sol» = más de 1,5 h al exterior (dicho por el usuario) en día soleado o despejado, con cansancio físico posterior. El trabajo (por defecto 08:30–18:30 en días laborables) no cuenta como socialización: solo lo que el usuario indique.
- **Preguntas de contraste:** cuando faltan datos de un día con factor presente, Mira pregunta (dentro del máximo de 5 por revisión). Un «no sucedió» confirma la ausencia y evita el sesgo de que solo se anote lo que ocurre.
- **Aprendizaje de Mira (exigido por el usuario):** cada cierre de pregunta (respondida, no lo recuerdo, **no relevante**, **no sucedió**) enseña a Mira. Con evidencia suficiente genera reglas visibles, reversibles y con su evidencia, que ajustan qué pregunta y con qué prioridad, sin dejar nunca de preguntar. La revisión semanal mide su evolución.
- **Revisión por el usuario (X22):** confirmar, matizar o descartar un contraste, por terminal y desde el panel; sección «Descubrimientos» del panel con el contraste semanal (X21).
- **Privacidad:** se registra la decisión de que, además del proveedor de IA, el servicio meteorológico recibe coordenadas aproximadas y fechas (nunca texto, registros ni identidad).

## Capabilities

### New Capabilities
- `contexto-meteorologico`: obtención, almacenamiento, ciudad por nota y límites del clima como contexto derivado.
- `aprendizaje-de-mira`: aprendizaje visible y reversible a partir de los cierres de preguntas, y su medición.
- `contraste-de-hipotesis`: hipótesis del usuario, análisis semanal con umbral de evidencia, resultados trazables y revisión.

### Modified Capabilities
- `privacidad-del-procesamiento`: el servicio meteorológico pasa a ser un segundo destino permitido, con datos mínimos.
- `preguntas-de-mira`: cierre «no sucedió», preguntas de contraste y aprendizaje sin prohibir la clase.
- `captura-de-texto`, `capturas-originales` y `captura-web`: campo `ciudad` indicado por el usuario (sustituye la regla de «no registrar ubicación» por «no obtener ubicación del dispositivo», X25).
- `panel-web`: nueva sección de descubrimientos y revisión de hipótesis.

## Impact

- Código: nuevo cliente de clima y comandos `orumi clima`, `orumi hipotesis`, `orumi analizar` (.NET); paso de clima y de análisis en el lote nocturno; endpoints y vista en el panel; tabla derivada `clima_dia` en el índice.
- Datos: directorios nuevos `clima/`, `analisis/`, `hipotesis/` y `aprendizajes/`; la captura gana un campo opcional `ciudad` (las existentes siguen válidas); los registros no cambian de formato.
- **Reinterpreta X25** (metadatos de captura sin ubicación): la ciudad la aporta el usuario, no el dispositivo; se registra como decisión nueva.
- Amplía X36 (preguntas solo estructurales) con las preguntas de contraste.
- Dependencias externas: Open-Meteo (sin clave; gratuito para uso no comercial). Sin nuevas bibliotecas.
- Coste de IA: un paso semanal de etiquetado por lotes; estimación por verificar en la primera ejecución.
- Documentación: nuevas decisiones X60+ en `docs/decisiones.md`; actualiza AGENTS.md («Pendiente»).
- **Fuera de alcance:** recomendaciones o consejos, causalidad, patrones no declarados por el usuario (descubrimiento libre), datos de otros dispositivos (sueño, salud) y notificaciones.
