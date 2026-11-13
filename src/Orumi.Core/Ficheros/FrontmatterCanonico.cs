using System.Text.RegularExpressions;
using Orumi.Core.Yaml;

namespace Orumi.Core.Ficheros;

/// <summary>
/// Escribe el frontmatter de registros, preguntas y lotes en su forma canónica: orden de claves fijo y el estilo
/// (bloque o en línea) que fija <c>docs/formato-de-ficheros.md</c>. Sirve tanto para crear ficheros nuevos como
/// para reescribir uno existente tras actualizarlo.
/// </summary>
public static partial class FrontmatterCanonico
{
    private static readonly string[] OrdenRegistro =
    [
        "id", "tipo", "clase", "titulo", "comunicada_el", "fuentes", "fecha", "hora", "duracion", "herramientas", "personas",
        "entidades", "lugares", "proyectos", "contexto", "acciones", "resultados", "problemas", "ideas", "multidia", "entradas",
        "relaciones", "conflictos", "rechazos", "historial", "certeza", "procedencia", "clave_extraccion",
    ];

    private static readonly string[] OrdenPregunta =
    [
        "id", "tipo", "registro", "otros_registros", "campo", "texto", "estado", "valor", "creada_el", "generada_por", "cerrada_el", "respuesta",
    ];

    private static readonly string[] OrdenLote =
        ["id", "tipo", "ejecutado_el", "ejecucion", "agente", "proveedor", "capturas", "problemas"];

    /// <summary>Mapas que se escriben en línea: <c>{a: 1, b: 2}</c>.</summary>
    private static readonly HashSet<string> MapasEnLinea =
    [
        "fecha", "hora", "duracion", "multidia.fin", "procedencia.generado_por", "procedencia.actualizado_por",
        "generada_por", "agente", "respuesta", "fuentes[].fragmento",
    ];

    /// <summary>Listas en línea (<c>[a, b]</c>).</summary>
    private static readonly HashSet<string> ListasEnLinea =
    [
        "herramientas", "personas", "lugares", "proyectos", "acciones", "resultados", "problemas", "ideas", "otros_registros",
    ];

    /// <summary>Listas en bloque cuyos elementos son mapas en línea.</summary>
    private static readonly HashSet<string> ListasDeMapasEnLinea =
        ["entidades", "entradas", "relaciones", "conflictos", "rechazos", "historial", "capturas", "problemas_lote"];

    private static readonly HashSet<string> ClavesDeTexto =
    [
        "titulo", "contexto", "cita", "resumen", "motivo", "texto", "nombre", "expresion", "etiqueta", "dispositivo", "valor_rechazo",
    ];

    /// <summary>Claves cuyo texto va siempre entre comillas.</summary>
    private static readonly HashSet<string> ClavesCitadas = ["texto", "resumen", "motivo", "cita", "version", "modelo", "valor_rechazo"];

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+-]\d{2}:\d{2}$")] private static partial Regex InstanteRx();
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")] private static partial Regex FechaRx();
    [GeneratedRegex(@"^(\d+|true|false)$")] private static partial Regex LiteralRx();

    public static string Escribir(Dictionary<string, object?> frontmatter, string cuerpo, string tipo)
    {
        var orden = tipo switch
        {
            "registro" => OrdenRegistro,
            "pregunta" => OrdenPregunta,
            "lote" => OrdenLote,
            _ => throw new ArgumentException($"Tipo sin formato canónico: {tipo}"),
        };

        var pares = new List<(string, YNodo)>();
        foreach (var clave in orden.Where(frontmatter.ContainsKey).Concat(frontmatter.Keys.Except(orden)))
        {
            pares.Add((clave, Convertir(frontmatter[clave], clave, clave, false)));
        }

        return "---\n" + YamlEscritor.Escribir(YMapa.De([.. pares])) + "---\n" + cuerpo;
    }

    private static YNodo Convertir(object? valor, string ruta, string clave, bool enLinea)
    {
        switch (valor)
        {
            case null:
                return new YLiteral("null");
            case string s:
                return Escalar(s, clave);
            case Dictionary<string, object?> mapa:
            {
                var linea = enLinea || MapasEnLinea.Contains(ruta);
                var pares = mapa.Select(p => new KeyValuePair<string, YNodo>(
                    p.Key, Convertir(p.Value, ruta + "." + p.Key, p.Key == "valor" && ruta.StartsWith("rechazos", StringComparison.Ordinal) ? "valor_rechazo" : p.Key, linea))).ToList();
                return new YMapa(pares, linea);
            }

            case List<object?> lista:
            {
                if (enLinea || ListasEnLinea.Contains(ruta))
                {
                    return new YLista(lista.Select(e => Convertir(e, ruta, clave, true)).ToList(), true);
                }

                if (ListasDeMapasEnLinea.Contains(ruta))
                {
                    return new YLista(lista.Select(e => e is Dictionary<string, object?> ? Convertir(e, ruta, clave, true) : Convertir(e, ruta, clave, true)).ToList(), lista.Count == 0);
                }

                // Lista en bloque de mapas en bloque (fuentes).
                return new YLista(lista.Select(e => Convertir(e, ruta + "[]", clave, false)).ToList(), lista.Count == 0);
            }

            default:
                throw new InvalidOperationException($"Tipo no soportado en {ruta}: {valor.GetType().Name}");
        }
    }

    private static YNodo Escalar(string s, string clave)
    {
        if (ClavesCitadas.Contains(clave))
        {
            return new YCitado(s);
        }

        if (ClavesDeTexto.Contains(clave))
        {
            return new YTexto(s);
        }

        if (InstanteRx().IsMatch(s))
        {
            return new YInstante(s);
        }

        if (FechaRx().IsMatch(s) || LiteralRx().IsMatch(s))
        {
            return new YLiteral(s);
        }

        return new YTexto(s);
    }
}
