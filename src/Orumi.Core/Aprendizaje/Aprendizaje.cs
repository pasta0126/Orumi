using System.Globalization;
using System.Text;
using Orumi.Core.Ficheros;
using Orumi.Core.Yaml;

namespace Orumi.Core.Aprendizaje;

public sealed record UmbralesDeAprendizaje(int MinimoCierres = 3, int MinimoDias = 2);

/// <summary>Clase de una pregunta: el campo por el que se pregunta y la clase del registro (<c>duracion:actividad</c>).</summary>
public static class ClaseDePregunta
{
    public const string Desconocida = "desconocida";

    public static string De(Documento pregunta, Almacen almacen)
    {
        // Las preguntas nuevas la llevan escrita; las anteriores se deducen de su campo y de la clase de su registro.
        if (pregunta.Frontmatter.GetValueOrDefault("clase_pregunta") is string escrita && escrita.Length > 0)
        {
            return escrita;
        }

        var campo = pregunta.Frontmatter.GetValueOrDefault("campo") as string ?? "campo";
        var registro = pregunta.Frontmatter.GetValueOrDefault("registro") as string;
        var clase = registro is null ? null : almacen.Documentos.FirstOrDefault(d => d.Id == registro)?.Frontmatter.GetValueOrDefault("clase") as string;
        return Construir(campo, clase);
    }

    public static string Construir(string campo, string? claseDeRegistro) => $"{campo}:{claseDeRegistro ?? Desconocida}";

    /// <summary>Prefijo de las clases de Noa (<c>noa:&lt;apartado&gt;</c>): su aprendizaje es suyo y no cambia lo que pregunta Mira.</summary>
    public const string PrefijoDeNoa = "noa:";

    public static bool EsDeNoa(string clase) => clase.StartsWith(PrefijoDeNoa, StringComparison.Ordinal);

    /// <summary>Texto legible de lo que se preguntaría menos, sin ningún dato del usuario.</summary>
    public static string Describir(string clase)
    {
        if (EsDeNoa(clase))
        {
            return $"las preguntas de Noa sobre «{clase[PrefijoDeNoa.Length..]}»";
        }

        var partes = clase.Split(':', 2);
        return partes.Length == 2 && partes[0] != "contraste"
            ? $"«{partes[0]}» en registros de clase «{partes[1]}»"
            : $"«{clase}»";
    }
}

public sealed record ReglaAprendida(
    string Id, string Clase, string Estado, string Descripcion, IReadOnlyList<string> Evidencia, DateTimeOffset Creada, DateTimeOffset Actualizada,
    bool FijadaPorUsuario, DateTimeOffset? Borrada, IReadOnlyList<EntradaDeAprendizaje> Historial)
{
    public const string Activa = "activa";
    public const string Restablecida = "restablecida";
    public const string Desactivada = "desactivada";
    public const string Borrada_ = "borrada";

    public bool EstaActiva => Estado == Activa;
}

public sealed record EntradaDeAprendizaje(DateTimeOffset Fecha, string Accion, string Origen, string? Motivo);

public sealed record ResultadoDeAprendizaje(IReadOnlyList<string> Nuevas, IReadOnlyList<string> Restablecidas, IReadOnlyList<string> Reactivadas);

public sealed class AprendizajeNoEncontradoException(string id) : Exception($"No existe el aprendizaje «{id}».");

/// <summary>
/// Aprendizaje de Mira a partir de cómo el usuario cierra sus preguntas (spec <c>aprendizaje-de-mira</c>, X64).
/// Visible (un fichero por regla, con su evidencia), reversible (el usuario la desactiva o borra y prevalece) y nunca
/// prohíbe una clase: solo la pone por detrás de las demás. Mira recalcula las reglas al empezar cada ejecución.
/// </summary>
public static class MotorDeAprendizaje
{
    public const string TextoNoRelevante = "No es relevante.";
    public const string Carpeta = "aprendizajes";

    private const string Formato = "yyyy-MM-dd'T'HH:mm:sszzz";

