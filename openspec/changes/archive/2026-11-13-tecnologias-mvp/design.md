## Context

Servidor propio pequeño (Raspberry Pi 4, aarch64, ~7,7 GB de RAM, ~88 GB libres) con Docker, proxy inverso y una instancia de PostgreSQL ya en uso por otros servicios (detalle en `docs/privado/infraestructura.md`, fuera de git). ORUMI es autónomo como software (X34), sin respaldo (X27), con ficheros como fuente de verdad (X3) y despliegue por SSH (X32). Volumen bajo (~10 capturas al día, X9). Mira tiene su contrato en `docs/agente-mira.md` y sus casos en `docs/evaluacion-mira.md`. La privacidad exige un proveedor que no reutilice los datos (X38). El MVP entra por ficheros, sin API ni app (X7, X6). El usuario prefiere no usar Node, quiere .NET para la API y le da igual el front; prefiere PostgreSQL compartido; y decidió que el proveedor de IA se elige con una evaluación ponderada con evidencias, sin pruebas reales, y que el que se pague es el que se usa. Evidencias y fuentes en `docs/tecnologias.md`.

## Goals / Non-Goals

**Goals:**
- Un stack mínimo, barato y mantenible por una persona.
- Proveedor y modelo elegidos con evidencias oficiales fechadas.
- Garantía de privacidad comprobable, no solo declarada.
- Un MVP que corra en el servidor actual sin servicios nuevos expuestos.

**Non-Goals:**
- Voz, STT, API de entrada, app y panel.
- Detección de patrones e informes.
- Compartir tablas o datos con otros proyectos.
- Probar proveedores con ejecuciones reales (decisión del usuario).

## Decisions

1. **Lenguaje: .NET (versión LTS vigente al implementar).** *Motivos:* SDK oficial de Anthropic (`Anthropic`, NuGet) y de Google (`Google.GenAI`); ecosistema maduro para PostgreSQL y YAML; sirve también para la API futura, que el usuario quiere en .NET. *Alternativas descartadas:* Node (preferencia del usuario); Rust (no hay SDK oficial de ninguno de los dos proveedores y sus ventajas de memoria y tamaño no importan en un proceso nocturno corto que espera a una API); Python y Go (sin ventaja sobre lo ya decidido).
2. **Índice: PostgreSQL, en la instancia compartida del servidor, con base de datos y rol propios.** Se reutiliza infraestructura (X32), no software de otros proyectos (X34); ninguna tabla compartida. *Alternativa descartada:* SQLite, por preferencia del usuario.
3. **El procesamiento no depende del índice.** `capturar`, `procesar` y `validar` funcionan solo con ficheros; Mira obtiene los registros candidatos leyendo los ficheros (el volumen lo permite). Solo `reindexar`, las consultas y el futuro panel usan Postgres. Si el índice falla, se regenera.
4. **Forma de ejecución: contenedor Docker lanzado por un temporizador del servidor.** El lote periódico lo lanza un temporizador del anfitrión (cron o systemd) y la ejecución a petición es el mismo comando por SSH. La exclusión mutua se hace con un fichero de bloqueo en `orumi/`. *Alternativas descartadas:* proceso residente (consume memoria y exige supervisión); sin contenedor (depende del entorno del anfitrión).
5. **Entrada del MVP: comando `capturar`** que escribe la captura inmutable en `orumi/capturas/` desde texto, por SSH o en local (X7). *Alternativa descartada:* carpeta vigilada; difícil de verificar y propensa a ficheros a medias.
6. **Comandos del MVP:** `capturar`, `procesar` (lote), `validar` y `reindexar`. Las reglas de `validar` son las de `docs/agente-mira.md` §5; `procesar` las aplica antes de escribir.
7. **Proveedor y modelo por evaluación ponderada** (`docs/tecnologias.md`): Anthropic (Claude), pago por API; **Claude Haiku 5.5 por defecto**, configurable a Sonnet 5.5 si la primera ejecución real revisada por el usuario muestra errores de interpretación. Se compararon precio, privacidad y retención, estabilidad del modelo, fiabilidad, calidad esperada, SDK y operativa, con fuentes oficiales del 2026-11-13. *Alternativas descartadas:* Gemini 3.5 Flash-Lite y 3.6 Flash (menos garantía de estabilidad, retención no especificada, sin SLA confirmado para AI Studio; 3.6 Flash dobla precio en 2027); un modelo local (la RPi 4 es demasiado lenta); el nivel gratuito (reutiliza datos, X9).
8. **Garantía de privacidad como dato de configuración fechado** (`verificada_el`, `fuente`), con caducidad por defecto de 180 días, comprobada antes de cada ejecución (fallo cerrado, X38). Una API no puede preguntar sus condiciones, así que la garantía la declara y la renueva el usuario tras leerlas.
9. **Cliente de IA sin parámetros de muestreo.** Desde la versión de API `2026-10-21` los parámetros `temperature`, `top_p` y `top_k` provocan error en Anthropic; el cliente no los envía.
10. **Configuración en un fichero fuera del repositorio**, con valores no secretos (proveedor, modelo, máximo de preguntas, ventana, horario) y los secretos en variables de entorno o en un fichero con permisos restringidos.
11. **Código y datos separados.** El código vive en este repositorio; los datos, en `orumi/` del servidor, fuera de git (X27, X34).
12. **Sin puertos nuevos** (sin API en el MVP). El detalle de despliegue y permisos va en `docs/privado/`.
13. **Fases.** Esta change decide y especifica sin escribir código; una change siguiente (`mvp-mira`) implementa los cuatro comandos partiendo de estas specs y de los casos de evaluación.

## Risks / Trade-offs

- **La evaluación se basa en evidencias, no en pruebas** → el criterio de calidad es el más débil; se compensa con un modelo configurable y con la revisión del usuario de la primera ejecución real.
- **Un proveedor cambia sus condiciones o su disponibilidad** → caducidad de 180 días de la verificación, fallo cerrado y proveedor sustituible por configuración; Anthropic se compromete a 60 días de aviso antes de retirar un modelo.
- **Disponibilidad no verificada** (sin porcentaje a 90 días y con incidencias recientes en la página de estado) → los lotes son reanudables y las capturas quedan pendientes si falla.
- **PostgreSQL compartido comparte recursos y dominio de fallo con otros servicios** → índice regenerable y procesamiento independiente de él.
- **Sin respaldo y con un único disco** (X27) → riesgo aceptado; el índice es desechable.
- **Una clave de API filtrada** → permisos restringidos, nunca en el repositorio ni en logs y límite de gasto en el proveedor.
- **La RPi está compartida** → el lote es corto y nocturno y no hay cómputo local pesado.
