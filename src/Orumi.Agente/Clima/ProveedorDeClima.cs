using System.Globalization;
using System.Text.Json;

namespace Orumi.Agente.Clima;

public interface IProveedorDeClima
{
    /// <summary>Clima de cada día de <paramref name="desde"/> a <paramref name="hasta"/> (ambos incluidos) en una ciudad.</summary>
    Task<IReadOnlyList<ClimaDia>> ObtenerAsync(Ciudad ciudad, DateOnly desde, DateOnly hasta, DateOnly hoy, DateTimeOffset ahora, CancellationToken cancelacion = default);
}

/// <summary>Fallo al consultar el servicio meteorológico: nunca debe impedir el procesamiento de capturas.</summary>
public sealed class ClimaNoDisponibleException(string mensaje, Exception? interna = null) : Exception(mensaje, interna);

/// <summary>
/// Open-Meteo (X60): la previsión con <c>past_days</c> (hasta 92) trae también el índice UV; el histórico no.
/// Solo salen coordenadas redondeadas, fechas y nombres de variables (X61): ni texto, ni identidad, ni cabeceras propias.
/// </summary>
public sealed class ProveedorOpenMeteo(HttpClient http, ConfiguracionDeClima configuracion) : IProveedorDeClima
{
    /// <summary>Días hacia atrás que admite la previsión (<c>past_days</c>).</summary>
    public const int VentanaDePrevision = 92;

    private const string HorariasComunes = "temperature_2m,relative_humidity_2m,precipitation,cloud_cover,shortwave_radiation,sunshine_duration";
    private const string DiariasComunes = "sunrise,sunset,daylight_duration,sunshine_duration,precipitation_sum";

    public async Task<IReadOnlyList<ClimaDia>> ObtenerAsync(Ciudad ciudad, DateOnly desde, DateOnly hasta, DateOnly hoy, DateTimeOffset ahora, CancellationToken cancelacion = default)
    {
        var resultado = new List<ClimaDia>();
        var limiteReciente = hoy.AddDays(-VentanaDePrevision);

        if (hasta >= limiteReciente)
        {
            var inicio = desde > limiteReciente ? desde : limiteReciente;
            var url = UrlDePrevision(ciudad, hoy.DayNumber - inicio.DayNumber);
            resultado.AddRange(Analizar(await Descargar(url, cancelacion), ciudad, desde > limiteReciente ? desde : limiteReciente, hasta, ahora, conUv: true));
        }

        if (desde < limiteReciente)
        {
            var fin = hasta < limiteReciente ? hasta : limiteReciente.AddDays(-1);
            resultado.AddRange(Analizar(await Descargar(UrlDeHistorico(ciudad, desde, fin), cancelacion), ciudad, desde, fin, ahora, conUv: false));
        }

        return resultado;
    }

    public string UrlDePrevision(Ciudad ciudad, int diasAtras) =>
        $"{configuracion.UrlPrevision}?{Ubicacion(ciudad)}&hourly={HorariasComunes},uv_index&daily={DiariasComunes},uv_index_max" +
        $"&timezone={Uri.EscapeDataString(configuracion.ZonaHoraria)}&past_days={Math.Clamp(diasAtras, 0, VentanaDePrevision)}&forecast_days=1";

    public string UrlDeHistorico(Ciudad ciudad, DateOnly desde, DateOnly hasta) =>
        $"{configuracion.UrlHistorico}?{Ubicacion(ciudad)}&start_date={desde:yyyy-MM-dd}&end_date={hasta:yyyy-MM-dd}" +
        $"&hourly={HorariasComunes}&daily={DiariasComunes}&timezone={Uri.EscapeDataString(configuracion.ZonaHoraria)}";

    private static string Ubicacion(Ciudad c) =>
        FormattableString.Invariant($"latitude={c.LatitudRedondeada:0.0}&longitude={c.LongitudRedondeada:0.0}");

    private async Task<string> Descargar(string url, CancellationToken cancelacion)
    {
        try
        {
            using var r = await http.GetAsync(url, cancelacion);
            if (!r.IsSuccessStatusCode)
            {
                throw new ClimaNoDisponibleException($"El servicio meteorológico respondió {(int)r.StatusCode}.");
            }

            return await r.Content.ReadAsStringAsync(cancelacion);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new ClimaNoDisponibleException("No se pudo contactar con el servicio meteorológico.", e);
        }
    }