    public static string Ruta(string datos, string id) => Path.Combine(datos, Carpeta, id + ".yaml");

    public static string IdDe(string clase)
    {
        var sb = new StringBuilder("apr-");
        foreach (var c in Texto.Normalizacion.SinAcentos(clase).ToLowerInvariant())
        {
            sb.Append(char.IsAsciiLetterOrDigit(c) ? c : '-');
        }

        return sb.ToString().TrimEnd('-');
    }

    // ------------------------------------------------------------------ cálculo

    public static ResultadoDeAprendizaje Actualizar(string datos, DateTimeOffset ahora, UmbralesDeAprendizaje? umbrales = null)
    {
        var u = umbrales ?? new UmbralesDeAprendizaje();
        var almacen = Almacen.Cargar(datos, incluirPerfil: true);
        var nuevas = new List<string>();
        var restablecidas = new List<string>();
        var reactivadas = new List<string>();

        var porClase = almacen.DeTipo("pregunta")
            .Select(p => (Pregunta: p, Clase: ClaseDePregunta.De(p, almacen), Cierre: Cierre(p)))
            .Where(x => x.Cierre is not null)
            .GroupBy(x => x.Clase);

        foreach (var grupo in porClase)
        {
            var clase = grupo.Key;
            var id = IdDe(clase);
            var existente = Leer(datos, id);
            var ultimaRespuesta = grupo.Where(x => x.Pregunta.Frontmatter.GetValueOrDefault("estado") as string == "respondida")
                .Select(x => x.Cierre!.Value).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
            var desde = existente?.Borrada is { } b && b > ultimaRespuesta ? b : ultimaRespuesta;
            var cierres = grupo.Where(x => EsNoRelevanteDelUsuario(x.Pregunta) && x.Cierre!.Value > desde).OrderBy(x => x.Cierre).ToList();
            var cumple = cierres.Count >= u.MinimoCierres && cierres.Select(x => x.Cierre!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Distinct().Count() >= u.MinimoDias;
            var evidencia = cierres.Select(x => x.Pregunta.Id!).ToList();

            if (existente is null)
            {
                if (cumple)
                {
                    Guardar(datos, Crear(id, clase, evidencia, ahora, cierres.Count, DistintosDias(cierres.Select(x => x.Cierre!.Value))));
                    nuevas.Add(id);
                }

                continue;
            }

            switch (existente.Estado)
            {
                case ReglaAprendida.Desactivada:
                    continue; // lo que el usuario desactivó solo lo reactiva el usuario
                case ReglaAprendida.Activa when existente.FijadaPorUsuario:
                    continue;
                case ReglaAprendida.Activa when !cumple:
                    Guardar(datos, Con(existente, ReglaAprendida.Restablecida, ahora, "restablecida", "mira", "El usuario respondió con datos a una pregunta de esta clase.", evidencia));
                    restablecidas.Add(id);
                    break;
                case ReglaAprendida.Activa when !existente.Evidencia.SequenceEqual(evidencia):
                    Guardar(datos, existente with { Evidencia = evidencia, Actualizada = ahora });
                    break;
                case ReglaAprendida.Restablecida or ReglaAprendida.Borrada_ when cumple:
                    Guardar(datos, Con(existente, ReglaAprendida.Activa, ahora, existente.Estado == ReglaAprendida.Borrada_ ? "recreada" : "reactivada", "mira",
                        "Nueva evidencia suficiente de cierres «no relevante».", evidencia) with { Borrada = null });
                    reactivadas.Add(id);
                    break;
            }
        }

        return new ResultadoDeAprendizaje(nuevas, restablecidas, reactivadas);
    }

    /// <summary>Clases cuya prioridad está reducida ahora mismo.</summary>
    public static HashSet<string> ClasesRebajadas(string datos) =>
        Listar(datos).Where(r => r.EstaActiva && !ClaseDePregunta.EsDeNoa(r.Clase)).Select(r => r.Clase).ToHashSet(StringComparer.Ordinal);

    /// <summary>Clases de Noa con la prioridad reducida: solo las suyas.</summary>
    public static HashSet<string> ClasesRebajadasDeNoa(string datos) =>
        Listar(datos).Where(r => r.EstaActiva && ClaseDePregunta.EsDeNoa(r.Clase)).Select(r => r.Clase).ToHashSet(StringComparer.Ordinal);

    /// <summary>Descripciones de las reglas activas, las únicas que se envían al modelo (sin textos del usuario).</summary>
    public static IReadOnlyList<string> Preferencias(string datos) =>
        [.. Listar(datos).Where(r => r.EstaActiva && !ClaseDePregunta.EsDeNoa(r.Clase)).OrderBy(r => r.Id, StringComparer.Ordinal).Select(r => r.Descripcion)];

    private static DateTimeOffset? Cierre(Documento p) =>
        p.Frontmatter.GetValueOrDefault("estado") as string is "respondida" or "no_relevante"
        && p.Frontmatter.GetValueOrDefault("cerrada_el") as string is { } t
        && DateTimeOffset.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : null;

    /// <summary>Solo cuenta lo que el usuario cerró como no relevante: los cierres del sistema (p. ej. al eliminar un registro) no enseñan nada.</summary>
    private static bool EsNoRelevanteDelUsuario(Documento p) =>
        p.Frontmatter.GetValueOrDefault("estado") as string == "no_relevante"
        && p.Frontmatter.GetValueOrDefault("respuesta") is Dictionary<string, object?> r
        && r.GetValueOrDefault("texto") as string == TextoNoRelevante;

    private static int DistintosDias(IEnumerable<DateTimeOffset> fechas) =>
        fechas.Select(f => f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Distinct().Count();

    private static ReglaAprendida Crear(string id, string clase, IReadOnlyList<string> evidencia, DateTimeOffset ahora, int cierres, int dias) =>
        new(id, clase, ReglaAprendida.Activa,
            $"Preguntar menos por {ClaseDePregunta.Describir(clase)}: el usuario lo ha cerrado como no relevante {cierres} veces en {dias} días distintos.",
            evidencia, ahora, ahora, false, null,
            [new EntradaDeAprendizaje(ahora, "creada", "mira", $"{cierres} cierres «no relevante» en {dias} días.")]);

    private static ReglaAprendida Con(ReglaAprendida r, string estado, DateTimeOffset ahora, string accion, string origen, string? motivo, IReadOnlyList<string> evidencia) =>
        r with { Estado = estado, Actualizada = ahora, Evidencia = evidencia, Historial = [.. r.Historial, new EntradaDeAprendizaje(ahora, accion, origen, motivo)] };

    // ------------------------------------------------------------------ control del usuario

    public static IReadOnlyList<ReglaAprendida> Listar(string datos, bool incluirBorradas = false)
    {
        var carpeta = Path.Combine(datos, Carpeta);
        if (!Directory.Exists(carpeta))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(carpeta, "apr-*.yaml").Order(StringComparer.Ordinal)
            .Select(f => Leer(datos, Path.GetFileNameWithoutExtension(f)))
            .OfType<ReglaAprendida>()
            .Where(r => incluirBorradas || r.Estado != ReglaAprendida.Borrada_)];
    }

    public static ReglaAprendida Desactivar(string datos, string id, DateTimeOffset ahora) =>
        Cambiar(datos, id, ahora, ReglaAprendida.Desactivada, "desactivada", fijada: false);

    /// <summary>El usuario la activa a mano: Mira ya no la restablece por su cuenta.</summary>
    public static ReglaAprendida Activar(string datos, string id, DateTimeOffset ahora) =>
        Cambiar(datos, id, ahora, ReglaAprendida.Activa, "activada", fijada: true);

    /// <summary>Borrar deja una marca: la regla solo vuelve si hay evidencia nueva posterior al borrado.</summary>
    public static ReglaAprendida Borrar(string datos, string id, DateTimeOffset ahora)
    {
        var r = Leer(datos, id) ?? throw new AprendizajeNoEncontradoException(id);
        var nueva = Con(r, ReglaAprendida.Borrada_, ahora, "borrada", "usuario", null, r.Evidencia) with { Borrada = ahora, FijadaPorUsuario = false };
        Guardar(datos, nueva);
        return nueva;
    }

    private static ReglaAprendida Cambiar(string datos, string id, DateTimeOffset ahora, string estado, string accion, bool fijada)
    {
        var r = Leer(datos, id);
        if (r is null || r.Estado == ReglaAprendida.Borrada_)
        {
            throw new AprendizajeNoEncontradoException(id);
        }

        var nueva = Con(r, estado, ahora, accion, "usuario", null, r.Evidencia) with { FijadaPorUsuario = fijada };
        Guardar(datos, nueva);
        return nueva;
    }

    // ------------------------------------------------------------------ ficheros

    public static ReglaAprendida? Leer(string datos, string id)
    {
        var ruta = Ruta(datos, id);
        if (!File.Exists(ruta))
        {
            return null;
        }

        try
        {
            var m = YamlLector.LeerMapa(File.ReadAllText(ruta, Encoding.UTF8));
            DateTimeOffset F(object? v) => DateTimeOffset.Parse((string)v!, CultureInfo.InvariantCulture);
            return new ReglaAprendida(
                (string)m["id"]!, (string)m["clase_pregunta"]!, (string)m["estado"]!, (string)m["descripcion"]!,
                (m.GetValueOrDefault("evidencia") as List<object?> ?? []).OfType<string>().ToList(),
                F(m["creada_el"]), F(m["actualizada_el"]), m.GetValueOrDefault("fijada_por_usuario") as string == "true",
                m.GetValueOrDefault("borrada_el") is string b ? F(b) : null,
                (m.GetValueOrDefault("historial") as List<object?> ?? []).OfType<Dictionary<string, object?>>()
                    .Select(h => new EntradaDeAprendizaje(F(h["fecha"]), (string)h["accion"]!, (string)h["origen"]!, h.GetValueOrDefault("motivo") as string)).ToList());
        }
        catch (Exception e) when (e is FormatException or KeyNotFoundException or InvalidCastException or YamlDotNet.Core.YamlException)
        {
            return null; // fichero ilegible: se ignora y Mira lo regenera si la evidencia lo sigue pidiendo
        }
    }

    public static void Guardar(string datos, ReglaAprendida r)
    {
        var historial = r.Historial.Select(h => (YNodo)YMapa.EnLineaDe(
            ("fecha", new YInstante(h.Fecha.ToString(Formato, CultureInfo.InvariantCulture))),
            ("accion", new YLiteral(h.Accion)), ("origen", new YLiteral(h.Origen)),
            ("motivo", h.Motivo is null ? new YLiteral("ninguno") : new YCitado(h.Motivo)))).ToList();
        var pares = new List<(string, YNodo)>
        {
            ("id", new YLiteral(r.Id)),
            ("tipo", new YLiteral("aprendizaje")),
            ("regla", new YLiteral("prioridad_baja")),
            ("clase_pregunta", new YCitado(r.Clase)),
            ("descripcion", new YCitado(r.Descripcion)),
            ("estado", new YLiteral(r.Estado)),
            ("evidencia", new YLista([.. r.Evidencia.Select(e => (YNodo)new YLiteral(e))], EnLinea: true)),
            ("creada_el", new YInstante(r.Creada.ToString(Formato, CultureInfo.InvariantCulture))),
            ("actualizada_el", new YInstante(r.Actualizada.ToString(Formato, CultureInfo.InvariantCulture))),
        };
        if (r.FijadaPorUsuario)
        {
            pares.Add(("fijada_por_usuario", new YLiteral("true")));
        }

        if (r.Borrada is { } b)
        {
            pares.Add(("borrada_el", new YInstante(b.ToString(Formato, CultureInfo.InvariantCulture))));
        }

        pares.Add(("historial", new YLista(historial)));
        var ruta = Ruta(datos, r.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        EscrituraAtomica.Reemplazar(ruta, YamlEscritor.Escribir(YMapa.De([.. pares])));
    }
}
