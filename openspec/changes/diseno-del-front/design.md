## Context

Las dos interfaces son HTML, CSS y JS sin dependencias, servidas con CSP `default-src 'self'; script-src 'self'; style-src 'self'` (sin estilos en línea). No se pueden usar CDN ni tipografías remotas. Los textos de usuario siempre se insertan como texto.

## Decisions

1. **Lenguaje visual «aurora nocturna»:** fondo oscuro profundo con tres manchas difuminadas (índigo, turquesa y un toque de rosa) que derivan despacio; superficies de cristal translúcido con `backdrop-filter`; acento en degradado índigo→turquesa; tema claro con la misma estructura en tonos pastel. Cada certeza tiene su color (dicho/confirmado turquesa, interpretado ámbar, hipótesis violeta, recomendación azul) y el rosa se reserva para conflictos y errores.
2. **Tipografía:** pila del sistema afinada (pesos, espaciado, cifras tabulares); sin fuentes remotas ni ficheros pesados.
3. **Iconos propios** como primitivas SVG (trazo de 1,75 sobre rejilla 24, extremos redondeados) definidas en `iconos.js` y creadas con `createElementNS` (sin `innerHTML`). Mismo trazo para navegación, clases, certezas y campos. El logotipo son anillos concéntricos con un núcleo, que giran muy despacio.
4. **Movimiento:** solo `transform` y `opacity` (más `filter` en las manchas del fondo, que es una capa fija). Entrada de vista (`subir` con desenfoque inicial), entrada escalonada de listas con la variable `--i` (se asigna con `style.setProperty`, que la CSP permite), esqueletos con brillo, foco luminoso que sigue al puntero (`--mx`/`--my` con `requestAnimationFrame`), indicador deslizante en la navegación, avisos que suben, contadores que cuentan, ondas del micrófono mientras se dicta. `prefers-reduced-motion` lo apaga todo.
5. **Compartir sin duplicar a mano:** `ui.css`, `iconos.js` y `tema.js` son los mismos ficheros en ambos proyectos y una prueba exige que sean idénticos. Cada interfaz añade su CSS y JS propios.
6. **Tema:** `tema.js` (cargado en `<head>`) aplica `data-tema` antes del primer pintado según la elección guardada o la preferencia del sistema; el selector alterna y guarda.
7. **Pruebas de reglas verificables:** exploración de los ficheros de la interfaz para comprobar que no hay emojis, estilos o scripts en línea ni recursos externos; igualdad de los ficheros compartidos.
8. **Verificación visual** con Chrome en modo headless contra el panel real con datos de ejemplo (escritorio y móvil, oscuro y claro).

## Risks / Trade-offs

- **`backdrop-filter` en equipos modestos** → se usa en pocas superficies y con valores moderados, y hay un fondo sólido de reserva donde no está disponible.
- **Más CSS que mantener** → un sistema pequeño y compartido.
- **No se ve el movimiento en capturas estáticas** → se revisa el resultado final y el código de animación con cuidado; las animaciones son simples y acotadas.
