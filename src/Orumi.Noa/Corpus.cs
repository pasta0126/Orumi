using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Orumi.Agente.Analisis;
using Orumi.Agente.Etiquetado;
using Orumi.Core.Ficheros;
using Orumi.Core.Texto;

namespace Orumi.Noa;

/// <summary>
/// Sustituye los nombres de terceras personas por claves («P1», «P2»…) antes de enviar nada al proveedor y los restituye en lo que vuelve,
/// para que lo que sale hacia fuera mencione lo mínimo posible a otras personas (decisión 10). Solo conoce los nombres que ORUMI ya tiene
/// registrados como personas: es una reducción de riesgo, no una garantía.
/// </summary>
public sealed class Seudonimizador
{
    private readonly Dictionary<string, string> _claves = new(StringComparer.Ordinal); // clave → nombre
    private readonly Regex? _nombres;
    private static readonly Regex Claves = new(@"«?\bP(\d{1,3})\b»?", RegexOptions.Compiled);

    private Seudonimizador(IReadOnlyList<string> nombres, bool activo)
    {
        Nombres = nombres;
        if (!activo || nombres.Count == 0)
        {
            return;
        }

        var i = 0;
        foreach (var n in nombres)
        {
            _claves[$"P{++i}"] = n;
        }

        _nombres = new Regex(@"(?<!\p{L})(" + string.Join("|", nombres.OrderByDescending(n => n.Length).Select(Regex.Escape)) + @")(?!\p{L})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    /// <summary>Nombres de terceros conocidos (se usan también para impedir que un rasgo hable de ellos).</summary>
    public IReadOnlyList<string> Nombres { get; }

    public static Seudonimizador Crear(IEnumerable<string> personas, bool activo)
    {
        var nombres = personas.Select(p => p.Trim()).Where(p => p.Length >= 3 && p.Any(char.IsLetter)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
        return new Seudonimizador(nombres, activo);
    }

    public string Ocultar(string texto)
    {
        if (_nombres is null)
        {
            return texto;
        }

        var porNombre = _claves.ToDictionary(c => c.Value, c => c.Key, StringComparer.OrdinalIgnoreCase);
        return _nombres.Replace(texto, m => porNombre.TryGetValue(m.Value, out var k) ? $"«{k}»" : m.Value);
    }

    public string Revelar(string texto) => Claves.Replace(texto, m => _claves.TryGetValue("P" + m.Groups[1].Value, out var n) ? n : m.Value);

    /// <summary>Si el texto menciona a una tercera persona (por su clave o por su nombre).</summary>
    public bool MencionaTercero(string texto) =>
        Claves.IsMatch(texto) || Nombres.Any(n => Regex.IsMatch(texto, @"(?<!\p{L})" + Regex.Escape(n) + @"(?!\p{L})", RegexOptions.IgnoreCase));
}

public sealed record FuenteDelCorpus(string Id, string Fecha, string? Hora, string Texto);

/// <summary>Lo que se le entrega a Noa: lo que el usuario dijo (el fragmento de su captura y sus respuestas), ya seudonimizado y acotado.</summary>
public sealed record Corpus(
    IReadOnlyList<FuenteDelCorpus> Fuentes, int Excluidas, int Registros, int DiasConRegistros, DateOnly? PrimerRegistro, Seudonimizador Seudonimos, IReadOnlyDictionary<string, string> NombreDeFuente)
{
    public FuenteDelCorpus? Buscar(string id) => Fuentes.FirstOrDefault(f => f.Id == id);

    public static Corpus Construir(Almacen almacen, ConfiguracionDeNoa config)
    {
        var personas = almacen.DeTipo("registro").Where(r => r.Frontmatter.GetValueOrDefault("estado") is not ("eliminado" or "fusionado"))
            .SelectMany(r => r.Frontmatter.GetValueOrDefault("personas") as List<object?> ?? []).OfType<string>();
        var seud = Seudonimizador.Crear(personas, config.SeudonimizarTerceros);

        var todas = new List<FuenteDelCorpus>();
        var registros = Etiquetador.Preparar(almacen);
        foreach (var r in registros)
        {
            todas.Add(new FuenteDelCorpus(r.Id, r.Fecha, r.Hora, seud.Ocultar(r.Texto)));
        }

        // Lo que el usuario contestó a las preguntas de Mira también es dicho por él (confirmado mediante aclaración).
        foreach (var p in almacen.DeTipo("pregunta").Where(p => p.Id is not null && p.Frontmatter.GetValueOrDefault("estado") as string == "respondida"))
        {
            var respuesta = (p.Frontmatter.GetValueOrDefault("respuesta") as Dictionary<string, object?>)?.GetValueOrDefault("texto") as string;
            var cerrada = p.Frontmatter.GetValueOrDefault("cerrada_el") as string;
            if (string.IsNullOrWhiteSpace(respuesta) || cerrada is not { Length: >= 10 })
            {
                continue;
            }

            todas.Add(new FuenteDelCorpus(p.Id!, cerrada[..10], null, seud.Ocultar($"Pregunta: {p.Frontmatter.GetValueOrDefault("texto")}\nRespuesta del usuario: {respuesta}")));
        }

        // Si no cabe todo, se conserva lo más reciente y se dice cuánto se dejó fuera.
        var incluidas = new List<FuenteDelCorpus>();
        var chars = 0;
        foreach (var f in todas.OrderByDescending(f => f.Fecha, StringComparer.Ordinal).ThenByDescending(f => f.Id, StringComparer.Ordinal))
        {
            if (chars + f.Texto.Length > config.MaximoCaracteres && incluidas.Count > 0)
            {
                continue;
            }

            incluidas.Add(f);
            chars += f.Texto.Length;
        }

        var ordenadas = incluidas.OrderBy(f => f.Fecha, StringComparer.Ordinal).ThenBy(f => f.Id, StringComparer.Ordinal).ToList();
        return new Corpus(ordenadas, todas.Count - incluidas.Count, registros.Count, registros.Select(r => r.Fecha).Distinct().Count(),
            MotorDeContraste.PrimerRegistro(almacen), seud, ordenadas.ToDictionary(f => f.Id, f => f.Texto));
    }

    /// <summary>La cita debe estar literalmente en el texto de esa fuente (sin distinguir mayúsculas ni espacios repetidos).</summary>
    public bool CitaEsLiteral(string fuente, string cita) =>
        NombreDeFuente.TryGetValue(fuente, out var texto) && Normalizar(texto).Contains(Normalizar(cita), StringComparison.Ordinal);

    internal static string Normalizar(string s) => string.Join(' ', s.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
