## Why

El usuario quiere comprenderse mejor y aprender a comunicarse (consigo mismo y con otros) con un perfil **completo** de sí mismo, como lo elaboraría un perfilista profesional o un atacante de ingeniería social que revisara todo lo que ORUMI conserva de él: capturas, registros, respuestas, correcciones e hipótesis, directa o indirectamente. Es una inferencia agregada y sensible, y no encaja en Mira, que captura y registra, no hace inferencias psicológicas y no da consejos no solicitados (principios 3–5, X36). Por eso se crea un **agente distinto, «Noa»**, con sus propias garantías (principio 7: componentes independientes que colaboran), que lee lo que Mira registra y escribe solo en su propio espacio.

## What Changes

- **Nuevo agente «Noa»** (Mira mira lo que ocurre; Noa te devuelve la imagen que se deduce de ello), independiente de Mira. Genera un perfil **del propio usuario**, versionado.
- **Cadencia:** actúa **solo cada 2 semanas** (configurable a mensual), **si hay información nueva suficiente**; si no, omite la ejecución y lo anota. También puede pedirse a mano. No empieza hasta pasadas ~4 semanas de datos.
- **Alcance máximo, a decisión del usuario:** personalidad, comunicación, motivaciones, valores, miedos y preocupaciones, hábitos, energía y ánimo, relaciones y su dinámica (desde el punto de vista del usuario), situación personal, familiar, laboral, económica y de salud, entorno y lugares, y toda deducción, indicio o suposición razonable, **incluidos los temas sensibles**. Más un apartado de **superficie de ingeniería social**: qué podría deducir un tercero, qué palancas de persuasión funcionarían con el usuario, sus puntos débiles y cómo protegerse. Y recomendaciones de cómo comunicarse él con otros y cómo conviene comunicarse con él.
- **Rigor sin recortar el alcance:** cada afirmación lleva su categoría de certeza (las cinco), su nivel de evidencia (*patrón* ≥ 3 registros en ≥ 2 semanas, *indicio* 2 registros, *conjetura* 1 registro o deducción no repetida, en apartado propio y de baja confianza) y los registros que la sustentan. Las recomendaciones solo se apoyan en patrones o indicios.
- **Lo sensible se deduce pero se marca:** nunca se presenta como hecho ni como confirmado sin que lo confirme el usuario; **sin diagnósticos clínicos ni etiquetas de trastornos** (se describen señales y patrones en lenguaje llano).
- **Control del usuario:** ver, corregir, matizar, descartar, ocultar y borrar cada afirmación, y borrar el perfil entero con su historial; lo corregido prevalece y lo descartado no vuelve sin evidencia nueva sustancial.
- **Terceras personas:** el perfil es solo del usuario; las demás personas, en especial los menores, solo aparecen como contexto de sus interacciones, sin perfiles ni rasgos inferidos de ellas.
- **Sin fuentes externas:** Noa deduce solo de lo que ORUMI ya conserva; no busca en internet.
- **Aislamiento y privacidad:** solo lee registros, respuestas e hipótesis; escribe únicamente en `perfil/`; Mira no lee el perfil. Usa el proveedor de IA ya contratado (X38) con envío mínimo y agregado por apartado y fallo cerrado; acceso solo por el login de Google con lista de correos, sin caché ni exposición pública. Se documenta el riesgo (ahora mayor) de concentración de datos sensibles.
- **Panel:** sección «Perfil» con versiones, calendario y última ejecución, lectura, revisión y borrado.

## Capabilities

### New Capabilities
- `perfil-personal`: agente Noa: ejecución periódica o a petición, alcance, niveles de evidencia, certeza, límites de protección y control del perfil del usuario.

### Modified Capabilities
- `privacidad-del-procesamiento`: Noa es otro emisor hacia el proveedor de IA y se somete a mínimos y garantías propios.
- `panel-web`: sección «Perfil» con las mismas garantías de acceso y límites que el resto.

## Impact

- Código: nuevo proyecto .NET `Orumi.Noa` y comando `orumi noa` (generar, ver, corregir, descartar, borrar), con temporizador en el servidor vía `infra` (cada 2 semanas); endpoints y vista en el panel. Sin bibliotecas ni bases de datos nuevas.
- Datos: directorio `perfil/` (ficheros como fuente de verdad; el índice, si lo necesita, es derivado y regenerable). Los datos existentes no cambian.
- Coste de IA: una generación cada 2 semanas (estimación por verificar en la primera ejecución, a precio de lote cuando sea posible).
- Depende de datos reales acumulados y, para el apartado de energía y ánimo, de la change `contexto-meteorologico-y-analisis-semanal` (hipótesis contrastadas).
- **Reinterpreta X20 solo para Noa:** admite *conjeturas* de un solo registro, marcadas y aisladas, por decisión del usuario; patrones y recomendaciones mantienen la evidencia mínima.
- **Fuera de alcance:** perfiles de terceras personas, diagnósticos clínicos, búsqueda en fuentes externas, uso del perfil para condicionar el registro de Mira, notificaciones y agente de propósito general.
