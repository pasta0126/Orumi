# Noa, el agente del perfil

Estado: implementado y desplegado (change `perfil-personal`, decisión X68). Describe **qué hace** Noa, sus límites, su privacidad y sus riesgos.

## Qué es

**Mira registra; Noa te devuelve la imagen que se deduce de lo que has contado.** Es un agente distinto de Mira, con su propio espacio (`perfil/`), sus propias garantías y su propio panel («Perfil»). Elabora un perfil **solo del propio usuario**: cómo se comunica, qué le ayuda y qué le desgasta, sus motivaciones, hábitos, relaciones (siempre desde su punto de vista), situación personal y de salud, y qué podría deducir un tercero de él (para que se proteja). Incluye los temas sensibles, siempre marcados.

## Reglas

- **Nunca un hecho:** cada afirmación lleva su categoría de certeza (dicho, confirmado, interpretación, hipótesis, recomendación), su **nivel de evidencia** calculado por código (*patrón*: ≥ 3 registros en ≥ 2 semanas; *indicio*: 2; *conjetura*: 1, en un apartado propio y de baja confianza) y las citas literales verificadas de los registros que la sustentan. Una afirmación sin citas verificables se descarta.
- **Solo del usuario:** los demás (hijos, familia, amigos, compañeros) solo aparecen como contexto. Un rasgo cuyo sujeto es otra persona, o que la nombra, se descarta.
- **Sin diagnósticos clínicos ni etiquetas de trastornos.** Se describen señales y patrones en lenguaje llano. La lista de términos prohibidos es configurable.
- **Recomendaciones aparte,** «del sistema», enlazadas a los patrones o indicios que las motivan; nunca a una conjetura.
- **Sin fuentes externas:** solo lee lo que ORUMI ya conserva. No busca en internet.
- **Mira no lee el perfil** (evita que una inferencia sobre ti condicione cómo se registran tus notas) y Noa escribe solo en `perfil/`.

## Cuándo actúa

Nunca antes de **4 semanas** desde el primer registro. Después, **cada 14 días si hay información nueva suficiente** (≥ 20 registros nuevos o ≥ 10 días de notas) y **a petición** (basta con datos suficientes en conjunto). Una sola generación a la vez (bloqueo propio). Cada versión conserva las anteriores en `perfil/historial/`.

## Control del usuario

Corregir (su palabra prevalece y Noa no la reescribe), matizar, descartar (no vuelve sin evidencia nueva sustancial), ocultar (sale de la vista y de la descarga, sin borrar), borrar un rasgo o el perfil entero. Por terminal (`orumi noa …`) y desde el panel («Perfil»), con descarga en Markdown.

## Preguntas de Noa

Pocas (3 por ejecución, configurable) y propias de Noa, en `perfil/preguntas/`: sobre una conjetura o indicio no sensible, una contradicción entre rasgos o un apartado sin datos. No sugieren la respuesta ni preguntan por terceros. Se muestran con su avatar y su nombre, en un bloque aparte, con cierres y aprendizaje propios (clase `noa:<apartado>`). Lo que contestas entra, como dicho por ti, en la siguiente versión.

## Privacidad

Mismo proveedor y garantías que Mira (X38): comprobación de la garantía de no reutilización y de la clave **antes** de enviar nada (fallo cerrado). Envío mínimo: solo los textos que dijiste, con los **nombres de terceros seudonimizados** (`P1`, `P2`…); nunca clima, resúmenes de Mira ni audio. Cada generación deja constancia de proveedor, modelo, fecha y coste, sin texto tuyo. Acceso solo con el login de Google y lista de correos, respuestas `no-store`.

## Riesgos asumidos

- **Concentración de datos sensibles** en un solo documento. Se acota con el acceso restringido, el borrado total y el aislamiento respecto a Mira.
- **Inferencias erróneas o alarmistas.** Se mitigan con la evidencia exigida y validada por código, los niveles visibles, el control del usuario y apartados vacíos antes que inventados. Los primeros perfiles se revisan con el usuario.
- **Efecto de leerse a uno mismo.** Por eso Mira no lee el perfil y todo se puede descartar o borrar.

## Operación

```bash
orumi noa estado                       # cadencia y última ejecución u omisión
orumi noa ejecutar                     # lo que hace el temporizador: respeta la cadencia y la información nueva
orumi noa generar                      # a petición
orumi noa ver [--markdown si]          # también: corregir, matizar, descartar, ocultar, borrar, borrar-todo
orumi consultar pendientes --agente Noa
```

Configuración (`orumi.yaml`, sección `noa`): sin ella Noa no está activa. Valores por defecto: `cadencia_dias: 14`, `semanas_de_observacion: 4`, `minimo_registros_nuevos: 20`, `minimo_dias_nuevos: 10`, `registros_para_patron: 3`, `semanas_para_patron: 2`, `registros_para_indicio: 2`, `maximo_preguntas: 3`, `seudonimizar_terceros: true`, `terminos_clinicos: [...]`. Formato de ficheros: `docs/formato-de-ficheros.md` §8. Avatares: `docs/agentes-avatares.md` y `imagenes/recortes/`.
