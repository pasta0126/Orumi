## 1. Formato y estado

- [x] 1.1 Definir el fichero `envio` (campos, ubicación `envios/AAAA/MM/`, estados abierto y recogido) en `docs/formato-de-ficheros.md`
- [x] 1.2 Leer, escribir y validar ficheros `envio` (referencias a capturas, identificador de lote del proveedor, ofrecidos por captura)
- [x] 1.3 Pruebas del formato y de la validación

## 2. Proveedor por lotes

- [x] 2.1 Definir `IProveedorPorLotes` (enviar, consultar, recoger) y el modelo de resultado (correcto, error, cancelado, expirado, con uso)
- [x] 2.2 Implementar el proveedor simulado por lotes para las pruebas
- [x] 2.3 Implementar el adaptador de Anthropic con el SDK oficial (crear, consultar y leer resultados; clasificar errores de saldo, límite y clave)

## 3. Procesamiento en dos fases

- [x] 3.1 Recoger envíos abiertos al empezar y aplicar cada resultado con la tubería existente (validación, claves, protegidos, preguntas, uso)
- [x] 3.2 Enviar las pendientes que no estén en un envío abierto y registrar el envío tras crear el lote
- [x] 3.3 Esperar hasta el máximo configurable, dejar abierto lo no terminado y retomarlo en la siguiente ejecución
- [x] 3.4 Modo inmediato (`--inmediato`) y configuración `procesamiento` (modo y espera máxima); factor de lote en el coste
- [x] 3.5 Pruebas con proveedor simulado: envío, recogida, sin duplicar, interrupción tras enviar, error, cancelado, expirado, salida no válida, saldo agotado al enviar, modo inmediato

## 4. Estado y despliegue

- [x] 4.1 `orumi estado` muestra los envíos abiertos y el modo
- [ ] 4.2 Unidad systemd con tiempo máximo de servicio; configuración del servidor (`procesamiento`, `factor_lote`)
- [ ] 4.3 Prueba real mínima con una captura ficticia (modo lotes) y comprobación del coste

## 5. Cierre

- [ ] 5.1 Documentar en `docs/agente-mira.md`, `docs/decisiones.md`, `infra` (README, ROADMAP, `security/orumi.md` con la retención de 29 días) y la guía de notas
