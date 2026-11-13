using Orumi.Agente;
using Orumi.Agente.Proveedores;
using Orumi.Core.Capturas;
using Orumi.Core.Ficheros;
using Orumi.Core.Validacion;

namespace Orumi.Tests;

/// <summary>Entorno de prueba de Mira: directorio de datos temporal, reloj controlado y proveedor simulado.</summary>
internal sealed class EntornoMira : IDisposable
{
    public string Datos { get; } = Path.Combine(Path.GetTempPath(), "orumi-mira-" + Guid.NewGuid().ToString("N")[..8]);

    public DateTimeOffset Ahora { get; set; } = new(2026, 11, 16, 9, 0, 0, TimeSpan.FromHours(1));

    public Func<SolicitudExtraccion, RespuestaExtraccion> Respuesta { get; set; } = _ => RespuestaExtraccion.SinHechos;

    public ProveedorSimulado Proveedor { get; }

    public OpcionesProcesador Opciones { get; set; } = new();

    public EntornoMira()
    {
        Directory.CreateDirectory(Datos);
        Proveedor = new ProveedorSimulado(s => Respuesta(s));
    }

    public void Dispose() => Directory.Delete(Datos, true);

    public CapturaCreada Capturar(string texto, DateTimeOffset? cuando = null, string? fechaAcontecimiento = null, string? etiqueta = null) =>
        CreadorDeCapturas.Crear(Datos, texto, cuando ?? Ahora, fechaAcontecimiento: fechaAcontecimiento, etiqueta: etiqueta);

    public Task<ResultadoEjecucion> Procesar(Action<string>? hito = null, Configuracion? configuracion = null, IProveedorIA? proveedor = null, string ejecucion = "peticion", IReadOnlyCollection<string>? reprocesar = null) =>
        new Procesador(Datos, proveedor ?? Proveedor, Opciones, () => Ahora, configuracion).EjecutarAsync(ejecucion, hito: hito, reprocesar: reprocesar);

    public Almacen Cargar() => Almacen.Cargar(Datos);

    public IReadOnlyList<Incidencia> Validar() => Validador.Validar(Cargar());

    public Documento Doc(string id) => Cargar().Documentos.Single(d => d.Id == id);

    public List<Documento> Registros() => Cargar().DeTipo("registro").ToList();

    public List<Documento> Preguntas() => Cargar().DeTipo("pregunta").ToList();

    public string Leer(string id) => File.ReadAllText(Path.Combine(Datos, Doc(id).Ruta));

    public void Escribir(string id, Action<Dictionary<string, object?>> edicion)
    {
        var d = Doc(id);
        var fm = Procesador_Clonar(d.Frontmatter);
        edicion(fm);
        File.WriteAllText(Path.Combine(Datos, d.Ruta), FrontmatterCanonico.Escribir(fm, d.Cuerpo, d.Tipo!));
    }

    private static Dictionary<string, object?> Procesador_Clonar(Dictionary<string, object?> o) => Orumi.Tests.Clon.De(o);
}

internal static class Clon
{
    public static Dictionary<string, object?> De(Dictionary<string, object?> o) => (Dictionary<string, object?>)Valor(o)!;

    private static object? Valor(object? v) => v switch
    {
        Dictionary<string, object?> m => m.ToDictionary(p => p.Key, p => Valor(p.Value)),
        List<object?> l => l.Select(Valor).ToList(),
        _ => v,
    };
}

internal static class Datos
{
    public static Dictionary<string, object?> Dia(string dia, string? expresion = null)
    {
        var f = new Dictionary<string, object?>();
        if (expresion is not null)
        {
            f["expresion"] = expresion;
        }

        f["desde"] = dia;
        f["hasta"] = dia;
        return f;
    }

    public static RegistroExtraido Registro(
        string fragmento, string clase, string titulo, string? dia = "2026-11-16", string? expresion = null,
        Action<RegistroExtraido>? _ = null, Dictionary<string, object?>? campos = null, Dictionary<string, string>? certeza = null,
        string? reclama = null, string? amplia = null, string? duda = null, string? enConflictoCon = null, string? conflictoCampo = null,
        MultidiaExtraido? multidia = null, List<RelacionExtraida>? relaciones = null)
    {
        var c = campos ?? [];
        var z = certeza ?? [];
        if (dia is not null && !c.ContainsKey("fecha"))
        {
            c["fecha"] = Dia(dia, expresion);
            z.TryAdd("fecha", "interpretado");
        }

        return new RegistroExtraido
        {
            Fragmento = fragmento,
            Clase = clase,
            Titulo = titulo,
            Descripcion = titulo + ".",
            Campos = c,
            Certeza = z,
            Reclama = reclama,
            Amplia = amplia,
            Duda = duda,
            EnConflictoCon = enConflictoCon,
            ConflictoCampo = conflictoCampo,
            Multidia = multidia,
            Relaciones = relaciones ?? [],
        };
    }

    public static RespuestaExtraccion Resp(RegistroExtraido[] registros, params PreguntaCandidata[] preguntas) => new(registros, preguntas);
}
