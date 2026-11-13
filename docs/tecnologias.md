# ORUMI — Tecnologías del MVP

Estado: propuesta de la change `tecnologias-mvp`. Fecha de la investigación: **2026-11-13**. Las cifras y condiciones salen de **fuentes oficiales leídas ese día** (lista al final). Nada de lo que sigue se ha probado con ejecuciones reales: es una evaluación ponderada simple, por decisión del usuario. Lo que no se pudo verificar se dice expresamente.

## 1. Proveedor y modelo de IA

Criterio del usuario: **se elige uno, y el que se pague es el que se usa.** Candidatos: los modelos baratos y rápidos de los dos proveedores que encajan con las preferencias del proyecto (X9): Claude (Anthropic) y Gemini (Google). Se descartan otros por no haberse investigado, no por una razón técnica.

### 1.1 Datos verificados

| Candidato | Entrada / salida (por millón de tokens) | Coste mensual con el volumen supuesto (1 M entrada + 1 M salida) | Estado del modelo |
|-----------|------------------------------------------|---------|-------------------|
| **Claude Haiku 5.5** | 0,10 $ / 0,50 $ (prompts ≤100.000 tokens); Batch API −50 % | **0,60 $** (0,30 $ con Batch) | Activo; retirada «no antes del 7-oct-2027» |
| **Claude Sonnet 5.5** | 2 $ / 10 $; Batch −50 % | **12 $** (6 $ con Batch) | Activo; retirada «no antes del 28-sep-2027» |
| **Gemini 3.5 Flash-Lite** | 0,30 $ / 2,50 $ | **2,80 $** | Lanzado el 21-jul-2026, sin fecha de cierre anunciada |
| **Gemini 3.6 Flash** | 0,75 $ / 3,75 $ hasta el 31-dic-2026; **1,50 $ / 7,50 $ desde el 1-ene-2027** | 4,50 $ → **9 $** | Activo |
| Gemini 3.1 Flash-Lite (descartado) | 0,25 $ / 1,50 $ | 1,75 $ | **Cierre el 7-may-2027**, solo 6 meses tras su lanzamiento |

Todos cuestan unos pocos euros al mes: **el precio no es el factor decisivo**, pese al peso que le damos.

**Privacidad y retención (X38):**
- **Anthropic (API de pago):** «los datos retenidos nunca se usan para entrenar sin tu permiso expreso». Su centro de privacidad dice que borra entradas y salidas de su sistema «en un máximo de 30 días» (excepto contenido marcado por seguridad, hasta 2 años). La documentación técnica dice que el contenido de la conversación no se retiene por defecto salvo en los «Covered Models», que exigen 30 días. **Haiku 5.5 y Sonnet 5.5 no están en esa lista** (solo Fable y Mythos). Las dos fuentes difieren en el detalle de retención, pero coinciden en lo que exige X38: no se entrena con los datos.
- **Google (Gemini de pago):** «Google no usa tus prompts ni respuestas para mejorar sus productos». Guarda prompts y respuestas «durante un período limitado» solo para detectar abusos; **no se especifica cuánto** y pueden almacenarse o cachearse «en cualquier país donde Google o sus agentes tengan instalaciones». En el nivel gratuito, Google sí usa los datos y revisores humanos pueden leerlos (por eso se descarta, X9).

**Estabilidad del modelo:**
- **Anthropic** se compromete por escrito a **al menos 60 días de aviso** antes de retirar un modelo público, y publica fechas «no antes de».
- **Google** no declara un plazo mínimo de aviso: solo «fechas más tempranas posibles». Ejemplos recientes: `gemini-2.5-flash` y `gemini-2.5-pro` se redirigen a otros modelos el 1-dic-2026, y `gemini-3.5-flash` ya no está disponible en AI Studio y se reencamina a 3.6.

**Fiabilidad del servicio:**
- **Anthropic:** su página de estado, a 13-nov-2026, muestra «Partial Outage» y 5 incidentes en unas dos semanas (uno en curso, otro de errores en Haiku/Fable resuelto en minutos). **No se pudo obtener un porcentaje de disponibilidad a 90 días.** No se ha verificado un SLA contractual para la API estándar.
- **Google:** su página de estado no se pudo leer. El SLA publicado es para Vertex AI (99,5 % en una versión archivada de 2024; la vigente no se pudo confirmar); **para la API de AI Studio no se pudo confirmar ningún SLA**, y fuentes no oficiales indican que no lo tiene. Hay un informe en el foro de Google de un 503 prolongado en nivel de pago (anécdota, febrero de 2026).

