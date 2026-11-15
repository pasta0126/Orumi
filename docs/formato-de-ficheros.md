# ORUMI — Formato de ficheros (propuesta de la change `modelo-de-registro`)

Estado: **propuesta pendiente de revisión** (ver X33 en `docs/decisiones.md`). Desarrolla las specs `capturas-originales` y `registros-personales`. No fija tecnología: todo son ficheros `.md` legibles a mano. Ejemplos completos y ficticios en `docs/ejemplos/orumi/`.

## 1. Convenciones generales

- Ficheros `.md` en UTF-8, saltos de línea LF, con *frontmatter* YAML (subconjunto simple: textos, números, listas y mapas; sin anclas ni etiquetas).
- Claves en `snake_case` sin acentos (`fecha`, `clave_extraccion`); los valores sí pueden llevar acentos.
- Fechas en ISO 8601: `2026-11-09` (día) o `2026-11-09T08:15:00+01:00` (instante, con desfase horario).
- El nombre del fichero es siempre `<id>.md`. Los enlaces entre ficheros se hacen **por identificador**: en el frontmatter como texto plano y en el cuerpo como wikilink `[[id]]` (legible en cualquier editor Markdown; ORUMI no depende de otro software, X34).
- Un dato desconocido se escribe con el valor literal `desconocido`. Un campo opcional que no aplica se omite.
- Dentro de mapas en línea `{…}` los instantes con hora van **entre comillas** (`"2026-11-12T20:05:00+01:00"`): algunos lectores YAML fallan con los dos puntos sin comillas. En el resto del frontmatter se escriben sin ellas.
- Los textos libres (`titulo`, `contexto`…) que contengan `:` seguido de espacio, o empiecen por un carácter especial, van **entre comillas dobles**. Surgió en la primera prueba con una nota real: `titulo: Incidencia: …` no es YAML válido.
- Un identificador nunca se reutiliza ni se cambia, aunque el contenido se corrija.

## 2. Identificadores (tarea 1.3)

No derivan del contenido (renombrar o corregir no rompe enlaces). Formato: prefijo + fecha de creación en hora local + sufijo aleatorio de caracteres `[a-z0-9]`.

| Tipo | Formato | Ejemplo |
|------|---------|---------|
| Captura | `cap-AAAAMMDD-HHMM-xxx` | `cap-20261109-0815-m4k` |
| Registro | `reg-AAAAMMDD-xxxx` | `reg-20261110-q2x1` |
| Pregunta | `preg-AAAAMMDD-xxxx` | `preg-20261113-b7c2` |
| Lote de Mira | `lote-AAAAMMDD-HHMM` | `lote-20261110-0300` |
| Informe | `inf-AAAAMMDD-xxxx` | (change posterior) |

Si el identificador generado ya existe, se genera otro sufijo.

### Clave de extracción (reprocesado idempotente)

Cada registro creado por Mira lleva `clave_extraccion`, con la forma:

```
<id de la captura>#f<n>:<clase>:<k>
```

- `n`: ordinal del fragmento de la captura en el orden en que se procesó por primera vez (`f1`, `f2`…).
- `clase`: clase del registro (`actividad`, `idea`…).
- `k`: ordinal dentro del mismo fragmento y clase (`1`, `2`…).

Reglas:

1. **La clave se asigna una vez y se guarda; nunca se recalcula** a partir del contenido. Así, aunque el modelo redacte distinto en otra pasada, la identidad del registro no cambia.
2. Al reprocesar una captura, Mira recibe las claves existentes de esa captura. Cada elemento extraído **reclama** una clave existente o recibe una nueva (`k` o `n` libres siguiente). Si la coincidencia es dudosa, no fusiona y deja una pregunta pendiente.
3. Una clave existente que la nueva pasada no reclame **se mantiene tal cual**: Mira nunca elimina un registro por reprocesar.
4. Un registro al que se añade otra captura (ampliación) conserva su clave original.
5. **Campos protegidos (X30):** en un reprocesado, Mira no modifica un campo con certeza `confirmado` ni uno con alguna entrada de `historial` de origen `usuario`. Puede añadir información a su alrededor y dejar preguntas. El resto de campos sí se actualizan, anotando el cambio en `historial` con origen `mira`.

## 3. Captura (tarea 1.1)

La captura es **inmutable**: ni su cuerpo ni su frontmatter cambian tras guardarse. Por eso no lleva estado de procesamiento (ese registro está en los *lotes*, §6).

