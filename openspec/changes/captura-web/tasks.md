## 1. Servicio

- [x] 1.1 Proyecto `Orumi.Web` (minimal API) y endpoint de creación de capturas con `CreadorDeCapturas` (origen, fecha del acontecimiento, etiqueta, dispositivo)
- [x] 1.2 Autenticación por token (tiempo constante), límite de intentos y bloqueo temporal
- [x] 1.3 Idempotencia por identificador de envío
- [x] 1.4 Pruebas: texto vacío, fecha inválida, token inválido y bloqueo, reenvío sin duplicado, origen texto y voz, no existen operaciones de lectura ni borrado

## 2. Página

- [x] 2.1 Página de captura: texto, dictado (es-ES, parciales, editable), fecha y etiqueta, confirmación con el identificador y aviso de privacidad del dictado
- [x] 2.2 PWA: manifest, service worker, cola sin conexión en IndexedDB con reintento
- [x] 2.3 Probar en Chrome (Mac) y en el móvil, incluido el modo sin conexión

## 3. Despliegue y documentación

- [ ] 3.1 Imagen y servicio en `infra` (Traefik, token, volumen), DNS del subdominio por el usuario
- [ ] 3.2 Registrar X53 (privacidad del dictado y acceso), actualizar `docs/tecnologias.md`, la guía y `infra` (README, ROADMAP, seguridad)
- [ ] 3.3 Prueba real extremo a extremo: dictar en el móvil, ver la captura en el servidor y procesarla
