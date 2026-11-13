using Orumi.Core.Validacion;

namespace Orumi.Core.Ficheros;

/// <summary>Los ficheros de un directorio <c>orumi/</c>, cargados y clasificados.</summary>
public sealed class Almacen
{
    private static readonly string[] Omitidos = ["entrada"];

    public string Raiz { get; }

    public List<Documento> Documentos { get; } = [];

    public List<Incidencia> Problemas { get; } = [];

    private Almacen(string raiz) => Raiz = raiz;

    public static Almacen Cargar(string raiz)
    {
        var almacen = new Almacen(Path.GetFullPath(raiz));
        foreach (var ruta in Directory.EnumerateFiles(almacen.Raiz, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relativa = Path.GetRelativePath(almacen.Raiz, ruta).Replace('\\', '/');
            var partes = relativa.Split('/');
            if (partes.Any(p => p.StartsWith('.')) || Omitidos.Contains(partes[0]))
            {
                continue;
            }

            try
            {
                almacen.Documentos.Add(Documento.Analizar(relativa, File.ReadAllText(ruta, System.Text.Encoding.UTF8)));
            }
            catch (Exception e) when (e is FormatException or YamlDotNet.Core.YamlException or IOException)
            {
                almacen.Problemas.Add(new Incidencia(relativa, $"No se puede leer: {e.Message}"));
            }
        }

        return almacen;
    }

    public IEnumerable<Documento> DeTipo(string tipo) => Documentos.Where(d => d.Tipo == tipo);
}
