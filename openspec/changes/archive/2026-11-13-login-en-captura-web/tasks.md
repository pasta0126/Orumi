## 1. Servicio

- [x] 1.1 Verificador de sesión contra GoTrue, con caché breve, lista de correos permitidos y respuestas 401/403/503/429
- [x] 1.2 Sustituir el acceso por token en los endpoints; endpoint público `/api/config`; CSP hacia GoTrue
- [x] 1.3 Pruebas: sin sesión, sesión válida y correo permitido, correo no permitido, correo no verificado, GoTrue caído, bloqueo, caché

## 2. Página

- [x] 2.1 Entrar y salir con Google, lectura del fragmento, renovación automática, notas conservadas si caduca la sesión
- [x] 2.2 Probar el flujo en Chrome

## 3. Despliegue y documentación

- [x] 3.1 Configuración en `infra` (lista de correos, GoTrue interno), eliminar el token, redesplegar
- [x] 3.2 Documentar (X54, guía, seguridad, infra) y probar con inicio de sesión real
