## 1. Decisión y protección

- [ ] 1.1 Registrar en `docs/decisiones.md` la decisión (agente **Noa** separado de Mira, solo del usuario, alcance máximo incluidos los temas sensibles marcados, sin diagnósticos clínicos, periódico cada 2 semanas con datos suficientes y a petición, Mira no lee el perfil, sin fuentes externas, conjeturas como excepción a X20) y el riesgo de concentración de datos sensibles; actualizar AGENTS.md (componentes, principios y «Pendiente»)
- [ ] 1.2 Definir en la configuración los términos clínicos prohibidos, los niveles de evidencia (patrón, indicio, conjetura), las semanas de observación, la cadencia (2 semanas o mensual) y el mínimo de información nueva

## 2. Núcleo del agente

- [ ] 2.1 Proyecto `Orumi.Noa` y comando `orumi noa` (ejecutar, generar, ver, corregir, matizar, descartar, ocultar, borrar); lectura de registros, respuestas, correcciones e hipótesis confirmadas; escritura solo en `perfil/`
- [ ] 2.2 Formato de ficheros: `perfil/perfil.yaml` (rasgos con apartado, certeza, evidencia, estado, origen e historial), `perfil/historial/`, `perfil/generaciones/`; documentarlo en `docs/formato-de-ficheros.md`
- [ ] 2.3 Selección determinista de evidencia por apartado y clasificación en patrón / indicio / conjetura, etapa de observación y recuento de datos disponibles; conjeturas aisladas y no usadas como base de patrones ni recomendaciones
- [ ] 2.4 Contrato de salida del modelo y validador: sujeto solo «el usuario», categoría de certeza obligatoria, evidencia enlazada y verificable, rechazo de terceros, de diagnósticos y etiquetas clínicas, de afirmaciones sensibles presentadas como hechos y de afirmaciones sin nivel de evidencia declarado; una salida inválida no escribe nada
- [ ] 2.5 Generación por apartados con el proveedor de Mira (X38): envío mínimo agregado, fallo cerrado sin garantías, constancia de proveedor, modelo, fecha y coste; apartados de superficie de ingeniería social y de recomendaciones, estas enlazadas a sus patrones o indicios

- [ ] 2.6 Ejecución periódica: `orumi noa ejecutar` comprueba la cadencia y la información nueva suficiente, omite y anota si no la hay; temporizador de sistema vía `infra` (lo instala el usuario con `sudo`) cada 2 semanas, sin recuperación de ejecuciones perdidas
- [ ] 2.7 Sin acceso a internet ni a otras fuentes: Noa solo lee los ficheros de ORUMI

## 3. Control del usuario

- [ ] 3.1 Corregir y matizar (pasa a «dicho por el usuario» y prevalece), descartar (huella de evidencia; reaparece solo con evidencia nueva sustancial), ocultar (retira de la vista y de la descarga sin borrar) y borrar rasgo y apartado
- [ ] 3.2 Borrado total del perfil e historial; bloqueo frente a generación en curso
- [ ] 3.3 Apartado de energía y ánimo solo con hipótesis confirmadas por el usuario; omitirlo si no hay

## 4. Panel

- [ ] 4.1 API: solicitar generación (una a la vez), ver calendario y última ejecución u omisión, leer perfil y rasgos con evidencia, corregir, matizar, descartar y borrar; descarga Markdown generada al vuelo; `no-store`, mismas guardias, límites y 409 que el resto de escrituras; montaje de solo lectura del resto de datos
- [ ] 4.2 Vista «Perfil» (X57: sin emojis, sin scripts ni estilos en línea, texto siempre como texto), con estado de observación, rasgos separados de las recomendaciones y borrado confirmado

## 5. Pruebas

- [ ] 5.1 Pruebas del validador con casos adversos: rasgos sobre hijos o compañeros, etiquetas clínicas, deducción sensible presentada como hecho, conjetura usada como base de una recomendación, afirmación sin nivel de evidencia, categorías mezcladas
- [ ] 5.2 Pruebas de aislamiento: no se escribe fuera de `perfil/`; Mira no recibe datos del perfil; el perfil se puede leer sin el agente
- [ ] 5.3 Pruebas de control: corregir prevalece, descartado no reaparece, borrado total, bloqueo durante generación; privacidad: envío mínimo y fallo cerrado; cadencia: omite sin datos suficientes y anota, ejecuta con datos suficientes
- [ ] 5.4 Pruebas del panel: acceso denegado sin sesión autorizada, límites y cabeceras

## 6. Documentación y cierre

- [ ] 6.1 Documentar el agente en `docs/` (alcance, límites, privacidad y riesgos) y actualizar README
- [ ] 6.2 Desplegar en el servidor vía `infra` y revisar con el usuario el primer perfil real, ajustando el prompt y los umbrales
