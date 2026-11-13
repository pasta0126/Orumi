using System.Globalization;
using System.Text.RegularExpressions;

namespace Orumi.Core.Correcciones;

/// <summary>Un valor, una operación o un identificador que el usuario ha dado mal; no se modifica ningún fichero.</summary>
public sealed class OperacionInvalidaException(string mensaje) : Exception(mensaje);

/// <summary>
/// Interpreta con reglas deterministas lo que el usuario escribe para un campo (sin modelo de IA): lo que no encaja se rechaza.
/// </summary>
public static partial class ValoresDeCampo
{
    public static readonly string[] CamposDeLista = ["personas", "lugares", "proyectos", "herramientas", "acciones", "ideas", "problemas"];

    public static readonly string[] CamposDeTexto = ["titulo", "contexto", "resultados"];

    /// <summary>Campos que el usuario puede corregir. Los identificadores, las fuentes, la procedencia y la clave de extracción no.</summary>
    public static readonly string[] Corregibles = ["titulo", "fecha", "hora", "duracion", .. CamposDeLista, "entidades", "contexto", "resultados"];

    private static readonly string[] TiposDeEntidad = ["organizacion", "sistema", "producto", "otro"];

    [GeneratedRegex(@"^(\d+)$")] private static partial Regex SoloMinutosRx();
    [GeneratedRegex(@"^(\d+)\s*m(in)?$", RegexOptions.IgnoreCase)] private static partial Regex MinutosRx();
    [GeneratedRegex(@"^(\d+)\s*h$", RegexOptions.IgnoreCase)] private static partial Regex HorasRx();
    [GeneratedRegex(@"^(\d+)\s*h\s*(\d{1,2})\s*(m|min)?$", RegexOptions.IgnoreCase)] private static partial Regex HorasMinutosRx();
    [GeneratedRegex(@"^([01]?\d|2[0-3]):([0-5]\d)$")] private static partial Regex HoraRx();

    public static bool EsCorregible(string campo) => Corregibles.Contains(campo);

    public static string AyudaDeFormato(string campo) => campo switch
    {
        "fecha" => "una fecha AAAA-MM-DD, o un rango AAAA-MM-DD..AAAA-MM-DD",
        "hora" => "una hora HH:MM (por ejemplo 18:30)",
        "duracion" => "una duración en minutos (90 o 90m) o en horas (2h, 1h30)",
        "entidades" => "nombres separados por comas, con tipo opcional (Gullón:organizacion, Outlook:sistema); tipos: organizacion, sistema, producto, otro",
        "titulo" or "contexto" or "resultados" => "un texto no vacío",
        _ when CamposDeLista.Contains(campo) => "uno o varios valores separados por comas",
        _ => "un valor válido",
    };

    /// <summary>Interpreta el texto para el campo. <paramref name="actual"/> permite conservar la expresión original de fecha y hora.</summary>
    public static object Analizar(string campo, string texto, object? actual)
    {
        var t = (texto ?? string.Empty).Trim();
        if (t.Length == 0)
        {
            throw Invalido(campo, "el valor está vacío");
        }

        switch (campo)
        {
            case "fecha":
                return Fecha(t, actual);
            case "hora":
                return Hora(t, actual);
            case "duracion":
                return Duracion(t);
            case "entidades":
                return Entidades(t);
            case "titulo" or "contexto" or "resultados":
                return t;
            default:
                if (CamposDeLista.Contains(campo))
                {
                    var elementos = t.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Cast<object?>().ToList();
                    return elementos.Count > 0 ? elementos : throw Invalido(campo, "no hay ningún valor");
                }

                throw new OperacionInvalidaException($"El campo «{campo}» no se puede corregir. Campos corregibles: {string.Join(", ", Corregibles)}.");
        }
    }

    private static OperacionInvalidaException Invalido(string campo, string motivo) =>
        new($"Valor no válido para «{campo}» ({motivo}). Se espera {AyudaDeFormato(campo)}.");