```yaml
---
id: cap-20261109-0815-m4k
tipo: captura
comunicada_el: 2026-11-09T08:15:00+01:00
origen: texto                  # texto | voz (transcripción literal)
huella: sha256:<hex>           # SHA-256 del cuerpo en UTF-8
contexto_automatico:
  dispositivo: movil
# Opcionales (si el usuario los aporta, cuentan como «dicho»):
fecha_acontecimiento: 2026-11-08
etiqueta: casa
ciudad: Segur de Calafell      # solo si no es la ciudad por defecto; la indica el usuario
---
<texto literal tal como se recibió>
```

- **Cuerpo:** todo lo que sigue a la línea de cierre `---` y su salto de línea, sin tocar (ni limpieza, ni resumen, ni salto final añadido).
- **`huella`:** permite comprobar que el cuerpo no se ha alterado. Si no coincide, el fichero se considera dañado.
- **`comunicada_el`** es siempre la fecha y hora de comunicación; `fecha_acontecimiento` no la altera.
- **`ciudad`** (opcional, X62): texto de 1 a 60 caracteres que **indica el usuario**; sin ella, la ciudad por defecto de la configuración. Es un metadato «dicho por el usuario», no una ubicación.
- **Prohibido:** coordenadas, ubicación obtenida del dispositivo y cualquier monitorización. `contexto_automatico` solo admite `dispositivo` (la hora ya está en `comunicada_el`).
- **Fragmentos:** un fragmento se identifica por posición en el cuerpo: `desde` (incluido) y `hasta` (excluido), en caracteres Unicode, base 0. Al ser el cuerpo inmutable, la posición es estable.
- **Audio:** se guarda fuera de `capturas/` (en `entrada/`, §5) hasta confirmar que la captura quedó escrita y verificada; después se borra (X5/X28). Si la transcripción falla, el audio permanece en `entrada/` con la marca `.pendiente` y no existe fichero de captura.

## 4. Registro (tarea 1.2)

Una unidad de memoria: `.md` con frontmatter y una descripción breve en el cuerpo. Es editable; los cambios se anotan en `historial`.

```yaml
---
id: reg-20261110-q2x1
tipo: registro
clase: actividad               # actividad | evento | idea | decision | problema | otro
titulo: Migrar el script de copias a Python
comunicada_el: 2026-11-09T08:15:00+01:00     # de la captura de origen
fuentes:                       # la primera es el origen; las demás, ampliaciones
  - captura: cap-20261109-0815-m4k
    fragmento: {desde: 0, hasta: 74, cita: "Esta mañana he empezado..."}
    rol: origen
  - captura: cap-20261111-1830-p3w
    rol: ampliacion
fecha:                         # fecha del acontecimiento (o su imprecisión)
  expresion: esta mañana       # expresión literal del usuario, si la hubo
  desde: 2026-11-09
  hasta: 2026-11-09
hora: {expresion: por la mañana, valor: "09:30", aproximada: true}   # opcional; ver §4.1
duracion: {expresion: unas seis horas en total, minutos: 360}
herramientas: [Python]
entidades:                     # opcional; ver §4.1
  - {nombre: Gullón, tipo: organizacion}
proyectos: [mantenimiento-servidor]
multidia:                      # solo en actividades de varios días
  estado: cerrado              # abierto | cerrado
  fin: {desde: 2026-11-11, hasta: 2026-11-11}    # o `desconocido` mientras esté abierto
entradas:
  - {fecha: 2026-11-09, captura: cap-20261109-0815-m4k, resumen: "…"}
relaciones:
  - {tipo: surge_de, destino: reg-…, certeza: interpretado}
certeza:                       # una certeza por campo
  fecha: interpretado
  duracion: confirmado
  herramientas: dicho
  proyectos: confirmado
procedencia:
  generado_por: {agente: Mira, version: "…", modelo: "…"}
  generado_el: 2026-11-10T03:00:00+01:00
  ejecucion: lote              # lote | peticion
  actualizado_por: {agente: Mira, version: "…", modelo: "…"}
  actualizado_el: 2026-11-12T03:00:00+01:00
clave_extraccion: cap-20261109-0815-m4k#f1:actividad:1
conflictos: []                 # §4.4
rechazos: []                   # §4.5
historial: []                  # §4.6
---
<descripción breve>

## Origen
- [[cap-20261109-0815-m4k]]

## Relacionados
- [[reg-…]]
```

### 4.1 Campos

