## 1. Núcleo

- [x] 1.1 Crear la solución en .NET con `Orumi.Core`, `Orumi.Agente`, `Orumi.Indice`, `Orumi.Cli` y `tests/`
- [x] 1.2 Implementar la lectura de ficheros (frontmatter YAML, cuerpo y wikilinks) y la huella SHA-256
- [x] 1.3 Implementar la escritura determinista de ficheros respetando las convenciones del formato, con escritura atómica y relectura
- [x] 1.4 Implementar las reglas de validación de `docs/agente-mira.md` §5
- [x] 1.5 Pruebas: `docs/ejemplos/orumi/` valida sin errores y cada caso negativo es detectado

## 2. Captura y validación

- [x] 2.1 Implementar `capturar` (texto por argumento, fichero o entrada estándar; metadatos opcionales; sin sobrescritura; confirmación tras verificar)
- [x] 2.2 Implementar `validar` (informe por fichero, sin modificar nada)
- [x] 2.3 Pruebas de los escenarios de `captura-de-texto` y de `validacion-e-indice` (validar)

## 3. Índice

- [ ] 3.1 Implementar el esquema de PostgreSQL de `docs/indice-derivado.md`
- [ ] 3.2 Implementar `reindexar` (esquema nuevo, informe, sustitución al final, fichero dañado omitido)
- [ ] 3.3 Implementar las consultas por fecha, hora, tipo, certeza, relación, proyecto, entidad y preguntas pendientes
- [ ] 3.4 Pruebas: los ejemplos regeneran 5 capturas, 7 registros, 4 preguntas y 5 lotes, con un conflicto abierto y una pregunta pendiente; el procesamiento funciona sin base de datos

## 4. Procesamiento

- [x] 4.1 Definir la interfaz del proveedor de IA y el contrato de entrada y salida (§7.1 del contrato)
- [x] 4.2 Implementar el cliente simulado con las respuestas esperadas de los casos de `docs/evaluacion-mira.md`
- [x] 4.3 Implementar `procesar`: selección de capturas, entrada mínima, extracción, validación previa, escritura, preguntas con máximo y valor, claves reclamadas, protegidos, aportaciones y conflictos, y lote con bitácora
- [x] 4.4 Implementar la comprobación de configuración y privacidad con fallo cerrado (garantía declarada, vigente y clave presente)
- [x] 4.5 Implementar la exclusión mutua y la recuperación tras interrupción
- [x] 4.6 Pruebas con el cliente simulado: C1–C12 de la evaluación revisadas contra sus resultados esperados
- [x] 4.7 Implementar el cliente real de Anthropic con el SDK oficial, sin ejercitarlo ni enviar nada

## 5. Contenedor y servidor

- [x] 5.1 Construir la imagen ARM64, sin puertos publicados y con usuario no raíz (construida en el servidor el 2026-11-13: `orumi:latest`, 344 MB)
- [ ] 5.2 (Con confirmación del usuario) Medir en el servidor el benchmark de `cryptsetup` y elegir el algoritmo del volumen cifrado (`crear-volumen.sh` lo mide; requiere sudo: lo ejecuta el usuario)
- [ ] 5.3 (Con confirmación del usuario) Crear el volumen cifrado, su montaje, la clave recuperable fuera del servidor y la copia de la cabecera (`sudo ~/infra/orumi/crear-volumen.sh`: lo ejecuta el usuario)
- [x] 5.4 Implementar la comprobación de montaje y las pruebas del fallo cerrado con el volumen bloqueado
- [x] 5.5 (Con confirmación del usuario) Directorios y permisos (700), base y rol de Postgres (tenant `orumi`) y unidades del temporizador escritas en `infra/orumi/` (sin instalar hasta contratar la IA). El usuario dedicado se descarta por ahora: pide sudo y queda anotado en `security/orumi.md`
- [ ] 5.6 Probar `capturar`, `validar` y `reindexar` en el servidor con los ejemplos ficticios (hechos `capturar`, `validar`, el fallo cerrado sin volumen y la parada por privacidad; falta `reindexar`, que depende de la sección 3)

## 6. Cierre

- [ ] 6.1 Actualizar `docs/decisiones.md` y `docs/privado/despliegue.md` con lo realizado
- [ ] 6.2 Anotar lo pendiente para la contratación de la IA y la primera ejecución real
