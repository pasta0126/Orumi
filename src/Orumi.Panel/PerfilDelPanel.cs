using System.Text.RegularExpressions;
using Orumi.Acceso;
using Orumi.Agente;
using Orumi.Agente.Etiquetado;
using Orumi.Core.Correcciones;
using Orumi.Noa;

namespace Orumi.Panel;

/// <summary>
/// El perfil de Noa en el panel (spec <c>perfil-personal</c>, tarea 4). Lee <c>perfil/</c> y ofrece las mismas acciones del usuario que la línea de comandos
/// (corregir, matizar, descartar, ocultar, borrar, borrar todo), la solicitud de una generación y la descarga en Markdown. Todo pasa por el bloqueo de Noa
/// (una generación a la vez; las acciones esperan) y no toca capturas, registros ni preguntas.
/// </summary>
public sealed class PerfilDelPanel(
    RepositorioDeDatos repositorio, Func<DateTimeOffset> reloj, Action<string> auditoria,
    ConfiguracionDeNoa? config, Configuracion? configuracion, Func<IProveedorDeTexto?> proveedor, Func<string, string?> entorno)
{
    private static readonly Regex ObjetivoValido = new(@"^(?:[a-z][a-z0-9_]{2,50}|apartado:[a-z][a-z_]{2,40}|rec-[0-9a-f]{8})$", RegexOptions.Compiled);

    public static bool EsObjetivoValido(string objetivo) => ObjetivoValido.IsMatch(objetivo);

    private string Datos => repositorio.Datos;

    // ---------------------------------------------------------------- lectura

    public object Ver()
    {
        var p = AlmacenDePerfil.Leer(Datos);
        var generaciones = AlmacenDePerfil.Generaciones(Datos).OrderBy(g => g.Fecha).ToList();
        var ultima = generaciones.LastOrDefault();
        var ultimaGenerada = generaciones.LastOrDefault(g => g.Resultado == Generacion.Generada);
        var generando = EstaGenerando();
        return new
        {
            activada = config is not null,
            puedeGenerar = config is not null && proveedor() is not null,
            generando,
            cadenciaDias = config?.CadenciaDias,
            semanasDeObservacion = config?.SemanasDeObservacion,
            proxima = config is not null && ultimaGenerada is not null ? ultimaGenerada.Fecha.AddDays(config.CadenciaDias).ToString("yyyy-MM-dd") : null,
            ultimaEjecucion = ultima is null ? null : new { fecha = ultima.Fecha.ToString("yyyy-MM-dd HH:mm"), resultado = ultima.Resultado, detalle = ultima.Detalle, motivo = ultima.Motivo },
            perfil = p is null ? null : new
            {
                version = p.Version,
                generada = p.Generada.ToString("yyyy-MM-dd"),
                registrosVistos = p.RegistrosVistos,
                apartados = Apartados.Todos.Select(a => new
                {
                    slug = a.Slug,
                    titulo = a.Titulo,
                    oculto = p.ApartadosOcultos.Contains(a.Slug),
                    sinDatos = p.ApartadosSinDatos.Contains(a.Slug),
                }),
                rasgos = p.Rasgos.Where(r => r.Estado is EstadosDeRasgo.Activo or EstadosDeRasgo.Matizado).Select(r => new
                {
                    clave = r.Clave,
                    apartado = r.Apartado,
                    texto = r.Texto,
                    certeza = r.Certeza,
                    nivel = r.Nivel,
                    sensible = r.Sensible,
                    oculto = r.Oculto,
                    estado = r.Estado,
                    origen = r.Origen,
                    comentario = r.ComentarioDelUsuario,
                    fuentes = r.Fuentes,
                    semanas = r.Semanas,
                    evidencia = r.Evidencia.Select(e => new { fuente = e.Fuente, fecha = e.Fecha, cita = e.Cita }),
                }),
                recomendaciones = p.Recomendaciones.Where(r => r.Estado == EstadosDeRasgo.Activo).Select(r => new { id = r.Id, texto = r.Texto, rasgos = r.Rasgos, oculta = r.Oculta }),
            },
        };
    }

    /// <summary>Descarga en Markdown generada al vuelo (sin copia persistente), sin lo oculto, descartado ni borrado.</summary>
    public string? Descarga() => AlmacenDePerfil.Leer(Datos) is { } p ? PerfilEnMarkdown.Generar(p) : null;

    private bool EstaGenerando()
    {
        using var b = BloqueoDeNoa.Adquirir(Datos);
        return b is null;
    }

    // ---------------------------------------------------------------- acciones del usuario

    public RespuestaDeEscritura Accion(string operacion, string? objetivo, string? correo, Func<string, DateTimeOffset, string> accion)
    {
        var r = Intentar(accion);
        auditoria(Auditoria.Linea(reloj(), correo, "perfil-" + operacion, objetivo, r.Estado == 200 ? "ok" : $"rechazada-{r.Estado}"));
        return r;
    }

    private RespuestaDeEscritura Intentar(Func<string, DateTimeOffset, string> accion)
    {
        using var bloqueo = BloqueoDeNoa.Adquirir(Datos);
        if (bloqueo is null)
        {
            return new RespuestaDeEscritura(409, null, "Noa está trabajando ahora mismo. Vuelve a intentarlo dentro de unos minutos.");
        }

        try
        {
            var mensaje = accion(Datos, reloj());
            return new RespuestaDeEscritura(200, mensaje, null);
        }
        catch (PerfilInvalidoException e)
        {
            return new RespuestaDeEscritura(e.Message.StartsWith("No existe", StringComparison.Ordinal) ? 404 : 400, null, e.Message);
        }
        catch (IOException)
        {
            return new RespuestaDeEscritura(503, null, "No se pudo guardar el cambio. Reinténtalo.");
        }
    }

    // ---------------------------------------------------------------- generación a petición

    /// <summary>
    /// Pide una generación a petición. Se hace en segundo plano (puede tardar minutos): responde enseguida y el estado se ve en <see cref="Ver"/>.
    /// Una sola a la vez: si Noa ya trabaja, 409. Sin la sección «noa» o sin proveedor, no se envía nada.
    /// </summary>
    public RespuestaDeEscritura Generar(string? correo)
    {
        var r = Iniciar();
        auditoria(Auditoria.Linea(reloj(), correo, "perfil-generar", null, r.Estado is 200 or 202 ? "ok" : $"rechazada-{r.Estado}"));
        return r;
    }

    private RespuestaDeEscritura Iniciar()
    {
        if (config is null)
        {
            return new RespuestaDeEscritura(409, null, "Noa no está activada (falta la sección «noa» en la configuración).");
        }

        var prov = proveedor();
        if (prov is null)
        {
            return new RespuestaDeEscritura(409, null, "Desde el panel no hay proveedor configurado. Genera el perfil desde la línea de comandos.");
        }

        var bloqueo = BloqueoDeNoa.Adquirir(Datos);
        if (bloqueo is null)
        {
            return new RespuestaDeEscritura(409, null, "Noa ya está trabajando. Vuelve a intentarlo dentro de unos minutos.");
        }

        _ = Task.Run(async () =>
        {
            using (bloqueo)
            {
                try
                {
                    await new GeneradorDePerfil(Datos, prov, reloj, config, configuracion, entorno).EjecutarAsync(aPeticion: true);
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    auditoria(Auditoria.Linea(reloj(), null, "perfil-generar", null, "error-" + e.GetType().Name));
                }
            }
        });
        return new RespuestaDeEscritura(202, "Noa ha empezado. El resultado aparecerá aquí cuando termine.", null);
    }
}
