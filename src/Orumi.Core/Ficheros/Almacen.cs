using Orumi.Core.Validacion;

namespace Orumi.Core.Ficheros;

/// <summary>Los ficheros de un directorio <c>orumi/</c>, cargados y clasificados.</summary>
public sealed class Almacen
{
    /// <summary>Carpetas que no son datos: audio pendiente y <c>lost+found</c>, que ext4 crea en la raíz de un volumen nuevo (de root, sin permiso).</summary>
    private static readonly string[] Omitidos = ["entrada", "lost+found"];

    public string Raiz { get; }

    public List<Documento> Documentos { get; } = [];

    public List<Incidencia> Problemas { get; } = [];

    private Almacen(string raiz) => Raiz = raiz;

    /// <summary>
    /// Carga el almacén. <c>perfil/</c> es el espacio de Noa y <b>no se carga por defecto</b>: así Mira (y todo lo que no sea Noa) no ve ni cuenta
    /// nada suyo. Solo quien debe verlo (panel, terminal, operaciones del usuario, aprendizaje y la propia Noa) lo pide con <paramref name="incluirPerfil"/>.
    /// </summary>
    public static Almacen Cargar(string raiz, bool incluirPerfil = false)
    {
        var almacen = new Almacen(Path.GetFullPath(raiz));
        // IgnoreInaccessible: una carpeta sin permiso de lectura no debe tumbar la lectura de todo lo demás.
        var opciones = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        foreach (var ruta in Directory.EnumerateFiles(almacen.Raiz, "*.md", opciones).Order(StringComparer.Ordinal))
        {
            var relativa = Path.GetRelativePath(almacen.Raiz, ruta).Replace('\\', '/');
            var partes = relativa.Split('/');
            if (partes.Any(p => p.StartsWith('.')) || Omitidos.Contains(partes[0]) || (!incluirPerfil && partes[0] == "perfil"))
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

    /// <summary>Copia del almacén con documentos añadidos o sustituidos (por ruta), sin tocar el disco.</summary>
    public Almacen Con(IEnumerable<Documento> cambios)
    {
        var copia = new Almacen(Raiz);
        var porRuta = Documentos.ToDictionary(d => d.Ruta);
        foreach (var c in cambios)
        {
            porRuta[c.Ruta] = c;
        }

        copia.Documentos.AddRange(porRuta.Values.OrderBy(d => d.Ruta, StringComparer.Ordinal));
        copia.Problemas.AddRange(Problemas);
        return copia;
    }

    public IEnumerable<Documento> DeTipo(string tipo) => Documentos.Where(d => d.Tipo == tipo);
}
