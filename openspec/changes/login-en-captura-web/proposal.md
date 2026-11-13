## Why

La web de captura se desplegó con un token que el usuario tendría que copiar a cada dispositivo. El usuario no quiere gestionar tokens: prefiere el inicio de sesión con Google que ya usan sus otras aplicaciones (GoTrue compartido de la plataforma). Con el login, el acceso es cómodo y se puede revocar por cuenta.

## What Changes

- **BREAKING (interno):** se elimina el token de acceso de `orumi-web` (y su fichero `orumi-web.env` con el token). El acceso pasa a ser **«Entrar con Google»** mediante el GoTrue compartido (`auth.northernarchive.com`).
- El servicio valida cada petición preguntando a GoTrue por la red interna quién es el dueño de la sesión (sin recibir el secreto JWT compartido, que es una credencial raíz de todos los proyectos) y **solo admite correos de una lista** configurada, porque el registro del GoTrue compartido está abierto a cualquier cuenta de Google.
- La página gestiona la sesión: inicio de sesión, renovación automática con el token de refresco, cierre de sesión y conservación de las notas pendientes si la sesión caduca.
- Se mantienen el bloqueo por intentos fallidos, la cola sin conexión y la idempotencia.
- Fuera de alcance: login con usuario y contraseña, varios usuarios, roles.

## Capabilities

### New Capabilities
<!-- Ninguna. -->

### Modified Capabilities
- `captura-web`: el requisito «Acceso restringido» pasa de token a sesión de Google con lista de correos permitidos.

## Impact

- `src/Orumi.Web` (verificación de sesión, endpoints, página), pruebas, compose y configuración en `infra`.
- Dependencia nueva en ejecución: el contenedor `platform-gotrue` (si no responde, la web no crea notas pero la página las guarda en el dispositivo).
- Documentación: X53 actualizado (X54), guía, seguridad.
