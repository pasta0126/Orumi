## Why

El dictado de la web de captura no funciona en la práctica y no hace falta: el usuario puede escribir, y el teclado del teléfono ya ofrece voz a texto nativo, más fiable que el reconocimiento del navegador. Mantenerlo añade código, superficie de pruebas y un aviso de privacidad (el audio sale hacia Google o Apple, X53) para una función que no se usa. El resto de la web de captura se considera adecuado.

## What Changes

- Se retira de la página de captura el botón «Dictar», el reconocimiento de voz del navegador, el aviso de privacidad asociado y sus estilos y animaciones.
- Se retira el icono `microfono` de la colección compartida (`iconos.js`) en captura y panel, manteniendo ambas copias idénticas.
- El envío web deja de enviar el indicador `voz`; toda captura web nace con origen `texto`.
- Se actualiza la descripción del manifiesto («por texto o voz» → «por texto»).
- Se actualizan los textos de ayuda («Escribe o dicta algo…») y la documentación (decisiones, README).
- **Sin cambios**: captura por texto, cola sin conexión, login con Google, diseño compartido y CSP. El valor `voz` sigue siendo un origen válido en el formato de ficheros (capturas existentes o futuras por otra vía); solo la web deja de producirlo.

## Capabilities

### New Capabilities

### Modified Capabilities
- `captura-web`: se elimina el requisito de dictado opcional y el escenario de nota dictada (el requisito de creación se reemplaza por «Crear una captura de texto desde la web»); se reemplaza el requisito de creación por uno solo de texto.

## Impact

- `src/Orumi.Web/wwwroot/` (`index.html`, `app.js`, `captura.css`, `iconos.js`, `manifest.webmanifest`) y la copia de `iconos.js` del panel (`src/Orumi.Panel/wwwroot/`).
- Contrato de la API de captura web (campo `voz` en el cuerpo) y sus pruebas.
- Mejora de privacidad: deja de existir la única vía por la que audio del usuario salía hacia terceros.
- Cierra el pendiente «Probar el dictado» de `docs/decisiones.md` (decisión nueva X59: retirado por innecesario).
