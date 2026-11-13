using System.Text;
using System.Text.RegularExpressions;

namespace Orumi.Core.Yaml;

/// <summary>Nodo de salida del escritor determinista de frontmatter.</summary>
public abstract record YNodo;

/// <summary>Escalar que se escribe tal cual (identificadores, fechas, números, valores de un conjunto cerrado).</summary>
public sealed record YLiteral(string Texto) : YNodo;

/// <summary>Texto libre: se entrecomilla solo si hace falta para que sea YAML válido y no ambiguo.</summary>
public sealed record YTexto(string Texto) : YNodo;

/// <summary>Instante con hora: entre comillas dentro de mapas y listas en línea, sin ellas en el resto.</summary>
public sealed record YInstante(string Texto) : YNodo;

public sealed record YMapa(IReadOnlyList<KeyValuePair<string, YNodo>> Pares, bool EnLinea = false) : YNodo
{
    public static YMapa De(params (string Clave, YNodo Valor)[] pares) =>
        new(pares.Select(p => new KeyValuePair<string, YNodo>(p.Clave, p.Valor)).ToList());

    public static YMapa EnLineaDe(params (string Clave, YNodo Valor)[] pares) =>
        new(pares.Select(p => new KeyValuePair<string, YNodo>(p.Clave, p.Valor)).ToList(), true);
}

public sealed record YLista(IReadOnlyList<YNodo> Elementos, bool EnLinea = false) : YNodo;

/// <summary>
/// Escribe frontmatter YAML con las convenciones de <c>docs/formato-de-ficheros.md</c>:
/// bloque con sangría de dos espacios, mapas y listas en línea cuando se piden, textos con
/// <c>:</c> entre comillas e instantes entre comillas dentro de <c>{}</c>.
/// </summary>
public static class YamlEscritor
{
    private static readonly Regex PareceNumero = new(@"^[-+]?(\d[\d_]*\.?\d*|\.\d+)([eE][-+]?\d+)?$", RegexOptions.Compiled);
    private static readonly Regex PareceFecha = new(@"^\d{4}-\d{1,2}-\d{1,2}", RegexOptions.Compiled);
    private static readonly Regex PareceHora = new(@"^\d{1,2}:\d{2}(:\d{2})?$", RegexOptions.Compiled);
    private static readonly HashSet<string> Palabras = new(StringComparer.OrdinalIgnoreCase)
    {
        "true", "false", "yes", "no", "on", "off", "null", "~", "y", "n",
    };

    public static string Escribir(YMapa raiz)
    {
        var sb = new StringBuilder();
        EscribirMapaBloque(sb, raiz, 0);
        return sb.ToString();
    }

    private static void EscribirMapaBloque(StringBuilder sb, YMapa mapa, int sangria)
    {
        var margen = new string(' ', sangria);
        foreach (var (clave, valor) in mapa.Pares)
        {
            switch (valor)
            {
                case YMapa m when !m.EnLinea && m.Pares.Count > 0:
                    sb.Append(margen).Append(clave).Append(":\n");
                    EscribirMapaBloque(sb, m, sangria + 2);
                    break;
                case YLista l when !l.EnLinea && l.Elementos.Count > 0:
                    sb.Append(margen).Append(clave).Append(":\n");
                    EscribirListaBloque(sb, l, sangria + 2);
                    break;
                default:
                    sb.Append(margen).Append(clave).Append(": ").Append(Inline(valor, false)).Append('\n');
                    break;
            }
        }
    }

    private static void EscribirListaBloque(StringBuilder sb, YLista lista, int sangria)
    {
        var margen = new string(' ', sangria);
        foreach (var elemento in lista.Elementos)
        {
            if (elemento is YMapa m && !m.EnLinea && m.Pares.Count > 0)
            {
                // Primer par junto al guion; el resto alineado debajo.
                var primero = true;
                foreach (var (clave, valor) in m.Pares)
                {
                    var prefijo = primero ? margen + "- " : margen + "  ";
                    primero = false;
                    switch (valor)
                    {
                        case YMapa sub when !sub.EnLinea && sub.Pares.Count > 0:
                            sb.Append(prefijo).Append(clave).Append(":\n");
                            EscribirMapaBloque(sb, sub, sangria + 4);
                            break;
                        case YLista sublista when !sublista.EnLinea && sublista.Elementos.Count > 0:
                            sb.Append(prefijo).Append(clave).Append(":\n");
                            EscribirListaBloque(sb, sublista, sangria + 4);
                            break;
                        default:
                            sb.Append(prefijo).Append(clave).Append(": ").Append(Inline(valor, false)).Append('\n');
                            break;
                    }
                }
            }
            else
            {
                sb.Append(margen).Append("- ").Append(Inline(elemento, false)).Append('\n');
            }
        }
    }

    private static string Inline(YNodo nodo, bool enLinea)
    {
        switch (nodo)
        {
            case YLiteral l:
                return l.Texto;
            case YTexto t:
                return Texto(t.Texto, enLinea);
            case YInstante i:
                return enLinea ? "\"" + i.Texto + "\"" : i.Texto;
            case YMapa m:
                return m.Pares.Count == 0
                    ? "{}"
                    : "{" + string.Join(", ", m.Pares.Select(p => p.Key + ": " + Inline(p.Value, true))) + "}";
            case YLista l:
                return l.Elementos.Count == 0
                    ? "[]"
                    : "[" + string.Join(", ", l.Elementos.Select(e => Inline(e, true))) + "]";
            default:
                throw new InvalidOperationException("Nodo YAML no soportado.");
        }
    }

    /// <summary>Devuelve el texto tal cual si es seguro como escalar plano; si no, entre comillas dobles.</summary>
    public static string Texto(string texto, bool enLinea)
    {
        return NecesitaComillas(texto, enLinea) ? Entrecomillar(texto) : texto;
    }

    private static bool NecesitaComillas(string t, bool enLinea)
    {
        if (t.Length == 0 || t != t.Trim())
        {
            return true;
        }

        if (t.Any(c => c == '\n' || c == '\r' || c == '\t' || char.IsControl(c)))
        {
            return true;
        }

        if ("-?:,[]{}#&*!|>'\"%@`".Contains(t[0]))
        {
            return true;
        }

        if (t.Contains(": ") || t.EndsWith(':') || t.Contains(" #"))
        {
            return true;
        }

        if (enLinea && t.IndexOfAny([',', '[', ']', '{', '}', ':']) >= 0)
        {
            return true;
        }

        return Palabras.Contains(t) || PareceNumero.IsMatch(t) || PareceFecha.IsMatch(t) || PareceHora.IsMatch(t);
    }

    private static string Entrecomillar(string t)
    {
        var sb = new StringBuilder("\"");
        foreach (var c in t)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (char.IsControl(c))
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        return sb.Append('"').ToString();
    }
}
