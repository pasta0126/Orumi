using Orumi.Core.Ficheros;

namespace Orumi.Core.Correcciones;

public sealed record RegistroImplicado(string Id, string? Titulo, string ValorActual);

/// <param name="Agente">Quién la formuló (Mira o Noa). Las anteriores, sin agente escrito, son de Mira.</param>
/// <param name="Rasgo">Solo las de Noa: el rasgo del perfil por el que pregunta (o <c>null</c> si habla de un apartado entero).</param>
public sealed record PreguntaConContexto(
    string Id, string Valor, string Texto, string Campo, RegistroImplicado? Registro, IReadOnlyList<RegistroImplicado> Otros, string? CreadaEl,
    string Agente = AgentesDePregunta.Mira, string? Rasgo = null, string? Apartado = null, IReadOnlyList<OpcionDeRespuesta>? Opciones = null);

/// <summary>Preguntas pendientes con el contexto necesario para contestarlas, leídas de los ficheros (sin depender del índice).</summary>
public static class PreguntasConContexto
{
    private static readonly Dictionary<string, int> OrdenDeValor = new() { ["alta"] = 0, ["media"] = 1, ["baja"] = 2 };

    public static IReadOnlyList<PreguntaConContexto> Pendientes(Almacen almacen, string? agente = null)
    {
        var registros = almacen.DeTipo("registro").Where(r => r.Id is not null).ToDictionary(r => r.Id!);
        RegistroImplicado Implicado(string id, string campo) => registros.TryGetValue(id, out var r)
            ? new RegistroImplicado(id, r.Frontmatter.GetValueOrDefault("titulo") as string, ValoresDeCampo.Resumen(r.Frontmatter.GetValueOrDefault(campo)))
            : new RegistroImplicado(id, null, "?");

        return almacen.DeTipo("pregunta")
            .Where(p => p.Id is not null && p.Frontmatter.GetValueOrDefault("estado") as string == "pendiente"
                        && (agente is null || AgentesDePregunta.De(p.Frontmatter).Equals(agente, StringComparison.OrdinalIgnoreCase))
                        && !(registros.TryGetValue(p.Frontmatter.GetValueOrDefault("registro") as string ?? string.Empty, out var rp) && rp.Frontmatter.ContainsKey("estado")))
            .OrderBy(p => OrdenDeValor.GetValueOrDefault(p.Frontmatter.GetValueOrDefault("valor") as string ?? string.Empty, 3))
            .ThenBy(p => p.Frontmatter.GetValueOrDefault("creada_el") as string, StringComparer.Ordinal)
            .Select(p =>
            {
                var fm = p.Frontmatter;
                var campo = fm.GetValueOrDefault("campo") as string ?? string.Empty;
                var principal = fm.GetValueOrDefault("registro") as string;
                var otros = (fm.GetValueOrDefault("otros_registros") as List<object?> ?? []).OfType<string>().Select(o => Implicado(o, campo)).ToList();
                var implicado = principal is null ? null : Implicado(principal, campo);
                var claseDelRegistro = principal is not null && registros.TryGetValue(principal, out var rr) ? rr.Frontmatter.GetValueOrDefault("clase") as string : null;
                return new PreguntaConContexto(
                    p.Id!, fm.GetValueOrDefault("valor") as string ?? string.Empty, fm.GetValueOrDefault("texto") as string ?? string.Empty,
                    campo, implicado, otros, fm.GetValueOrDefault("creada_el") as string,
                    AgentesDePregunta.De(fm), fm.GetValueOrDefault("rasgo") as string, fm.GetValueOrDefault("apartado") as string,
                    RespuestasRapidas.De(fm, claseDelRegistro, implicado, otros));
            })
            .ToList();
    }
}
