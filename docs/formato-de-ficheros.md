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
---
<texto literal tal como se recibió>
```

- **Cuerpo:** todo lo que sigue a la línea de cierre `---` y su salto de línea, sin tocar (ni limpieza, ni resumen, ni salto final añadido).
- **`huella`:** permite comprobar que el cuerpo no se ha alterado. Si no coincide, el fichero se considera dañado.
- **`comunicada_el`** es siempre la fecha y hora de comunicación; `fecha_acontecimiento` no la altera.
- **Prohibido:** ubicación y cualquier monitorización. `contexto_automatico` solo admite `dispositivo` (la hora ya está en `comunicada_el`).
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

### 4.5 Rechazos

Una interpretación rechazada se conserva para no volver a proponerla sin evidencia nueva:

```yaml
rechazos:
  - {campo: contexto, valor: "Posible falta de tiempo", certeza_original: interpretado, rechazada_el: 2026-11-12T20:10:00+01:00}
```

El campo queda `desconocido` o con el valor que dé el usuario.

### 4.6 Historial

Una entrada por cambio, en orden cronológico; incluye cambios de Mira y del usuario:

```yaml
historial:
  - {fecha: 2026-11-12T20:05:00+01:00, origen: usuario, campo: proyectos, anterior: [copias-de-seguridad], nuevo: [mantenimiento-servidor]}
```

`origen`: `usuario` o `mira`. Opcionales: `pregunta` (si el cambio responde una pregunta), `captura`, `motivo`. Un cambio de versión de Mira se anota en `procedencia.actualizado_*` y conserva en `historial` lo que sustituye.

## 5. Estructura de directorios de `orumi/` (tarea 1.4)

```
orumi/
├── capturas/AAAA/MM/cap-….md       # inmutables
├── registros/AAAA/MM/reg-….md
├── preguntas/AAAA/MM/preg-….md
├── lotes/AAAA/MM/lote-….md         # bitácora de ejecuciones de Mira
├── informes/AAAA/MM/inf-….md       # revisiones y análisis (change posterior)
├── entrada/                         # audio pendiente de transcribir/verificar (fuera de git)
└── .indice/                         # índice derivado, desechable (ver docs/indice-derivado.md)
```

- La ruta se deduce del identificador (fecha de creación): no cambia al editar el fichero.
- El estado (`pendiente`, `abierto`…) va en el frontmatter, nunca en la ruta, para no mover ficheros.
- Nada de `orumi/` se copia a vedlvm ni a su remoto (X12).

## 6. Preguntas y lotes

**Pregunta** (X13: siempre pendiente, nunca interrumpe, ordenada por valor):

```yaml
id: preg-20261113-b7c2
tipo: pregunta
registro: reg-20261110-t5k8
otros_registros: []             # p. ej. conflictos
campo: contexto
texto: "¿Qué te llevó a dejar la automatización para más adelante?"
estado: no_recuerda             # pendiente | respondida | no_recuerda | no_relevante | cerrada_por_limite
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
capturas:
  - {captura: cap-20261109-0815-m4k, registros_creados: [reg-…], registros_actualizados: [], preguntas_creadas: []}
```

## 7. Lectura sin herramientas

Todo es texto: cualquier editor muestra capturas, registros y preguntas sin depender de Mira ni del índice. El índice se puede borrar y regenerar (docs/indice-derivado.md).
