## Context

Las specs vigentes definen el comportamiento: `captura-de-texto`, `validacion-e-indice`, `ejecucion-y-despliegue`, `configuracion-del-proveedor-ia`, `procesamiento-de-capturas`, `preguntas-de-mira` y `privacidad-del-procesamiento`, además del modelo de registro. El stack está decidido (`docs/tecnologias.md`, X39–X42): .NET, PostgreSQL con base y rol propios, contenedor en ARM64, proveedor Anthropic (Haiku 5.5 por defecto) y contratación aplazada. Los ejemplos de `docs/ejemplos/` y los casos de `docs/evaluacion-mira.md` sirven como pruebas. Un script desechable en Ruby validó los ejemplos; sus reglas, documentadas en `docs/agente-mira.md` §5, son la referencia para la validación en C#. Disposición del servidor y revisión de seguridad en `docs/privado/despliegue.md` (fuera de git).

## Goals / Non-Goals

**Goals:**
- Un MVP que capture, valide, indexe y procese, todo probado sin gastar en el proveedor de IA.
- Lectura y escritura de ficheros fiel al formato, sin perder información y sin estados intermedios visibles.
- Cifrado en reposo con fallo cerrado.

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
8. **Cifrado: volumen LUKS respaldado por fichero y montado por bucle para el directorio de datos**, no cifrado de todo el disco. *Motivo:* no toca los demás servicios del servidor, no exige reformatear ni parar nada y aísla el volumen. *Algoritmo:* se mide en el servidor con el benchmark de `cryptsetup` (AES-XTS frente a Adiantum, este último habitual en procesadores sin aceleración AES); el volumen es de ficheros pequeños (~10 al día), de modo que el impacto de rendimiento es insignificante con cualquiera. *Alternativas:* cifrado a nivel de directorio (depende del sistema de ficheros y del kernel), cifrado de aplicación (haría los ficheros ilegibles sin Mira, contra el principio de acceso independiente del agente).
9. **Desbloqueo manual tras cada reinicio.** La frase se introduce por SSH; hasta entonces las herramientas no escriben ni procesan y las capturas quedan pendientes. Se prefiere esto a guardar la clave en el propio servidor, que anularía la protección ante la extracción del almacenamiento. Mira comprueba que el volumen está montado y es distinto del punto de montaje vacío antes de cualquier operación de datos.
10. **Clave recuperable fuera del servidor.** Sin respaldo (X27), perder la frase implica perder todos los datos. La frase y una copia de la cabecera de LUKS (cuya pérdida también inutiliza el volumen) las guarda el usuario en su gestor de contraseñas.
11. **Contenedor ARM64, no raíz, sin puertos publicados**, con el volumen de datos y la configuración montados y el sistema de ficheros del resto en solo lectura si es viable.
12. **Cambios en el servidor con confirmación explícita del usuario en cada paso** (usuario, volumen cifrado, base de datos, temporizador). El detalle vive en `docs/privado/despliegue.md`.
13. **Sin gasto hasta contratar.** `procesar` con el cliente real queda desactivado por la comprobación de configuración (garantía y clave ausentes) hasta que el usuario lo habilite.

## Risks / Trade-offs

- **Perder la frase de cifrado destruye los datos** → frase y cabecera en el gestor de contraseñas del usuario; sin esa copia, no se cifra.
- **Tras un reinicio, el lote no corre hasta desbloquear** → fallo cerrado y visible (capturas pendientes); es el precio de no guardar la clave en el servidor.
- **Escribir en el punto de montaje vacío con el volumen bloqueado** → comprobación de montaje antes de toda operación de datos y prueba específica.
- **Un serializador YAML genérico alteraría el formato** → escritura propia con relectura y validación.
- **Divergencia entre la validación en C# y las reglas del contrato** → pruebas con los ejemplos y casos negativos; el contrato manda.
- **El cliente real nunca se ejercita antes de contratar** → se aísla tras la interfaz y se verifica en la primera ejecución real revisada por el usuario.
- **Rendimiento del cifrado en una Pi 4** → medido con el benchmark; con este volumen no es relevante.
