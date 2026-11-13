## 1. Comparación de proveedores

- [x] 1.1 Recoger de fuentes oficiales, con fecha, los precios, las condiciones de uso de datos, los avisos de retirada y los datos de fiabilidad de los candidatos (Gemini y Claude)
- [x] 1.2 Documentar en `docs/tecnologias.md` la evaluación ponderada, la sensibilidad, los límites y las fuentes; sin cifras sin fuente oficial
- [x] 1.3 Estimar el coste mensual con el volumen supuesto (X9)

## 2. Decisión del proveedor

- [x] 2.1 Elegir proveedor y modelo por evaluación ponderada: Anthropic, Claude Haiku 5.5 por defecto, configurable a Sonnet 5.5 (la contratación y la configuración de la garantía se aplazan hasta necesitar la API; ver `docs/tecnologias.md` §7)

## 3. Decisiones de stack

- [x] 3.1 Confirmar o ajustar lenguaje (.NET), índice (PostgreSQL con base y rol propios), forma de ejecución y planificador, con alternativas y motivos, en `docs/tecnologias.md`
- [x] 3.2 Definir el esquema del fichero de configuración (proveedor, modelo, garantía con fecha y fuente, máximo de preguntas, ventana, horario)
- [x] 3.3 Definir la estructura del repositorio de código (carpetas) y de los directorios en el servidor, sin incluir datos de infraestructura

## 4. Servidor y seguridad

- [x] 4.1 Definir en `docs/privado/` el despliegue: usuario, permisos de `orumi/` y de los secretos, temporizador y volumen del contenedor
- [x] 4.2 Revisar los huecos de seguridad del servidor relevantes para datos reales (acceso SSH, 2FA, permisos) y anotar lo que haya que corregir antes de subirlos

## 5. Cierre

- [x] 5.1 Registrar en `docs/decisiones.md` las decisiones (X39 y siguientes)
- [x] 5.1b Actualizar el estado en `AGENTS.md`
- [x] 5.2 Anotar la change siguiente (`mvp-mira`) y su alcance
