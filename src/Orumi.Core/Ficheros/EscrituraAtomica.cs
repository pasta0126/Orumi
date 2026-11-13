using System.Text;

namespace Orumi.Core.Ficheros;

public sealed class EscrituraFallidaException(string mensaje, Exception? interna = null) : Exception(mensaje, interna);

/// <summary>
/// Escribe un fichero nuevo sin sobrescribir: contenido a un temporal en el mismo directorio, sincronizado,
/// renombrado al destino y releído para comprobar que quedó idéntico. Si algo falla no queda un fichero
/// a medias dado por bueno.
/// </summary>
public static class EscrituraAtomica
{
    private static readonly UTF8Encoding Utf8SinBom = new(false);

    public static void EscribirNuevo(string ruta, string contenido)
    {
        var directorio = Path.GetDirectoryName(ruta) ?? throw new ArgumentException("Ruta sin directorio.", nameof(ruta));
        Directory.CreateDirectory(directorio);

        if (File.Exists(ruta))
        {
            throw new EscrituraFallidaException($"Ya existe un fichero en {ruta}; no se sobrescribe.");
        }

        var temporal = Path.Combine(directorio, "." + Path.GetFileName(ruta) + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp");
        try
        {
            var bytes = Utf8SinBom.GetBytes(contenido);
            using (var flujo = new FileStream(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                flujo.Write(bytes);
                flujo.Flush(true);
            }

            // File.Move sin overwrite falla si el destino aparece entre medias: no se pisa nada.
            File.Move(temporal, ruta, overwrite: false);

            var releido = File.ReadAllBytes(ruta);
            if (!releido.AsSpan().SequenceEqual(bytes))
            {
                File.Delete(ruta);
                throw new EscrituraFallidaException($"La verificación de {ruta} falló: el contenido leído no coincide.");
            }
        }
        catch (EscrituraFallidaException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new EscrituraFallidaException($"No se pudo escribir {ruta}: {e.Message}", e);
        }
        finally
        {
            if (File.Exists(temporal))
            {
                File.Delete(temporal);
            }
        }
    }
}