    /// <summary>Convierte la respuesta JSON en climas diarios; lo que falta o viene vacío queda desconocido.</summary>
    public IReadOnlyList<ClimaDia> Analizar(string json, Ciudad ciudad, DateOnly desde, DateOnly hasta, DateTimeOffset ahora, bool conUv)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new ClimaNoDisponibleException("La respuesta del servicio meteorológico no es válida.", e);
        }

        using (doc)
        {
            var raiz = doc.RootElement;
            if (!raiz.TryGetProperty("hourly", out var horario) || !horario.TryGetProperty("time", out var tiempos))
            {
                throw new ClimaNoDisponibleException("La respuesta del servicio meteorológico no incluye datos horarios.");
            }

            raiz.TryGetProperty("daily", out var diario);
            var porDia = new Dictionary<DateOnly, ClimaHora[]>();
            var n = tiempos.GetArrayLength();
            for (var i = 0; i < n; i++)
            {
                var t = tiempos[i].GetString();
                if (t is null || t.Length < 13 || !DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out var momento))
                {
                    continue;
                }

                var dia = DateOnly.FromDateTime(momento);
                if (dia < desde || dia > hasta)
                {
                    continue;
                }

                if (!porDia.TryGetValue(dia, out var horas))
                {
                    porDia[dia] = horas = new ClimaHora[24];
                }

                horas[momento.Hour] = new ClimaHora(
                    momento.Hour, Valor(horario, "temperature_2m", i), Valor(horario, "relative_humidity_2m", i), Valor(horario, "precipitation", i),
                    Valor(horario, "cloud_cover", i), Valor(horario, "shortwave_radiation", i), Valor(horario, "sunshine_duration", i),
                    conUv ? Valor(horario, "uv_index", i) : null);
            }

            var resultado = new List<ClimaDia>();
            foreach (var (dia, horas) in porDia.OrderBy(p => p.Key))
            {
                var completas = Enumerable.Range(0, 24).Select(h => horas[h] ?? new ClimaHora(h, null, null, null, null, null, null, null)).ToList();
                var idx = Indice(diario, dia);
                var resumen = new ResumenDelDia(
                    conUv ? ValorDiario(diario, "uv_index_max", idx) : null,
                    HoraDelDia(diario, "sunrise", idx), HoraDelDia(diario, "sunset", idx),
                    ValorDiario(diario, "daylight_duration", idx), ValorDiario(diario, "sunshine_duration", idx), ValorDiario(diario, "precipitation_sum", idx),
                    Estadistica(completas.Select(h => h.Temperatura), Enumerable.Min), Estadistica(completas.Select(h => h.Temperatura), Enumerable.Max),
                    Estadistica(completas.Select(h => h.Humedad), Enumerable.Average), Estadistica(completas.Select(h => h.Nubosidad), Enumerable.Average));
                resultado.Add(new ClimaDia(dia, ciudad.Nombre, configuracion.Fuente, ciudad.LatitudRedondeada, ciudad.LongitudRedondeada, configuracion.ZonaHoraria, ahora, resumen, completas));
            }

            return resultado;
        }
    }

    private static double? Valor(JsonElement bloque, string nombre, int i) =>
        bloque.ValueKind == JsonValueKind.Object && bloque.TryGetProperty(nombre, out var serie) && serie.ValueKind == JsonValueKind.Array && i < serie.GetArrayLength()
        && serie[i].ValueKind == JsonValueKind.Number ? serie[i].GetDouble() : null;

    private static int Indice(JsonElement diario, DateOnly dia)
    {
        if (diario.ValueKind != JsonValueKind.Object || !diario.TryGetProperty("time", out var t))
        {
            return -1;
        }

        var clave = dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        for (var i = 0; i < t.GetArrayLength(); i++)
        {
            if (t[i].GetString() == clave)
            {
                return i;
            }
        }

        return -1;
    }

    private static double? ValorDiario(JsonElement diario, string nombre, int idx) => idx < 0 ? null : Valor(diario, nombre, idx);

    private static string? HoraDelDia(JsonElement diario, string nombre, int idx)
    {
        if (idx < 0 || diario.ValueKind != JsonValueKind.Object || !diario.TryGetProperty(nombre, out var s) || idx >= s.GetArrayLength())
        {
            return null;
        }

        var texto = s[idx].GetString();
        return texto is { Length: >= 16 } ? texto[11..16] : null;
    }

    private static double? Estadistica(IEnumerable<double?> valores, Func<IEnumerable<double>, double> f)
    {
        var conocidos = valores.Where(v => v is not null).Select(v => v!.Value).ToList();
        return conocidos.Count == 0 ? null : f(conocidos);
    }
}
