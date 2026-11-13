using System.Security.Cryptography;
using System.Text;
using Orumi.Core.Ficheros;

namespace Orumi.Panel;

/// <summary>
/// Los ficheros son la fuente de verdad. Mantiene el almacén cargado en memoria y lo recarga cuando cambia algún fichero
/// (huella de rutas, tamaños y fechas), comprobándolo como mucho una vez por <c>vigencia</c>. Así cualquier cambio (del panel,
/// de la línea de comandos o de Mira) se ve enseguida, sin depender del índice de PostgreSQL.
/// </summary>
public sealed class RepositorioDeDatos(string datos, Func<DateTimeOffset> reloj, TimeSpan? vigencia = null)
{
    private readonly TimeSpan _vigencia = vigencia ?? TimeSpan.FromSeconds(2);
    private readonly object _cerrojo = new();
    private Almacen? _almacen;
    private string? _firma;
    private DateTimeOffset _comprobadoEl = DateTimeOffset.MinValue;

    public string Datos => datos;

    public Almacen Actual()
    {
        lock (_cerrojo)
        {
            var ahora = reloj();
            if (_almacen is null || ahora - _comprobadoEl >= _vigencia)
            {
                var firma = Firma();
                if (firma != _firma || _almacen is null)
                {
                    _almacen = Almacen.Cargar(datos);
                    _firma = firma;
                }

                _comprobadoEl = ahora;
            }

            return _almacen;
        }
    }

    /// <summary>Fuerza una recarga en la siguiente lectura (tras una escritura propia).</summary>
    public void Invalidar()
    {
        lock (_cerrojo)
        {
            _comprobadoEl = DateTimeOffset.MinValue;
            _firma = null;
        }
    }

    private string Firma()
    {
        var sb = new StringBuilder();
        var opciones = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        foreach (var ruta in Directory.EnumerateFiles(datos, "*.md", opciones).Order(StringComparer.Ordinal))
        {
            var relativa = Path.GetRelativePath(datos, ruta);
            if (relativa.Split(Path.DirectorySeparatorChar).Any(p => p.StartsWith('.')))
            {
                continue;
            }

            var info = new FileInfo(ruta);
            sb.Append(relativa).Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }
}
