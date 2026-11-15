namespace Orumi.Core.Correcciones;

/// <summary>Qué agente formuló una pregunta (spec <c>origen-de-las-preguntas</c>). Una pregunta sin agente escrito es de Mira, sin tocar su fichero.</summary>
public static class AgentesDePregunta
{
    public const string Mira = "Mira";
    public const string Noa = "Noa";

    public static string De(Dictionary<string, object?> frontmatter) =>
        frontmatter.GetValueOrDefault("generada_por") is Dictionary<string, object?> g && g.GetValueOrDefault("agente") is string a && a.Equals(Noa, StringComparison.OrdinalIgnoreCase) ? Noa : Mira;

    public static bool EsValido(string? agente) => agente is null || agente.Equals(Mira, StringComparison.OrdinalIgnoreCase) || agente.Equals(Noa, StringComparison.OrdinalIgnoreCase);
}
