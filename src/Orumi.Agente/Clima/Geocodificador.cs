using System.Globalization;
using System.Text.Json;

namespace Orumi.Agente.Clima;

public sealed record CandidatoDeCiudad(string Nombre, string? Region, string? Pais, string? CodigoPais, double Latitud, double Longitud, long? Poblacion)
{
    /// <summary>«Lleida (Comunidad Autónoma de Cataluña, ES)»: lo que se le enseña al usuario para que confirme.</summary>
    public string Descripcion => Nombre + " (" + string.Join(", ", new[] { Region, CodigoPais ?? Pais }.Where(x => !string.IsNullOrWhiteSpace(x))) + ")";
}

public interface IGeocodificador
{
    /// <summary>Candidatos para el nombre de una ciudad (solo lugares habitados). Solo sale el nombre que escribió el usuario (X67).</summary>
    Task<IReadOnlyList<CandidatoDeCiudad>> BuscarAsync(string nombre, CancellationToken ct = default);
}

/// <summary>
/// Geocodificación de Open-Meteo: <b>solo sale el nombre de la ciudad</b> (sin coordenadas, fechas, texto ni identidad). Los resultados
/// que no son lugares habitados (aeropuertos, estaciones…) se descartan.
/// </summary>
public sealed class GeocodificadorOpenMeteo(HttpClient http, string url = "https://geocoding-api.open-meteo.com/v1/search") : IGeocodificador
{
    public string Url(string nombre) => $"{url}?name={Uri.EscapeDataString(nombre.Trim())}&count=6&language=es&format=json";

    public async Task<IReadOnlyList<CandidatoDeCiudad>> BuscarAsync(string nombre, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 60)
        {
            return [];
        }

        string json;
        try
        {
            using var r = await http.GetAsync(Url(nombre), ct);
            if (!r.IsSuccessStatusCode)
            {
                throw new ClimaNoDisponibleException($"El servicio de geocodificación respondió {(int)r.StatusCode}.");
            }

            json = await r.Content.ReadAsStringAsync(ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new ClimaNoDisponibleException("No se pudo contactar con el servicio de geocodificación.", e);
        }

        return Analizar(json);
    }

    public static IReadOnlyList<CandidatoDeCiudad> Analizar(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("results", out var lista) || lista.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            string? S(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var resultado = new List<CandidatoDeCiudad>();
            foreach (var e in lista.EnumerateArray())
            {
                var codigo = S(e, "feature_code");
                if (codigo is null || !(codigo.StartsWith("PPL", StringComparison.Ordinal) || codigo is "PCLI" or "PCLD" or "ADM3" or "ADM4"))
                {
                    continue; // aeropuertos, estaciones, puertos…: no son la ciudad
                }

                if (!e.TryGetProperty("latitude", out var la) || !e.TryGetProperty("longitude", out var lo) || !la.TryGetDouble(out var lat) || !lo.TryGetDouble(out var lon)
                    || lat is < -90 or > 90 || lon is < -180 or > 180 || S(e, "name") is not { } nombre)
                {
                    continue;
                }

                resultado.Add(new CandidatoDeCiudad(nombre, S(e, "admin1"), S(e, "country"), S(e, "country_code"), lat, lon,
                    e.TryGetProperty("population", out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var pob) ? pob : null));
            }

            return resultado;
        }
        catch (JsonException e)
        {
            throw new ClimaNoDisponibleException("La respuesta del servicio de geocodificación no es válida.", e);
        }
    }
}

/// <summary>
/// Cuándo la ciudad está «clara» y Mira puede ir a buscar el clima sin esperar (X67): un único candidato, o uno que domina
/// (los demás tienen menos del 10 % de su población). Con dudas no se supone: se pregunta.
/// </summary>
public static class ElegirCiudad
{
    public static CandidatoDeCiudad? Clara(IReadOnlyList<CandidatoDeCiudad> candidatos)
    {
        if (candidatos.Count == 0)
        {
            return null;
        }

        if (candidatos.Count == 1)
        {
            return candidatos[0];
        }

        var primero = candidatos[0];
        return primero.Poblacion is > 0 && candidatos.Skip(1).All(c => (c.Poblacion ?? 0) < primero.Poblacion.Value * 0.10) ? primero : null;
    }

    public static Ciudad ACiudad(string nombreEscrito, CandidatoDeCiudad c) => new(nombreEscrito.Trim(), Math.Round(c.Latitud, Ciudad.Decimales, MidpointRounding.AwayFromZero), Math.Round(c.Longitud, Ciudad.Decimales, MidpointRounding.AwayFromZero));

    public static string Texto(CandidatoDeCiudad c) => FormattableString.Invariant($"{c.Descripcion}");
}
