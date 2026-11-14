using System.Text.RegularExpressions;
using Orumi.Core.Aprendizaje;

namespace Orumi.Core.Correcciones;

/// <summary>El usuario gobierna lo que Mira ha aprendido (X64): desactivar, activar a mano o borrar una regla.</summary>
public sealed partial class Operaciones
{
    [GeneratedRegex(@"^apr-[a-z0-9-]{1,80}$")]
    public static partial Regex IdDeAprendizaje();

    public ResultadoOperacion CambiarAprendizaje(string id, string accion)
    {
        if (!IdDeAprendizaje().IsMatch(id))
        {
            throw new OperacionInvalidaException("Identificador de aprendizaje no válido.");
        }

        try
        {
            var r = accion switch
            {
                "desactivar" => MotorDeAprendizaje.Desactivar(datos, id, ahora()),
                "activar" => MotorDeAprendizaje.Activar(datos, id, ahora()),
                "borrar" => MotorDeAprendizaje.Borrar(datos, id, ahora()),
                _ => throw new OperacionInvalidaException("La acción debe ser desactivar, activar o borrar."),
            };
            return new ResultadoOperacion(
                accion switch
                {
                    "desactivar" => $"{r.Id} desactivado: Mira dejará de aplicarlo.",
                    "activar" => $"{r.Id} activado: Mira lo aplica y no lo restablecerá por su cuenta.",
                    _ => $"{r.Id} borrado: solo volverá a aprenderse con evidencia nueva.",
                },
                [MotorDeAprendizaje.Ruta(datos, id)]);
        }
        catch (AprendizajeNoEncontradoException e)
        {
            throw new OperacionInvalidaException("No existe el aprendizaje «" + id + "». " + e.Message);
        }
    }
}
