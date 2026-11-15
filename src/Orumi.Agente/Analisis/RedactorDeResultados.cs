using System.Globalization;

namespace Orumi.Agente.Analisis;

/// <summary>
/// Texto en español de cada contraste, por plantilla (sin modelo): describe lo observado y su evidencia, sin causalidad, sin
/// recomendaciones y sin consejos (X23). Lo matizado por el usuario prevalece y se muestra.
/// </summary>
public static class RedactorDeResultados
{
    public static string Texto(ResultadoDeContraste r)
    {
        var efecto = r.Efecto.Replace('_', ' ');
        var cuando = r.RetardoHasta == 0 ? "el mismo día"
            : r.RetardoDesde == 0 ? $"ese día o en los {r.RetardoHasta} siguientes"
            : $"entre {r.RetardoDesde} y {r.RetardoHasta} día(s) después";
        string Pct(double? v) => v is null ? "—" : (v.Value * 100).ToString("0", CultureInfo.InvariantCulture) + " %";
        var conFactor = $"con «{r.FactorTexto}», «{efecto}» ({cuando}) aparece en {r.EfectoConFactor} de {r.DiasConFactor} día(s) ({Pct(r.TasaConFactor)})";
        var sinFactor = $"sin ese factor, en {r.EfectoSinFactor} de {r.DiasSinFactor} ({Pct(r.TasaSinFactor)})";

        var cuerpo = r.Veredicto switch
        {
            Veredicto.Apoyada => $"Los datos apoyan tu hipótesis: {conFactor}, y {sinFactor}. Es una asociación observada: no indica que sea la causa.",
            Veredicto.SinApoyo => $"Los datos no muestran la diferencia que esperabas: {conFactor}, y {sinFactor}.",
            _ => $"Aún no hay evidencia suficiente. Hasta ahora, {conFactor}, y {sinFactor}. Falta: {(r.Motivos.Count == 0 ? "más días" : string.Join("; ", r.Motivos))}.",
        };

        var notas = new List<string>();
        if (r.ExcluidosSinClima > 0)
        {
            notas.Add($"{r.ExcluidosSinClima} día(s) sin datos del clima, excluidos");
        }

        if (r.ExcluidosFactorSinDato > 0)
        {
            notas.Add($"{r.ExcluidosFactorSinDato} día(s) en que no se sabe cuánto tiempo estuviste fuera, excluidos");
        }

        if (r.ExcluidosEfectoSinDato > 0)
        {
            notas.Add($"{r.ExcluidosEfectoSinDato} día(s) sin dato del efecto (no se cuentan como que no ocurriera)");
        }

        var texto = cuerpo + (notas.Count > 0 ? " (" + string.Join("; ", notas) + ".)" : string.Empty);
        if (r.Revision is { } rv)
        {
            texto += rv.Estado switch
            {
                Orumi.Core.Hipotesis.Hipotesis.Confirmada => " Lo confirmaste tú" + (rv.Comentario is null ? "." : $": «{rv.Comentario}»."),
                Orumi.Core.Hipotesis.Hipotesis.Matizada => $" Lo matizaste tú: «{rv.Comentario}».",
                _ => r.Reabierto ? " Lo descartaste, pero hay evidencia nueva sustancial y vuelve a mostrarse." : string.Empty,
            };
        }

        return texto;
    }
}
