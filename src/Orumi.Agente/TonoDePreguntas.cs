using System.Text.RegularExpressions;
using Orumi.Core.Texto;

namespace Orumi.Agente;

/// <summary>
/// Las preguntas de Mira no aconsejan, no valoran ni sugieren la respuesta (spec <c>preguntas-de-mira</c>, «Tono»). Comprobación de último recurso sobre lo que
/// propone el modelo: lo que no cumple se descarta y se cuenta.
/// </summary>
public static partial class TonoDePreguntas
{
    [GeneratedRegex(@"\b(deberias|deberia|debes|debe ser|tendrias que|tienes que|te recomiendo|te aconsejo|te sugiero|recomendable|convendria|seria mejor|seria buena idea|lo mejor es|haria falta|ojala|que pena|lastima|enhorabuena|felicidades|que bien|genial|excelente)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Consejo();

    public static bool EsValido(string? texto) =>
        !string.IsNullOrWhiteSpace(texto) && texto.Length <= 400 && !Consejo().IsMatch(Normalizacion.SinAcentos(texto));
}
