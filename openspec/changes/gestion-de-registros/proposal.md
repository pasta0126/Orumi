## Why

El usuario puede corregir un dato y rechazar una interpretación, pero no puede deshacer una corrección, quitar un registro erróneo, unir dos registros que son el mismo hecho ni separar una nota mal asignada a otro registro. Hoy cada error de Mira o del propio usuario queda ahí o se arregla a mano en los ficheros. Principio 6: el usuario puede consultar, corregir, **fusionar, separar, eliminar** y rechazar conclusiones.

## What Changes

- **Deshacer** (`orumi deshacer REGISTRO [CAMPO]`): revierte la última corrección o rechazo del usuario sobre un campo (valor y certeza anteriores), dejándolo anotado en el historial.
- **Eliminar y restaurar** (`orumi eliminar REGISTRO`, `orumi restaurar REGISTRO`): el registro pasa a estado `eliminado` (papelera reversible; el fichero y su clave se conservan). Desaparece de consultas, día, búsqueda y preguntas, y sus preguntas pendientes y conflictos abiertos se cierran. **La nota original nunca se toca.**
- **Fusionar** (`orumi fusionar DESTINO ORIGEN`): el origen se funde en el destino (fuentes, entradas, relaciones y datos que el destino no tenía) y queda como «tumba» `fusionado` apuntando al destino. Si ambos tienen un valor distinto para un campo, gana el del destino y el otro queda en el historial.
- **Separar** (`orumi separar REGISTRO CAPTURA [--titulo T]`): saca de un registro una captura que Mira añadió como ampliación por error y crea con ella un registro nuevo. Los datos que esa captura aportó al registro original no se revierten solos: se listan para revisarlos con `corregir`.
- **Mira respeta lo hecho:** no recrea ni reabre un registro eliminado o fusionado, ni vuelve a ampliar un registro con una captura que se separó de él.
- **Panel:** botones Deshacer, Eliminar, Fusionar y Separar en la ficha, y vista «Papelera» con Restaurar.
- El índice de consultas ignora los registros no activos.
- Fuera de alcance: borrado definitivo (una decisión aparte), eliminar o modificar capturas, deshacer varios pasos a la vez.

## Capabilities

### New Capabilities
- `gestion-de-registros`: deshacer, eliminar, restaurar, fusionar y separar registros con trazabilidad.

### Modified Capabilities
- `registros-personales`: un registro puede estar activo, eliminado o fusionado.
- `procesamiento-diferido` y el flujo de Mira: reprocesar no recrea lo eliminado ni lo fusionado.

## Impact

- `Orumi.Core` (formato y validador, operaciones), `Orumi.Agente` (reprocesado), `Orumi.Indice` (columna de estado y filtros), `Orumi.Cli` y `Orumi.Panel` (comandos, API y página).
- Documentación: `docs/formato-de-ficheros.md`, `docs/agente-mira.md`, guía y decisiones (X58).
- Despliegue: reconstruir imágenes; sin cambios de configuración ni gasto.
