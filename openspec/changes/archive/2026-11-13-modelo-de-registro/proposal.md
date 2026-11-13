## Why

ORUMI necesita un modelo de datos común antes de construir captura, agente (Mira) o panel: qué se guarda, cómo se vincula cada interpretación con su origen y cómo se distingue lo dicho de lo supuesto. Sin él, cada componente inventaría su propio formato y se perdería la trazabilidad exigida por los principios del producto (original irremplazable, certeza explícita, control del usuario). Ver `docs/decisiones.md` (X2, X3, X5, X10, X11).

## What Changes

- Define las **capturas originales**: texto crudo e inmutable, que pasa a ser el original del sistema (el audio se borra tras transcribir, X5).
- Define el **registro personal**: una unidad significativa (evento, actividad, idea, decisión, problema…) como fichero `.md` propio con metadatos estructurados, enlazado a su captura de origen.
- Define la **certeza** de cada dato en cinco categorías y la **procedencia** (captura, versión de Mira, correcciones).
- Define la **vista derivada**: un índice reconstruible desde los ficheros para consultas estructuradas, y el **aislamiento** de los datos de ORUMI respecto a vedlvm (sin copias, X12).
- No decide tecnología, formato físico de ficheros más allá de lo ya acordado (`.md` con frontmatter), ni el mecanismo de entrada de capturas.

## Capabilities

### New Capabilities
- `capturas-originales`: almacenamiento inmutable de lo que el usuario comunica, con identidad, fecha y origen.
- `registros-personales`: modelo del registro (identidad, fechas, tipo, relaciones, certeza, procedencia, historial de correcciones) y su relación con las capturas.
- `indice-y-respaldo`: índice derivado reconstruible y separación de los datos de ORUMI respecto a vedlvm.

### Modified Capabilities
<!-- Ninguna: el repositorio aún no tiene specs vigentes. -->

## Impact

- Nuevas specs en `openspec/specs/` al archivar el cambio.
- Condiciona las changes futuras: agente Mira, API de entrada, app Flutter y panel web.
- Sin código ni dependencias nuevas en este cambio.