**SDK para .NET:** existe SDK **oficial** en ambos: `Anthropic` en NuGet (repositorio oficial `anthropics/anthropic-sdk-csharp`, versión 13.7.0) y `Google.GenAI` (repositorio `googleapis/dotnet-genai`, versión 2.1.0, con salida JSON estructurada).

**Nota técnica de Anthropic:** desde la versión de API `2026-10-21` los parámetros `temperature`, `top_p` y `top_k` dan error 400. El cliente de Mira no debe enviarlos.

### 1.2 Evaluación ponderada

Puntuaciones de 1 (peor) a 5 (mejor), subjetivas pero justificadas con los datos de arriba. Pesos que suman 100.

| Criterio | Peso | Haiku 5.5 | Sonnet 5.5 | Gemini 3.5 Flash-Lite | Gemini 3.6 Flash |
|----------|-----:|:---------:|:----------:|:---------------------:|:----------------:|
| Privacidad y retención (X38) | 25 | 4 | 4 | 3 | 3 |
| Fiabilidad del servicio | 15 | 3 | 3 | 2 | 2 |
| Estabilidad del modelo (avisos y plazos) | 15 | 5 | 5 | 3 | 3 |
| Precio | 15 | 5 | 3 | 4 | 3 |
| Calidad esperada en extracción en español (**sin probar**) | 15 | 3 | 4 | 3 | 4 |
| SDK oficial y encaje con .NET | 10 | 5 | 5 | 4 | 4 |
| Sencillez operativa (Batch, límites) | 5 | 5 | 5 | 4 | 4 |
| **Total ponderado (sobre 5)** | 100 | **4,15** | **4,00** | **3,15** | **3,15** |

**Por qué estas notas:**
- *Privacidad:* Anthropic da un plazo máximo explícito (30 días) y no entrena; Google no especifica el plazo de retención. Ambos cumplen lo mínimo de X38.
- *Fiabilidad:* ninguno ofrece datos de disponibilidad verificables; Google, además, sin SLA confirmado para AI Studio.
- *Estabilidad:* el compromiso de 60 días de Anthropic y las fechas «no antes de» a ~11 meses pesan frente a la ausencia de plazo en Google y a la rotación reciente de modelos.
- *Precio:* el más barato es Haiku; todos son baratos.
- *Calidad:* **no hay pruebas.** Se asume que un modelo de gama media (Sonnet, Flash) extrae mejor que uno ligero (Haiku, Flash-Lite), pero para este trabajo —texto corto en español a datos estructurados con reglas claras— cualquiera de los cuatro es plausible. Es el criterio más débil de la tabla.

### 1.3 Sensibilidad

- Aunque Gemini 3.5 Flash-Lite subiera a 4 en privacidad y a 3 en fiabilidad, llegaría a **3,55**, por debajo de Haiku.
- Si se pusiera calidad con peso 30 y precio con peso 0, Sonnet (4,15) superaría a Haiku (3,85): **la elección entre los dos de Claude depende de cuánto se valore la calidad frente a 11 $/mes de diferencia.**
- Gemini 3.6 Flash dobla su precio el 1-ene-2027, lo que baja su atractivo.

### 1.4 Decisión propuesta

**Proveedor: Anthropic (Claude), pagado por API.** Modelo por defecto: **Claude Haiku 5.5**, configurable. Si la primera ejecución real revisada por el usuario (criterio de `docs/evaluacion-mira.md`) muestra errores de interpretación, se sube a **Sonnet 5.5** cambiando solo la configuración (≈6–12 $/mes). Pasos de seguridad: límite de gasto mensual en la consola del proveedor y clave de API solo en el servidor.

**Límites de esta evaluación:** sin pruebas reales, con un criterio de calidad sin datos, y con disponibilidad no verificada. Se compensa con que el modelo es configurable y con la revisión de la primera ejecución real.

## 2. Lenguaje

El usuario prefiere no usar Node; .NET para la API; Rust si aporta mejora. Evidencia:
- **.NET:** SDK oficial de Anthropic y de Google; ecosistema maduro para PostgreSQL, YAML y servicios web; el usuario ya lo quiere para la API futura.
- **Rust:** **no se encontró SDK oficial** de ninguno de los dos proveedores (solo crates de la comunidad). Sus ventajas —binario pequeño, poca memoria— son irrelevantes para un proceso nocturno corto que espera a una API. Cuesta más escribir y mantener a mano el cliente HTTP.

**Decisión propuesta: .NET** (versión LTS vigente al implementar, a confirmar) para las herramientas de línea de comandos y la futura API. El front se decide después (al usuario le da igual).

## 3. Índice derivado

