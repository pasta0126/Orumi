## Purpose

Dar a las interfaces web de ORUMI un aspecto cuidado y coherente, con movimiento con sentido y sin sacrificar accesibilidad, rendimiento ni seguridad.

## ADDED Requirements

### Requirement: Iconografía sin emojis
Las interfaces MUST usar iconos vectoriales propios con un trazo coherente y MUST NOT contener emojis ni emoticonos en ningún fichero de la interfaz.

#### Scenario: Revisión de ficheros
- **WHEN** se examinan los ficheros HTML, CSS y JavaScript de las interfaces
- **THEN** ninguno contiene caracteres de emoji ni emoticonos

### Requirement: Sistema de diseño compartido
El panel y la captura MUST compartir los mismos tokens de color, tipografía, componentes e iconos, de modo que los ficheros compartidos sean idénticos en ambas interfaces.

#### Scenario: Copias idénticas
- **WHEN** se comparan los ficheros compartidos del sistema de diseño
- **THEN** el contenido de cada uno es idéntico en el panel y en la captura

### Requirement: Temas claro y oscuro
Las interfaces MUST ofrecer tema claro y oscuro, MUST seguir la preferencia del sistema por defecto, MUST permitir elegir uno manualmente y recordar la elección, y MUST mantener un contraste suficiente de texto en ambos.

#### Scenario: Preferencia del sistema
- **WHEN** el usuario no ha elegido tema
- **THEN** se aplica el tema del sistema

#### Scenario: Elección manual
- **WHEN** el usuario elige un tema
- **THEN** se aplica de inmediato y se recuerda en visitas posteriores

### Requirement: Movimiento respetuoso
Las animaciones MUST aportar información o continuidad (entrada de vistas y listas, estados de carga, confirmaciones), MUST usar solo propiedades que no provoquen reflujo de diseño y MUST desactivarse por completo cuando el usuario prefiere movimiento reducido.

#### Scenario: Movimiento reducido
- **WHEN** el sistema indica preferencia por movimiento reducido
- **THEN** no se ejecuta ninguna animación ni transición decorativa y la interfaz sigue siendo completamente usable

### Requirement: Cumplimiento de la política de seguridad de contenido
La interfaz MUST funcionar con la política de contenido vigente: sin scripts ni estilos en línea, sin recursos de terceros y sin tipografías remotas.

#### Scenario: Ficheros de la interfaz
- **WHEN** se examinan los HTML de las interfaces
- **THEN** no contienen atributos de estilo en línea, bloques de estilo ni scripts en línea, y no cargan recursos externos

### Requirement: Accesibilidad básica
Toda acción MUST ser alcanzable y operable con teclado con foco visible, los iconos meramente decorativos MUST ocultarse a los lectores de pantalla, los iconos con significado MUST tener nombre accesible, y los controles táctiles MUST tener un tamaño mínimo cómodo.

#### Scenario: Navegación con teclado
- **WHEN** el usuario navega con el teclado
- **THEN** cada control muestra un indicador de foco visible
