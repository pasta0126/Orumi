## Why

La prueba con una nota real (2026-11-13, ver `docs/decisiones.md`) mostró dos huecos del modelo de registro. Los nombres de clientes, empresas y sistemas (Gullón, GH, Infobip, Outlook, Adding Plus…) solo cabían en texto libre, así que no se puede consultar «todo lo relacionado con Gullón». Y la hora del acontecimiento («sobre las 9.30») no tenía dónde guardarse, porque la fecha solo admite días.

## What Changes

- Añade al registro el campo **`entidades`**: organizaciones, clientes, proveedores, sistemas o productos nombrados, cada uno con su certeza y consultable.
- Añade al registro el campo **`hora`**: hora exacta, aproximada o franja de un acontecimiento de un día, con su propia certeza, separada de la fecha.
- El índice derivado pasa a poder consultar por entidad y por hora.
- Se actualizan `docs/formato-de-ficheros.md`, `docs/indice-derivado.md` y los ejemplos de `docs/ejemplos/`.
- Las preguntas pendientes pueden cerrarse también como **«no relevante»** y se orientan a completar datos estructurales del registro, no su contenido sustantivo (X36).
- Los campos nuevos son opcionales: los registros ya existentes siguen siendo válidos sin migrarlos.
- No decide tecnología ni cambia capturas, preguntas ni lotes.

## Capabilities

### New Capabilities
<!-- Ninguna -->

### Modified Capabilities
- `registros-personales`: el registro admite entidades nombradas y hora del acontecimiento, cada una con su certeza; las preguntas se orientan a datos estructurales y pueden cerrarse como no relevantes.
- `indice-y-respaldo`: el índice permite consultar por entidad y por hora.

## Impact

- Specs vigentes `registros-personales` e `indice-y-respaldo` (deltas MODIFIED/ADDED).
- Documentos de formato e índice y ejemplos ficticios.
- Los datos de prueba locales (`data/orumi/`, fuera de git) pueden actualizarse a mano para ejercitar los campos nuevos; no es obligatorio.
- Condiciona la futura change de Mira, que tendrá que extraer estos campos.
