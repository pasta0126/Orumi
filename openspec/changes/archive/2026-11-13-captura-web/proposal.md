## Why

Hoy capturar exige un terminal y SSH. El principio 1 dice que capturar debe ser más fácil que organizar. Una **web de captura** con dictado por voz, accesible desde el Mac, un PC o el móvil (instalable como app en la pantalla de inicio), elimina esa fricción sin desarrollar una app nativa, y es el primer paso del panel web (componente 3).

## What Changes

- **Servicio web `orumi-web`** (.NET, contenedor propio detrás de Traefik con TLS, en un subdominio de `northernarchive.com`), de un solo usuario. Esta change solo **crea capturas**; no lee ni modifica registros.
- **Página de captura** (instalable, PWA): un cuadro de texto grande, botón de **dictado por voz** (reconocimiento de voz del navegador, en español), edición del texto antes de enviar, opciones de fecha del acontecimiento y etiqueta, y confirmación del identificador creado.
- **No se pierde nada sin conexión:** la nota se guarda en el dispositivo y se envía cuando haya red; el original no se pierde aunque falle el servidor.
- **Acceso:** un token de acceso largo, solo en el servidor y en el dispositivo del usuario, con límite de intentos; sin él no se puede crear nada. Todo por HTTPS.
- Las capturas se crean con el mismo código y las mismas validaciones que `orumi capturar` (inmutables, con huella), con `origen: voz` si se dictó (y se conserva el texto tal como quedó; el dictado se considera texto del usuario) o `texto` si se escribió, y `dispositivo`.
- **Decisión de privacidad a registrar:** el reconocimiento de voz del navegador envía el **audio** al proveedor del navegador (Google en Chrome/Edge, Apple en Safari). Es un envío externo nuevo, distinto del de Anthropic, y hay que aceptarlo explícitamente. Queda como opción (se puede escribir sin dictar) y se avisa en la propia página.
- Fuera de alcance: guardar el audio, transcripción propia (Whisper), ver o corregir registros, responder preguntas (siguiente change: panel), cuentas múltiples.

## Capabilities

### New Capabilities
- `captura-web`: creación de capturas desde una web instalable, con dictado, cola sin conexión y acceso por token.

### Modified Capabilities
<!-- Ninguna. -->

## Impact

- Proyecto nuevo `src/Orumi.Web` y su imagen/contenedor; reutiliza `Orumi.Core` (CreadorDeCapturas).
- Infra (repo `infra` del servidor): servicio con etiquetas de Traefik, DNS del subdominio, token en `~/orumi-config`, volumen de datos de escritura. Requiere sudo solo si hay que tocar DNS/firewall (lo gestiona el usuario).
- Documentación: decisión X53 (privacidad del dictado y acceso), guía de notas y `docs/tecnologias.md`.
