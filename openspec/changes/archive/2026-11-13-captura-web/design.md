## Context

Stack decidido: .NET, contenedores Docker en el servidor, Traefik con TLS en la red `proxy`, dominio `northernarchive.com`. `Orumi.Core` ya crea capturas (`CreadorDeCapturas`) y el formato admite `origen: voz`. Los navegadores solo permiten grabar con HTTPS, que Traefik ya da. Los datos viven en `~/orumi-datos`.

## Goals / Non-Goals

**Goals:** capturar desde Mac, PC y móvil con dictado, sin perder notas, de forma segura y con muy poco mantenimiento.
**Non-Goals:** audio guardado, transcripción propia, panel de lectura/edición, multiusuario.

## Decisions

1. **ASP.NET Core minimal API + página estática** en `src/Orumi.Web`, un solo contenedor. Sin framework de frontend: HTML, CSS y JS simples (una página), un `manifest` y un *service worker* mínimo para instalarla y funcionar sin red. *Alternativa:* Flutter/app nativa (descartada por coste de mantenimiento).
2. **Dictado con la API de reconocimiento de voz del navegador** (`es-ES`, resultados parciales en pantalla, texto editable antes de enviar). Coste cero, sin carga en la Raspberry Pi y sin guardar audio. *Coste:* en Chrome/Edge el audio va a Google y en Safari a Apple → decisión de privacidad explícita (X53), opcional y avisada. *Alternativa para más adelante:* Whisper propio (en la Pi es lento) o un servicio de transcripción (otro proveedor externo).
3. **Acceso por token largo aleatorio** (≥ 32 bytes) guardado en `~/orumi-config/orumi-web.env` (600); la página lo pide una vez y lo guarda en el dispositivo. Comparación en tiempo constante, límite de intentos por origen (p. ej. 5 fallos → bloqueo de 15 minutos). *Alternativas:* contraseña con sesión (más piezas), VPN (más fricción en el móvil); se puede añadir BasicAuth de Traefik como segunda capa.
4. **Idempotencia:** cada nota lleva un identificador de envío (UUID) generado en el dispositivo; el servidor lo guarda en un registro mínimo (`.envios-web/`, ignorado por los índices) y, si llega repetido, devuelve la misma captura sin crear otra.
5. **Cola sin conexión** en IndexedDB del navegador, con reintento al recuperar red; la nota solo se borra del dispositivo tras confirmar el servidor.
6. **Reutiliza `CreadorDeCapturas`** con `origen` texto/voz y `dispositivo` («web» + plataforma), de modo que las validaciones, la huella y la escritura atómica son las mismas. El contenedor corre con el usuario del host y escribe solo en `capturas/` (montaje de lectura y escritura; el resto del árbol, de solo lectura si Docker lo permite, o simplemente sin código que lo use).
7. **Sin exclusión con Mira:** las capturas nuevas son ficheros nuevos; Mira procesa solo lo que ya existe al arrancar.
8. **Despliegue** vía `infra`: servicio en el compose con etiquetas de Traefik, subdominio (p. ej. `orumi.northernarchive.com`, DNS a cargo del usuario), imagen `orumi-web:latest` construida en el servidor.

## Risks / Trade-offs

- **El audio sale a Google/Apple** (dictado) → opcional, avisado y registrado; escribir no envía nada fuera.
- **Una URL pública con datos personales** → solo crea capturas, no lee nada; token largo, límite de intentos y HTTPS. El peor caso de un token robado es que alguien añada notas falsas (se pueden eliminar a mano) y no que lea las existentes.
- **Calidad del dictado** variable → el texto se revisa antes de enviar y la captura conserva exactamente lo enviado.
- **Navegadores sin dictado** (Firefox) → se puede escribir.
- **Reintentos** → idempotencia con UUID.
