## Context

La plataforma ya ofrece GoTrue (`auth.northernarchive.com`, Google activo, un solo conjunto de usuarios, tokens de acceso de 1 h y de refresco). Comprobado: el registro está abierto (`disable_signup: false`), CORS permite llamar desde otros orígenes, `GOTRUE_URI_ALLOW_LIST: "**"` admite volver a `orumi.northernarchive.com`, y `platform-gotrue:9999` es alcanzable desde la red `proxy`.

## Decisions

1. **Validar preguntando a GoTrue (`GET /user` con el token de la sesión)** en lugar de verificar la firma con el secreto JWT compartido. Así `orumi-web` no tiene una credencial que sirva para suplantar a cualquier usuario en todos los proyectos. Coste: una llamada interna por petición; se cachea el resultado positivo 60 s por huella del token.
2. **Lista de correos permitidos** (`ORUMI_WEB_EMAILS`, separados por comas) y correo verificado (`email_confirmed_at`), porque cualquiera puede registrarse en el GoTrue compartido. Se comparan sin distinguir mayúsculas.
3. **Flujo OAuth implícito de GoTrue:** la página redirige a `/authorize?provider=google&redirect_to=<origen>` y recibe `access_token` y `refresh_token` en el fragmento de la URL; se guardan en `localStorage` y el fragmento se borra de la barra. Renovación con `POST /token?grant_type=refresh_token` desde la página (CORS permitido).
4. **Respuestas distintas:** 401 sin sesión válida, 403 sesión válida pero cuenta no autorizada, 503 si GoTrue no responde, 429 por bloqueo. Los 401/403 cuentan para el límite de intentos; los 503 no.
5. **Sin token estático:** se elimina (menos superficie; no hay nada que copiar). Para uso por scripts queda `orumi capturar` por SSH.
6. **CSP:** se permite `connect-src` hacia el origen de GoTrue (configurable con `ORUMI_WEB_AUTH_PUBLICO`, por defecto `https://auth.northernarchive.com/auth/v1`) y la página lo lee de `/api/config`, que es pública y solo devuelve esa URL.

## Risks / Trade-offs

- **Tokens en `localStorage`** (alcance XSS): la página no carga scripts de terceros, la CSP es estricta y el servicio solo crea notas.
- **Dependencia de GoTrue** para crear notas: la página guarda las notas en el dispositivo y las envía al volver.
- **Una cuenta Google comprometida** de la lista permitiría crear notas falsas (no leer): mismo alcance que antes.
