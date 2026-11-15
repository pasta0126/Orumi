using System.Globalization;
using Orumi.Core.Yaml;

namespace Orumi.Agente.Clima;

/// <summary>Condiciones de una hora. Un valor que el servicio no ofrece es <c>null</c> (desconocido): nunca se estima.</summary>
public sealed record ClimaHora(int Hora, double? Temperatura, double? Humedad, double? Precipitacion, double? Nubosidad, double? Radiacion, double? SolSegundos, double? Uv);

public sealed record ResumenDelDia(
    double? UvMaximo, string? Amanecer, string? Atardecer, double? LuzSegundos, double? SolSegundos,
    double? PrecipitacionMm, double? TemperaturaMinima, double? TemperaturaMaxima, double? HumedadMedia, double? NubosidadMedia);

/// <summary>
/// Clima de un día en una ciudad: contexto derivado y regenerable, nunca parte de una captura ni de un registro
/// (spec <c>contexto-meteorologico</c>).
/// </summary>
public sealed record ClimaDia(
    DateOnly Fecha, string Ciudad, string Fuente, double Latitud, double Longitud, string ZonaHoraria,
    DateTimeOffset Obtenido, ResumenDelDia Dia, IReadOnlyList<ClimaHora> Horas)
{
    public bool TieneUv => Dia.UvMaximo is not null || Horas.Any(h => h.Uv is not null);
}

/// <summary>Formato de <c>clima/AAAA/MM/AAAA-MM-DD-ciudad.yaml</c> (ver <c>docs/formato-de-ficheros.md</c>).</summary>
public static class FormatoDeClima
{
    public const string Desconocido = "desconocido";

    public static string Ruta(string raizDatos, DateOnly fecha, Ciudad ciudad) =>
        Path.Combine(raizDatos, "clima", fecha.ToString("yyyy", CultureInfo.InvariantCulture), fecha.ToString("MM", CultureInfo.InvariantCulture),
            $"{fecha:yyyy-MM-dd}-{ciudad.Clave}.yaml");

    private static YNodo N(double? v) =>
        v is null ? new YLiteral(Desconocido) : new YLiteral(Math.Round(v.Value, 3).ToString("0.###", CultureInfo.InvariantCulture));

    private static YNodo T(string? v) => v is null ? new YLiteral(Desconocido) : new YCitado(v);

    public static string Escribir(ClimaDia d)
    {
        var dia = d.Dia;
        var horas = d.Horas.Select(h => (YNodo)YMapa.EnLineaDe(
            ("h", new YLiteral(h.Hora.ToString(CultureInfo.InvariantCulture))),
            ("temperatura", N(h.Temperatura)), ("humedad", N(h.Humedad)), ("precipitacion", N(h.Precipitacion)),
            ("nubosidad", N(h.Nubosidad)), ("radiacion", N(h.Radiacion)), ("sol_s", N(h.SolSegundos)), ("uv", N(h.Uv)))).ToList();
        var raiz = YMapa.De(
            ("fecha", new YLiteral(d.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))),
            ("ciudad", new YTexto(d.Ciudad)),
            ("fuente", new YTexto(d.Fuente)),
            ("ubicacion", YMapa.EnLineaDe(("lat", N(d.Latitud)), ("lon", N(d.Longitud)))),
            ("zona_horaria", new YTexto(d.ZonaHoraria)),
            ("obtenido", new YInstante(d.Obtenido.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture))),
            ("dia", YMapa.De(
                ("uv_maximo", N(dia.UvMaximo)), ("amanecer", T(dia.Amanecer)), ("atardecer", T(dia.Atardecer)),
                ("luz_s", N(dia.LuzSegundos)), ("sol_s", N(dia.SolSegundos)), ("precipitacion_mm", N(dia.PrecipitacionMm)),
                ("temperatura_minima", N(dia.TemperaturaMinima)), ("temperatura_maxima", N(dia.TemperaturaMaxima)),
                ("humedad_media", N(dia.HumedadMedia)), ("nubosidad_media", N(dia.NubosidadMedia)))),
            ("horas", new YLista(horas)));
        return YamlEscritor.Escribir(raiz);
    }

    public static ClimaDia Leer(string yaml)
    {
        var r = YamlLector.LeerMapa(yaml);
        double? Num(object? v) => v is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
        string? Txt(object? v) => v is string s && s != Desconocido ? s : null;
        var dia = r.GetValueOrDefault("dia") as Dictionary<string, object?> ?? [];
        var ub = r.GetValueOrDefault("ubicacion") as Dictionary<string, object?> ?? [];
        var horas = (r.GetValueOrDefault("horas") as IEnumerable<object?> ?? [])
            .OfType<Dictionary<string, object?>>()
            .Select(h => new ClimaHora(
                (int)(Num(h.GetValueOrDefault("h")) ?? 0), Num(h.GetValueOrDefault("temperatura")), Num(h.GetValueOrDefault("humedad")),
                Num(h.GetValueOrDefault("precipitacion")), Num(h.GetValueOrDefault("nubosidad")), Num(h.GetValueOrDefault("radiacion")),
                Num(h.GetValueOrDefault("sol_s")), Num(h.GetValueOrDefault("uv"))))
            .ToList();
        return new ClimaDia(
            DateOnly.ParseExact((string)r["fecha"]!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            (string)r["ciudad"]!, (string)r["fuente"]!, Num(ub.GetValueOrDefault("lat")) ?? 0, Num(ub.GetValueOrDefault("lon")) ?? 0,
            (string)r["zona_horaria"]!,
            DateTimeOffset.Parse((string)r["obtenido"]!, CultureInfo.InvariantCulture),
            new ResumenDelDia(
                Num(dia.GetValueOrDefault("uv_maximo")), Txt(dia.GetValueOrDefault("amanecer")), Txt(dia.GetValueOrDefault("atardecer")),
                Num(dia.GetValueOrDefault("luz_s")), Num(dia.GetValueOrDefault("sol_s")), Num(dia.GetValueOrDefault("precipitacion_mm")),
                Num(dia.GetValueOrDefault("temperatura_minima")), Num(dia.GetValueOrDefault("temperatura_maxima")),
                Num(dia.GetValueOrDefault("humedad_media")), Num(dia.GetValueOrDefault("nubosidad_media"))),
            horas);
    }
}
