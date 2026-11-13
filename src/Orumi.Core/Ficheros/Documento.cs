using System.Security.Cryptography;
using System.Text;
using Orumi.Core.Yaml;

namespace Orumi.Core.Ficheros;

/// <summary>Fichero de ORUMI leído: frontmatter YAML y cuerpo exacto.</summary>
public sealed record Documento(string Ruta, Dictionary<string, object?> Frontmatter, string Cuerpo)
{
    public string? Id => Frontmatter.TryGetValue("id", out var v) ? v as string : null;

    public string? Tipo => Frontmatter.TryGetValue("tipo", out var v) ? v as string : null;

    /// <summary>Separa frontmatter y cuerpo. El cuerpo es todo lo que sigue a la línea de cierre y su salto de línea.</summary>
    public static Documento Analizar(string ruta, string contenido)
    {
        const string apertura = "---\n";
        const string cierre = "\n---\n";
        if (!contenido.StartsWith(apertura, StringComparison.Ordinal))
        {
            throw new FormatException("Falta el frontmatter (el fichero debe empezar por '---').");
        }

        var fin = contenido.IndexOf(cierre, apertura.Length - 1, StringComparison.Ordinal);
        if (fin < 0)
        {
            throw new FormatException("Falta la línea de cierre '---' del frontmatter.");
        }

        var yaml = contenido.Substring(apertura.Length, fin - (apertura.Length - 1));
        var cuerpo = contenido[(fin + cierre.Length)..];
        return new Documento(ruta, YamlLector.LeerMapa(yaml), cuerpo);
    }
}

public static class Huella
{
    /// <summary>SHA-256 del cuerpo en UTF-8, con el prefijo <c>sha256:</c>.</summary>
    public static string De(string cuerpo) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cuerpo))).ToLowerInvariant();
}
