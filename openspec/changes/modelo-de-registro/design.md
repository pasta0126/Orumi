## Context

`orumi/` es un directorio propio en el servidor de despliegue, separado de vedlvm (X2). Los ficheros son `.md` con frontmatter, el formato que ya manejan Atlas y vedlvm. El agente Mira corre por lotes de noche y a petición (X4). El audio se borra tras transcribir (X5). Motivación en `proposal.md`; requisitos en `specs/`.

## Goals / Non-Goals

**Goals:**
- Modelo legible a mano, versionable con git y consistente entre componentes.
- Trazabilidad completa: registro → captura → texto literal.
- Reprocesado idempotente.

**Non-Goals:**
- Elegir lenguaje, framework, base de datos concreta o proveedor de IA.
- Diseñar la API de entrada, la app Flutter o el panel.
- Definir las reglas de detección de patrones (change posterior).

## Decisions

1. **Dos tipos de fichero: `captura` y `registro`** (X10). Capturas inmutables; registros editables con historial. *Alternativa:* un fichero por captura con registros dentro; descartada por dificultar fusionar, separar y relacionar.
2. **Certeza en el frontmatter, por dato** (X11). Cada campo relevante lleva su categoría. Permite filtrar en el índice. *Alternativa:* en prosa; no es consultable.
3. **Identificadores estables y no derivados del contenido** (p. ej. fecha + sufijo), de modo que renombrar o corregir no rompe enlaces. El formato exacto se fija en la implementación.
4. **Enlaces por identificador** entre registros y capturas, además de wikilinks legibles para compatibilidad con Atlas.
5. **Historial de correcciones dentro del propio registro**, y git como respaldo adicional. *Alternativa:* solo git; no es consultable desde el panel.
6. **Reprocesado idempotente:** cada registro guarda la captura de origen y una clave de extracción estable, de modo que regenerar actualiza en lugar de duplicar. Se registra versión de Mira y modelo.
7. **Índice derivado reconstruible** (X3). El motor concreto (SQLite o el Postgres existente) se decide en una change posterior.
8. **Sin respaldo en vedlvm** (X12, sustituye a X2 en ese punto): los datos de ORUMI no salen hacia vedlvm ni GitHub por privacidad. El respaldo de `orumi/` se decidirá aparte. *Alternativa descartada:* copia de informes en vedlvm.
9. **Información imperfecta** (X16–X19): conflictos marcados sin ganador, un registro multi-día con entradas fechadas, rango estimado para fechas imprecisas y completitud no bloqueante. *Alternativas descartadas:* prevalece lo más reciente, un registro por día, no estimar fechas, estado «completo» explícito.
10. **Capturas multitema y aportaciones** (X24–X26): la captura no se divide; los registros referencian fragmentos de origen. La coincidencia con un registro existente se decide por la clave de extracción (decisión 6) y, ante duda, se pregunta. Metadatos opcionales sin ubicación.

## Risks / Trade-offs

- **Fichero y registro divergen del índice** → el índice es siempre regenerable; el fichero prevalece.
- **Corrección manual y regeneración de Mira se pisan** → los datos confirmados o corregidos por el usuario no los sobrescribe una regeneración.
- **Frontmatter con certeza por campo se vuelve verboso** → limitar la certeza a los datos relevantes, no a todos.
- **Sin audio, un error de STT es irrecuperable** → verificar la transcripción antes de borrar y reabrir X5 si la calidad es insuficiente.
- **Único respaldo es el disco del servidor** → decidir cómo respaldar `orumi/` (repo propio con remoto privado, copia local en otro equipo) en una change posterior.
