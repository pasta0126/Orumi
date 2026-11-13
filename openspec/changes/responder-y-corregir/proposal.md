## Why

Mira deja preguntas pendientes («¿cuánto duró…?», «¿es la misma conversación?») y puede equivocarse al interpretar, pero hoy el usuario **no tiene forma de contestar ni de corregir** salvo editar los ficheros a mano, que es frágil. Los principios del proyecto dicen que el usuario controla (puede corregir y rechazar conclusiones) y que lo que él aporta se asume válido y no se pisa (X30, X31). El modelo de datos ya lo admite (`confirmado`, `historial`, `rechazos`, estados de pregunta); faltan las herramientas.

## What Changes

- **`orumi responder <pregunta> <valor>`**: contesta una pregunta pendiente con un valor interpretado según el campo por el que se preguntó (duración, fecha, hora, personas, lugares, proyectos, entidades, resultado…). El dato del registro pasa a `confirmado`, la respuesta queda como evidencia en la pregunta y en el `historial` (origen `usuario`) y la pregunta se cierra como respondida.
- **`orumi responder <pregunta> --no-recuerdo`** y **`--no-relevante`**: cierran la pregunta sin tocar el registro (y Mira no vuelve a preguntar lo mismo).
- **`orumi corregir <registro> <campo> <valor>`**: corrige o completa un dato de un registro con la misma lógica de valores; queda `confirmado`, con historial, y Mira lo respeta al reprocesar (X30).
- **`orumi rechazar <registro> <campo>`**: rechaza una interpretación de Mira: se guarda en `rechazos`, el campo queda desconocido y no se vuelve a proponer sin evidencia nueva.
- **Conflictos y coincidencias dudosas:** responder la pregunta de un conflicto indicando a qué registro corresponde el valor lo da por resuelto; la de una coincidencia dudosa admite `misma` o `distinta`.
- Todo se **valida antes de escribir**, se escribe de forma atómica, no toca las capturas y no se ejecuta a la vez que Mira. `consultar pendientes` muestra el contexto necesario para contestar.
- Sin modelo de IA: los valores se interpretan con reglas deterministas; lo que no se entiende se rechaza sin modificar nada.
- Fuera de alcance: fusionar o separar registros, eliminar, y responder por voz o desde el panel.

## Capabilities

### New Capabilities
- `respuestas-y-correcciones`: responder preguntas, corregir y rechazar datos, con historial y respeto de Mira.

### Modified Capabilities
<!-- Ninguna: los requisitos de registros-personales y preguntas-de-mira ya describen el resultado; esta change da las herramientas. -->

## Impact

- Código nuevo en `Orumi.Core` (operaciones y análisis de valores) y `Orumi.Cli` (comandos).
- Documentación: `docs/formato-de-ficheros.md` (origen `usuario` en el historial, estados de conflicto), la guía de notas y `docs/decisiones.md`.
- Despliegue: reconstruir la imagen; sin cambios de configuración ni de gasto.
