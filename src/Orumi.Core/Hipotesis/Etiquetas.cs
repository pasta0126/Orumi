using System.Globalization;
using System.Text.RegularExpressions;
using Orumi.Core.Ficheros;
using Orumi.Core.Yaml;

namespace Orumi.Core.Hipotesis;

/// <summary>
/// Un concepto reconocido en un registro: solo lo que el usuario dijo. <c>presente</c> si lo dijo, <c>negado</c> si dijo
/// expresamente que no ocurrió y <c>quitado</c> si el usuario descarta la etiqueta de Mira. Lo que no se menciona no se etiqueta.
/// </summary>
public sealed record Etiqueta(string Concepto, string Estado, string Origen, string? Cita, int? Minutos)
{
    public const string Presente = "presente";
    public const string Negado = "negado";
    public const string Quitado = "quitado";
}

public sealed record EtiquetasDeRegistro(
    string Registro, string Entrada, DateTimeOffset Generada, string? Proveedor, string? Modelo, IReadOnlyList<Etiqueta> Etiquetas);

/// <summary>
/// Etiquetas derivadas (<c>analisis/etiquetas/AAAA/MM/reg-….yaml</c>): regenerables, con certeza «interpretación no verificada».
/// Lo que corrige el usuario (<c>origen: usuario</c>) prevalece y se respeta al regenerar.
/// </summary>
public static class AlmacenDeEtiquetas
{
    public const string Carpeta = "analisis/etiquetas";
    private const string Formato = "yyyy-MM-dd'T'HH:mm:sszzz";
    private static readonly Regex RegistroRx = new(@"^reg-(\d{4})(\d{2})\d{2}-[a-z0-9]{3,12}$", RegexOptions.Compiled);

    public static bool EsRegistroValido(string id) => RegistroRx.IsMatch(id);

    public static string Ruta(string datos, string registro)
    {
        var m = RegistroRx.Match(registro);
        if (!m.Success)
        {
            throw new ArgumentException("Identificador de registro no válido.", nameof(registro));
        }

        return Path.Combine(datos, "analisis", "etiquetas", m.Groups[1].Value, m.Groups[2].Value, registro + ".yaml");
    }

    public static EtiquetasDeRegistro? Leer(string datos, string registro)
    {
        if (!RegistroRx.IsMatch(registro) || !File.Exists(Ruta(datos, registro)))
        {
            return null;
        }

        try
        {
            var m = YamlLector.LeerMapa(File.ReadAllText(Ruta(datos, registro), System.Text.Encoding.UTF8));
            return new EtiquetasDeRegistro(
                (string)m["registro"]!, (string)m["entrada"]!, DateTimeOffset.Parse((string)m["generada_el"]!, CultureInfo.InvariantCulture),
                m.GetValueOrDefault("proveedor") as string, m.GetValueOrDefault("modelo") as string,
                (m.GetValueOrDefault("etiquetas") as List<object?> ?? []).OfType<Dictionary<string, object?>>().Select(e => new Etiqueta(
                    (string)e["concepto"]!, (string)e["estado"]!, (string)e["origen"]!,
                    e.GetValueOrDefault("cita") as string is { } c && c != "ninguna" ? c : null,
                    int.TryParse(e.GetValueOrDefault("minutos") as string, CultureInfo.InvariantCulture, out var min) ? min : null)).ToList());
        }
        catch (Exception e) when (e is FormatException or KeyNotFoundException or InvalidCastException or YamlDotNet.Core.YamlException)
        {
            return null; // derivado y regenerable
        }
    }

    public static IReadOnlyList<EtiquetasDeRegistro> LeerTodas(string datos)
    {
        var carpeta = Path.Combine(datos, "analisis", "etiquetas");
        if (!Directory.Exists(carpeta))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(carpeta, "reg-*.yaml", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            .Select(f => Leer(datos, Path.GetFileNameWithoutExtension(f))).OfType<EtiquetasDeRegistro>()];
    }

    public static void Guardar(string datos, EtiquetasDeRegistro e)
    {
        var etiquetas = e.Etiquetas.Select(t => (YNodo)YMapa.EnLineaDe(
            ("concepto", new YLiteral(t.Concepto)), ("estado", new YLiteral(t.Estado)), ("origen", new YLiteral(t.Origen)),
            ("cita", t.Cita is null ? new YLiteral("ninguna") : new YCitado(t.Cita)),
            ("minutos", t.Minutos is null ? new YLiteral("desconocido") : new YLiteral(t.Minutos.Value.ToString(CultureInfo.InvariantCulture))))).ToList();
        var pares = new List<(string, YNodo)>
        {
            ("registro", new YLiteral(e.Registro)),
            ("tipo", new YLiteral("etiquetas")),
            ("entrada", new YCitado(e.Entrada)),
            ("generada_el", new YInstante(e.Generada.ToString(Formato, CultureInfo.InvariantCulture))),
        };
        if (e.Proveedor is not null)
        {
            pares.Add(("proveedor", new YLiteral(e.Proveedor)));
        }

        if (e.Modelo is not null)
        {
            pares.Add(("modelo", new YLiteral(e.Modelo)));
        }

        pares.Add(("certeza", new YLiteral("interpretado")));
        pares.Add(("etiquetas", new YLista(etiquetas)));
        var ruta = Ruta(datos, e.Registro);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        EscrituraAtomica.Reemplazar(ruta, YamlEscritor.Escribir(YMapa.De([.. pares])));
    }

    /// <summary>
    /// El usuario corrige una etiqueta (<c>presente</c>, <c>negado</c> o <c>quitar</c>): su palabra prevalece y no se
    /// pisa al regenerar. Crea el fichero si el registro aún no se había etiquetado.
    /// </summary>
    public static EtiquetasDeRegistro Corregir(string datos, string registro, string concepto, string estado, DateTimeOffset ahora)
    {
        if (!RegistroRx.IsMatch(registro))
        {
            throw new HipotesisInvalidaException("Identificador de registro no válido.");
        }

        if (!Regex.IsMatch(concepto, @"^[a-z][a-z0-9_]{1,40}$"))
        {
            throw new HipotesisInvalidaException("Concepto no válido.");
        }

        var nuevoEstado = estado switch
        {
            "presente" => Etiqueta.Presente,
            "negado" => Etiqueta.Negado,
            "quitar" => Etiqueta.Quitado,
            _ => throw new HipotesisInvalidaException("El estado debe ser presente, negado o quitar."),
        };
        var actual = Leer(datos, registro) ?? new EtiquetasDeRegistro(registro, "usuario", ahora, null, null, []);
        var resto = actual.Etiquetas.Where(t => t.Concepto != concepto).ToList();
        var previa = actual.Etiquetas.FirstOrDefault(t => t.Concepto == concepto);
        resto.Add(new Etiqueta(concepto, nuevoEstado, "usuario", previa?.Cita, previa?.Minutos));
        var nueva = actual with { Etiquetas = [.. resto.OrderBy(t => t.Concepto, StringComparer.Ordinal)] };
        Guardar(datos, nueva);
        return nueva;
    }
}
