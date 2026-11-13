## Why

Hoy ver lo que Mira ha entendido exige leer ficheros por SSH, y contestar o corregir exige comandos. El panel web (componente 3) es la forma natural de consultar, corregir y responder, y su API es la base de la capa conversacional ya aprobada en principio (servidor MCP, consulta en lenguaje natural, bot de captura). Principio 6: el usuario controla y puede consultar, corregir y rechazar conclusiones.

## What Changes

- **Servicio `orumi-panel`** (.NET, contenedor propio tras Traefik): la **API JSON** en `orumi-api.northernarchive.com` y la **página del panel en `orumi.northernarchive.com/panel`**, junto a la captura, con un único inicio de sesión compartido (mismo origen que la captura). La API solo acepta peticiones entre orígenes de una lista cerrada (CORS). Es un servicio **distinto del de captura** (que solo escribe capturas y no puede leer nada): el panel lee todo y solo escribe donde las operaciones lo permiten. Menor privilegio por servicio.
- **Acceso** con el mismo login de Google y la misma lista de correos que la captura (X54), con el código de acceso extraído a una librería compartida.
- **Seguridad en profundidad (requisito del propietario):** toda petición a la API exige sesión válida (solo la carcasa estática y `/api/config` son públicas); **limitación de peticiones por IP, por usuario y por tiempo** (ventanas deslizantes, con `429` y `Retry-After`), bloqueo temporal tras fallos de acceso, tope global para proteger la Raspberry Pi, límites de tamaño y de resultados, comprobación de origen en las escrituras, cabeceras de seguridad (HSTS, CSP, `no-store`, etc.), errores genéricos, registro de auditoría sin contenido y una segunda capa de limitación en Traefik. La misma librería de seguridad se aplica también al servicio de captura.
- **Ver:** registros por día (con navegación), ficha de registro con cada dato y su certeza, el fragmento exacto de la nota del que sale, el historial de cambios, relaciones y conflictos, y el texto de la captura de origen (solo lectura).
- **Buscar:** por texto, fecha, entidad, proyecto, clase y certeza.
- **Preguntas pendientes** con su contexto, y **responderlas** (valor, «no lo recuerdo», «no es relevante», «misma/distinta», registro destino en un conflicto).
- **Corregir y rechazar** datos desde la ficha.
- Las operaciones reutilizan `Operaciones` (responder, corregir, rechazar) ya probadas: validan antes de escribir, escriben de forma atómica, nunca tocan capturas y se rechazan mientras Mira procesa.
- El panel **lee los ficheros directamente** (fuente de verdad, siempre al día tras una corrección), sin depender del índice de PostgreSQL.
- **Fuera de alcance:** fusionar, separar, eliminar o deshacer; patrones y análisis; consulta en lenguaje natural; MCP y bot de Telegram (changes posteriores sobre esta API); varios usuarios.

## Capabilities

### New Capabilities
- `panel-web`: consulta, búsqueda, respuesta y corrección de registros desde una web con API.

### Modified Capabilities
<!-- Ninguna: respuestas-y-correcciones y captura-web no cambian de requisitos. -->

## Impact

- Proyecto nuevo `src/Orumi.Panel` y librería `src/Orumi.Acceso` (acceso por sesión, limitación de peticiones y cabeceras de seguridad, extraídos de `Orumi.Web` y compartidos con él); imagen `Dockerfile.panel`.
- Infra: servicio en `infra/orumi/panel` con Traefik, montaje de `~/orumi-datos` con lectura y escritura **salvo `capturas/` en solo lectura**, y la misma configuración de correos.
- **Riesgo nuevo:** una URL pública que sirve todo el contenido personal (a diferencia de la captura). Mitigaciones en el diseño.
- Documentación: X56, guía, seguridad.
