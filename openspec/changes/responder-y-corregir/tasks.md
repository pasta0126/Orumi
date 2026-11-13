## 1. Núcleo

- [x] 1.1 Analizar valores por campo (fecha, hora, duración, listas, entidades, texto) con mensajes de error claros
- [x] 1.2 Operación «corregir»: aplicar el valor, certeza confirmada e historial de origen usuario, validar y escribir de forma atómica
- [x] 1.3 Operación «rechazar»: valor a rechazos, campo desconocido, historial
- [x] 1.4 Operación «responder»: aplicar al registro, evidencia en la pregunta, cierre, y los cierres «no lo recuerdo» y «no relevante»
- [x] 1.5 Conflictos (resolver con registro destino) y coincidencias dudosas (`distinta` y `misma`)
- [x] 1.6 Pruebas: cada campo y cada escenario de la spec; valores inválidos, identificadores inexistentes, pregunta ya cerrada, protección ante el reprocesado de Mira

## 2. Línea de comandos

- [x] 2.1 Comandos `responder`, `corregir` y `rechazar` con exclusión mutua con Mira y códigos de salida
- [x] 2.2 `consultar pendientes` con contexto (registro, campo, valor actual, registros implicados)
- [x] 2.3 Pruebas de los comandos y del contexto

## 3. Documentación y despliegue

- [x] 3.1 Documentar en `docs/formato-de-ficheros.md` (conflicto resuelto, historial de origen usuario) y `docs/agente-mira.md`
- [x] 3.2 Actualizar la guía de notas (responder, corregir, rechazar) y `docs/decisiones.md`
- [ ] 3.3 Reconstruir la imagen en el servidor y probar con datos ficticios, incluido que un reprocesado de Mira respeta lo corregido
- [ ] 3.4 Actualizar `infra` (README y ROADMAP)
