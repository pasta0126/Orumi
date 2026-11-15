using Orumi.Acceso;
using Orumi.Agente;
using Orumi.Core.Correcciones;
using Orumi.Core.Ficheros;

namespace Orumi.Panel;

public sealed record RespuestaDeEscritura(int Estado, string? Mensaje, string? Error, string? Deshacer = null);

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

    private sealed record PorDeshacer(DateTimeOffset Caduca, IReadOnlyDictionary<string, string?> Originales, IReadOnlyDictionary<string, string> Despues);

    private readonly Dictionary<string, PorDeshacer> _porDeshacer = [];
    private readonly object _cerrojoDeshacer = new();

    /// <summary>Segundos durante los que se puede deshacer una respuesta rápida que cambió varios ficheros (p. ej. una fusión).</summary>
    public const int SegundosParaDeshacer = 60;

    /// <summary>
    /// Como <see cref="Ejecutar"/> pero con varias operaciones seguidas (p. ej. responder y fusionar) que se pueden deshacer juntas durante un minuto.
    /// Devuelve en <see cref="RespuestaDeEscritura.Deshacer"/> la ficha para deshacerlas.
    /// </summary>
    public RespuestaDeEscritura EjecutarConDeshacer(string operacion, string? id, string? correo, Func<Operaciones, IReadOnlyList<ResultadoOperacion>> acciones)
    {
        ResultadoOperacion[] resultados = [];
        var respuesta = Intentar(op =>
        {
            resultados = [.. acciones(op)];
            return new ResultadoOperacion(string.Join(" ", resultados.Select(r => r.Mensaje)), [.. resultados.SelectMany(r => r.Ficheros)]);
        });
        if (respuesta.Estado == 200)
        {
            var originales = new Dictionary<string, string?>();
            foreach (var r in resultados.Where(r => r.Originales is not null))
            {
                foreach (var (ruta, anterior) in r.Originales!)
                {
                    originales.TryAdd(ruta, anterior); // vale el estado anterior a la primera operación
                }
            }

            var despues = originales.Keys.Where(File.Exists).ToDictionary(r => r, r => File.ReadAllText(r, System.Text.Encoding.UTF8));
            var ficha = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
            lock (_cerrojoDeshacer)
            {
                foreach (var caducada in _porDeshacer.Where(p => p.Value.Caduca <= reloj()).Select(p => p.Key).ToList())
                {
                    _porDeshacer.Remove(caducada);
                }

                _porDeshacer[ficha] = new PorDeshacer(reloj().AddSeconds(SegundosParaDeshacer), originales, despues);
            }

            respuesta = respuesta with { Deshacer = ficha };
        }

        auditoria(Auditoria.Linea(reloj(), correo, operacion, id, respuesta.Estado == 200 ? "ok" : $"rechazada-{respuesta.Estado}"));
        return respuesta;
    }

    /// <summary>Deshace lo que hizo una respuesta rápida, si aún está a tiempo y nada ha cambiado desde.</summary>
    public RespuestaDeEscritura Deshacer(string ficha, string? correo)
    {
        PorDeshacer? p;
        lock (_cerrojoDeshacer)
        {
            _porDeshacer.Remove(ficha, out p);
        }

        if (p is null || p.Caduca <= reloj())
        {
            return new RespuestaDeEscritura(404, null, "Ya no se puede deshacer (pasó el tiempo o no existe).");
        }

        var respuesta = Intentar(_ => Operaciones.Revertir(p.Originales, p.Despues)
            ? new ResultadoOperacion("Hecho: vuelve a estar como antes.", [])
            : throw new OperacionInvalidaException("Algo ha cambiado desde entonces y no se puede deshacer sin pisarlo."));
        auditoria(Auditoria.Linea(reloj(), correo, "deshacer-rapida", null, respuesta.Estado == 200 ? "ok" : $"rechazada-{respuesta.Estado}"));
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