    private static Dictionary<string, object?> Fecha(string t, object? actual)
    {
        var partes = t.Split("..", StringSplitOptions.TrimEntries);
        if (partes.Length is < 1 or > 2 || partes.Any(p => !DateOnly.TryParseExact(p, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
        {
            throw Invalido("fecha", "no es una fecha AAAA-MM-DD ni un rango válido");
        }

        var desde = partes[0];
        var hasta = partes.Length == 2 ? partes[1] : partes[0];
        if (string.CompareOrdinal(desde, hasta) > 0)
        {
            throw Invalido("fecha", "el rango empieza después de acabar");
        }

        var fecha = new Dictionary<string, object?>();
        if (actual is Dictionary<string, object?> a && a.GetValueOrDefault("expresion") is string expresion)
        {
            fecha["expresion"] = expresion; // la expresión original del usuario se conserva
        }

        fecha["desde"] = desde;
        fecha["hasta"] = hasta;
        return fecha;
    }

    private static Dictionary<string, object?> Hora(string t, object? actual)
    {
        var m = HoraRx().Match(t);
        if (!m.Success)
        {
            throw Invalido("hora", "no es una hora HH:MM");
        }

        var valor = $"{int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture):00}:{m.Groups[2].Value}";
        var expresion = actual is Dictionary<string, object?> a && a.GetValueOrDefault("expresion") is string e ? e : t;
        return new Dictionary<string, object?> { ["expresion"] = expresion, ["valor"] = valor, ["aproximada"] = "false" };
    }

    private static Dictionary<string, object?> Duracion(string t)
    {
        int? minutos = null;
        Match m;
        if ((m = SoloMinutosRx().Match(t)).Success || (m = MinutosRx().Match(t)).Success)
        {
            minutos = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }
        else if ((m = HorasRx().Match(t)).Success)
        {
            minutos = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 60;
        }
        else if ((m = HorasMinutosRx().Match(t)).Success)
        {
            minutos = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 60 + int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        }

        if (minutos is null or <= 0)
        {
            throw Invalido("duracion", "no es una duración válida");
        }

        return new Dictionary<string, object?>
        {
            ["expresion"] = t,
            ["minutos"] = minutos.Value.ToString(CultureInfo.InvariantCulture),
        };
    }

    private static List<object?> Entidades(string t)
    {
        var lista = new List<object?>();
        foreach (var elemento in t.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var partes = elemento.Split(':', 2, StringSplitOptions.TrimEntries);
            if (partes[0].Length == 0)
            {
                throw Invalido("entidades", "hay un nombre vacío");
            }

            var entidad = new Dictionary<string, object?> { ["nombre"] = partes[0] };
            if (partes.Length == 2)
            {
                if (!TiposDeEntidad.Contains(partes[1]))
                {
                    throw Invalido("entidades", $"tipo desconocido «{partes[1]}»");
                }

                entidad["tipo"] = partes[1];
            }

            lista.Add(entidad);
        }

        return lista.Count > 0 ? lista : throw Invalido("entidades", "no hay ningún nombre");
    }

    /// <summary>Texto corto y legible de un valor del frontmatter (para mensajes, rechazos y preguntas).</summary>
    public static string Resumen(object? valor) => valor switch
    {
        null => "desconocido",
        string s => s,
        List<object?> l => string.Join(", ", l.Select(Resumen)),
        Dictionary<string, object?> m when m.ContainsKey("desde") && m.ContainsKey("hasta") =>
            m["desde"] as string == m["hasta"] as string ? (string)m["desde"]! : $"{m["desde"]}..{m["hasta"]}",
        Dictionary<string, object?> m when m.ContainsKey("nombre") => m.ContainsKey("tipo") ? $"{m["nombre"]}:{m["tipo"]}" : (string)m["nombre"]!,
        Dictionary<string, object?> m when m.ContainsKey("valor") => (string)m["valor"]!,
        Dictionary<string, object?> m when m.ContainsKey("minutos") => $"{m["expresion"]} ({m["minutos"]} min)",
        Dictionary<string, object?> m when m.ContainsKey("expresion") => (string)m["expresion"]!,
        _ => valor.ToString() ?? string.Empty,
    };
}
