## 1. Base

- [x] 1.1 Extraer `Orumi.Acceso` (acceso por sesión con identidad del usuario, verificador GoTrue, configuración) y adaptar `Orumi.Web` sin cambiar su comportamiento
- [x] 1.1b `LimitadorDePeticiones` (ventana deslizante, reloj inyectable) y cabeceras de seguridad en la librería compartida; aplicarlos también a `Orumi.Web`
- [x] 1.2 Proyecto `Orumi.Panel` con acceso, cabeceras de seguridad (`no-store`, CSP) y lector de datos con recarga por huella
- [x] 1.3 Vistas de lectura: estado, días, registros con filtros y paginación, ficha con certezas, fuentes (cita), historial y relaciones, y captura de origen solo lectura
- [x] 1.4 Preguntas pendientes con contexto
- [x] 1.5 Operaciones de escritura (responder, cerrar, corregir, rechazar) con bloqueo frente a Mira y errores 400/404/409
- [x] 1.5b Endurecimiento: límites por IP, usuario y global con `429` y `Retry-After`, tamaño de cuerpo, longitud de búsqueda y página máxima, validación de identificadores, comprobación de `Origin` y tipo de contenido, errores genéricos y auditoría sin contenido
- [x] 1.6 Pruebas: acceso (todas las rutas de datos devuelven 401 sin sesión), limitación con tiempo simulado, filtros, ficha, desconocidos, cada operación, Mira procesando, no existen PUT/DELETE ni operaciones sobre capturas, no-store, datos al día tras cambios externos

## 2. Página

- [x] 2.1 Vistas Día, Pendientes, Buscar y Ficha (texto siempre como texto, móvil, tema, teclado)
- [ ] 2.2 Probar en Chrome contra datos de ejemplo

## 3. Despliegue y documentación

- [ ] 3.1 `Dockerfile.panel` y servicio en `infra` (Traefik con `ratelimit`, `inflightreq` y límite de cuerpo, `orumi-api.northernarchive.com`, `capturas/` en solo lectura)
- [ ] 3.2 Documentar (X56, guía, seguridad, infra) y probar con datos reales
