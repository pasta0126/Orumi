using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;

namespace Orumi.Agente.Proveedores;

/// <summary>
/// Cliente real de Anthropic (SDK oficial). Envía solo el texto de la captura, sus metadatos, las claves de la captura
/// y los datos mínimos de los registros relacionados (<c>docs/agente-mira.md</c> §7.1). No envía parámetros de muestreo
/// (la API los rechaza desde la versión 2026-10-21). <b>No se ejercita hasta contratar el servicio.</b>
/// </summary>
public sealed class ProveedorAnthropic(string modelo, AnthropicClient? cliente = null) : IProveedorIA
{
    private const int MaximoDeTokens = 8192;

    private readonly Lazy<AnthropicClient> _cliente = new(() => cliente ?? new AnthropicClient());

    public string Nombre => "anthropic";

    public string Modelo => modelo;

    public bool EnviaDatosAFuera => true;

    public async Task<RespuestaExtraccion> ExtraerAsync(SolicitudExtraccion solicitud, CancellationToken ct)
    {
        var parametros = new MessageCreateParams
        {
            Model = modelo,
            MaxTokens = MaximoDeTokens,
            System = Instrucciones,
            Messages = [new UserMessageParam { Content = Mensaje(solicitud) }],
        };

        Message respuesta;
        try
        {
            respuesta = await _cliente.Value.Messages.Create(parametros, ct);
        }
        catch (AnthropicApiException e) when (Traducir(e) is { } traducida)
        {
            throw traducida;
        }

        var texto = string.Concat(respuesta.Content.Select(b => b.TryPickText(out var t) ? t.Text : string.Empty));
        RespuestaExtraccion analizada;
        try
        {
            analizada = LectorDeRespuesta.Analizar(texto);
        }
        catch (Exception) when (VolcarSalidaParaDepurar(texto, respuesta.StopReason?.ToString()))
        {
            throw; // nunca se alcanza: el filtro solo sirve para volcar la salida sin atrapar la excepción
        }

        // El uso se informa aunque luego la salida no sea válida: la llamada ya se ha cobrado.
        return analizada with { Uso = new UsoDeTokens(respuesta.Usage.InputTokens, respuesta.Usage.OutputTokens) };
    }

    /// <summary>
    /// Solo para depurar: si ORUMI_DEPURAR_DIR está definido, guarda en ese directorio la salida cruda del modelo cuando no se puede
    /// leer. Por defecto está apagado (la salida contiene texto del usuario y nunca se registra).
    /// </summary>
    private static bool VolcarSalidaParaDepurar(string texto, string? motivoDeParada)
    {
        var dir = Environment.GetEnvironmentVariable("ORUMI_DEPURAR_DIR");
        if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
        {
            File.WriteAllText(Path.Combine(dir, $"salida-{DateTime.UtcNow:HHmmssfff}.txt"), $"stop_reason: {motivoDeParada}\n{texto}");
        }

        return false;
    }

    /// <summary>
    /// Traduce un error de la API a <see cref="ProveedorException"/> si corta la ejecución (saldo, límite de gasto o clave); null para un
    /// error puntual, que se relanza tal cual y solo deja pendiente esa captura.
    /// </summary>
    internal static ProveedorException? Traducir(AnthropicApiException e)
    {
        var (tipo, codigo, mensaje) = LeerError(e.ResponseBody, e.Message);
        var categoria = ClasificadorDeErrores.Clasificar((int)e.StatusCode, tipo, codigo, mensaje);
        return categoria == CategoriaError.Otro ? null : new ProveedorException(categoria, ClasificadorDeErrores.Explicacion(categoria));
    }

    /// <summary>Tipo, código y mensaje del cuerpo de error de la API (<c>{"error": {"type", "message", "details": {"error_code"}}}</c>).</summary>
    internal static (string? Tipo, string? Codigo, string? Mensaje) LeerError(string? cuerpo, string? mensajeDeLaExcepcion)
    {
        try
        {
            using var doc = JsonDocument.Parse(cuerpo ?? "{}");
            if (doc.RootElement.TryGetProperty("error", out var e))
            {
                string? Texto(JsonElement x, string n) => x.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                var codigo = e.TryGetProperty("details", out var d) && d.ValueKind == JsonValueKind.Object ? Texto(d, "error_code") : null;
                return (Texto(e, "type"), codigo, Texto(e, "message") ?? mensajeDeLaExcepcion);
            }
        }
        catch (JsonException)
        {
            // cuerpo no JSON: se clasifica por el mensaje de la excepción
        }

        return (null, null, mensajeDeLaExcepcion);
    }

