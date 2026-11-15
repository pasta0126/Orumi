using System.Globalization;
using System.Text.RegularExpressions;
using Orumi.Core.Ficheros;
using Orumi.Core.Yaml;

namespace Orumi.Core.Hipotesis;

public sealed class HipotesisInvalidaException(string mensaje) : Exception(mensaje);

/// <summary>
/// Revisión del usuario sobre un contraste (X22): <c>confirmada</c>, <c>matizada</c> (con su comentario, que prevalece) o <c>descartada</c>.
/// <c>EfectosAlRevisar</c> es la evidencia que había entonces: un contraste descartado solo se reabre con evidencia nueva sustancial.
/// </summary>
public sealed record RevisionDeContraste(string Estado, string? Comentario, DateTimeOffset Fecha, int EfectosAlRevisar);

/// <summary>Una relación factor → efecto que se contrasta, con su ventana de retardo en días (0 = el mismo día).</summary>
public sealed record Contraste(string Id, IReadOnlyList<string> Factor, string Efecto, int RetardoDesde, int RetardoHasta, RevisionDeContraste? Revision = null);

public sealed record EntradaDeHipotesis(DateTimeOffset Fecha, string Accion, string Origen, string? Motivo);

/// <summary>
/// Una hipótesis del usuario sobre sí mismo (spec <c>contraste-de-hipotesis</c>): su formulación literal, «dicho por el usuario»,
/// y los contrastes que Mira hará con sus registros y el clima. Nunca es un hecho hasta que lo diga el análisis y el usuario lo confirme.
/// </summary>
public sealed record Hipotesis(
    string Id, string Formulacion, string Estado, DateTimeOffset Declarada,
    IReadOnlyDictionary<string, string> Conceptos, IReadOnlyList<Contraste> Contrastes, IReadOnlyList<EntradaDeHipotesis> Historial,
    IReadOnlyDictionary<string, string>? Preguntas = null)
{
    public const string PorVerificar = "por_verificar";
    public const string Confirmada = "confirmada";
    public const string Matizada = "matizada";
    public const string Descartada = "descartada";

    public bool EstaActiva => Estado != Descartada;

    /// <summary>Frase con que Mira pregunta por un efecto en un día; <c>{dia}</c> se sustituye por la fecha. Neutra: no sugiere la respuesta.</summary>
    public string PreguntaDe(string concepto, string dia) =>
        (Preguntas?.GetValueOrDefault(concepto) ?? $"¿Ocurrió «{concepto.Replace('_', ' ')}» el {{dia}}?").Replace("{dia}", dia, StringComparison.Ordinal);
}

/// <summary>Condición de un factor: <c>clima:uv_alto</c>, <c>clima:lluvia</c>, <c>clima:soleado</c> o <c>registro:CONCEPTO[>=MINUTOS]</c>.</summary>
public sealed record CondicionDeFactor(string Tipo, string Nombre, int? MinutosMinimos)
{
    public static readonly string[] ClimaConocido = ["uv_alto", "lluvia", "soleado"];

    private static readonly Regex Rx = new(@"^(clima|registro):([a-z][a-z0-9_]{1,40})(?:>=(\d{1,4}))?$", RegexOptions.Compiled);

    public static CondicionDeFactor Analizar(string texto)
    {
        var m = Rx.Match(texto ?? string.Empty);
        if (!m.Success)
        {
            throw new HipotesisInvalidaException($"Condición no válida: «{texto}». Usa clima:uv_alto | clima:lluvia | clima:soleado | registro:CONCEPTO[>=MINUTOS].");
        }

        var c = new CondicionDeFactor(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Success ? int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : null);
        if (c.Tipo == "clima" && (!ClimaConocido.Contains(c.Nombre) || c.MinutosMinimos is not null))
        {
            throw new HipotesisInvalidaException($"Factor de clima desconocido: «{texto}». Disponibles: {string.Join(", ", ClimaConocido.Select(x => "clima:" + x))}.");
        }

        return c;
    }
}

/// <summary>Hipótesis del usuario: formato <c>hipotesis/hip-*.yaml</c>, validación y operaciones. Ficheros legibles a mano.</summary>
public static class AlmacenDeHipotesis
{
    public const string Carpeta = "hipotesis";
    public const int LongitudMaximaFormulacion = 500;

    private const string Formato = "yyyy-MM-dd'T'HH:mm:sszzz";
    private static readonly Regex IdRx = new(@"^hip-[a-z0-9-]{1,60}$", RegexOptions.Compiled);
    private static readonly Regex ConceptoRx = new(@"^[a-z][a-z0-9_]{1,40}$", RegexOptions.Compiled);

