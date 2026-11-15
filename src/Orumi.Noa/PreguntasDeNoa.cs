using System.Security.Cryptography;
using Orumi.Core.Aprendizaje;
using Orumi.Core.Correcciones;
using Orumi.Core.Ficheros;

namespace Orumi.Noa;

/// <summary>
/// Las preguntas de Noa (spec <c>origen-de-las-preguntas</c>, decisiones 15–17 del diseño). Viven en <c>perfil/preguntas/</c>, no van atadas a un registro
/// sino a un rasgo (o a un apartado sin datos) y tienen su propio cupo y su propio aprendizaje. Las candidatas las elige el código, no el modelo:
/// una conjetura o un indicio que una respuesta podría afianzar, y los apartados sin datos. Nunca preguntan por terceros ni sugieren la respuesta.
/// </summary>
public static class PreguntasDeNoa
{
    public static string Carpeta(string datos) => Path.Combine(AlmacenDePerfil.Directorio(datos), "preguntas");

    /// <summary>Clase de pregunta de Noa para el aprendizaje: <c>noa:&lt;apartado&gt;</c>.</summary>
    public static string Clase(string apartado) => ClaseDePregunta.PrefijoDeNoa + apartado;

    private static readonly string[] SinPreguntas = [Apartados.EnergiaYAnimo, Apartados.Superficie];

    private sealed record Candidata(string? Rasgo, string Apartado, string Texto, int Orden, string? Contradice = null);

    /// <summary>Crea las preguntas que quepan en el cupo de Noa. Devuelve los ids creados.</summary>
    public static IReadOnlyList<string> Formular(string datos, Perfil perfil, ConfiguracionDeNoa config, DateTimeOffset ahora, IReadOnlyList<(string A, string B)>? contradicciones = null)
    {
        if (config.MaximoPreguntas <= 0)
        {
            return [];
        }

        var almacen = Almacen.Cargar(datos, incluirPerfil: true);
        var deNoa = almacen.DeTipo("pregunta").Where(p => AgentesDePregunta.De(p.Frontmatter) == AgentesDePregunta.Noa).ToList();
        var cupo = config.MaximoPreguntas - deNoa.Count(p => p.Frontmatter.GetValueOrDefault("estado") as string == "pendiente");
        if (cupo <= 0)
        {
            return [];
        }

        // No se repite lo que ya se preguntó, se contestara o no.
        var yaPreguntado = deNoa.Select(p => (p.Frontmatter.GetValueOrDefault("rasgo") as string) ?? "apartado:" + p.Frontmatter.GetValueOrDefault("apartado")).ToHashSet();
        var yaContradichos = deNoa.Where(p => p.Frontmatter.ContainsKey("contradice")).Select(p => (p.Frontmatter["rasgo"] as string) + "|" + (p.Frontmatter["contradice"] as string)).ToHashSet();
        var rebajadas = MotorDeAprendizaje.ClasesRebajadasDeNoa(datos);

        var candidatas = new List<Candidata>();
        var porClave = perfil.Rasgos.Where(r => r.Visible).ToDictionary(r => r.Clave);
        foreach (var (a, b) in contradicciones ?? [])
        {
            if (porClave.TryGetValue(a, out var ra) && porClave.TryGetValue(b, out var rb) && !ra.Sensible && !rb.Sensible && !yaContradichos.Contains(a + "|" + b)
                && !SinPreguntas.Contains(ra.Apartado) && !SinPreguntas.Contains(rb.Apartado))
            {
                candidatas.Add(new Candidata(a, ra.Apartado, $"Noa ve dos cosas que no terminan de encajar: «{ra.Texto}» y «{rb.Texto}». ¿Cómo lo vives tú, con tus palabras?", -1, b));
            }
        }

        foreach (var r in perfil.Rasgos.Where(r => r.Visible && r.Origen == Rasgo.DelAgente && r.Estado == EstadosDeRasgo.Activo && !r.Sensible
                                                   && r.Nivel is Niveles.Conjetura or Niveles.Indicio && !SinPreguntas.Contains(r.Apartado) && !yaPreguntado.Contains(r.Clave)))
        {
            candidatas.Add(new Candidata(r.Clave, r.Apartado, $"Noa deduce de tus notas: «{r.Texto}». ¿Cómo lo describirías tú, con tus palabras?", r.Nivel == Niveles.Conjetura ? 0 : 1));
        }

        foreach (var slug in perfil.ApartadosSinDatos.Where(a => !SinPreguntas.Contains(a) && Apartados.Existe(a) && !yaPreguntado.Contains("apartado:" + a)))
        {
            candidatas.Add(new Candidata(null, slug, $"Noa todavía no tiene datos sobre «{Apartados.Buscar(slug)!.Titulo}». Si quieres, cuéntale algo con tus palabras.", 2));
        }

        var creadas = new List<string>();
        var usados = almacen.Documentos.Where(d => d.Id is not null).Select(d => d.Id!).ToHashSet();
        foreach (var c in candidatas.OrderBy(c => rebajadas.Contains(Clase(c.Apartado)) ? 1 : 0).ThenBy(c => c.Orden).ThenBy(c => c.Rasgo ?? c.Apartado, StringComparer.Ordinal).Take(cupo))
        {
            var id = NuevoId(ahora, usados);
            usados.Add(id);
            var fm = new Dictionary<string, object?>
            {
                ["id"] = id,
                ["tipo"] = "pregunta",
                ["campo"] = "perfil",
                ["clase_pregunta"] = Clase(c.Apartado),
                ["texto"] = c.Texto,
                ["estado"] = "pendiente",
                ["valor"] = c.Orden <= 0 ? "media" : "baja",
                ["creada_el"] = ahora.ToString("yyyy-MM-dd'T'HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture),
                ["generada_por"] = new Dictionary<string, object?> { ["agente"] = AgentesDePregunta.Noa },
            };
            if (c.Rasgo is not null)
            {
                fm["rasgo"] = c.Rasgo;
                if (c.Contradice is not null)
                {
                    fm["contradice"] = c.Contradice;
                }
            }
            else
            {
                fm["apartado"] = c.Apartado;
            }

            var ruta = Path.Combine(Carpeta(datos), id + ".md");
            Directory.CreateDirectory(Carpeta(datos));
            EscrituraAtomica.EscribirNuevo(ruta, FrontmatterCanonico.Escribir(fm, c.Rasgo is null ? $"Pregunta de Noa sobre el apartado {c.Apartado}.\n" : $"Pregunta de Noa sobre el rasgo {c.Rasgo}.\n", "pregunta"));
            creadas.Add(id);
        }

        return creadas;
    }

    private static string NuevoId(DateTimeOffset ahora, HashSet<string> usados)
    {
        const string Alfabeto = "abcdefghijklmnopqrstuvwxyz0123456789";
        while (true)
        {
            var sufijo = new string(Enumerable.Range(0, 4).Select(_ => Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)]).ToArray());
            var id = $"preg-{ahora:yyyyMMdd}-{sufijo}";
            if (!usados.Contains(id))
            {
                return id;
            }
        }
    }
}
