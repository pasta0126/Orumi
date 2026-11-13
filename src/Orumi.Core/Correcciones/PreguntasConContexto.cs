using Orumi.Core.Ficheros;

namespace Orumi.Core.Correcciones;

public sealed record RegistroImplicado(string Id, string? Titulo, string ValorActual);

public sealed record PreguntaConContexto(
    string Id, string Valor, string Texto, string Campo, RegistroImplicado Registro, IReadOnlyList<RegistroImplicado> Otros, string? CreadaEl);

/// <summary>Preguntas pendientes con el contexto necesario para contestarlas, leídas de los ficheros (sin depender del índice).</summary>
public static class PreguntasConContexto
{
    private static readonly Dictionary<string, int> OrdenDeValor = new() { ["alta"] = 0, ["media"] = 1, ["baja"] = 2 };

    public static IReadOnlyList<PreguntaConContexto> Pendientes(Almacen almacen)
    {
        var registros = almacen.DeTipo("registro").Where(r => r.Id is not null).ToDictionary(r => r.Id!);
        RegistroImplicado Implicado(string id, string campo) => registros.TryGetValue(id, out var r)
            ? new RegistroImplicado(id, r.Frontmatter.GetValueOrDefault("titulo") as string, ValoresDeCampo.Resumen(r.Frontmatter.GetValueOrDefault(campo)))
            : new RegistroImplicado(id, null, "?");

        return almacen.DeTipo("pregunta")
            .Where(p => p.Id is not null && p.Frontmatter.GetValueOrDefault("estado") as string == "pendiente")
            .OrderBy(p => OrdenDeValor.GetValueOrDefault(p.Frontmatter.GetValueOrDefault("valor") as string ?? string.Empty, 3))
            .ThenBy(p => p.Frontmatter.GetValueOrDefault("creada_el") as string, StringComparer.Ordinal)
            .Select(p =>
            {
                var fm = p.Frontmatter;
                var campo = fm.GetValueOrDefault("campo") as string ?? string.Empty;
                var principal = fm.GetValueOrDefault("registro") as string ?? string.Empty;
                var otros = (fm.GetValueOrDefault("otros_registros") as List<object?> ?? []).OfType<string>().Select(o => Implicado(o, campo)).ToList();
                return new PreguntaConContexto(
                    p.Id!, fm.GetValueOrDefault("valor") as string ?? string.Empty, fm.GetValueOrDefault("texto") as string ?? string.Empty,
                    campo, Implicado(principal, campo), otros, fm.GetValueOrDefault("creada_el") as string);
            })
            .ToList();
    }
}