    public static string Ruta(string datos, string id) => Path.Combine(datos, Carpeta, id + ".yaml");

    public static bool EsIdValido(string id) => IdRx.IsMatch(id);

    // ------------------------------------------------------------------ hipótesis iniciales (declaradas por el usuario el 2026-11-14)

    /// <summary>Las cuatro sospechas que el usuario ha ido detectando con el tiempo, con sus palabras. Son hipótesis a verificar, no hechos.</summary>
    public static IReadOnlyList<Hipotesis> Iniciales(DateTimeOffset ahora)
    {
        var d = new EntradaDeHipotesis(ahora, "declarada", "usuario", "Hipótesis inicial del usuario (2026-11-14).");
        Contraste C(string id, string[] factor, string efecto, int desde, int hasta) => new(id, factor, efecto, desde, hasta);
        Hipotesis H(string id, string texto, Dictionary<string, string> conceptos, Dictionary<string, string> preguntas, params Contraste[] contrastes) =>
            new(id, texto, Hipotesis.PorVerificar, ahora, conceptos, contrastes, [d], preguntas);

        const string Exterior = "El usuario estuvo al aire libre (calle, playa, campo, terraza, paseo, deporte fuera…). Si dice cuánto tiempo, indícalo en minutos.";
        return
        [
            H("hip-sol-cansancio",
                "una larga exposición al sol me hace estar más cansado y hacer siestas largas",
                new() { ["exterior"] = Exterior,
                        ["cansancio_fisico"] = "El usuario dice sentirse físicamente cansado, agotado de cuerpo o sin fuerzas.",
                        ["siesta_larga"] = "El usuario dice que ha hecho una siesta larga, o de más de una hora, o que se ha pasado durmiendo la siesta." },
                new() { ["cansancio_fisico"] = "¿Te sentiste físicamente cansado el {dia}?", ["siesta_larga"] = "¿Hiciste una siesta larga el {dia}?" },
                C("c1", ["registro:exterior>=90", "clima:soleado"], "cansancio_fisico", 0, 1),
                C("c2", ["registro:exterior>=90", "clima:soleado"], "siesta_larga", 0, 1)),
            H("hip-uv-dolor-de-cabeza",
                "un índice UV alto con luz fuerte me provoca dolor de cabeza",
                new() { ["dolor_de_cabeza"] = "El usuario dice que tiene o ha tenido dolor de cabeza o jaqueca." },
                new() { ["dolor_de_cabeza"] = "¿Tuviste dolor de cabeza el {dia}?" },
                C("c1", ["clima:uv_alto"], "dolor_de_cabeza", 0, 0)),
            H("hip-lluvia-relajacion",
                "la lluvia me relaja",
                new() { ["relajacion"] = "El usuario dice sentirse relajado, tranquilo o en calma." },
                new() { ["relajacion"] = "¿Te sentiste relajado el {dia}?" },
                C("c1", ["clima:lluvia"], "relajacion", 0, 0)),
            H("hip-socializacion-agotamiento",
                "la socialización me agota y tiendo a compensar estando posteriormente más solitario o introspectivo",
                new() { ["socializacion"] = "El usuario dice que ha quedado o ha estado con otras personas en un plan social (amigos, familia, celebraciones, reuniones sociales). El trabajo ordinario NO cuenta.",
                        ["agotamiento_social"] = "El usuario dice sentirse agotado, cansado o saturado tras estar con gente.",
                        ["soledad_introspeccion"] = "El usuario dice querer estar solo, o sentirse solitario, retraído o introspectivo." },
                new() { ["agotamiento_social"] = "¿Te sentiste agotado tras estar con gente el {dia}?", ["soledad_introspeccion"] = "¿Te sentiste más solo o introspectivo el {dia}?" },
                C("c1", ["registro:socializacion"], "agotamiento_social", 0, 0),
                C("c2", ["registro:socializacion"], "soledad_introspeccion", 1, 2)),
        ];
    }

    // ------------------------------------------------------------------ validación

