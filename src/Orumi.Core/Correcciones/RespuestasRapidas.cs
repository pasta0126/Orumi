using System.Text.RegularExpressions;

namespace Orumi.Core.Correcciones;

/// <summary>
/// Una respuesta cerrada ya fijada para una pregunta (spec <c>respuestas-rapidas</c>). Un clic equivale a escribir esa respuesta: o responde con
/// <paramref name="Valor"/> (y, en un conflicto, sobre <paramref name="Registro"/>), o cierra con <paramref name="Cierre"/> (p. ej. «no_sucedio»).
/// <paramref name="Fusiona"/> solo en una coincidencia: además de responder, fusiona los dos registros.
/// </summary>
public sealed record OpcionDeRespuesta(string Etiqueta, string? Valor, string? Registro = null, string? Cierre = null, bool Fusiona = false);

/// <summary>
/// Opciones de respuesta rápida, derivadas siempre del propio contenido de la pregunta (no se guardan: las preguntas ya existentes las tienen al instante
/// y no pueden quedar desfasadas). Solo hay opciones donde el conjunto de respuestas es cerrado; el resto sigue siendo una pregunta abierta.
/// </summary>
public static partial class RespuestasRapidas
{
    public const string ConceptoDeDuracion = "exterior_duracion";

    [GeneratedRegex(@"resuel", RegexOptions.IgnoreCase)]
    private static partial Regex Resolucion();

    public static IReadOnlyList<OpcionDeRespuesta> De(
        Dictionary<string, object?> pregunta, string? claseDelRegistro, RegistroImplicado? registro, IReadOnlyList<RegistroImplicado> otros)
    {
        if (AgentesDePregunta.De(pregunta) == AgentesDePregunta.Noa)
        {
            return [];
        }

        var campo = pregunta.GetValueOrDefault("campo") as string ?? string.Empty;
        var texto = pregunta.GetValueOrDefault("texto") as string ?? string.Empty;
        switch (campo)
        {
            case "coincidencia":
                return
                [
                    new OpcionDeRespuesta("Es el mismo", "misma", Fusiona: true),
                    new OpcionDeRespuesta("Relacionados", "misma"),
                    new OpcionDeRespuesta("Son distintos", "distinta"),
                ];
            case "ciudad":
                return pregunta.GetValueOrDefault("ciudad_tipo") as string is "confirmar" or "mencion"
                    ? [new OpcionDeRespuesta("Sí", "Sí"), new OpcionDeRespuesta("No", "No")]
                    : [];
            case "contraste":
                return pregunta.GetValueOrDefault("concepto") as string == ConceptoDeDuracion
                    ? []
                    : [new OpcionDeRespuesta("Sí", "Sí"), new OpcionDeRespuesta("No", null, Cierre: "no_sucedio")];
        }

        if (otros.Count > 0 && registro is not null)
        {
            // Conflicto: un botón por cada valor candidato (solo los que el campo acepta tal cual).
            var candidatas = new List<OpcionDeRespuesta>();
            foreach (var r in new[] { registro }.Concat(otros))
            {
                if (string.IsNullOrWhiteSpace(r.ValorActual) || r.ValorActual is "desconocido" or "?" || !Aplicable(campo, r.ValorActual))
                {
                    continue;
                }

                candidatas.Add(new OpcionDeRespuesta($"{r.ValorActual} · {r.Titulo ?? r.Id}", r.ValorActual, r.Id));
            }

            return candidatas.Count >= 2 ? candidatas : [];
        }

        if (campo == "resultados" && claseDelRegistro == "problema" && Resolucion().IsMatch(texto))
        {
            return [new OpcionDeRespuesta("Sí, quedó resuelto", "Resuelto"), new OpcionDeRespuesta("No, sigue abierto", "Sigue sin resolverse")];
        }

        return [];
    }

    /// <summary>En una coincidencia dudosa, «Es el mismo» fusiona en el registro más antiguo (empate: el de la pregunta). Devuelve (destino, origen).</summary>
    public static (string Destino, string Origen) DestinoDeLaFusion(Orumi.Core.Ficheros.Almacen almacen, string preguntaId)
    {
        var pregunta = almacen.DeTipo("pregunta").FirstOrDefault(p => p.Id == preguntaId)
                       ?? throw new OperacionInvalidaException($"No existe ninguna pregunta con el identificador «{preguntaId}».");
        if (pregunta.Frontmatter.GetValueOrDefault("campo") as string != "coincidencia" || pregunta.Frontmatter.GetValueOrDefault("registro") is not string principal
            || (pregunta.Frontmatter.GetValueOrDefault("otros_registros") as List<object?> ?? []).OfType<string>().FirstOrDefault() is not { } otro)
        {
            throw new OperacionInvalidaException("Esta pregunta no es una coincidencia de dos registros: no hay nada que fusionar.");
        }

        string Cuando(string id) => almacen.DeTipo("registro").FirstOrDefault(r => r.Id == id)?.Frontmatter.GetValueOrDefault("comunicada_el") as string ?? string.Empty;
        return string.CompareOrdinal(Cuando(otro), Cuando(principal)) < 0 ? (otro, principal) : (principal, otro);
    }

    /// <summary>La opción solo existe si el campo la acepta con el mismo analizador que una respuesta escrita.</summary>
    private static bool Aplicable(string campo, string valor)
    {
        if (!ValoresDeCampo.EsCorregible(campo))
        {
            return false;
        }

        try
        {
            ValoresDeCampo.Analizar(campo, valor, "desconocido");
            return true;
        }
        catch (OperacionInvalidaException)
        {
            return false;
        }
    }
}
