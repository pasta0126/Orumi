## 1. Detección de saldo agotado y clave inválida

- [x] 1.1 Clasificar los errores del proveedor (402 o `billing_error`, mensaje de saldo insuficiente, límite de gasto, 401 o `authentication_error`) y traducir las excepciones del SDK de Anthropic
- [x] 1.2 Detener la ejecución ante saldo agotado o clave inválida: capturas pendientes, problema claro en el lote y códigos de salida 7 y 8
- [x] 1.3 Pruebas con proveedor simulado: saldo agotado, clave inválida, otro error, reanudación tras recargar

## 2. Uso y presupuesto

- [x] 2.1 Leer el uso (tokens) de la respuesta del proveedor y guardarlo con el coste estimado en la entrada del lote; precios en la configuración
- [x] 2.2 Presupuesto (`saldo_usd`, `desde`, `aviso_porcentaje`): gasto estimado desde esa fecha y avisos al alcanzar y al superar el umbral
- [x] 2.3 Pruebas: coste con y sin precios, aviso al 90 %, aviso al superar el saldo sin detener

## 3. Visibilidad

- [x] 3.1 Comando `estado`: pendientes, último lote, gasto estimado frente al presupuesto y avisos vigentes
- [x] 3.2 Aviso breve en `capturar` y `validar`, sin bloquear; se apaga cuando un lote posterior termina sin problemas de saldo
- [x] 3.3 Pruebas: aviso al capturar, aviso resuelto, estado completo; los avisos no contienen contenido de notas ni la clave

## 4. Documentación y despliegue

- [x] 4.1 Documentar los campos nuevos del lote en `docs/formato-de-ficheros.md` y el comportamiento en `docs/agente-mira.md`
- [x] 4.2 Configurar el presupuesto (5 US$ desde 2026-11-13, aviso al 90 %) y los precios de Haiku 5.5 en `orumi.yaml` del servidor, reconstruir la imagen y probar `estado`
- [x] 4.3 Actualizar `infra` (README, ROADMAP, `security/orumi.md`) y `docs/decisiones.md`
