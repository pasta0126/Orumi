namespace Orumi.Core.Ficheros;

public sealed class VolumenNoDisponibleException(string mensaje) : Exception(mensaje);

/// <summary>
/// Comprobación del volumen cifrado (spec <c>ejecucion-y-despliegue</c>). Dentro del volumen existe un fichero
/// marcador creado al prepararlo. Si el volumen no está montado, el directorio de datos es un punto de montaje
/// vacío en el disco sin cifrar: no tiene el marcador, y las herramientas no escriben ni procesan nada (fallo cerrado),
/// de modo que nunca se crean ficheros de datos en ese directorio vacío.
/// </summary>
public static class Volumen
{
    public const string Marcador = ".orumi-volumen";

    public static bool EstaDisponible(string datos) => File.Exists(Path.Combine(datos, Marcador));

    /// <summary>Lanza si se exige el volumen y no está disponible.</summary>
    public static void Exigir(string datos)
    {
        if (!EstaDisponible(datos))
        {
            throw new VolumenNoDisponibleException(
                "El volumen cifrado de datos no está disponible (¿sin desbloquear tras un reinicio?). No se escribe ni se procesa nada.");
        }
    }

    /// <summary>Marca un directorio como el volumen de datos (se hace una vez, con el volumen ya montado).</summary>
    public static void Preparar(string datos)
    {
        if (!Directory.Exists(datos))
        {
            throw new VolumenNoDisponibleException($"No existe el directorio {datos}: monta primero el volumen.");
        }

        var ruta = Path.Combine(datos, Marcador);
        if (!File.Exists(ruta))
        {
            EscrituraAtomica.EscribirNuevo(ruta, "Volumen de datos de ORUMI. No borrar: sin este fichero las herramientas no escriben.\n");
        }
    }
}