    public static void Validar(Hipotesis h)
    {
        if (!IdRx.IsMatch(h.Id))
        {
            throw new HipotesisInvalidaException("Identificador no válido (hip-… en minúsculas, números y guiones).");
        }

        if (string.IsNullOrWhiteSpace(h.Formulacion) || h.Formulacion.Trim().Length < 5 || h.Formulacion.Length > LongitudMaximaFormulacion || h.Formulacion.Any(char.IsControl))
        {
            throw new HipotesisInvalidaException($"La formulación debe tener entre 5 y {LongitudMaximaFormulacion} caracteres, sin saltos de línea.");
        }

        if (h.Contrastes.Count is 0 or > 8)
        {
            throw new HipotesisInvalidaException("Una hipótesis necesita entre 1 y 8 contrastes.");
        }

        var ids = new HashSet<string>();
        var necesarios = new HashSet<string>();
        foreach (var c in h.Contrastes)
        {
            if (!ids.Add(c.Id) || !Regex.IsMatch(c.Id, @"^c\d{1,2}$"))
            {
                throw new HipotesisInvalidaException($"Identificador de contraste no válido o repetido: «{c.Id}».");
            }

            if (c.Factor.Count is 0 or > 4)
            {
                throw new HipotesisInvalidaException("Un contraste necesita entre 1 y 4 condiciones de factor.");
            }

            foreach (var cond in c.Factor.Select(CondicionDeFactor.Analizar))
            {
                if (cond.Tipo == "registro")
                {
                    necesarios.Add(cond.Nombre);
                }
            }

            if (!ConceptoRx.IsMatch(c.Efecto))
            {
                throw new HipotesisInvalidaException($"Efecto no válido: «{c.Efecto}» (minúsculas, números y _).");
            }

            necesarios.Add(c.Efecto);
            if (c.RetardoDesde < 0 || c.RetardoHasta < c.RetardoDesde || c.RetardoHasta > 14)
            {
                throw new HipotesisInvalidaException("El retardo debe ir de 0 a 14 días y «desde» no puede superar a «hasta».");
            }
        }

        foreach (var (concepto, frase) in h.Preguntas ?? new Dictionary<string, string>())
        {
            if (!h.Conceptos.ContainsKey(concepto) || frase.Length is < 5 or > 200 || frase.Any(char.IsControl) || !frase.Contains("{dia}", StringComparison.Ordinal))
            {
                throw new HipotesisInvalidaException($"La pregunta de «{concepto}» no es válida (5 a 200 caracteres y con {{dia}}).");
            }
        }

        foreach (var concepto in necesarios)
        {
            if (!h.Conceptos.TryGetValue(concepto, out var desc) || desc.Trim().Length is < 5 or > 400 || desc.Any(char.IsControl))
            {
                throw new HipotesisInvalidaException($"Falta la descripción del concepto «{concepto}» (5 a 400 caracteres): el modelo la necesita para reconocerlo.");
            }
        }
    }

    // ------------------------------------------------------------------ operaciones

