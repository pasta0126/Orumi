## 1. Decisión y protección

- [x] 1.1 Registrar en `docs/decisiones.md` la decisión (agente **Noa** separado de Mira, solo del usuario, alcance máximo incluidos los temas sensibles marcados, sin diagnósticos clínicos, periódico cada 2 semanas con datos suficientes y a petición, Mira no lee el perfil, sin fuentes externas, conjeturas como excepción a X20) y el riesgo de concentración de datos sensibles; actualizar AGENTS.md (componentes, principios y «Pendiente»)
- [x] 1.2 Definir en la configuración los términos clínicos prohibidos, los niveles de evidencia (patrón, indicio, conjetura), las semanas de observación, la cadencia (2 semanas o mensual) y el mínimo de información nueva

## 2. Núcleo del agente

- [x] 2.1 Proyecto `Orumi.Noa` y comando `orumi noa` (ejecutar, generar, ver, corregir, matizar, descartar, ocultar, borrar); lectura de registros, respuestas, correcciones e hipótesis confirmadas; escritura solo en `perfil/`
- [x] 2.2 Formato de ficheros: `perfil/perfil.yaml` (rasgos con apartado, certeza, evidencia, estado, origen e historial), `perfil/historial/`, `perfil/generaciones/`; documentarlo en `docs/formato-de-ficheros.md`
- [x] 2.3 Selección determinista de evidencia por apartado y clasificación en patrón / indicio / conjetura, etapa de observación y recuento de datos disponibles; conjeturas aisladas y no usadas como base de patrones ni recomendaciones
- [x] 2.4 Contrato de salida del modelo y validador: sujeto solo «el usuario», categoría de certeza obligatoria, evidencia enlazada y verificable, rechazo de terceros, de diagnósticos y etiquetas clínicas, de afirmaciones sensibles presentadas como hechos y de afirmaciones sin nivel de evidencia declarado; una salida inválida no escribe nada
- [x] 2.5 Generación por apartados con el proveedor de Mira (X38): envío mínimo agregado, fallo cerrado sin garantías, constancia de proveedor, modelo, fecha y coste; apartados de superficie de ingeniería social y de recomendaciones, estas enlazadas a sus patrones o indicios

- [ ] 2.6 Ejecución periódica: `orumi noa ejecutar` comprueba la cadencia y la información nueva suficiente, omite y anota si no la hay; temporizador de sistema vía `infra` (lo instala el usuario con `sudo`) cada 2 semanas, sin recuperación de ejecuciones perdidas
- [x] 2.7 Sin acceso a internet ni a otras fuentes: Noa solo lee los ficheros de ORUMI

## 3. Control del usuario

- [x] 3.1 Corregir y matizar (pasa a «dicho por el usuario» y prevalece), descartar (huella de evidencia; reaparece solo con evidencia nueva sustancial), ocultar (retira de la vista y de la descarga sin borrar) y borrar rasgo y apartado
- [x] 3.2 Borrado total del perfil e historial; bloqueo frente a generación en curso
- [x] 3.3 Apartado de energía y ánimo solo con hipótesis confirmadas por el usuario; omitirlo si no hay

## 4. Panel

- [x] 4.1 API: solicitar generación (una a la vez), ver calendario y última ejecución u omisión, leer perfil y rasgos con evidencia, corregir, matizar, descartar y borrar; descarga Markdown generada al vuelo; `no-store`, mismas guardias, límites y 409 que el resto de escrituras; montaje de solo lectura del resto de datos (esto último se aplica al desplegar, 6.2)
- [x] 4.2 Vista «Perfil» (X57: sin emojis, sin scripts ni estilos en línea, texto siempre como texto), con estado de observación, rasgos separados de las recomendaciones y borrado confirmado

## 4b. Preguntas de Noa y avatares

- [x] 4b.1 Etiqueta de agente: `generada_por.agente` obligatorio para las preguntas nuevas, las anteriores se entienden de Mira; `registro` opcional con `rasgo` para las de Noa; validador y `docs/formato-de-ficheros.md`
- [x] 4b.2 Avatares de Mira y Noa: los emotes ilustrados (PNG con transparencia, decisión 18 revisada) servidos desde `wwwroot/agentes/`, con nombre accesible (`alt`) y probados
- [x] 4b.3 Panel y terminal: la pregunta muestra el avatar y el nombre de su agente; bloques por agente con cabecera (avatar, nombre, número); filtro por agente (`orumi consultar pendientes --agente`)
- [x] 4b.4 Espacio propio: `perfil/preguntas/`; el almacén de Mira excluye `perfil/` y sus cupos, deduplicación y aprendizaje no ven las de Noa; el panel y la terminal cargan ambos orígenes
- [x] 4b.5 Generación de preguntas de Noa: candidatas deterministas (conjetura o indicio que una respuesta podría confirmar, contradicción entre rasgos, apartado sin datos), redacción sin sugerir respuesta ni aconsejar, solo sobre el usuario, cupo propio (3 por defecto), solo tras la etapa de observación
- [x] 4b.6 Cierres y aprendizaje por agente: clase `noa:<tema>`; mismos cierres que las de Mira; la respuesta se guarda como «dicho por el usuario» y entra en la siguiente versión del perfil
- [x] 4b.7 Pruebas: Mira no ve ni cuenta las preguntas de Noa; cupos independientes; aprendizaje por agente; una pregunta anterior sin agente se muestra como de Mira; Noa no pregunta por terceros ni antes de la observación

## 5. Pruebas

- [x] 5.1 Pruebas del validador con casos adversos: rasgos sobre hijos o compañeros, etiquetas clínicas, deducción sensible presentada como hecho, conjetura usada como base de una recomendación, afirmación sin nivel de evidencia, categorías mezcladas
- [x] 5.2 Pruebas de aislamiento: no se escribe fuera de `perfil/`; Mira no recibe datos del perfil; el perfil se puede leer sin el agente
- [x] 5.3 Pruebas de control: corregir prevalece, descartado no reaparece, borrado total, bloqueo durante generación; privacidad: envío mínimo y fallo cerrado; cadencia: omite sin datos suficientes y anota, ejecuta con datos suficientes
- [x] 5.4 Pruebas del panel: acceso denegado sin sesión autorizada, límites y cabeceras

## 6. Documentación y cierre

- [x] 6.1 Documentar el agente en `docs/` (alcance, límites, privacidad y riesgos) y actualizar README
- [ ] 6.2 Desplegar en el servidor vía `infra` y revisar con el usuario el primer perfil real, ajustando el prompt y los umbrales
