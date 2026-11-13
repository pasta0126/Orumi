## Context

Las specs vigentes definen el comportamiento: `captura-de-texto`, `validacion-e-indice`, `ejecucion-y-despliegue`, `configuracion-del-proveedor-ia`, `procesamiento-de-capturas`, `preguntas-de-mira` y `privacidad-del-procesamiento`, además del modelo de registro. El stack está decidido (`docs/tecnologias.md`, X39–X42): .NET, PostgreSQL con base y rol propios, contenedor en ARM64, proveedor Anthropic (Haiku 5.5 por defecto) y contratación aplazada. Los ejemplos de `docs/ejemplos/` y los casos de `docs/evaluacion-mira.md` sirven como pruebas. Un script desechable en Ruby validó los ejemplos; sus reglas, documentadas en `docs/agente-mira.md` §5, son la referencia para la validación en C#. Disposición del servidor y revisión de seguridad en `docs/privado/despliegue.md` (fuera de git).

## Goals / Non-Goals

**Goals:**
- Un MVP que capture, valide, indexe y procese, todo probado sin gastar en el proveedor de IA.
- Lectura y escritura de ficheros fiel al formato, sin perder información y sin estados intermedios visibles.
- Un despliegue sencillo, con la opción de cifrar más adelante.

**Non-Goals:**
- Voz, API de entrada, app y panel.
- Contratar el servicio de IA o ejercitar el cliente real.
- Detección de patrones e informes.
- Un respaldo (X27).

## Decisions

1. **Orden de construcción por capas, de menos a más dependiente del exterior:** `Orumi.Core` (modelo, lectura y escritura de ficheros, huella, validación) → `capturar` → `validar` → `reindexar` → `procesar` con cliente simulado → cliente real (sin ejercitar). Cada capa se prueba antes de pasar a la siguiente.
2. **Pruebas con los ejemplos como datos.** `docs/ejemplos/orumi/` debe validar sin errores, y se parte de los casos negativos usados en la comprobación (cuerpo alterado, cita incorrecta, conflicto no simétrico, hora aproximada sin valor, tipo de entidad inválido). Las reglas son las de `docs/agente-mira.md` §5.
3. **Escritura de YAML propia y determinista, lectura con una biblioteca YAML.** El formato exige convenciones concretas (mapas en línea, textos con `:` entre comillas, instantes entre comillas dentro de `{}`) que un serializador genérico no garantiza. Se escribe con plantillas controladas y se vuelve a leer y validar lo escrito antes de darlo por bueno.
4. **Escritura atómica.** Se escribe a un fichero temporal en el mismo directorio, se sincroniza, se renombra y se relee para verificar contenido y huella; las capturas se crean sin sobrescribir (creación exclusiva). Ante un fallo no queda un fichero a medias dado por bueno.
5. **Exclusión mutua con un fichero de bloqueo** en el directorio de datos, con detección de bloqueos obsoletos de ejecuciones muertas.
6. **Interfaz de proveedor de IA** con dos implementaciones: simulada (respuestas fijas por caso, para pruebas) y Anthropic (SDK oficial). El contrato de entrada y salida es el de `docs/agente-mira.md` §7.1; la salida es estructurada y se valida antes de escribir. El cliente no envía parámetros de muestreo (X39).
7. **Índice en PostgreSQL** con el esquema de `docs/indice-derivado.md`. Se reconstruye en un esquema nuevo y se sustituye al final en una transacción, de modo que un fallo conserva el índice vigente. Se usa un controlador de Postgres para .NET.
8. **Sin cifrado en reposo por defecto (X45).** Se probó un volumen LUKS2 de fichero con Adiantum en el servidor (la CPU de la Raspberry Pi 4 no tiene aceleración AES; medido: AES-XTS 53/71 MiB/s frente a Adiantum 204/220 MiB/s) y se descartó: sin respaldo, con una frase débil o perdida el cifrado no protege o destruye los datos, exige desbloqueo manual tras cada reinicio y el resto del disco del servidor sigue sin cifrar. Los datos van en un directorio de permisos restringidos (700).
9. **Exigencia de volumen opcional.** La herramienta conserva la comprobación de un marcador (`volumen.exigir` o `ORUMI_EXIGIR_VOLUMEN`), desactivada por defecto, para el caso de que se cifre más adelante: sin el volumen, no escribe ni procesa y no crea ficheros en el punto de montaje vacío.
10. **Si algún día se cifra:** la frase y la cabecera LUKS deben guardarse fuera del servidor, porque sin respaldo su pérdida es irreversible.
11. **Contenedor ARM64, no raíz, sin puertos publicados**, con el volumen de datos y la configuración montados y el sistema de ficheros del resto en solo lectura si es viable.
12. **Cambios en el servidor con confirmación explícita del usuario en cada paso** (usuario, volumen cifrado, base de datos, temporizador). El detalle vive en `docs/privado/despliegue.md`.
13. **Sin gasto hasta contratar.** `procesar` con el cliente real queda desactivado por la comprobación de configuración (garantía y clave ausentes) hasta que el usuario lo habilite.

## Risks / Trade-offs

- **Datos sin cifrar en reposo:** si se roba o se desecha el almacenamiento, quedan legibles; riesgo aceptado (X45), con permisos 700 y sin servicio expuesto.
- **Un serializador YAML genérico alteraría el formato** → escritura propia con relectura y validación.
- **Divergencia entre la validación en C# y las reglas del contrato** → pruebas con los ejemplos y casos negativos; el contrato manda.
- **El cliente real nunca se ejercita antes de contratar** → se aísla tras la interfaz y se verifica en la primera ejecución real revisada por el usuario.
