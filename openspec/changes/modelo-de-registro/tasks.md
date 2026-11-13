## 1. Definición del formato

- [x] 1.1 Redactar la especificación de campos de `captura` (identificador, fechas, origen) con ejemplo completo
- [x] 1.2 Redactar la especificación de campos de `registro` (tipos, fechas imprecisas, relaciones, certeza por dato, procedencia, historial) con ejemplos
- [x] 1.3 Fijar el formato de identificadores y la clave de extracción para reprocesado idempotente, respetando X30 (no pisar lo aportado por el usuario)
- [x] 1.4 Definir la estructura de directorios de `orumi/` (capturas, registros, informes, preguntas pendientes)

## 2. Ejemplos y validación

- [x] 2.1 Crear un conjunto de ejemplos ficticios (captura con varios registros, dato desconocido, corrección, rechazo)
- [x] 2.1b Añadir ejemplos de contradicción, evento multi-día abierto/cerrado y fecha imprecisa con rango
- [x] 2.1c Añadir ejemplos de captura multitema con fragmentos de origen, de metadatos opcionales y de aportación a un registro existente
- [x] 2.2 Revisar los ejemplos contra cada escenario de las specs
- [x] 2.3 ~~Probar la lectura de los ejemplos con Atlas~~ Descartada: ORUMI no se integra con otro software (X34); los ejemplos se leen como texto y los validó el script de 2.2

## 3. Índice y respaldo

- [x] 3.1 Definir el esquema lógico del índice derivado (campos consultables) sin fijar motor
- [x] 3.2 Respaldo de `orumi/`: decidido que no hay (X27); riesgo documentado en design.md
- [x] 3.3 Documentar el procedimiento de regeneración del índice

## 4. Cierre

- [x] 4.1 Actualizar `docs/decisiones.md` con las decisiones cerradas