- **Obligatorios:** `id`, `tipo`, `clase`, `titulo`, `comunicada_el`, `fuentes` (con un `origen`), `fecha` (o `desconocido`), `procedencia`, `clave_extraccion`, `certeza`, y una descripción en el cuerpo.
- **Compatibilidad:** `hora` y `entidades` se añadieron después de la primera versión; los registros que no los tienen siguen siendo válidos y no se migran.
- **Opcionales:** `hora`, `duracion`, `herramientas`, `contexto`, `personas`, `entidades`, `lugares`, `proyectos`, `acciones`, `resultados`, `problemas`, `ideas`, `relaciones`, `multidia`, `entradas`. Su ausencia no invalida el registro.
- **Clases:** `actividad`, `evento`, `idea`, `decision`, `problema`, `otro`. La lista se amplía solo mediante una change.
- **`fecha`:** `desde` y `hasta` son siempre un rango (iguales si el día es exacto). `expresion` conserva lo que dijo el usuario. Un rango o un día **deducido de una expresión relativa** («ayer», «la semana pasada») es `interpretado`; solo es `dicho` si el usuario dio la fecha explícita (por texto o como metadato) y `confirmado` si la aclaró.
- **`hora`:** hora del acontecimiento, **campo aparte de `fecha` y con su propia certeza** (el día puede ser `interpretado` y la hora `dicho`). Forma: `{expresion, valor, aproximada}`. `valor` es `HH:MM` en hora local, sin zona horaria, y se refiere al **inicio** del acontecimiento; `aproximada: true` cuando el usuario dijo «sobre las…». Si el usuario solo dio una franja («por la mañana», «por la tarde»), se guarda solo `expresion` y **no se inventa un `valor`**. Si Mira deduce la hora, es `interpretado`.
- **`entidades`:** organizaciones, clientes, proveedores, sistemas o productos **nombrados** en el registro, como lista de `{nombre, tipo}`. `tipo` es opcional: `organizacion`, `sistema`, `producto`, `otro`. El `nombre` se guarda con la ortografía normalizada (la captura conserva la errata: «gullon» → «Gullón») y se compara sin distinguir mayúsculas ni acentos. No hay catálogo de entidades. Si en un mismo registro hay entidades de distinta certeza, cada elemento lleva `certeza` propia y el campo no aparece en el mapa `certeza`. Una entidad es **a qué se refiere** el registro (un cliente, un sistema con una incidencia); una herramienta es **con qué se hace** algo (Excel, Teams); una misma cosa puede ir en ambos campos. Nunca se añade una entidad que el usuario no mencionó salvo como `interpretado`.
- **`duracion`:** `expresion` literal y, solo si el usuario dio una cifra, `minutos`. Si dijo algo vago («toda la tarde») se guarda solo la expresión. Nunca se estima sin marcarla `interpretado`.
- **Actividades de varios días:** un único registro; `fecha` es el inicio; `multidia` guarda estado y fin; `entradas` guarda una entrada fechada por captura que la menciona.
- **Fragmentos de origen:** `fragmento` en cada fuente que proceda de una parte de la captura. `cita` es opcional (primeros ≤80 caracteres del fragmento) y existe para leerlo sin herramientas.

### 4.2 Certeza

Valores: `dicho`, `confirmado`, `interpretado`, `hipotesis`, `recomendacion`. Reglas:

- El mapa `certeza` tiene una entrada por campo relevante (para campos anidados, `multidia.estado`, `multidia.fin`). Los campos `desconocido` no llevan certeza.
- Un campo, una certeza. Si en una lista hay elementos de distinta certeza, cada elemento se escribe `{valor: X, certeza: Y}` y el campo no aparece en el mapa.
- Cualquier corrección o aclaración del usuario pasa el campo a `confirmado`; lo aportado por el usuario como metadato es `dicho`.

### 4.3 Relaciones

`tipo`: `surge_de`, `continua`, `relacionado_con`. Cada relación lleva su certeza. Una hipótesis o patrón añade `evidencia: [ids de registros]` y certeza `hipotesis` (la lógica de patrones es de una change posterior).

### 4.4 Conflictos (X16)

Cuando una captura contradice un dato de otro registro, **no se elige ganador y no se fusionan**: cada registro conserva su valor y ambos se marcan.

```yaml
conflictos:
  - {campo: fecha, con: reg-20261113-w1h5, pregunta: preg-20261113-k2v9, estado: abierto}   # abierto | resuelto
```

La marca es simétrica (aparece en los dos registros) y enlaza con la pregunta pendiente. Mientras el usuario no lo resuelva, siguen en conflicto.

Al **resolver** un conflicto (`orumi responder`), `estado` pasa a `resuelto` en los dos registros; el valor del usuario se aplica solo al registro que él indica, y la otra versión no se modifica.

### 4.4b Estado del registro: eliminado y fusionado («tumbas»)

Por defecto un registro está **activo** (sin campo `estado`). El usuario puede eliminarlo o fusionarlo con otro; el fichero y su `clave_extraccion` **se conservan** (así Mira no reutiliza la clave ni recrea el hecho y todo es reversible):

