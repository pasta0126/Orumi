using System.Globalization;
using Orumi.Core.Ficheros;

namespace Orumi.Agente.Clima;

public sealed record DiaYCiudad(DateOnly Fecha, Ciudad Ciudad);

public sealed record CiudadSinReconocer(string Captura, string Ciudad, string? Registro = null);

public sealed record PlanDeClima(IReadOnlyList<DiaYCiudad> Necesarios, IReadOnlyList<CiudadSinReconocer> SinReconocer);

public sealed record ResultadoDeClima(int Obtenidos, int YaGuardados, IReadOnlyList<CiudadSinReconocer> SinReconocer, IReadOnlyList<string> Fallos, ResultadoDeCiudades? Ciudades = null);

/// <summary>Qué días y ciudades necesitan clima, y su obtención. Nunca bloquea el procesamiento (spec <c>contexto-meteorologico</c>).</summary>
public static class ObtencionDeClima
{
    /// <summary>
    /// Un día de clima por cada fecha exacta de un registro activo (desde = hasta) y la ciudad de su captura de origen
    /// (la indicada por el usuario o la de por defecto). Una ciudad que no se reconoce no se supone: se devuelve para preguntar.
    /// </summary>
    public static PlanDeClima Planificar(Almacen almacen, ConfiguracionDeClima config)
    {
        var capturas = almacen.Documentos.Where(d => d.Tipo == "captura" && d.Id is not null).ToDictionary(d => d.Id!);
        var necesarios = new HashSet<(DateOnly, string)>();
        var lista = new List<DiaYCiudad>();
        var sin = new Dictionary<(string, string), CiudadSinReconocer>();

        foreach (var r in almacen.Documentos.Where(d => d.Tipo == "registro"))
        {
            if (r.Frontmatter.GetValueOrDefault("estado") is "eliminado" or "fusionado")
            {
                continue;
            }

            if (r.Frontmatter.GetValueOrDefault("fecha") is not Dictionary<string, object?> f
                || f.GetValueOrDefault("desde") is not string desde
                || desde != f.GetValueOrDefault("hasta") as string
                || !DateOnly.TryParseExact(desde, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia))
            {
                continue;
            }

            var captura = r.Frontmatter.GetValueOrDefault("fuentes") is IEnumerable<object?> fuentes
                ? fuentes.OfType<Dictionary<string, object?>>().FirstOrDefault()?.GetValueOrDefault("captura") as string
                : null;
            var ciudadDicha = captura is not null && capturas.TryGetValue(captura, out var c) ? c.Frontmatter.GetValueOrDefault("ciudad") as string : null;
            var res = config.ResolverDeCaptura(captura, ciudadDicha);
            if (res.Ciudad is { } ciudad)
            {
                if (necesarios.Add((dia, ciudad.Clave)))
                {
                    lista.Add(new DiaYCiudad(dia, ciudad));
                }
            }
            else if (captura is not null)
            {
                sin[(captura, res.NoReconocida!)] = new CiudadSinReconocer(captura, res.NoReconocida!, r.Id);
            }
        }

        return new PlanDeClima([.. lista.OrderBy(x => x.Fecha).ThenBy(x => x.Ciudad.Clave)], [.. sin.Values.OrderBy(s => s.Captura)]);
    }

    /// <summary>Si la ciudad se corrigió (otras coordenadas), el clima guardado era de otro sitio y se vuelve a pedir.</summary>
    internal static bool MismaUbicacion(ClimaDia guardado, Ciudad ciudad) =>
        Math.Abs(guardado.Latitud - ciudad.LatitudRedondeada) < 0.05 && Math.Abs(guardado.Longitud - ciudad.LongitudRedondeada) < 0.05;

    /// <summary>Un día se vuelve a pedir si se guardó el mismo día (los datos de las últimas horas cambian).</summary>
    internal static bool Incompleto(ClimaDia guardado) => DateOnly.FromDateTime(guardado.Obtenido.Date) <= guardado.Fecha;

    public static async Task<ResultadoDeClima> EjecutarAsync(
        string raizDatos, Almacen almacen, ConfiguracionDeClima config, IProveedorDeClima proveedor,
        DateOnly hoy, DateTimeOffset ahora, CancellationToken cancelacion = default, IGeocodificador? geocodificador = null, int cupoDePreguntas = 5)
    {
        // Ciudades aprendidas y respuestas del usuario (X67): con un geocodificador, Mira ubica las ciudades que no conoce y pregunta.
        config = config.ConAprendidas(raizDatos);
        ResultadoDeCiudades? ciudades = null;
        if (geocodificador is not null)
        {
            (config, ciudades) = await ResolutorDeCiudades.ResolverAsync(raizDatos, config, geocodificador, cupoDePreguntas, ahora, cancelacion);
            almacen = Almacen.Cargar(raizDatos);
        }

        var plan = Planificar(almacen, config);
        var pendientes = new List<DiaYCiudad>();
        var yaGuardados = 0;
        foreach (var necesario in plan.Necesarios)
        {
            var ruta = FormatoDeClima.Ruta(raizDatos, necesario.Fecha, necesario.Ciudad);
            if (File.Exists(ruta) && Guardado(ruta) is { } g && !Incompleto(g) && MismaUbicacion(g, necesario.Ciudad))
            {
                yaGuardados++;
                continue;
            }

            pendientes.Add(necesario);
        }

        var obtenidos = 0;
        var fallos = new List<string>();
        foreach (var grupo in pendientes.GroupBy(p => p.Ciudad.Clave))
        {
            var ciudad = grupo.First().Ciudad;
            var fechas = grupo.Select(p => p.Fecha).Order().ToList();
            try
            {
                var dias = await proveedor.ObtenerAsync(ciudad, fechas[0], fechas[^1], hoy, ahora, cancelacion);
                foreach (var dia in dias.Where(d => fechas.Contains(d.Fecha)))
                {
                    var ruta = FormatoDeClima.Ruta(raizDatos, dia.Fecha, ciudad);
                    Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
                    EscrituraAtomica.Reemplazar(ruta, FormatoDeClima.Escribir(dia));
                    obtenidos++;
                }
            }
            catch (ClimaNoDisponibleException e)
            {
                // El día queda pendiente y se reintenta en la siguiente ejecución.
                fallos.Add($"{ciudad.Nombre}: {e.Message}");
            }
        }

        return new ResultadoDeClima(obtenidos, yaGuardados, plan.SinReconocer, fallos, ciudades);
    }

    private static ClimaDia? Guardado(string ruta)
    {
        try
        {
            return FormatoDeClima.Leer(File.ReadAllText(ruta, System.Text.Encoding.UTF8));
        }
        catch (Exception e) when (e is FormatException or KeyNotFoundException or InvalidCastException or YamlDotNet.Core.YamlException)
        {
            return null; // fichero dañado: derivado y regenerable, se vuelve a pedir
        }
    }
}
