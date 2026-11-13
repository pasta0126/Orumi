using System.Text.Json;

namespace Orumi.Agente.Proveedores;

/// <summary>
/// Convierte la respuesta JSON del proveedor en <see cref="RespuestaExtraccion"/>. Es estricta: lo que no encaja
/// con el formato falla y la captura queda pendiente, en lugar de adivinarse.
/// </summary>
public static class LectorDeRespuesta
{
    public static RespuestaExtraccion Analizar(string texto)
    {
        var json = Limpiar(texto);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("la respuesta no es un objeto JSON");
            }

            var registros = Lista(raiz, "registros").Select(Registro).ToList();
            var preguntas = Lista(raiz, "preguntas").Select(Pregunta).ToList();
            return new RespuestaExtraccion(registros, preguntas);
        }
        catch (Exception e) when (e is JsonException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            // Solo se indica qué campo falla (nombres de campo del formato, nunca contenido de las notas).
            var detalle = e switch
            {
                JsonException je => $"JSON no válido en {je.Path ?? "?"}",
                FormatException fe => fe.Message,
                _ => e.GetType().Name,
            };
            throw new ProcesamientoFallidoException($"la respuesta del proveedor no tiene el formato esperado ({detalle})");
        }
    }

    /// <summary>Quita una valla de código (```json … ```) si el modelo la añadió.</summary>
    private static string Limpiar(string texto)
    {
        var t = texto.Trim();
        if (t.StartsWith("```", StringComparison.Ordinal))
        {
            var primera = t.IndexOf('\n');
            var ultima = t.LastIndexOf("```", StringComparison.Ordinal);
            if (primera > 0 && ultima > primera)
            {
                t = t[(primera + 1)..ultima].Trim();
            }
        }

        return t;
    }

    private static IEnumerable<JsonElement> Lista(JsonElement e, string nombre) =>
        e.TryGetProperty(nombre, out var l) && l.ValueKind == JsonValueKind.Array ? l.EnumerateArray() : [];

    private static string? Texto(JsonElement e, string nombre) =>
        e.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string Requerido(JsonElement e, string nombre) =>
        Texto(e, nombre) ?? throw new FormatException($"falta {nombre}");

    private static RegistroExtraido Registro(JsonElement e) => new()
    {
        Fragmento = Requerido(e, "fragmento"),
        Clase = Requerido(e, "clase"),
        Titulo = Requerido(e, "titulo"),
        Descripcion = Texto(e, "descripcion") ?? Requerido(e, "titulo"),
        Reclama = Texto(e, "reclama"),
        Amplia = Texto(e, "amplia"),
        Duda = Texto(e, "duda"),
        EnConflictoCon = Texto(e, "en_conflicto_con"),
        ConflictoCampo = Texto(e, "conflicto_campo"),
        Campos = e.TryGetProperty("campos", out var c) && c.ValueKind == JsonValueKind.Object ? Mapa(c) : [],
        Certeza = e.TryGetProperty("certeza", out var z) && z.ValueKind == JsonValueKind.Object
            ? z.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String).ToDictionary(p => p.Name, p => p.Value.GetString()!)
            : [],
        // Si el modelo omite la certeza de una relación o de un estado, se usa la más débil («interpretado»): nunca se supone «dicho».
        Relaciones = Lista(e, "relaciones").Select(r => new RelacionExtraida(
            Texto(r, "tipo") ?? "relacionado_con", Texto(r, "certeza") ?? "interpretado", Texto(r, "destino_id"),
            r.TryGetProperty("destino_indice", out var di) && di.ValueKind == JsonValueKind.Number ? di.GetInt32() : null)).ToList(),
        Multidia = e.TryGetProperty("multidia", out var m) && m.ValueKind == JsonValueKind.Object
            ? new MultidiaExtraido(Requerido(m, "estado"), Texto(m, "fin_desde"), Texto(m, "fin_hasta"), Texto(m, "certeza_estado") ?? "interpretado", Texto(m, "certeza_fin") ?? "interpretado")
            : null,
        ResumenEntrada = Texto(e, "resumen_entrada"),
    };

    private static PreguntaCandidata Pregunta(JsonElement e) => new(
        Requerido(e, "campo"), Requerido(e, "texto"), Texto(e, "valor") ?? "media", Texto(e, "registro_id"),
        e.TryGetProperty("registro_indice", out var i) && i.ValueKind == JsonValueKind.Number ? i.GetInt32() : null);

    private static Dictionary<string, object?> Mapa(JsonElement e) =>
        e.EnumerateObject().ToDictionary(p => p.Name, p => Valor(p.Value));

    /// <summary>Árbol simple: textos, listas y mapas. Números y booleanos pasan a su texto, como en el frontmatter.</summary>
    private static object? Valor(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => null,
        JsonValueKind.Array => e.EnumerateArray().Select(Valor).ToList(),
        JsonValueKind.Object => Mapa(e),
        _ => throw new FormatException("valor JSON no admitido"),
    };
}
