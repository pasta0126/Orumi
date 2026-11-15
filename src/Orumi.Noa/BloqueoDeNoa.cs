namespace Orumi.Noa;

/// <summary>Exclusión mutua de Noa, independiente de la de Mira: una generación a la vez y las acciones del usuario esperan a que termine.</summary>
public sealed class BloqueoDeNoa : IDisposable
{
    private readonly FileStream _flujo;

    private BloqueoDeNoa(FileStream flujo) => _flujo = flujo;

    public static BloqueoDeNoa? Adquirir(string datos)
    {
        Directory.CreateDirectory(datos);
        try
        {
            var flujo = new FileStream(Path.Combine(datos, ".bloqueo-noa"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            flujo.SetLength(0);
            using var escritor = new StreamWriter(flujo, leaveOpen: true);
            escritor.Write($"{Environment.ProcessId}\n");
            escritor.Flush();
            return new BloqueoDeNoa(flujo);
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose() => _flujo.Dispose();
}
