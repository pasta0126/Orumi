## Why

El panel y la captura funcionan, pero su aspecto es el de un prototipo. ORUMI es una memoria personal que se usa a diario, y su interfaz debe transmitir calma, cuidado y confianza: un lenguaje visual propio, movimiento con sentido, iconos dibujados con criterio y detalles pulidos.

## What Changes

- **Sistema de diseño compartido** (`ui.css`, `iconos.js`, `tema.js`) usado por el panel y la captura: tokens de color y tipografía, tema oscuro y claro (automático y con selector), fondo «aurora» con degradados difuminados y movimiento lento, superficies de cristal translúcido con desenfoque, foco luminoso que sigue al puntero, transiciones entre vistas, entrada escalonada de listas, esqueletos de carga con brillo, avisos animados e indicador deslizante en la navegación.
- **Iconografía propia en SVG** (trazo uniforme, esquinas redondeadas) para navegación, clases de registro, certezas y campos. **Sin emojis ni emoticonos en toda la interfaz.**
- **Panel rediseñado:** pantalla de acceso con logotipo animado; día con tira semanal con recuento, línea de tiempo y tarjetas por clase; búsqueda con campo destacado y filtros plegables; preguntas como tarjetas con respuestas rápidas; ficha con jerarquía clara, citas de origen destacadas, historial como línea de tiempo y edición en línea.
- **Captura rediseñada:** lienzo de escritura con cristal, botón de dictado con ondas animadas mientras escucha, envío con degradado y confirmación animada.
- **Accesibilidad y rendimiento:** contraste suficiente en ambos temas, respeto de `prefers-reduced-motion` (sin animaciones), foco visible, tamaños táctiles, animaciones solo con `transform` y `opacity`, sin dependencias externas ni tipografías remotas, y cumpliendo la CSP vigente (sin estilos ni scripts en línea).
- Corrige el recuadro de foco que rodea todo el contenido tras navegar.
- Fuera de alcance: nuevas funciones, ilustraciones externas, bibliotecas de animación.

## Capabilities

### New Capabilities
- `diseno-del-front`: lenguaje visual, iconografía, movimiento y accesibilidad de las interfaces web de ORUMI.

### Modified Capabilities
<!-- Ninguna: no cambia el comportamiento funcional. -->

## Impact

- `src/Orumi.Panel/wwwroot` y `src/Orumi.Web/wwwroot` (HTML, CSS y JS). Sin cambios en los servicios .NET salvo una prueba.
- Despliegue: reconstruir las dos imágenes.
