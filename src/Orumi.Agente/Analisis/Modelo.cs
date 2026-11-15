using System.Globalization;

namespace Orumi.Agente.Analisis;

/// <summary>Umbrales del análisis (X20, X21): configuración ajustable, no parte fija del modelo.</summary>
public sealed record UmbralesDeAnalisis(
    double DiferenciaApoyo = 0.25, double DiferenciaSinApoyo = 0.10, int MinimoDiasPorGrupo = 4, int MinimoEfectos = 3, int MinimoSemanas = 2, int SemanasDeObservacion = 4,
    int MaximoPreguntasDeContraste = 3, int DiasDeEvidencia = 40)
{
    public static UmbralesDeAnalisis Leer(Dictionary<string, object?>? m)
    {
        if (m is null)
        {
            return new UmbralesDeAnalisis();
        }

        var d = new UmbralesDeAnalisis();
        double N(string k, double x) => double.TryParse(m.GetValueOrDefault(k) as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : x;
        int E(string k, int x) => int.TryParse(m.GetValueOrDefault(k) as string, CultureInfo.InvariantCulture, out var v) && v >= 0 ? v : x;
        return new UmbralesDeAnalisis(
            N("diferencia_apoyo", d.DiferenciaApoyo), N("diferencia_sin_apoyo", d.DiferenciaSinApoyo), E("minimo_dias_por_grupo", d.MinimoDiasPorGrupo),
            E("minimo_efectos", d.MinimoEfectos), E("minimo_semanas", d.MinimoSemanas), E("semanas_de_observacion", d.SemanasDeObservacion),
            E("maximo_preguntas_de_contraste", d.MaximoPreguntasDeContraste), E("dias_de_evidencia", d.DiasDeEvidencia));
    }
}

public enum Observado
{
    SinDato,
    Presente,
    Ausente,
}

public static class Veredicto
{
    public const string Apoyada = "apoyada";
    public const string SinApoyo = "sin_apoyo";
    public const string Insuficiente = "insuficiente";
}

/// <summary>Un día usado como evidencia: qué se vio, de dónde sale y qué clima había.</summary>
public sealed record EvidenciaDeDia(
    DateOnly Fecha, string Ciudad, bool? Factor, string Efecto, IReadOnlyList<string> Registros, string Clima, IReadOnlyList<string> Citas);

public sealed record ResultadoDeContraste(
    string Hipotesis, string Contraste, string Formulacion, string FactorTexto, string Efecto, int RetardoDesde, int RetardoHasta,
    string Veredicto, int DiasConFactor, int EfectoConFactor, int DiasSinFactor, int EfectoSinFactor,
    double? TasaConFactor, double? TasaSinFactor, double? Diferencia,
    int EfectosTotales, int SemanasConEfecto, int ExcluidosSinClima, int ExcluidosFactorSinDato, int ExcluidosEfectoSinDato,
    IReadOnlyList<string> Motivos, IReadOnlyList<EvidenciaDeDia> Evidencia, string Texto,
    Orumi.Core.Hipotesis.RevisionDeContraste? Revision, bool Reabierto)
{
    public string Id => $"{Hipotesis}#{Contraste}";
}

public sealed record ResultadoDelAnalisis(
    DateOnly? PrimerRegistro, int DiasDeObservacion, bool EnObservacion, int DiasConRegistros,
    IReadOnlyList<ResultadoDeContraste> Contrastes, IReadOnlyList<string> Descartados);

/// <summary>Un día sin dato por el que Mira puede preguntar (preguntas de contraste, X63).</summary>
public sealed record HuecoDeContraste(
    string Hipotesis, string Contraste, string Concepto, DateOnly Dia, string RegistroDeReferencia, string Tipo /* efecto | duracion */, bool? ConFactor = null);
