using YamlDotNet.RepresentationModel;

namespace Orumi.Core.Yaml;

/// <summary>
/// Lee el frontmatter YAML a un árbol simple: escalares como texto, listas como
/// <see cref="List{Object}"/> y mapas como <see cref="Dictionary{String, Object}"/>.
/// </summary>
public static class YamlLector
{
    public static Dictionary<string, object?> LeerMapa(string yaml)
    {
        var flujo = new YamlStream();
        using (var lector = new StringReader(yaml))
        {
            flujo.Load(lector);
        }

        if (flujo.Documents.Count != 1 || flujo.Documents[0].RootNode is not YamlMappingNode raiz)
        {
            throw new FormatException("El frontmatter debe ser un mapa YAML.");
        }

        return (Dictionary<string, object?>)Convertir(raiz)!;
    }

    private static object? Convertir(YamlNode nodo)
    {
        switch (nodo)
        {
            case YamlScalarNode escalar:
                return escalar.Value;
            case YamlSequenceNode secuencia:
                return secuencia.Children.Select(Convertir).ToList();
            case YamlMappingNode mapa:
                var resultado = new Dictionary<string, object?>();
                foreach (var par in mapa.Children)
                {
                    if (par.Key is not YamlScalarNode clave || clave.Value is null)
                    {
                        throw new FormatException("Las claves del frontmatter deben ser texto.");
                    }

                    if (!resultado.TryAdd(clave.Value, Convertir(par.Value)))
                    {
                        throw new FormatException($"Clave duplicada: {clave.Value}");
                    }
                }

                return resultado;
            default:
                throw new FormatException("Nodo YAML no admitido.");
        }
    }
}
