## Context

La IA está contratada con créditos prepago de 5 US$ y sin recarga automática (X47). El lote nocturno está activo (X48) y, hasta ahora, un fallo del proveedor aparece solo como «pendiente» en el lote. La documentación oficial de la API indica que los problemas de facturación se devuelven como `billing_error` (402), y que otros límites de gasto devuelven 400 o 429; el SDK de C# no tiene una excepción específica para 402. Con una cuenta individual no hay API para consultar el saldo real: el uso y el coste solo están en la API de administración, que requiere una organización y una clave de administrador y que, además, no devuelve el saldo restante. Las respuestas del Messages incluyen los tokens exactos de entrada y salida.

## Goals / Non-Goals

**Goals:**
- Que un saldo agotado o una clave inválida sean evidentes y no se confundan con un fallo puntual.
- Un aviso previo útil aunque sea estimado.
- Reanudar sola tras recargar.

**Non-Goals:**
- Notificar por correo, móvil o panel.
- Conocer el saldo real del proveedor.
- Cambiar de modelo ni limitar el gasto por estimación.

## Decisions

1. **Clasificación de errores independiente del proveedor.** Una función que recibe estado HTTP, tipo, código y mensaje del error y devuelve `SaldoAgotado`, `LimiteDeGasto`, `ClaveInvalida` u `Otro`: 402 o `billing_error`, o un mensaje que mencione saldo o créditos insuficientes, es saldo agotado; el código `enforced_spend_limit_reached` (429) o el mensaje «You have reached your specified API usage limits» (400) es límite de gasto; 401 o `authentication_error` es clave inválida. El adaptador de Anthropic traduce las excepciones del SDK a esa clasificación. Como no se puede provocar un saldo agotado real sin gastar todo el saldo, la detección se prueba con errores simulados y se vigila en producción. *Alternativa descartada:* detectar solo por el texto del mensaje; es frágil.
2. **Las categorías fatales detienen la ejecución.** Si el saldo se ha agotado o la clave es inválida, seguir enviando el resto de capturas fallaría igual y gastaría tiempo; se corta, se deja todo pendiente y se escribe el lote con el problema. Código de salida 7 (saldo) y 8 (clave); el servicio de systemd queda como fallido, otra señal visible.
3. **Reanudación sin estado extra.** La captura que no está en ningún lote sigue pendiente; la siguiente ejecución la procesa. No hay «modo parado» persistente.
4. **Uso en el lote.** La respuesta del proveedor incluye el uso (tokens de entrada y salida); se guarda por captura en la entrada del lote (`tokens_entrada`, `tokens_salida`, `coste_estimado_usd`). Campos opcionales: los lotes anteriores siguen siendo válidos.
5. **Precios en la configuración, no en el código** (`precios.entrada_por_millon_usd`, `precios.salida_por_millon_usd`), tomados de las fuentes oficiales (Haiku 5.5: 0,10 y 0,50 US$). Sin precios, coste desconocido. Es una estimación: no cuenta posibles costes de caché ni de pensamiento adaptativo más allá de los tokens que informa la API.
6. **Presupuesto manual y visible.** `presupuesto.saldo_usd`, `presupuesto.desde` y `presupuesto.aviso_porcentaje` (por defecto **90**, a petición del usuario). Como solo hay una clave, la estimación por tokens es casi exacta; para calibrarla, `estado` muestra la estimación y el usuario puede compararla con la página de coste de la consola. Tras recargar, el usuario actualiza `saldo_usd` y `desde` (o suma al saldo). El gasto estimado se calcula sumando los lotes desde esa fecha. *Alternativa descartada:* un comando `recarga` que reescriba la configuración; añade escritura de configuración sin necesidad.
7. **Visibilidad donde mira el usuario.** El comando `estado` y un aviso de una o dos líneas en `capturar` y `validar` (en la salida de error, sin bloquear). Los avisos se derivan del último lote y de los lotes desde la fecha del presupuesto, sin ficheros de estado propios. No contienen contenido de notas ni la clave.
8. **Fuera de alcance pero anotado:** avisar por correo o móvil cuando exista el panel o la app.

## Risks / Trade-offs

- **La detección de saldo agotado no se ha podido probar con el proveedor real** → la clasificación cubre 402, el tipo `billing_error` y el texto, y queda anotado para verificarlo la primera vez que ocurra; un error no reconocido sigue dejando la captura pendiente y registrado en el lote.
- **La estimación puede diferir del saldo real** (otros proyectos usan el mismo saldo, caché, pensamiento) → solo avisa y nunca detiene; el aviso se explica como estimación.
- **Otros consumos del mismo saldo:** el *playground* de la consola y Claude Code también gastan de los créditos y Mira no los ve → el aviso puede quedarse corto; se anota y se sugiere calibrar con la consola. Hoy solo existe la clave de ORUMI.
- **Alternativa descartada:** la API de uso y coste con clave de administrador; exige crear una organización y guardar en el servidor una clave con poder sobre claves y miembros, solo para afinar un aviso.
- **Si el usuario no ejecuta nada**, el aviso no se ve → el lote fallido deja el servicio de systemd en estado fallido y `estado` lo muestra cuando se consulte.
