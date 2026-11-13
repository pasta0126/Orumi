## Context

El modelo de registro se definió en la change archivada `2026-11-13-modelo-de-registro`. Los campos y ejemplos viven en `docs/formato-de-ficheros.md` y `docs/ejemplos/`; el esquema del índice, en `docs/indice-derivado.md`. La prueba real mostró que `contexto` y `herramientas` acaban mezclando entidades, y que `fecha` (rango de días) no admite hora. ORUMI es autónomo y no se integra con otro software (X34), y no se elige tecnología.

## Goals / Non-Goals

**Goals:**
- Poder consultar registros por entidad nombrada y por hora.
- Mantener la regla «un campo, una certeza»: el día y la hora pueden tener certezas distintas.
- Compatibilidad: los registros existentes siguen siendo válidos.

**Non-Goals:**
- Un catálogo maestro de entidades, relaciones entre ellas o fusión automática de entidades.
- Hora con zona horaria, duración calculada o calendario.
- Decidir qué contenido cotidiano guarda Mira (pertenece a la change del agente).
- Cambiar capturas, preguntas, lotes o la clave de extracción.

## Decisions

1. **`entidades` como campo propio, con elementos `{nombre, tipo}`.** `tipo` es opcional: `organizacion`, `sistema`, `producto`, `otro`. Es el nombre tal como se mencionó, con ortografía normalizada (por ejemplo «gullon» pasa a «Gullón»). *Alternativa descartada:* seguir usando `contexto`, que no es consultable.
2. **`entidades` y `herramientas` se distinguen por uso:** la entidad es aquello a lo que se refiere el registro (un cliente, un sistema con una incidencia); la herramienta es el instrumento con el que se hace algo (Excel, Teams). Una misma cosa puede aparecer en ambos campos si cumple los dos papeles. *Alternativa descartada:* unificarlos; perdería la distinción entre «lo que se hace» y «con qué».
3. **Sin catálogo de entidades.** Se identifican por nombre; el usuario puede corregir el nombre en un registro y esa corrección respeta X30. Una consulta por entidad compara nombres sin distinguir mayúsculas ni acentos. *Alternativa descartada:* fichero maestro de entidades; añade fusión y mantenimiento sin necesidad probada.
4. **`hora` como campo separado de `fecha`,** con certeza propia: `{expresion, valor, aproximada}`. `valor` es `HH:MM` en hora local; `aproximada: true` cuando el usuario dijo «sobre las»; si solo dio una franja («por la mañana») se guarda `expresion` sin `valor`. Se refiere al inicio del acontecimiento. *Alternativa descartada:* meter la hora en `fecha`; obligaría a una única certeza para día y hora.
5. **Franjas no se convierten en horas.** «Por la mañana» no genera un `valor`: no se inventan cifras.
6. **Campos opcionales y sin versión de formato.** Su ausencia no invalida un registro, de modo que no hace falta migrar ni versionar el formato.
7. **Preguntas: datos estructurales y cierre «no relevante» (X36).** Mira pregunta para completar cuándo, hora, duración, personas, lugar, estado o resultado, no el contenido sustantivo de lo hablado. «No relevante» es un estado de cierre más (`no_relevante`), que no se repite y desalienta preguntas parecidas. *Alternativa descartada:* dejar que Mira pregunte cualquier laguna y que el usuario las descarte una a una; genera ruido.
8. **Índice:** nueva tabla `registro_entidad` (registro, nombre, tipo, certeza) y columnas `hora_valor` y `hora_aproximada` en `registro`.

## Risks / Trade-offs

- **La misma entidad escrita de formas distintas dispersa las consultas** → normalización de nombre y comparación sin acentos ni mayúsculas; el usuario puede corregir. Si no basta, una change posterior añadirá alias.
- **Frontera difusa entre entidad y herramienta** → criterio de la decisión 2 y ejemplos; se revisará con el uso real.
- **Más campos aumentan la verbosidad del frontmatter** → ambos son opcionales y solo se rellenan cuando el usuario los menciona.
