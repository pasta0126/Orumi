using Orumi.Acceso;
using Orumi.Agente;
using Orumi.Core.Correcciones;
using Orumi.Core.Ficheros;

namespace Orumi.Panel;

public sealed record RespuestaDeEscritura(int Estado, string? Mensaje, string? Error);

/// <summary>
/// Escrituras del panel. Solo existen las operaciones de responder, cerrar, corregir y rechazar (las mismas de la línea de comandos):
/// ninguna toca capturas. Toman el bloqueo frente a Mira, validan antes de escribir y dejan una línea de auditoría sin valores.
/// </summary>
public sealed class EscrituraDelPanel(RepositorioDeDatos repositorio, Func<DateTimeOffset> reloj, Action<string> auditoria)
{
    public RespuestaDeEscritura Ejecutar(string operacion, string? id, string? correo, Func<Operaciones, ResultadoOperacion> accion)
    {
        var respuesta = Intentar(accion);
        auditoria(Auditoria.Linea(reloj(), correo, operacion, id, respuesta.Estado == 200 ? "ok" : $"rechazada-{respuesta.Estado}"));
        return respuesta;
    }

    private RespuestaDeEscritura Intentar(Func<Operaciones, ResultadoOperacion> accion)
    {
        using var bloqueo = BloqueoDeEjecucion.Adquirir(repositorio.Datos);
        if (bloqueo is null)
        {
            return new RespuestaDeEscritura(409, null, "Mira está procesando ahora mismo. Vuelve a intentarlo dentro de unos minutos.");
        }

        try
        {
            var r = accion(new Operaciones(repositorio.Datos, reloj));
            repositorio.Invalidar();
            return new RespuestaDeEscritura(200, r.Mensaje, null);
        }
        catch (OperacionInvalidaException e)
        {
            return new RespuestaDeEscritura(e.Message.StartsWith("No existe", StringComparison.Ordinal) ? 404 : 400, null, e.Message);
        }
        catch (EscrituraFallidaException)
        {
            return new RespuestaDeEscritura(503, null, "No se pudo guardar el cambio. Reinténtalo.");
        }
    }
}
