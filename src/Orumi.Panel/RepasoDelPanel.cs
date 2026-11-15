using Orumi.Acceso;
using Orumi.Agente;
using Orumi.Agente.Etiquetado;
using Orumi.Agente.Repaso;
using Orumi.Core.Yaml;

namespace Orumi.Panel;

/// <summary>
/// Lanzar y ver el repaso de los registros desde el panel (spec <c>repaso-de-registros</c> y <c>panel-web</c>). Se ejecuta en segundo plano con el bloqueo de
/// Mira (409 si Mira está procesando o ya hay un repaso en curso). Sin proveedor configurado en el panel, usa solo la capa determinista.
/// </summary>
public sealed class RepasoDelPanel(
    RepositorioDeDatos repositorio, Func<DateTimeOffset> reloj, Action<string> auditoria, Configuracion? configuracion, Func<IProveedorDeTexto?> proveedor, Func<string, string?> entorno)
{
    private int _enCurso;

    public object Ver()
    {
        object? ultimo = null;
        var dir = Path.Combine(repositorio.Datos, "analisis", "repaso");
        if (Directory.Exists(dir))
        {
            var fichero = Directory.EnumerateFiles(dir, "rep-*.yaml", SearchOption.AllDirectories).OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal).LastOrDefault();
            if (fichero is not null)
            {
                try
                {
                    var m = YamlLector.LeerMapa(File.ReadAllText(fichero, System.Text.Encoding.UTF8));
                    ultimo = new
                    {
                        fecha = m.GetValueOrDefault("ejecutado_el") as string,
                        revisados = m.GetValueOrDefault("registros_revisados") as string,
                        creadas = m.GetValueOrDefault("preguntas_creadas") as string,
                        modelo = (m.GetValueOrDefault("modelo_usado") as string) == "true",
                    };
                }
                catch (Exception e) when (e is YamlDotNet.Core.YamlException or IOException or FormatException)
                {
                    ultimo = null;
                }
            }
        }

        return new { enCurso = Volatile.Read(ref _enCurso) == 1, conModelo = proveedor() is not null, ultimo };
    }

    public RespuestaDeEscritura Lanzar(string? correo)
    {
        var r = Iniciar();
        auditoria(Auditoria.Linea(reloj(), correo, "repaso", null, r.Estado is 200 or 202 ? "ok" : $"rechazada-{r.Estado}"));
        return r;
    }

    private RespuestaDeEscritura Iniciar()
    {
        if (Interlocked.CompareExchange(ref _enCurso, 1, 0) != 0)
        {
            return new RespuestaDeEscritura(409, null, "Ya hay un repaso en curso.");
        }

        var bloqueo = BloqueoDeEjecucion.Adquirir(repositorio.Datos);
        if (bloqueo is null)
        {
            Interlocked.Exchange(ref _enCurso, 0);
            return new RespuestaDeEscritura(409, null, "Mira está procesando ahora mismo. Vuelve a intentarlo dentro de unos minutos.");
        }

        var prov = proveedor();
        _ = Task.Run(async () =>
        {
            try
            {
                using (bloqueo)
                {
                    await new RepasoDeRegistros(repositorio.Datos, reloj, configuracion?.Repaso ?? new ConfiguracionDeRepaso(), prov, configuracion, entorno)
                        .EjecutarAsync(soloDeterminista: prov is null);
                    repositorio.Invalidar();
                }
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                auditoria(Auditoria.Linea(reloj(), null, "repaso", null, "error-" + e.GetType().Name));
            }
            finally
            {
                Interlocked.Exchange(ref _enCurso, 0);
            }
        });
        return new RespuestaDeEscritura(202, "Mira está repasando tus registros. Las preguntas nuevas aparecerán en «Preguntas».", null);
    }
}