El usuario prefiere el **Postgres compartido** del servidor. Se acepta con estas condiciones:
- **Base de datos y rol propios** de ORUMI dentro de la instancia existente; ninguna tabla compartida con otros proyectos. Se reutiliza infraestructura (X32), no software de otros (X34).
- **El procesamiento no depende del índice.** `capturar`, `procesar` y `validar` funcionan solo con ficheros; el índice sirve para consultas y para el panel. Si Postgres cae o se borra, solo `reindexar` y las consultas se ven afectados, y el índice se regenera.
- El índice es **desechable y reconstruible** desde los ficheros (X3).

Riesgo asumido: comparte recursos y dominio de fallo con otros servicios de un servidor pequeño. Alternativa descartada por preferencia del usuario: SQLite en un fichero.

## 4. Ejecución

Sin cambios respecto al diseño de la change: contenedor Docker lanzado por un temporizador del servidor y el mismo comando a petición por SSH, bloqueo de ejecución exclusiva, sin puertos nuevos.

## 5. Configuración (esquema)

Un fichero fuera del repositorio, con valores no secretos. Los secretos (clave de API, contraseña de la base de datos) van en variables de entorno o en un fichero con permisos restringidos, nunca aquí.

```yaml
datos: /ruta/a/orumi                  # directorio de datos (fuera de git)
proveedor:
  nombre: anthropic
  modelo: claude-haiku-5-5            # configurable; alternativa: claude-sonnet-5-5
  garantia_no_reutilizacion:
    declarada: true
    verificada_el: 2026-11-13         # caduca a los 180 días (configurable)
    fuente: https://privacy.claude.com/en/articles/7996866-how-long-do-you-store-my-organization-s-data
    caducidad_dias: 180
lote:
  horario: "03:00"                    # lote periódico
  ventana_relacionados_dias: 14       # registros candidatos que se envían (mínimo necesario)
preguntas:
  maximo_pendientes: 5
indice:
  # la cadena de conexión y la contraseña van en el entorno, no aquí
  base: orumi
```

Variables de entorno (secretos): clave de API del proveedor y credenciales de la base de datos. Si falta alguna de las comprobaciones (garantía declarada y vigente, clave presente), Mira no envía datos (X38).

## 6. Estructura del repositorio de código

Código en este repositorio; datos fuera (X27, X34). Estructura lógica propuesta para `mvp-mira`:

```
src/
├── Orumi.Core/        # modelo de ficheros, lectura/escritura, reglas de validación, huella
├── Orumi.Agente/      # contrato de Mira: extracción, claves, preguntas, privacidad
├── Orumi.Indice/      # regeneración y consultas del índice (PostgreSQL)
├── Orumi.Cli/         # comandos: capturar, procesar, validar, reindexar
tests/                 # pruebas, con los ejemplos de docs/ejemplos como datos de prueba
docs/ openspec/        # ya existentes
```

En el servidor hay tres cosas separadas: los **datos** (`orumi/`), la **configuración** y los **secretos**, cada una con permisos propios. Las rutas, el usuario y el temporizador concretos se documentan en `docs/privado/despliegue.md` (fuera de git).

## 7. Qué queda para cuando se necesite la API

Se aplaza a propósito (decisión del usuario): al llegar a la ejecución real de Mira se para y se contrata el servicio de IA. Entonces:
1. Crear la cuenta de pago por API del proveedor elegido, con **límite de gasto mensual**.
2. Guardar la clave solo en el servidor.
3. Declarar en la configuración la garantía de privacidad con su fecha y fuente.
4. Tras una semana de uso, revisar el coste real y, si hace falta, el modelo.

## 8. Fuentes (leídas el 2026-11-13)

- Precios de Claude: https://platform.claude.com/docs/en/about-claude/pricing
- Retiradas de modelos de Claude: https://platform.claude.com/docs/en/about-claude/model-deprecations
- Retención de datos de la API de Claude: https://platform.claude.com/docs/en/manage-claude/api-and-data-retention
- Retención para clientes comerciales: https://privacy.claude.com/en/articles/7996866-how-long-do-you-store-my-organization-s-data
- Modelos con retención de 30 días: https://support.claude.com/en/articles/15425695-covered-models
- Estado de Claude: https://status.claude.com
- Precios de Gemini: https://aistudio.google.com/docs/pricing
- Condiciones de Gemini (datos): https://aistudio.google.com/docs/terms
- Retiradas de modelos de Gemini: https://aistudio.google.com/docs/deprecations
- SDK de C# de Anthropic: https://platform.claude.com/docs/en/cli-sdks-libraries/sdks/csharp
- SDK de .NET de Google: https://ai.google.dev/gemini-api/docs/libraries
- SLA de Vertex AI (solo referencia; versión vigente sin confirmar): https://cloud.google.com/vertex-ai/generative-ai/sla
