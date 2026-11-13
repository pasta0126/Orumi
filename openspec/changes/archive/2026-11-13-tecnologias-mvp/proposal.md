## Why

El modelo de registro, el contrato de Mira y la privacidad están definidos y archivados, pero no hay tecnología elegida (X32) y por tanto nada que ejecutar. Antes de escribir código hay que decidir con criterios explícitos —coste, privacidad, simplicidad, encaje con un servidor pequeño ya existente— el lenguaje, el índice, la ejecución y, sobre todo, el proveedor y el modelo de IA, que no deben elegirse por reputación sino con precios, condiciones de privacidad, estabilidad y fiabilidad leídos en fuentes oficiales, mediante una evaluación ponderada.

## What Changes

- Decide el **stack del MVP**: .NET como lenguaje, PostgreSQL (base de datos propia en la instancia compartida del servidor) como índice derivado, ejecución en contenedor con temporizador y configuración, con alternativas y motivos.
- Elige **proveedor y modelo de IA** con una evaluación ponderada basada en evidencias oficiales fechadas (precio, privacidad, estabilidad, fiabilidad, SDK), sin pruebas reales; el proveedor que se contrate es el que se usa.
- Especifica el comportamiento de cuatro capacidades del MVP: ejecución y despliegue, captura de texto por línea de comandos, configuración del proveedor de IA (con la garantía de privacidad comprobable) y validación e índice.
- Documenta todo en `docs/tecnologias.md` y deja el esquema de configuración.
- **No escribe código.** La implementación va en una change posterior que parte de estas specs.
- No incluye voz (STT), API de entrada, app ni panel.

## Capabilities

### New Capabilities
- `ejecucion-y-despliegue`: cómo y dónde corre Mira, planificación, exclusión mutua, secretos y exposición.
- `captura-de-texto`: crear una captura inmutable desde texto con un comando.
- `configuracion-del-proveedor-ia`: proveedor y modelo sustituibles, garantía de privacidad declarada, fechada y comprobada antes de cada ejecución.
- `validacion-e-indice`: comandos para validar `orumi/` contra el formato y regenerar el índice derivado.

### Modified Capabilities
<!-- Ninguna: no cambian requisitos ya vigentes; se concretan con comportamiento nuevo. -->

## Impact

- Nuevas specs en `openspec/specs/` al archivar.
- Nuevo `docs/tecnologias.md` y datos privados de despliegue en `docs/privado/` (fuera de git).
- Actualiza `AGENTS.md` (estado) y `docs/decisiones.md`.
- Necesita una cuenta de pago por API del proveedor elegido (por X38), con límite de gasto mensual, y una clave de API solo en el servidor.