```yaml
estado: eliminado            # eliminado | fusionado
eliminado_el: 2026-11-14T09:12:00+01:00     # si está eliminado
fusionado_en: reg-20261110-q2x1             # si está fusionado: otro registro existente (no fusionado: no se encadenan)
fusionado_el: 2026-11-14T09:15:00+01:00
separados: [cap-20261111-1830-p3w]          # capturas que el usuario separó de este registro; Mira no las vuelve a ampliar a él
```

Los registros no activos no salen en consultas, días, búsquedas ni recuentos; los eliminados están en la papelera y se restauran. Un registro creado a mano al **separar** una captura lleva `procedencia.generado_por.agente: usuario` y no figura en ningún lote.

### 4.5 Rechazos

Una interpretación rechazada se conserva para no volver a proponerla sin evidencia nueva:

```yaml
rechazos:
  - {campo: contexto, valor: "Posible falta de tiempo", certeza_original: interpretado, rechazada_el: 2026-11-12T20:10:00+01:00}
```

El campo queda `desconocido` o con el valor que dé el usuario. `valor` es el texto legible del valor rechazado (para listas, los elementos separados por comas) y coincide con el `anterior` de la entrada de historial del usuario.

### 4.6 Historial

Una entrada por cambio, en orden cronológico; incluye cambios de Mira y del usuario:

```yaml
historial:
  - {fecha: 2026-11-12T20:05:00+01:00, origen: usuario, campo: proyectos, anterior: [copias-de-seguridad], nuevo: [mantenimiento-servidor]}
```

`origen`: `usuario` o `mira`. Opcionales: `pregunta` (si el cambio responde una pregunta), `captura`, `motivo`, `certeza_anterior` (la certeza que tenía el campo antes de un cambio del usuario, para poder deshacerlo) y `deshace` (posición, empezando en 0, de la entrada del historial que esta entrada revierte; solo en reversiones del usuario). Los campos estructurales (`estado`, `fuentes`, `fusion`) llevan texto en `anterior` y `nuevo` y no certeza. Un cambio de versión de Mira se anota en `procedencia.actualizado_*` y conserva en `historial` lo que sustituye.

## 5. Estructura de directorios de `orumi/` (tarea 1.4)

```
orumi/
├── capturas/AAAA/MM/cap-….md       # inmutables
├── registros/AAAA/MM/reg-….md
├── preguntas/AAAA/MM/preg-….md
├── lotes/AAAA/MM/lote-….md         # bitácora de ejecuciones de Mira
├── envios/AAAA/MM/envio-….md       # lotes diferidos enviados al proveedor (estado abierto o recogido)
├── informes/AAAA/MM/inf-….md       # revisiones y análisis (change posterior)
├── hipotesis/hip-….yaml            # hipótesis del usuario, «por verificar» (X65)
├── perfil/                          # Noa (X68): lo único que escribe
│   ├── perfil.yaml                  # versión vigente
│   ├── historial/perfil-vNNN-AAAAMMDD-HHMM.yaml   # una copia por versión
│   └── generaciones/gen-….yaml      # constancia de cada ejecución (sin texto del usuario)
├── analisis/etiquetas/AAAA/MM/reg-….yaml   # conceptos dichos en cada registro (derivado, regenerable)
├── analisis/ejecuciones/AAAA/MM/eti-….yaml # constancia de cada etiquetado (proveedor, modelo, uso)
├── analisis/AAAA/semana-AAAA-Www.yaml      # revisión semanal: contrastes, evidencia y aprendizaje de Mira
├── aprendizajes/apr-….yaml         # lo que Mira ha aprendido de los cierres de preguntas (X64)
├── clima/AAAA/MM/AAAA-MM-DD-ciudad.yaml   # clima diario por ciudad (contexto derivado y regenerable)
├── clima/ciudades.yaml             # ciudades aprendidas y ciudad fijada para notas concretas (X67)
├── entrada/                         # audio pendiente de transcribir/verificar (fuera de git)
└── .indice/                         # índice derivado, desechable (ver docs/indice-derivado.md)
```

- La ruta se deduce del identificador (fecha de creación): no cambia al editar el fichero.
- El estado (`pendiente`, `abierto`…) va en el frontmatter, nunca en la ruta, para no mover ficheros.
- Nada de `orumi/` se copia a vedlvm ni a su remoto (X12).

### 5.00 Hipótesis y etiquetas (`hipotesis/`, `analisis/`, X65)

**Hipótesis** del usuario, con sus palabras (certeza `dicho`), a verificar. Cada una tiene uno o varios **contrastes** (factor → efecto con una ventana de retardo en días) y la descripción de los **conceptos** que el modelo debe reconocer:

