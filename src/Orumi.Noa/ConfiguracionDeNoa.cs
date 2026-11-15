using System.Globalization;
using Orumi.Core.Yaml;

namespace Orumi.Noa;

/// <summary>
/// Configuración de Noa (sección <c>noa</c> de <c>orumi.yaml</c>). Su presencia activa al agente: sin ella no genera nada.
/// Todos los umbrales son ajustables (X20, decisión 5 y 13 del diseño).
/// </summary>
public sealed record ConfiguracionDeNoa(
    int CadenciaDias = 14, int SemanasDeObservacion = 4, int MinimoRegistrosNuevos = 20, int MinimoDiasNuevos = 10,
    int RegistrosParaPatron = 3, int SemanasParaPatron = 2, int RegistrosParaIndicio = 2, int MaximoCaracteres = 150_000,
    bool SeudonimizarTerceros = true, IReadOnlyList<string>? TerminosClinicos = null, int MaximoPreguntas = 3)
{
    /// <summary>Nombres de trastornos y etiquetas clínicas que Noa nunca emite como conclusión (sin acentos, en minúsculas). El prompt y las pruebas adversas lo refuerzan.</summary>
    public static readonly IReadOnlyList<string> TerminosClinicosPorDefecto =
    [
        "depresion", "trastorno", "tdah", "autismo", "autista", "bipolar", "esquizofreni", "psicosis", "psicotic", "estres postraumatico", "tept",
        "obsesivo compulsivo", "anorexia", "bulimia", "adiccion", "alcoholismo", "alcoholico", "demencia", "alzheimer", "diabetes", "cancer",
        "diagnostic", "padece", "sindrome", "borderline", "narcisista", "psicopat", "sociopat", "ansiedad generalizada", "ataque de panico", "burnout",
    ];

    public IReadOnlyList<string> Terminos => TerminosClinicos is { Count: > 0 } ? TerminosClinicos : TerminosClinicosPorDefecto;

    public static ConfiguracionDeNoa? Leer(string yaml)
    {
        var raiz = YamlLector.LeerMapa(yaml);
        if (!raiz.TryGetValue("noa", out var v))
        {
            return null;
        }

        var m = v as Dictionary<string, object?> ?? [];
        var d = new ConfiguracionDeNoa();
        int E(string k, int x) => int.TryParse(m.GetValueOrDefault(k) as string, CultureInfo.InvariantCulture, out var n) && n >= 0 ? n : x;
        var terminos = (m.GetValueOrDefault("terminos_clinicos") as List<object?>)?.OfType<string>().Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).ToList();
        return new ConfiguracionDeNoa(
            E("cadencia_dias", d.CadenciaDias), E("semanas_de_observacion", d.SemanasDeObservacion), E("minimo_registros_nuevos", d.MinimoRegistrosNuevos),
            E("minimo_dias_nuevos", d.MinimoDiasNuevos), E("registros_para_patron", d.RegistrosParaPatron), E("semanas_para_patron", d.SemanasParaPatron),
            E("registros_para_indicio", d.RegistrosParaIndicio), E("maximo_caracteres", d.MaximoCaracteres),
            (m.GetValueOrDefault("seudonimizar_terceros") as string) != "false", terminos, E("maximo_preguntas", d.MaximoPreguntas));
    }
}
