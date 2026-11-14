## Context

La web de captura (`orumi-web`) tiene un botón de dictado basado en `SpeechRecognition` (X53), con aviso de privacidad, estilos de halo y ondas (X57) y un icono propio. El usuario confirma que no funciona y que no lo necesita. `iconos.js`, `ui.css` y `tema.js` son idénticos entre captura y panel y una prueba lo exige (X57).

## Goals / Non-Goals

**Goals:** retirar el dictado por completo (código, estilos, icono, aviso, campo de la API, documentación) sin tocar el resto.

**Non-Goals:** transcripción propia (Whisper), cambios en el formato de capturas (el origen `voz` sigue válido), cambios en login, cola sin conexión o diseño.

## Decisions

1. **Eliminación completa, no ocultación.** Código muerto y un aviso de privacidad sobre algo inexistente serían ruido. Reversible por git si algún día se retoma.
2. **El servidor ignora `voz` en lugar de rechazarlo.** Una página antigua en la caché del service worker puede seguir enviándolo; ignorarlo evita perder notas. La captura se crea siempre con origen `texto`.
3. **Icono `microfono` fuera de ambas copias de `iconos.js`**, de modo que la prueba de igualdad sigue verde. Se comprueba que nadie más lo use.
4. **Versión de la caché del service worker**: se sube para que los dispositivos descarguen la página sin el botón.

## Risks / Trade-offs

- Una nota ya guardada en cola en el dispositivo con `voz: true` → se envía igualmente y queda como `texto` (la voz solo era dictado del navegador; el texto lo escribió o revisó el usuario). Aceptable.
- Perder el dictado en navegadores de escritorio: aceptado por el propietario.