```yaml
id: hip-uv-dolor-de-cabeza
tipo: hipotesis
formulacion: "un índice UV alto con luz fuerte me provoca dolor de cabeza"
certeza: dicho
estado: por_verificar          # por_verificar | confirmada | matizada | descartada (la revisión es del usuario)
declarada_el: "2026-11-14T23:50:00+01:00"
conceptos:
  dolor_de_cabeza: "El usuario dice que tiene o ha tenido dolor de cabeza o jaqueca."
contrastes:
  - id: c1
    factor: ["clima:uv_alto"]  # clima:uv_alto | clima:lluvia | clima:soleado | registro:CONCEPTO[>=MINUTOS]; todas deben cumplirse
    efecto: dolor_de_cabeza
    retardo_dias: {desde: 0, hasta: 0}
historial:
  - {fecha: "2026-11-14T23:50:00+01:00", accion: declarada, origen: usuario, motivo: ninguno}
```

**Etiquetas** de un registro: qué conceptos aparecen **explícitamente** en lo que el usuario dijo (el fragmento de su captura, no la paráfrasis de Mira). `presente` si lo dijo; `negado` solo si dijo expresamente que no ocurrió; `quitado` si el usuario descarta la etiqueta. **Lo no mencionado no se etiqueta** y no cuenta como ausencia. Certeza `interpretado`; se regeneran cuando cambia el texto o los conceptos (`entrada` es la huella de lo enviado); lo que corrige el usuario (`origen: usuario`) prevalece. Cada cita se verifica literalmente contra el texto del usuario: una etiqueta sin cita literal se descarta.

```yaml
registro: reg-20261118-ab12
tipo: etiquetas
entrada: "sha256:…"
generada_el: "2026-11-19T04:00:11+01:00"
proveedor: anthropic
modelo: claude-sonnet-5-5
certeza: interpretado
etiquetas:
  - {concepto: exterior, estado: presente, origen: mira, cita: "tres horas en la playa", minutos: 180}
  - {concepto: dolor_de_cabeza, estado: negado, origen: usuario, cita: "no tuve dolor de cabeza", minutos: desconocido}
```

La constancia de cada ejecución (`analisis/ejecuciones/…/eti-AAAAMMDD-HHMM.yaml`) lleva proveedor, modelo, registros enviados, etiquetas, descartadas, tokens y coste estimado; nunca texto del usuario. Al modelo solo le llegan los conceptos y el texto del usuario: **ningún dato meteorológico**.

### 5.01 Revisión semanal (`analisis/AAAA/semana-AAAA-Www.yaml`, X21–X23, X66)

Se genera la primera noche de cada semana (también con `orumi analizar`) y se reemplaza si se regenera. **Etapa de observación** (4 semanas desde el primer registro): solo `observacion` (días transcurridos y necesarios, días con registros), sin contrastes ni preguntas. Después, un **contraste por hipótesis** con solo frecuencia observada y evidencia enlazada, sin causalidad ni recomendaciones:

```yaml
id: semana-2026-W49
tipo: revision_semanal
estado: completa               # observacion | completa
contrastes:
  - id: "hip-uv-dolor-de-cabeza#c1"
    formulacion: "un índice UV alto con luz fuerte me provoca dolor de cabeza"   # tus palabras
    factor: "UV alto (≥ 6)"
    veredicto: apoyada          # apoyada | sin_apoyo | insuficiente
    resumen: {dias_con_factor: 12, efecto_con_factor: 9, dias_sin_factor: 12, efecto_sin_factor: 1, tasa_con_factor: 0.75, tasa_sin_factor: 0.083, diferencia: 0.667, dias_con_efecto: 10, semanas_con_efecto: 4}
    excluidos: {sin_clima: 0, factor_sin_dato: 0, efecto_sin_dato: 3}   # días que NO cuentan como «no ocurrió»
    motivos: []                 # lo que falta cuando es insuficiente
    texto: "Los datos apoyan tu hipótesis: … Es una asociación observada: no indica que sea la causa."
    revision: ninguna           # o {estado: confirmada|matizada|descartada, comentario, fecha}; lo descartado no sale salvo evidencia nueva sustancial
    evidencia:                  # trazabilidad a los registros y al clima usado
      - {fecha: 2026-10-03, ciudad: "Barcelona", factor: si, efecto: presente, registros: [reg-20261003-ab12], clima: "UV máx 7.5, lluvia 0 mm, nubes 40 %", citas: ["me dolía la cabeza"]}
sugerencias: ["Hay 2 día(s) con UV ≥ 6; con un umbral de 5 habría 10. No se cambia solo."]
aprendizaje: {reglas_activas: 1, cambios: [...], tendencia: [{desde, hasta, formuladas, respondidas, no_relevantes, no_sucedio, no_recuerda, por_limite, pendientes, tasa_respuesta, tasa_no_relevante}]}
preguntas_creadas: [preg-…]
etiquetado: {ejecucion: eti-…, proveedor: anthropic, modelo: claude-sonnet-5-5, coste_estimado_usd: "0.013210"}
```

