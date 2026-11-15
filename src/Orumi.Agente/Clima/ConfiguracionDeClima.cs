using System.Globalization;
using System.Text;

namespace Orumi.Agente.Clima;

/// <summary>Una ciudad conocida con sus coordenadas, siempre redondeadas (spec <c>contexto-meteorologico</c>).</summary>
public sealed record Ciudad(string Nombre, double Latitud, double Longitud)
{
    /// <summary>Resolución de unos 10 km: es lo único que sale hacia el servicio meteorológico (X61).</summary>
    public const int Decimales = 1;

    public double LatitudRedondeada => Math.Round(Latitud, Decimales, MidpointRounding.AwayFromZero);

    public double LongitudRedondeada => Math.Round(Longitud, Decimales, MidpointRounding.AwayFromZero);

    /// <summary>Nombre sin acentos ni mayúsculas, para comparar y para nombrar ficheros.</summary>
    public string Clave => Normalizar(Nombre);

    public static string Normalizar(string nombre)
    {
        var descompuesto = nombre.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
        }

        return string.Join('-', sb.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}

public sealed record Umbrales(double UvAlto, double LluviaMmDia, double NubosidadDespejadoMax, double SolDespejadoMinPct);

public sealed record HorarioLaboral(TimeOnly Desde, TimeOnly Hasta, IReadOnlyList<DayOfWeek> Dias);

/// <summary>
/// Configuración del contexto meteorológico. Su sola presencia en <c>orumi.yaml</c> activa la consulta;
/// sin ella, Mira no contacta con ningún servicio meteorológico (X61).
/// </summary>
public sealed record ConfiguracionDeClima(
    string Fuente,
    DateOnly? CondicionesVerificadasEl,
    string UrlPrevision,
    string UrlHistorico,
    string CiudadPorDefecto,
    IReadOnlyList<Ciudad> Ciudades,
    string ZonaHoraria,
    Umbrales Umbrales,
    HorarioLaboral Laboral,
    IReadOnlyDictionary<string, string>? CiudadPorCaptura = null)
{
    public const string FuentePorDefecto = "open-meteo";

    public static readonly IReadOnlyList<Ciudad> CiudadesIniciales =
    [
        new("Barcelona", 41.4, 2.2),
        new("Segur de Calafell", 41.3, 1.6),
    ];

    public static readonly Umbrales UmbralesIniciales = new(UvAlto: 6, LluviaMmDia: 1, NubosidadDespejadoMax: 30, SolDespejadoMinPct: 60);

    public static readonly HorarioLaboral LaboralInicial =
        new(new TimeOnly(8, 30), new TimeOnly(18, 30), [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);

    public Ciudad PorDefecto => Buscar(CiudadPorDefecto) ?? CiudadesIniciales[0];

    /// <summary>Ciudad indicada por el usuario → conocida, o <c>null</c> si no se reconoce (Mira preguntará, no supondrá).</summary>
    public Ciudad? Buscar(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return null;
        }

        var clave = Ciudad.Normalizar(nombre);
        return Ciudades.FirstOrDefault(c => c.Clave == clave);
    }

    /// <summary>
    /// Suma las ciudades que ORUMI ha aprendido (no rechazadas) y las ciudades que el usuario indicó para notas concretas al responder a Mira.
    /// Lo de la configuración manda sobre lo aprendido.
    /// </summary>
    public ConfiguracionDeClima ConAprendidas(string datos)
    {
        var d = CiudadesAprendidasStore.Leer(datos);
        var nuevas = d.Ciudades.Where(c => c.Estado != CiudadAprendida.Rechazada && Ciudades.All(x => x.Clave != c.Clave)).Select(c => c.ACiudad()).ToList();
        return this with
        {
            Ciudades = [.. Ciudades, .. nuevas],
            CiudadPorCaptura = d.Notas.GroupBy(n => n.Captura).ToDictionary(g => g.Key, g => g.Last().Ciudad),
        };
    }

    /// <summary>La misma configuración con las ciudades que el usuario fijó para notas concretas (se usa mientras se resuelven respuestas).</summary>
    public ConfiguracionDeClima ConNotas(IEnumerable<NotaConCiudad> notas) =>
        this with { CiudadPorCaptura = notas.GroupBy(n => n.Captura).ToDictionary(g => g.Key, g => g.Last().Ciudad) };

    /// <summary>Ciudad de una captura concreta: la que el usuario fijó al responder a Mira, o la indicada en la captura, o la de por defecto.</summary>
    public ResolucionDeCiudad ResolverDeCaptura(string? capturaId, string? ciudadDeLaCaptura) =>
        capturaId is not null && CiudadPorCaptura?.GetValueOrDefault(capturaId) is { } fijada ? Resolver(fijada) : Resolver(ciudadDeLaCaptura);

    /// <summary>Ciudad de una captura: la indicada por el usuario o, si no indicó ninguna, la de por defecto.</summary>
    public ResolucionDeCiudad Resolver(string? ciudadDeLaCaptura) =>
        string.IsNullOrWhiteSpace(ciudadDeLaCaptura)
            ? new ResolucionDeCiudad(PorDefecto, null)
            : Buscar(ciudadDeLaCaptura) is { } c ? new ResolucionDeCiudad(c, null) : new ResolucionDeCiudad(null, ciudadDeLaCaptura.Trim());

    public static ConfiguracionDeClima? Leer(Dictionary<string, object?>? mapa)
    {
        if (mapa is null)
        {
            return null;
        }

        string Texto(Dictionary<string, object?> m, string k, string d) => m.GetValueOrDefault(k) is string s && s.Length > 0 ? s : d;
        Dictionary<string, object?> Sub(Dictionary<string, object?> m, string k) => m.GetValueOrDefault(k) as Dictionary<string, object?> ?? [];
        double Num(Dictionary<string, object?> m, string k, double d) =>
            double.TryParse(m.GetValueOrDefault(k) as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : d;

        var ciudades = new List<Ciudad>();
        foreach (var (nombre, valor) in Sub(mapa, "ciudades"))
        {
            if (valor is Dictionary<string, object?> c
                && double.TryParse(c.GetValueOrDefault("lat") as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                && double.TryParse(c.GetValueOrDefault("lon") as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)
                && lat is >= -90 and <= 90 && lon is >= -180 and <= 180)
            {
                ciudades.Add(new Ciudad(nombre, lat, lon));
            }
        }

        foreach (var inicial in CiudadesIniciales)
        {
            if (ciudades.All(c => c.Clave != inicial.Clave))
            {
                ciudades.Add(inicial);
            }
        }

        var u = Sub(mapa, "umbrales");
        var l = Sub(mapa, "horario_laboral");
        var ini = UmbralesIniciales;
        var verificada = DateOnly.TryParseExact(mapa.GetValueOrDefault("condiciones_verificadas_el") as string, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : (DateOnly?)null;

        return new ConfiguracionDeClima(
            Texto(mapa, "fuente", FuentePorDefecto),
            verificada,
            Texto(mapa, "url_prevision", "https://api.open-meteo.com/v1/forecast"),
            Texto(mapa, "url_historico", "https://archive-api.open-meteo.com/v1/archive"),
            Texto(mapa, "ciudad_por_defecto", "Barcelona"),
            ciudades,
            Texto(mapa, "zona_horaria", "Europe/Madrid"),
            new Umbrales(Num(u, "uv_alto", ini.UvAlto), Num(u, "lluvia_mm_dia", ini.LluviaMmDia), Num(u, "nubosidad_despejado_max", ini.NubosidadDespejadoMax), Num(u, "sol_despejado_min_pct", ini.SolDespejadoMinPct)),
            new HorarioLaboral(
                TimeOnly.TryParseExact(l.GetValueOrDefault("desde") as string, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : LaboralInicial.Desde,
                TimeOnly.TryParseExact(l.GetValueOrDefault("hasta") as string, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var h) ? h : LaboralInicial.Hasta,
                LaboralInicial.Dias));
    }
}

/// <summary>Ciudad resuelta de una captura, o el nombre sin reconocer (Mira preguntará qué ciudad es).</summary>
public sealed record ResolucionDeCiudad(Ciudad? Ciudad, string? NoReconocida)
{
    public bool Reconocida => Ciudad is not null;
}