    /// <summary>El mensaje de usuario: JSON con lo mínimo necesario.</summary>
    public static string Mensaje(SolicitudExtraccion s) => JsonSerializer.Serialize(new
    {
        captura_id = s.CapturaId,
        comunicada_el = s.ComunicadaEl.ToString("yyyy-MM-dd'T'HH:mm:sszzz"),
        fecha_acontecimiento = s.FechaAcontecimiento,
        etiqueta = s.Etiqueta,
        texto = s.Texto,
        claves_existentes = s.ClavesExistentes,
        registros_relacionados = s.Relacionados.Select(r => new
        {
            r.Id, r.Clase, r.Titulo, fecha_desde = r.FechaDesde, fecha_hasta = r.FechaHasta, r.Personas, r.Entidades, r.Proyectos, multidia_estado = r.MultidiaEstado,
        }),
    }, new JsonSerializerOptions { WriteIndented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    /// <summary>Resumen del contrato de <c>docs/agente-mira.md</c> §3, §4 y §6 para el modelo.</summary>
    public const string Instrucciones = """
        Eres Mira, el agente de ORUMI, una memoria personal. Conviertes una nota del usuario en registros estructurados.
        Responde SOLO con un objeto JSON, sin texto antes ni después.

        Reglas:
        - Nunca inventes datos. Lo no dicho es "desconocido" (el valor literal) o se omite. No supongas que algo se resolvió ni inventes cifras, horas, motivos o causas.
        - Registra TODO lo que cuenta el usuario, también lo cotidiano. No filtres por relevancia. Un hecho = un registro; no partas un mismo hecho en dos.
        - "fragmento" es un trozo LITERAL y contiguo del texto de la nota (copia exacta) del que sale el registro.
        - certeza por campo: "dicho" (lo dijo el usuario), "interpretado" (lo deduces). Las fechas que resuelves de expresiones relativas (ayer, la semana pasada, el lunes) o sin referencia temporal son "interpretado"; usa comunicada_el como referencia. Una imprecisión es un rango (desde/hasta), nunca un día exacto inventado.
        - hora: {"expresion", "valor": "HH:MM" solo si hay hora explícita, "aproximada": "true" si dice "sobre las"}. Si solo hay franja (por la mañana), solo "expresion" y sin "valor".
        - duracion: {"expresion", "minutos"} solo si el usuario dio una cifra; si es vaga, solo "expresion". Si no la dijo, "desconocido".
        - entidades: [{"nombre", "tipo"}] de organizaciones, clientes, sistemas o productos nombrados (tipo: organizacion | sistema | producto | otro, solo si está claro). herramientas: con qué se hace algo.
        - No pongas certeza a un campo que vale "desconocido" ni a uno que no incluyas: la certeza solo va con datos reales.
        - Campos permitidos: fecha, hora, duracion, herramientas, personas, entidades, lugares, proyectos, contexto, acciones, resultados, problemas, ideas. "fecha" = {"expresion", "desde": "AAAA-MM-DD", "hasta": "AAAA-MM-DD"}: SOLO el día, sin hora (la hora va en el campo "hora"), y siempre con desde y hasta (iguales si es un solo día). Si el hecho aún no ha ocurrido (p. ej. «mañana tengo que…»), usa igualmente la fecha futura resuelta desde comunicada_el.
        - contexto solo contiene lo dicho; no generes motivos ni causas. Si se dice que hubo acuerdos sin detallarlos, registra que los hubo y que no se indican.
        - Una actividad que el usuario deja abierta ("sigo mañana") lleva "multidia" con estado "abierto".
        - Si una nota continúa o cierra uno de los registros_relacionados con claridad, usa "amplia" con su id. Si no estás seguro de que sea el mismo, usa "duda" con su id. Si contradice un dato de uno de ellos, usa "en_conflicto_con" y "conflicto_campo". Solo puedes citar ids de registros_relacionados.
        - Si reprocesas una nota con claves_existentes, usa "reclama" con la clave del registro que corresponde a cada elemento.
        - Preguntas (opcionales, pocas): solo para completar datos estructurales (fecha, hora, duracion, personas, lugares, proyectos, o si lo que quedó abierto se resolvió). NUNCA preguntes por el contenido de lo hablado o decidido (qué acuerdos, por qué). Sin consejos ni valoraciones.
        - Si la nota no contiene hechos registrables, devuelve {"registros": [], "preguntas": []}.

        Formato:
        {"registros":[{"fragmento":"","clase":"actividad|evento|idea|decision|problema|otro","titulo":"","descripcion":"","reclama":null,"amplia":null,"duda":null,"en_conflicto_con":null,"conflicto_campo":null,"campos":{},"certeza":{},"relaciones":[{"tipo":"surge_de|continua|relacionado_con","certeza":"dicho|interpretado","destino_id":null,"destino_indice":null}],"multidia":null,"resumen_entrada":null}],"preguntas":[{"campo":"","texto":"","valor":"alta|media|baja","registro_id":null,"registro_indice":0}]}
        Tono de títulos, descripciones y preguntas: neutro, sereno, sin entusiasmo ni consejos.
        """;
}