**Veredicto (X20):** *insuficiente* si faltan días conocidos con y sin el factor (≥ 4 por grupo), días con el efecto (≥ 3) o semanas distintas con el efecto (≥ 2); *apoyada* si la diferencia de tasas es ≥ 0,25 y los intervalos de Wilson no se solapan; *sin apoyo* si es ≤ 0,10; lo intermedio es *insuficiente* («no concluyente»). Todos los umbrales son configuración (`analisis:`). **Un día sin dato del efecto no cuenta como «no ocurrió»**: solo cuentan lo dicho (`presente`) o lo confirmado como ausente (`negado` o la respuesta «No sucedió»). Límite conocido: lo mencionado espontáneamente cuenta siempre, y la ausencia solo si se confirmó, por lo que puede sobrerrepresentarse el efecto; por eso las preguntas de contraste buscan equilibrar ambos grupos.

**Preguntas de contraste (X63):** `campo: contraste` con `hipotesis`, `contraste`, `concepto`, `dia` y `clase_pregunta: "contraste:hip-…#cN"`; el `registro` es el de referencia del día. «Respondida» = ocurrió; «No sucedió» = ausencia confirmada; la duración de estar fuera (`concepto: exterior_duracion`) se lee de la respuesta («2 horas», «1h30», «media hora»; lo vago es desconocido).

### 5.0 Aprendizajes (`aprendizajes/`, X64)

Un fichero YAML por regla, visible y editable por el usuario (desde la terminal o el panel). Se regenera desde las preguntas cerradas; lo que el usuario desactiva o borra prevalece.

```yaml
id: apr-duracion-actividad
tipo: aprendizaje
regla: prioridad_baja
clase_pregunta: "duracion:actividad"
descripcion: "Preguntar menos por «duracion» en registros de clase «actividad»: el usuario lo ha cerrado como no relevante 3 veces en 2 días distintos."
estado: activa                 # activa | restablecida | desactivada | borrada
evidencia: [preg-20261116-ab12, preg-20261117-cd34, preg-20261118-ef56]   # cierres «no relevante» que la sustentan
creada_el: "2026-11-19T04:00:11+01:00"
actualizada_el: "2026-11-19T04:00:11+01:00"
fijada_por_usuario: true       # solo si el usuario la activó a mano: Mira no la restablece
borrada_el: "…"                # solo en las borradas: vuelven únicamente con evidencia posterior
historial:
  - {fecha: "2026-11-19T04:00:11+01:00", accion: creada, origen: mira, motivo: "3 cierres «no relevante» en 2 días."}
```

### 5.1 Clima (`clima/`, X60–X62)

Contexto derivado y regenerable obtenido por `orumi clima`; **nunca** se escribe en capturas ni en registros y el modelo no lo recibe al interpretar notas. Un fichero por día y ciudad, en YAML plano (sin frontmatter):

```yaml
fecha: 2026-11-13
ciudad: Barcelona
fuente: open-meteo
ubicacion: {lat: 41.4, lon: 2.2}      # redondeada a 1 decimal (~10 km): lo único que sale al servicio
zona_horaria: Europe/Madrid
obtenido: "2026-11-14T23:22:36+01:00"   # un día guardado el mismo día se vuelve a pedir: está incompleto
dia:
  uv_maximo: 3.15
  amanecer: "07:37"
  atardecer: "17:33"
  luz_s: 35770.55
  sol_s: 32910.12
  precipitacion_mm: 0
  temperatura_minima: 16.3
  temperatura_maxima: 20.8
  humedad_media: 70.625
  nubosidad_media: 31.083
horas:                                 # 24 entradas, hora local
  - {h: 0, temperatura: 18.8, humedad: 63, precipitacion: 0, nubosidad: 0, radiacion: 0, sol_s: 0, uv: 0}
```

