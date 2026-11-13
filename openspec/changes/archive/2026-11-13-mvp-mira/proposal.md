## Why

Con el modelo de registro, el contrato de Mira, la privacidad y el stack ya decididos y archivados, falta lo que convierte todo eso en algo que funciona: el código de los cuatro comandos del MVP. Se construye por capas, empezando por lo que no necesita el servicio de IA (captura, validación, índice), de modo que todo se pueda probar con los ejemplos ficticios y el proveedor de IA se contrate solo cuando haga falta (X42). Los datos se guardan sin cifrar en reposo por decisión del propietario (X45): sin respaldo y con un servidor doméstico, el cifrado aportaba poca protección y añadía riesgo de pérdida y trabajo de operación. La herramienta conserva una exigencia de volumen opcional por si algún día se cifra.

## What Changes

- Implementa en .NET los comandos `capturar`, `validar`, `reindexar` y `procesar`, con la estructura `Orumi.Core`, `Orumi.Agente`, `Orumi.Indice` y `Orumi.Cli`.
- Implementa la validación del formato con las reglas del contrato, usando los ejemplos de `docs/ejemplos/` como datos de prueba, más casos negativos.
- Implementa el índice en PostgreSQL (base y rol propios), regenerable desde los ficheros.
- Implementa `procesar` detrás de una interfaz de proveedor de IA, con un **cliente simulado** para las pruebas y un cliente real de Anthropic que **no se ejercita hasta contratar el servicio**.
- Empaqueta la herramienta en un contenedor para ARM64, sin puertos publicados.
- **Cifrado en reposo opcional**, desactivado por defecto, con fallo cerrado si se exige el volumen y no está disponible: nuevos requisitos en `ejecucion-y-despliegue`.
- Prepara el despliegue en el servidor, con confirmación del usuario en cada paso que lo modifique.
- No incluye voz (STT), API de entrada, app ni panel, ni la contratación del servicio de IA.

## Capabilities

### New Capabilities
<!-- Ninguna: el comportamiento ya está especificado en las specs vigentes. -->

### Modified Capabilities
- `ejecucion-y-despliegue`: el cifrado en reposo es opcional (por defecto no) y, si se exige el volumen, las herramientas no escriben ni procesan sin él.

## Impact

- Código nuevo en `src/` y `tests/` y un fichero de contenedor.
- Cambios en el servidor (directorios, base de datos, imagen y, al contratar la IA, el temporizador), por el repo `infra`.
- Las specs vigentes `captura-de-texto`, `validacion-e-indice`, `configuracion-del-proveedor-ia`, `procesamiento-de-capturas`, `preguntas-de-mira` y `privacidad-del-procesamiento` pasan a tener implementación.
- Sin gasto hasta contratar el servicio de IA.
