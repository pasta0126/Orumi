namespace Orumi.Agente;

/// <summary>
/// Exclusión mutua de las ejecuciones de Mira con un fichero de bloqueo exclusivo. El bloqueo lo mantiene el sistema
/// operativo mientras el proceso vive: si una ejecución muere, se libera solo y la siguiente arranca sin intervención.
/// </summary>
public sealed class BloqueoDeEjecucion : IDisposable
{
    private readonly FileStream _flujo;

    private BloqueoDeEjecucion(FileStream flujo) => _flujo = flujo;

    public static BloqueoDeEjecucion? Adquirir(string datos)
    {
        Directory.CreateDirectory(datos);
        var ruta = Path.Combine(datos, ".bloqueo");
        try
        {
            var flujo = new FileStream(ruta, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            flujo.SetLength(0);
            using var escritor = new StreamWriter(flujo, leaveOpen: true);
            escritor.Write($"{Environment.ProcessId}\n");
            escritor.Flush();
            return new BloqueoDeEjecucion(flujo);
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose() => _flujo.Dispose();
}