    public static IReadOnlyList<Hipotesis> Listar(string datos)
    {
        var carpeta = Path.Combine(datos, Carpeta);
        if (!Directory.Exists(carpeta))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(carpeta, "hip-*.yaml").Order(StringComparer.Ordinal)
            .Select(f => Leer(datos, Path.GetFileNameWithoutExtension(f))).OfType<Hipotesis>()];
    }

    /// <summary>
    /// Conceptos (con su descripción) que el modelo tiene que reconocer. Incluye los de hipótesis descartadas: se siguen etiquetando en silencio
    /// porque un contraste descartado solo se reabre con evidencia nueva sustancial, y para detectarla hay que seguir viéndola.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Conceptos(string datos)
    {
        var d = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var h in Listar(datos))
        {
            foreach (var (k, v) in h.Conceptos)
            {
                d.TryAdd(k, v);
            }
        }

        return d;
    }

    /// <summary>Crea las hipótesis iniciales que aún no existen. Idempotente: no toca las que ya existen ni resucita las descartadas.</summary>
    public static IReadOnlyList<string> CargarIniciales(string datos, DateTimeOffset ahora)
    {
        var nuevas = new List<string>();
        foreach (var h in Iniciales(ahora))
        {
            if (File.Exists(Ruta(datos, h.Id)))
            {
                continue;
            }

            Guardar(datos, h);
            nuevas.Add(h.Id);
        }

        return nuevas;
    }

    public static Hipotesis Anadir(string datos, string formulacion, IReadOnlyList<string> factor, string efecto, int retardoDesde, int retardoHasta,
        IReadOnlyDictionary<string, string> conceptos, DateTimeOffset ahora)
    {
        var id = "hip-" + Slug(formulacion);
        if (File.Exists(Ruta(datos, id)))
        {
            throw new HipotesisInvalidaException($"Ya existe la hipótesis {id}.");
        }

        var h = new Hipotesis(id, formulacion.Trim(), Hipotesis.PorVerificar, ahora, new Dictionary<string, string>(conceptos),
            [new Contraste("c1", factor, efecto, retardoDesde, retardoHasta)], [new EntradaDeHipotesis(ahora, "declarada", "usuario", null)]);
        Validar(h);
        Guardar(datos, h);
        return h;
    }

    /// <summary>
    /// El usuario confirma, matiza o descarta un contraste (X22). Lo matizado se reescribe con su comentario, que prevalece; lo descartado no
    /// vuelve a proponerse salvo que la evidencia se duplique (<paramref name="efectosActuales"/> es la que hay ahora). Si todos los contrastes
    /// de una hipótesis se descartan, la hipótesis queda descartada y deja de usarse.
    /// </summary>
    public static Hipotesis Revisar(string datos, string hipotesisId, string? contrasteId, string accion, string? comentario, int efectosActuales, DateTimeOffset ahora)
    {
        var h = Leer(datos, hipotesisId) ?? throw new HipotesisInvalidaException($"No existe la hipótesis «{hipotesisId}».");
        var estado = accion switch
        {
            "confirmar" => Hipotesis.Confirmada,
            "matizar" => Hipotesis.Matizada,
            "descartar" => Hipotesis.Descartada,
            _ => throw new HipotesisInvalidaException("La acción debe ser confirmar, matizar o descartar."),
        };
        if (accion == "matizar" && string.IsNullOrWhiteSpace(comentario))
        {
            throw new HipotesisInvalidaException("Para matizar hay que decir cómo (--comentario \"…\"): tu comentario prevalece sobre la interpretación de Mira.");
        }

        if (comentario is { Length: > 500 } || comentario?.Any(char.IsControl) == true)
        {
            throw new HipotesisInvalidaException("El comentario debe tener como máximo 500 caracteres y ningún salto de línea.");
        }

        var objetivo = contrasteId is null
            ? (h.Contrastes.Count == 1 ? h.Contrastes[0] : throw new HipotesisInvalidaException($"La hipótesis tiene {h.Contrastes.Count} contrastes: indica cuál (p. ej. {hipotesisId}#c1)."))
            : h.Contrastes.FirstOrDefault(c => c.Id == contrasteId) ?? throw new HipotesisInvalidaException($"No existe el contraste «{contrasteId}» en {hipotesisId}.");
        var contrastes = h.Contrastes.Select(c => c.Id == objetivo.Id
            ? c with { Revision = new RevisionDeContraste(estado, string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim(), ahora, efectosActuales) }
            : c).ToList();
        var global = contrastes.All(c => c.Revision?.Estado == Hipotesis.Descartada) ? Hipotesis.Descartada
            : contrastes.Any(c => c.Revision?.Estado == Hipotesis.Matizada) ? Hipotesis.Matizada
            : contrastes.All(c => c.Revision?.Estado == Hipotesis.Confirmada) ? Hipotesis.Confirmada
            : Hipotesis.PorVerificar;
        var nueva = h with
        {
            Estado = global,
            Contrastes = contrastes,
            Historial = [.. h.Historial, new EntradaDeHipotesis(ahora, accion, "usuario", $"{objetivo.Id}: {estado}" + (string.IsNullOrWhiteSpace(comentario) ? "" : " — " + comentario.Trim()))],
        };
        Guardar(datos, nueva);
        return nueva;
    }

    private static string Slug(string texto)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in Texto.Normalizacion.SinAcentos(texto).ToLowerInvariant())
        {
            sb.Append(char.IsAsciiLetterOrDigit(c) ? c : '-');
        }

        var s = string.Join('-', sb.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        return s.Length > 50 ? s[..50].TrimEnd('-') : s;
    }

    // ------------------------------------------------------------------ ficheros

    public static Hipotesis? Leer(string datos, string id)
    {
        var ruta = Ruta(datos, id);
        if (!IdRx.IsMatch(id) || !File.Exists(ruta))
        {
            return null;
        }

        try
        {
            var m = YamlLector.LeerMapa(File.ReadAllText(ruta, System.Text.Encoding.UTF8));
            DateTimeOffset F(object? v) => DateTimeOffset.Parse((string)v!, CultureInfo.InvariantCulture);
            return new Hipotesis(
                (string)m["id"]!, (string)m["formulacion"]!, (string)m["estado"]!, F(m["declarada_el"]),
                (m.GetValueOrDefault("conceptos") as Dictionary<string, object?> ?? []).Where(p => p.Value is string).ToDictionary(p => p.Key, p => (string)p.Value!),
                (m.GetValueOrDefault("contrastes") as List<object?> ?? []).OfType<Dictionary<string, object?>>().Select(c =>
                {
                    var r = c.GetValueOrDefault("retardo_dias") as Dictionary<string, object?> ?? [];
                    var rv = c.GetValueOrDefault("revision") as Dictionary<string, object?>;
                    return new Contraste((string)c["id"]!, (c.GetValueOrDefault("factor") as List<object?> ?? []).OfType<string>().ToList(), (string)c["efecto"]!,
                        int.Parse((string)(r.GetValueOrDefault("desde") ?? "0")!, CultureInfo.InvariantCulture),
                        int.Parse((string)(r.GetValueOrDefault("hasta") ?? "0")!, CultureInfo.InvariantCulture),
                        rv is null ? null : new RevisionDeContraste((string)rv["estado"]!, rv.GetValueOrDefault("comentario") is string t && t != "ninguno" ? t : null,
                            F(rv["fecha"]), int.Parse((string)(rv.GetValueOrDefault("efectos") ?? "0")!, CultureInfo.InvariantCulture)));
                }).ToList(),
                (m.GetValueOrDefault("historial") as List<object?> ?? []).OfType<Dictionary<string, object?>>()
                    .Select(h => new EntradaDeHipotesis(F(h["fecha"]), (string)h["accion"]!, (string)h["origen"]!, h.GetValueOrDefault("motivo") is string t && t != "ninguno" ? t : null)).ToList(),
                (m.GetValueOrDefault("preguntas") as Dictionary<string, object?> ?? []).Where(p => p.Value is string).ToDictionary(p => p.Key, p => (string)p.Value!));
        }
        catch (Exception e) when (e is FormatException or KeyNotFoundException or InvalidCastException or YamlDotNet.Core.YamlException)
        {
            return null;
        }
    }

    public static void Guardar(string datos, Hipotesis h)
    {
        Validar(h);
        var contrastes = h.Contrastes.Select(c =>
        {
            var pares = new List<(string, YNodo)>
            {
                ("id", new YLiteral(c.Id)),
                ("factor", new YLista([.. c.Factor.Select(f => (YNodo)new YCitado(f))], EnLinea: true)),
                ("efecto", new YLiteral(c.Efecto)),
                ("retardo_dias", YMapa.EnLineaDe(("desde", new YLiteral(c.RetardoDesde.ToString(CultureInfo.InvariantCulture))), ("hasta", new YLiteral(c.RetardoHasta.ToString(CultureInfo.InvariantCulture))))),
            };
            if (c.Revision is { } rv)
            {
                pares.Add(("revision", YMapa.EnLineaDe(
                    ("estado", new YLiteral(rv.Estado)), ("comentario", rv.Comentario is null ? new YLiteral("ninguno") : new YCitado(rv.Comentario)),
                    ("fecha", new YInstante(rv.Fecha.ToString(Formato, CultureInfo.InvariantCulture))), ("efectos", new YLiteral(rv.EfectosAlRevisar.ToString(CultureInfo.InvariantCulture))))));
            }

            return (YNodo)YMapa.De([.. pares]);
        }).ToList();
        var historial = h.Historial.Select(e => (YNodo)YMapa.EnLineaDe(
            ("fecha", new YInstante(e.Fecha.ToString(Formato, CultureInfo.InvariantCulture))), ("accion", new YLiteral(e.Accion)), ("origen", new YLiteral(e.Origen)),
            ("motivo", e.Motivo is null ? new YLiteral("ninguno") : new YCitado(e.Motivo)))).ToList();
        var raiz = YMapa.De(
            ("id", new YLiteral(h.Id)),
            ("tipo", new YLiteral("hipotesis")),
            ("formulacion", new YCitado(h.Formulacion)),
            ("certeza", new YLiteral("dicho")),
            ("estado", new YLiteral(h.Estado)),
            ("declarada_el", new YInstante(h.Declarada.ToString(Formato, CultureInfo.InvariantCulture))),
            ("conceptos", YMapa.De([.. h.Conceptos.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, (YNodo)new YCitado(p.Value)))])),
            ("preguntas", YMapa.De([.. (h.Preguntas ?? new Dictionary<string, string>()).OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, (YNodo)new YCitado(p.Value)))])),
            ("contrastes", new YLista(contrastes)),
            ("historial", new YLista(historial)));
        var ruta = Ruta(datos, h.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        EscrituraAtomica.Reemplazar(ruta, YamlEscritor.Escribir(raiz));
    }
}
