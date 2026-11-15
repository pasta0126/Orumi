using Orumi.Core.Hipotesis;

namespace Orumi.Core.Correcciones;

/// <summary>El usuario declara y revisa sus hipótesis (X22, spec <c>contraste-de-hipotesis</c>). Mira las contrasta; nunca las convierte en hechos.</summary>
public sealed partial class Operaciones
{
    /// <summary>Declara una hipótesis con una sola relación factor → efecto. Los factores de registro usan los conceptos ya descritos.</summary>
    public ResultadoOperacion DeclararHipotesis(string formulacion, string factor, string efecto, string descripcionDelEfecto, int retardoDesde, int retardoHasta)
    {
        try
        {
            var condiciones = (factor ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var conocidos = AlmacenDeHipotesis.Conceptos(datos);
            var conceptos = new Dictionary<string, string> { [efecto] = descripcionDelEfecto };
            foreach (var c in condiciones.Select(CondicionDeFactor.Analizar).Where(c => c.Tipo == "registro"))
            {
                if (!conocidos.TryGetValue(c.Nombre, out var descripcion))
                {
                    throw new OperacionInvalidaException($"El concepto «{c.Nombre}» aún no tiene descripción: usa «orumi hipotesis añadir» con --descripciones.");
                }

                conceptos[c.Nombre] = descripcion;
            }

            var h = AlmacenDeHipotesis.Anadir(datos, formulacion, condiciones, efecto, retardoDesde, retardoHasta, conceptos, ahora());
            return new ResultadoOperacion($"Hipótesis {h.Id} declarada: queda «por verificar».", [AlmacenDeHipotesis.Ruta(datos, h.Id)]);
        }
        catch (HipotesisInvalidaException e)
        {
            throw new OperacionInvalidaException(e.Message);
        }
    }

    /// <summary><paramref name="efectosActuales"/> es la evidencia que hay ahora: un contraste descartado solo se reabre si se duplica.</summary>
    public ResultadoOperacion RevisarHipotesis(string id, string? contraste, string accion, string? comentario, int efectosActuales)
    {
        try
        {
            var h = AlmacenDeHipotesis.Revisar(datos, id, contraste, accion, comentario, efectosActuales, ahora());
            return new ResultadoOperacion($"{id}{(contraste is null ? "" : "#" + contraste)}: {accion}. La hipótesis queda «{h.Estado}».", [AlmacenDeHipotesis.Ruta(datos, id)]);
        }
        catch (HipotesisInvalidaException e)
        {
            throw new OperacionInvalidaException(e.Message);
        }
    }
}
