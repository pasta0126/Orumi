## 1. Definición del formato

- [ ] 1.1 Redactar la especificación de campos de `captura` (identificador, fechas, origen) con ejemplo completo
- [ ] 1.2 Redactar la especificación de campos de `registro` (tipos, fechas imprecisas, relaciones, certeza por dato, procedencia, historial) con ejemplos
- [ ] 1.3 Fijar el formato de identificadores y la clave de extracción para reprocesado idempotente
- [ ] 1.4 Definir la estructura de directorios de `orumi/` (capturas, registros, informes, preguntas pendientes)

## 2. Ejemplos y validación

- [ ] 2.1 Crear un conjunto de ejemplos ficticios (captura con varios registros, dato desconocido, corrección, rechazo)
- [ ] 2.1b Añadir ejemplos de contradicción, evento multi-día abierto/cerrado y fecha imprecisa con rango
- [ ] 2.1c Añadir ejemplos de captura multitema con fragmentos de origen, de metadatos opcionales y de aportación a un registro existente
- [ ] 2.2 Revisar los ejemplos contra cada escenario de las specs
- [ ] 2.3 Probar a mano la lectura de los ejemplos con Atlas (frontmatter y wikilinks)

## 3. Índice y respaldo

- [ ] 3.1 Definir el esquema lógico del índice derivado (campos consultables) sin fijar motor
- [ ] 3.2 Definir la estrategia de respaldo de `orumi/` (sin vedlvm) o registrarla como dependencia de otra change
- [ ] 3.3 Documentar el procedimiento de regeneración del índice

## 4. Cierre

- [ ] 4.1 Actualizar `docs/decisiones.md` con las decisiones cerradas
