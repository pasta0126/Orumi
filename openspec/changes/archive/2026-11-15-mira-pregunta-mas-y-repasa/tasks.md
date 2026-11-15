## 1. Decisión y configuración

- [x] 1.1 Registrar en `docs/decisiones.md` la decisión que reinterpreta X36, X13 y X37 (pregunta estructural y de contenido, sin tope de pendientes por defecto, repaso nocturno y a petición, tope de creación por repaso) y actualizar `AGENTS.md`
- [x] 1.2 Configuración: `maximo_pendientes` opcional (ausente = sin máximo) y sección `repaso` (`activado`, `maximo_nuevas`, `registros_por_llamada`); mensajes y validación de configuración coherentes con «sin máximo»

## 2. Preguntar más al procesar

- [x] 2.1 Cambiar el *prompt* de extracción: preguntar todo dato estructural que falte y el contenido que la nota deja abierto, sin inventar ni sugerir respuesta; preguntas de contenido apuntan a un campo corregible
- [x] 2.2 `Procesador` sin tope cuando no hay máximo configurado, manteniendo el orden por valor, el cierre «por límite» cuando sí lo hay y la deduplicación de preguntas; ajustar la garantía de existencia de preguntas de conflicto
- [x] 2.3 Validación de las preguntas de contenido: campo corregible, registro existente, sin consejos ni valoraciones, sin repetir cerradas

## 3. Repaso de registros

- [x] 3.1 Capa determinista: campos estructurales desconocidos o dudosos por clase de registro, con plantillas de pregunta y valor; sin duplicar pendientes ni cerradas
- [x] 3.2 Capa con modelo: envío mínimo por lotes de registros con huecos, contrato de salida (`registro_id`, `campo`, `texto`, `valor`), lectura robusta (X55) y validador común; salida inválida no escribe nada
- [x] 3.3 Tope de creación por repaso, con elección por valor y el resto para el siguiente repaso
- [x] 3.4 Integración en el lote nocturno (tras procesar, bajo el bloqueo de Mira) y constancia `repaso` en la bitácora del lote; privacidad con fallo cerrado (solo capa determinista sin garantías) y control de saldo
- [x] 3.5 Comando `orumi repasar` (con `--simulado` para pruebas y `--solo-determinista`) y su salida
- [x] 3.6 Reabrir una sola vez las preguntas cerradas por límite antes de este cambio (cuestión abierta 2), solo si el usuario lo confirma

## 3b. Respuestas rápidas

- [x] 3b.1 Derivación de opciones en el contenido de la pregunta (sin campo nuevo en el fichero) y `Responder` para ciudad y contraste
- [x] 3b.2 Generación de opciones por el código: coincidencia (mismo, relacionados, distintos), conflicto (un valor por candidato), confirmaciones sí/no y estados; cada valor se valida con el analizador del campo y, si falla, la pregunta queda abierta
- [x] 3b.3 Responder con una opción: mismo efecto que la respuesta escrita; «Es el mismo» fusiona (destino el registro más antiguo, tumba reversible) y «Son distintos» no vuelve a preguntar por la pareja; mismo bloqueo, validación y auditoría
- [x] 3b.4 Terminal: `orumi responder ID --opcion N` (y mostrar las opciones numeradas en `consultar pendientes`)

## 4. Panel

- [x] 4.1 API: lanzar el repaso en segundo plano (409 si Mira está ocupada o ya hay uno) y ver el estado y el resultado del último; mismas guardias y límites que el resto de escrituras
- [x] 4.2 Botones de respuesta rápida en las tarjetas de pregunta (un clic, sin campo ni avisos de formato), cierres siempre presentes y «Deshacer» tras fusionar (ficha con instantánea de los ficheros, un minuto, sin pisar cambios); accesibles y con movimiento reducido
- [x] 4.3 Botón «Repasar ahora» y resumen del último repaso en la vista «Preguntas» (X57, sin emojis ni estilos en línea)

## 5. Pruebas

- [x] 5.1 Procesar: sin máximo se crean todas las preguntas válidas; con máximo se mantiene el comportamiento anterior; las preguntas de contenido se aceptan y las inválidas (consejos, campo inexistente) se descartan
- [x] 5.2 Repaso: capa determinista (campo desconocido → pregunta; sin huecos → nada), no repite pendientes ni cerradas, tope de creación y reparto entre noches, no modifica registros
- [x] 5.3 Privacidad y saldo: sin garantías solo capa determinista y aviso; sin presupuesto se omite el modelo; envío mínimo (sin clima ni texto de capturas)
- [x] 5.4 Panel y terminal: acceso denegado sin sesión, 409 con Mira ocupada, constancia del repaso
- [x] 5.5 Aprendizaje: lo cerrado «no relevante» baja la prioridad de la clase, no la prohíbe, también en las preguntas del repaso

- [x] 5.6 Respuestas rápidas: cada opción equivale a la respuesta escrita; opción inválida deja la pregunta abierta; fusionar y deshacer; coincidencia «distintos» no se repite; los tres cierres siempre presentes

## 6. Documentación y cierre

- [x] 6.1 Actualizar `docs/agente-mira.md`, `docs/formato-de-ficheros.md` (bloque `repaso` del lote y configuración) y README
- [x] 6.2 Desplegar vía `infra` y revisar con el usuario las primeras noches (volumen, tono y utilidad de las preguntas), ajustando *prompt* y tope — **Desplegado el 2026-11-15**; la revisión de las primeras noches queda en «Pendientes con fecha» de `docs/decisiones.md`