- **`desconocido`** es un valor explícito: lo que el servicio no ofrece (p. ej. el UV de un día de hace más de 92 días, que el histórico no trae) nunca se estima ni se rellena.
- La ciudad de un día es la de la captura de origen del registro (la que indicó el usuario o la de por defecto, X62); una ciudad sin reconocer no se supone: no se consulta y se avisa.
- **`clima/ciudades.yaml`** (X67): `ciudades` (clave, nombre, descripción, `lat`/`lon` ya redondeadas, `estado`: `por_confirmar` | `confirmada` | `rechazada`, `origen`: `geocodificada` | `usuario`), `notas` (la ciudad que el usuario fijó para una captura concreta al responder «¿estabas allí?») y `aplicadas` (preguntas de ciudad ya aplicadas). Lo de la configuración manda sobre lo aprendido. Las preguntas de ciudad llevan `campo: ciudad`, `ciudad_tipo` (`confirmar` | `elegir` | `mencion`), `ciudad_clave`, `ciudad_nombre` y, en las de mención, `ciudad_captura`.
- Unidades: °C, %, mm, nubosidad en %, radiación en W/m², horas de sol y de luz en segundos, UV como índice.

## 6. Preguntas y lotes

**Pregunta** (X13: siempre pendiente, nunca interrumpe, ordenada por valor):

```yaml
id: preg-20261113-b7c2
tipo: pregunta
registro: reg-20261110-t5k8
otros_registros: []             # p. ej. conflictos
campo: contexto
texto: "¿Qué te llevó a dejar la automatización para más adelante?"
estado: no_recuerda             # pendiente | respondida | no_recuerda | no_relevante | no_sucedio | cerrada_por_limite
clase_pregunta: "duracion:actividad"   # campo y clase del registro; opcional (las anteriores se deducen); base del aprendizaje
valor: media                    # alta | media | baja
creada_el: 2026-11-13T03:00:00+01:00
generada_por: {agente: Mira, version: "…", modelo: "…"}
cerrada_el: 2026-11-13T08:30:00+01:00     # al cerrarse
respuesta: {texto: "No lo recuerdo.", respondida_el: 2026-11-13T08:30:00+01:00}
```

Las preguntas completan **datos estructurales** del registro (cuándo, duración, personas, lugar, estado o resultado), no su contenido sustantivo (X36). Una pregunta `no_recuerda` o `no_relevante` no se repite; `no_relevante` es el usuario diciendo que el dato no le importa y sirve de señal para no formular preguntas parecidas. Si es `respondida`, el registro afectado pasa el campo a `confirmado` y su `historial` enlaza con la pregunta (la respuesta es la evidencia).

**Lote** (bitácora de cada ejecución de Mira, nocturna o a petición): lista las capturas procesadas y los registros y preguntas creados o actualizados, con versión y modelo. Es lo que permite saber qué capturas ya se procesaron sin modificar las capturas (inmutables) y evita reprocesar capturas que no produjeron registros.

```yaml
id: lote-20261110-0300
tipo: lote
ejecutado_el: 2026-11-10T03:00:00+01:00
ejecucion: lote                 # lote | peticion
agente: {agente: Mira, version: "…", modelo: "…"}
proveedor: anthropic            # obligatorio: a quién se enviaron los datos (o `simulado`/`manual`)
capturas:
  - {captura: cap-20261109-0815-m4k, registros_creados: [reg-…], registros_actualizados: [], preguntas_creadas: []}
uso: {tokens_entrada: 1000, tokens_salida: 500, coste_estimado_usd: "0.000350"}   # opcional: toda la ejecución, incluidas llamadas cuya salida no se pudo guardar
avisos: ["Gasto estimado 4.50 de 5.00 US$ (90%) …"]   # opcional: aviso por presupuesto
problemas: []                   # opcional: [{captura, motivo, tipo}], sin reproducir contenido
```

Cada entrada de `capturas` puede añadir `preguntas_cerradas_por_limite`, `preguntas_descartadas` (número), `nota` (p. ej. `sin hechos registrables`) y el uso de esa captura (`tokens_entrada`, `tokens_salida`, `coste_estimado_usd`; el coste es `desconocido` si la configuración no tiene precios: nunca se inventa). En `problemas`, `tipo` es `saldo_agotado`, `limite_de_gasto` o `clave_invalida` cuando el proveedor cortó la ejecución (lo que deja el lote como el «aviso vigente» hasta que un lote posterior termine sin ese problema); los avisos y los problemas nunca contienen texto de las notas ni la clave. Los registros que una ejecución interrumpida dejó guardados sin lote se adoptan como `registros_creados` en el siguiente lote.

**Envío** (procesamiento diferido con la API de lotes del proveedor, 50 % de descuento): registra un lote enviado para que **una captura no se envíe dos veces** y para recogerlo cuando termine, también tras un reinicio. Es, junto con las preguntas, el único fichero que cambia de estado:

