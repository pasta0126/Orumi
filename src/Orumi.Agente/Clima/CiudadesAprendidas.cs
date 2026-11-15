using System.Globalization;
using Orumi.Core.Ficheros;
using Orumi.Core.Yaml;

namespace Orumi.Agente.Clima;

public sealed record CiudadAprendida(string Clave, string Nombre, string Descripcion, double Latitud, double Longitud, string Estado, string Origen, DateTimeOffset Fecha)
{
    public const string PorConfirmar = "por_confirmar";
    public const string Confirmada = "confirmada";
    public const string Rechazada = "rechazada";

    public Ciudad ACiudad() => new(Nombre, Latitud, Longitud);
}

public sealed record NotaConCiudad(string Captura, string Ciudad, string Origen, DateTimeOffset Fecha);

public sealed record DatosDeCiudades(IReadOnlyList<CiudadAprendida> Ciudades, IReadOnlyList<NotaConCiudad> Notas, IReadOnlyList<string> Aplicadas)
{
    public static readonly DatosDeCiudades Vacio = new([], [], []);
}

/// <summary>
/// Ciudades que ORUMI ha aprendido más allá de la configuración (X62, X67): ficheros propios y editables, porque la configuración es de solo lectura.
/// Las coordenadas se guardan ya redondeadas. Una ciudad <c>rechazada</c> por el usuario no se usa. También guarda qué ciudad indicó el usuario
/// para una nota concreta cuando respondió a la pregunta de Mira (las capturas son inmutables).
/// </summary>
public static class CiudadesAprendidasStore
{
    private const string Formato = "yyyy-MM-dd'T'HH:mm:sszzz";

    public static string Ruta(string datos) => Path.Combine(datos, "clima", "ciudades.yaml");

    public static DatosDeCiudades Leer(string datos)
    {
        var ruta = Ruta(datos);
        if (!File.Exists(ruta))
        {
            return DatosDeCiudades.Vacio;
        }

        try
        {
            var m = YamlLector.LeerMapa(File.ReadAllText(ruta, System.Text.Encoding.UTF8));
            DateTimeOffset F(object? v) => DateTimeOffset.Parse((string)v!, CultureInfo.InvariantCulture);
            double N(object? v) => double.Parse((string)v!, CultureInfo.InvariantCulture);
            return new DatosDeCiudades(
                (m.GetValueOrDefault("ciudades") as List<object?> ?? []).OfType<Dictionary<string, object?>>().Select(c => new CiudadAprendida(
                    (string)c["clave"]!, (string)c["nombre"]!, (string)c["descripcion"]!, N(c["lat"]), N(c["lon"]), (string)c["estado"]!, (string)c["origen"]!, F(c["fecha"]))).ToList(),
                (m.GetValueOrDefault("notas") as List<object?> ?? []).OfType<Dictionary<string, object?>>().Select(c => new NotaConCiudad(
                    (string)c["captura"]!, (string)c["ciudad"]!, (string)c["origen"]!, F(c["fecha"]))).ToList(),
                (m.GetValueOrDefault("aplicadas") as List<object?> ?? []).OfType<string>().ToList());
        }
        catch (Exception e) when (e is FormatException or KeyNotFoundException or InvalidCastException or YamlDotNet.Core.YamlException)
        {
            return DatosDeCiudades.Vacio;
        }
    }

    public static void Guardar(string datos, DatosDeCiudades d)
    {
        var ciudades = d.Ciudades.Select(c => (YNodo)YMapa.EnLineaDe(
            ("clave", new YCitado(c.Clave)), ("nombre", new YCitado(c.Nombre)), ("descripcion", new YCitado(c.Descripcion)),
            ("lat", new YLiteral(c.Latitud.ToString("0.0", CultureInfo.InvariantCulture))), ("lon", new YLiteral(c.Longitud.ToString("0.0", CultureInfo.InvariantCulture))),
            ("estado", new YLiteral(c.Estado)), ("origen", new YLiteral(c.Origen)), ("fecha", new YInstante(c.Fecha.ToString(Formato, CultureInfo.InvariantCulture))))).ToList();
        var notas = d.Notas.Select(n => (YNodo)YMapa.EnLineaDe(
            ("captura", new YLiteral(n.Captura)), ("ciudad", new YCitado(n.Ciudad)), ("origen", new YLiteral(n.Origen)),
            ("fecha", new YInstante(n.Fecha.ToString(Formato, CultureInfo.InvariantCulture))))).ToList();
        var raiz = YMapa.De(
            ("ciudades", new YLista(ciudades, EnLinea: ciudades.Count == 0)),
            ("notas", new YLista(notas, EnLinea: notas.Count == 0)),
            ("aplicadas", new YLista([.. d.Aplicadas.Select(a => (YNodo)new YLiteral(a))], EnLinea: true)));
        var ruta = Ruta(datos);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        EscrituraAtomica.Reemplazar(ruta, YamlEscritor.Escribir(raiz));
    }
}
