using System.Globalization;
using Orumi.Core.Aprendizaje;
using Orumi.Core.Ficheros;
using Orumi.Core.Hipotesis;

namespace Orumi.Agente.Analisis;

/// <summary>
/// Preguntas de contraste (spec <c>preguntas-de-mira</c>, X63): por la presencia o ausencia de un efecto ya declarado por el usuario en un día sin
/// dato, o por la duración de estar al aire libre. Pocas (dentro del máximo de preguntas de Mira), neutras, sin sugerir la respuesta ni aconsejar.
/// «Respondida» = ocurrió; «No sucedió» = ausencia confirmada. Nunca se repite un (concepto, día) ya preguntado.
/// </summary>
public static class PreguntasDeContraste
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    public static IReadOnlyList<string> Crear(
        string datos, Configuracion? configuracion, UmbralesDeAnalisis u, IReadOnlyList<HuecoDeContraste> huecos, IReadOnlyList<ResultadoDeContraste> resultados, DateTimeOffset ahora)
    {
        var almacen = Almacen.Cargar(datos);
        var preguntas = almacen.DeTipo("pregunta").ToList();
        var cupoDeMira = configuracion?.MaximoPendientes ?? 5;
        var pendientesDeMira = preguntas.Count(p => p.Frontmatter.GetValueOrDefault("estado") as string == "pendiente" && EsDeMira(p));
        var libres = Math.Min(cupoDeMira - pendientesDeMira, u.MaximoPreguntasDeContraste);
        if (libres <= 0)
        {
            return [];
        }

        var yaPreguntados = preguntas
            .Where(p => p.Frontmatter.GetValueOrDefault("concepto") is string && p.Frontmatter.GetValueOrDefault("dia") is string)
            .Select(p => ((string)p.Frontmatter["concepto"]!, (string)p.Frontmatter["dia"]!)).ToHashSet();
        var rebajadas = MotorDeAprendizaje.ClasesRebajadas(datos);
        var hipotesis = AlmacenDeHipotesis.Listar(datos).ToDictionary(h => h.Id);
        var contraste = resultados.ToDictionary(r => r.Id);

        // Primero la duración (un dato estructural simple), luego los efectos del grupo con menos días conocidos y los más recientes.
        var ordenados = huecos
            .OrderBy(h => h.Tipo == "duracion" ? 0 : 1)
            .ThenBy(h => rebajadas.Contains(Clase(h)) ? 1 : 0)
            .ThenBy(h => h.Tipo == "duracion" ? 0 : (h.ConFactor == true) == (contraste[$"{h.Hipotesis}#{h.Contraste}"].DiasConFactor <= contraste[$"{h.Hipotesis}#{h.Contraste}"].DiasSinFactor) ? 0 : 1)
            .ThenByDescending(h => h.Dia)
            .ToList();

        var creadas = new List<string>();
        var vistos = new HashSet<(string, string)>(yaPreguntados);
        var ids = preguntas.Select(p => p.Id).OfType<string>().ToHashSet();
        foreach (var hueco in ordenados)
        {
            if (creadas.Count >= libres)
            {
                break;
            }

            var dia = hueco.Dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (!vistos.Add((hueco.Concepto, dia)))
            {
                continue;
            }

            var h = hipotesis[hueco.Hipotesis];
            var cuando = hueco.Dia.ToString("d 'de' MMMM", Es);
            var texto = hueco.Tipo == "duracion"
                ? $"¿Cuánto tiempo estuviste al aire libre el {cuando}? Responde con la duración aproximada o ciérrala como «No lo recuerdo»."
                : h.PreguntaDe(hueco.Concepto, cuando) + " Responde con lo que pasó o ciérrala como «No sucedió».";
            string id;
            do
            {
                id = $"preg-{ahora:yyyyMMdd}-{Identificadores.Sufijo(4)}";
            }
            while (!ids.Add(id));

            var fm = new Dictionary<string, object?>
            {
                ["id"] = id,
                ["tipo"] = "pregunta",
                ["registro"] = hueco.RegistroDeReferencia,
                ["otros_registros"] = new List<object?>(),
                ["campo"] = "contraste",
                ["clase_pregunta"] = Clase(hueco),
                ["hipotesis"] = hueco.Hipotesis,
                ["contraste"] = hueco.Contraste,
                ["concepto"] = hueco.Concepto,
                ["dia"] = dia,
                ["texto"] = texto,
                ["estado"] = "pendiente",
                ["valor"] = "baja",
                ["creada_el"] = ahora.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
                ["generada_por"] = new Dictionary<string, object?> { ["agente"] = "Mira", ["version"] = "0.1.0-mvp", ["modelo"] = "determinista" },
            };
            var ruta = Path.Combine(datos, Identificadores.RutaRelativa(id)!);
            EscrituraAtomica.EscribirNuevo(ruta, FrontmatterCanonico.Escribir(fm, $"Pregunta de contraste de Mira sobre [[{hueco.RegistroDeReferencia}]].\n", "pregunta"));
            creadas.Add(id);
        }

        return creadas;
    }

    private static string Clase(HuecoDeContraste h) => $"contraste:{h.Hipotesis}#{h.Contraste}";

    /// <summary>Las preguntas sin agente escrito se entienden de Mira.</summary>
    internal static bool EsDeMira(Documento p) =>
        (p.Frontmatter.GetValueOrDefault("generada_por") as Dictionary<string, object?>)?.GetValueOrDefault("agente") as string is null or "Mira";
}