```yaml
id: envio-20261114-0300
tipo: envio
creado_el: 2026-11-14T03:00:00+00:00
estado: abierto                 # abierto | recogido
modo: lotes
proveedor: anthropic
modelo: claude-sonnet-5-5
lote_proveedor: msgbatch_…      # identificador del lote en el proveedor
capturas:
  - {captura: cap-20261113-1733-aev, claves_existentes: [], ofrecidos: [reg-20261112-ab12]}   # ofrecidos: ids de registros que se enviaron como contexto
recogido_el: 2026-11-14T03:20:00+00:00   # solo si está recogido
lote: lote-20261114-0320                 # lote de Mira donde quedó el resultado (solo si está recogido)
```

Los `ofrecidos` se guardan para comprobar, al recoger, que el proveedor solo cita registros que se le enviaron. El proveedor retiene los datos de un lote 29 días y no admite retención cero. El lote de Mira lleva `modo` (`lotes` | `inmediato`) y, si procede, `envios`.

## 8. Perfil de Noa (X68, change `perfil-personal`)

Noa escribe **solo** en `perfil/` y Mira no lo lee (el almacén de Mira no carga `perfil/`). Son YAML legibles a mano.

**`perfil/perfil.yaml`** (versión vigente; cada versión nueva se copia además en `perfil/historial/`):

```yaml
version: 3
generada: 2026-12-04T05:00:00+01:00
generacion: gen-20261204-0500
registros_vistos: 64
apartados_sin_datos: [miedos_preocupaciones]
apartados_ocultos: []           # los oculta el usuario
rasgos:
  - clave: prefiere_mensajes_cortos       # a-z, 0-9 y _; identifica el rasgo entre versiones
    apartado: comunicacion
    texto: "Prefiere mensajes cortos y directos, sin rodeos."
    certeza: hipotesis                    # dicho | confirmado | interpretado | hipotesis
    nivel: patron                         # patron | indicio | conjetura (lo calcula el código)
    sensible: false
    fuentes: 3                            # registros distintos con cita literal verificada
    semanas: 3
    evidencia: [{fuente: reg-20261001-ab12, fecha: 2026-10-01, cita: "mensajes cortos y directos"}]
    estado: activo                        # activo | matizado | descartado | borrado
    origen: noa                           # noa | usuario (corregido por el usuario: prevalece)
    oculto: false
    comentario_usuario: "Solo cuando voy con prisa"   # opcional
    evidencia_al_descartar: 3             # opcional: con cuántas fuentes se descartó
    version_creada: 1
    actualizado: 2026-12-04T05:00:00+01:00
    historial: [{fecha: …, accion: propuesto, origen: noa, detalle: "patron; 3 fuente(s) en 3 semana(s)"}]
recomendaciones:                           # del sistema, no son hechos; solo sobre patrones o indicios
  - {id: rec-0a1b2c3d, texto: "…", rasgos: [prefiere_mensajes_cortos], estado: activo, oculta: false}
```

El apartado `energia_animo` solo admite rasgos con `hipotesis: [ids]` de hipótesis que el usuario confirmó. `superficie` es el apartado de ingeniería social (qué podría deducir un tercero), para que el usuario se proteja.

**`perfil/generaciones/gen-….yaml`**: motivo (`periodica` o `peticion`), resultado (`generada`, `omitida` con su motivo o `fallida`), proveedor, modelo, registros enviados y excluidos, rasgos aceptados y descartados (con el recuento de descartes por causa), tokens y coste estimado. **Nunca contiene texto del usuario.**

**Preguntas de Noa** (`perfil/preguntas/preg-….md`): mismo formato que las de Mira (sección 6), con `generada_por: {agente: Noa}`, **sin `registro`** (no se atan a un registro) y con `rasgo` (la clave del rasgo del perfil) o `apartado` (un apartado sin datos), más `contradice` si pregunta por dos rasgos que no encajan, `campo: perfil` y `clase_pregunta: "noa:<apartado>"` (el aprendizaje de Noa es aparte). Las candidatas las elige el código (conjeturas e indicios no sensibles, contradicciones señaladas por el modelo y comprobadas, apartados sin datos); el cupo por ejecución es `noa.maximo_preguntas` (3). Lo que responde el usuario se guarda en la propia pregunta y entra, como dicho por él, en la siguiente versión. Mira no las carga (el almacén de Mira excluye `perfil/`).

En toda pregunta, `generada_por.agente` es `Mira` o `Noa`; una pregunta anterior sin agente escrito se entiende de Mira sin tocar el fichero.

Borrar el perfil elimina `perfil/` entero (versiones, generaciones y preguntas de Noa) y no toca capturas, registros ni preguntas de Mira.

## 7. Lectura sin herramientas

Todo es texto: cualquier editor muestra capturas, registros y preguntas sin depender de Mira ni del índice. El índice se puede borrar y regenerar (docs/indice-derivado.md).
