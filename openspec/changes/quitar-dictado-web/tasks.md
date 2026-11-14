## 1. Página de captura

- [x] 1.1 Quitar de `index.html` el botón de dictado y el aviso de privacidad del dictado; ajustar el texto de ayuda («Escribe o dicta…» → «Escribe…»)
- [x] 1.2 Quitar de `app.js` el reconocimiento de voz, `huboDictado`, `marcarMic` y el campo `voz` del envío
- [x] 1.3 Quitar de `captura.css` los estilos del botón de dictado (halo, ondas) y las animaciones que solo usaba
- [x] 1.4 Quitar el icono `microfono` de `iconos.js` en captura y panel (copias idénticas) tras comprobar que no se usa en otro sitio
- [x] 1.5 Actualizar la descripción de `manifest.webmanifest` y subir la versión de la caché del service worker

## 2. Servicio

- [x] 2.1 Ignorar el indicador `voz` del cuerpo de la petición: la captura web se crea siempre con origen `texto`
- [x] 2.2 Actualizar las pruebas (nota dictada → eliminada; indicador heredado ignorado; igualdad de ficheros compartidos y ausencia de emojis siguen pasando)

## 3. Documentación y cierre

- [x] 3.1 `docs/decisiones.md`: nueva decisión X59 (dictado retirado), anotar en X53 que queda sustituida en lo del dictado y quitar el pendiente «Probar el dictado»; actualizar AGENTS.md (estado actual) y README si lo mencionan
- [ ] 3.2 Desplegar `orumi-web` en el servidor vía `infra` y comprobar desde el móvil que se puede escribir y enviar
