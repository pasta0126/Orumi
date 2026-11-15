using System.Globalization;
using Orumi.Core.Ficheros;
using Orumi.Core.Yaml;

namespace Orumi.Noa;

/// <summary>
/// Ficheros de Noa (<c>perfil/</c>): <c>perfil.yaml</c> (la versión vigente), <c>historial/</c> (una copia por versión) y
/// <c>generaciones/</c> (constancia de cada ejecución). Son lo único que Noa escribe; legibles a mano y accesibles sin el agente.
/// </summary>
public static class AlmacenDePerfil
{
    private const string Formato = "yyyy-MM-dd'T'HH:mm:sszzz";

    public static string Directorio(string datos) => Path.Combine(datos, "perfil");

    public static string RutaDelPerfil(string datos) => Path.Combine(Directorio(datos), "perfil.yaml");

    public static string RutaDeVersion(string datos, int version, DateTimeOffset fecha) =>
        Path.Combine(Directorio(datos), "historial", $"perfil-v{version:000}-{fecha:yyyyMMdd-HHmm}.yaml");

    public static string RutaDeGeneracion(string datos, string id) => Path.Combine(Directorio(datos), "generaciones", id + ".yaml");

    // ------------------------------------------------------------------ perfil

    public static Perfil? Leer(string datos)
    {
        var ruta = RutaDelPerfil(datos);
        if (!File.Exists(ruta))
        {
            return null;
        }

        try
        {
            return Analizar(File.ReadAllText(ruta, System.Text.Encoding.UTF8));
        }
        catch (Exception e) when (e is FormatException or KeyNotFoundException or InvalidCastException or YamlDotNet.Core.YamlException)
        {
            return null;
        }
    }

    public static void Guardar(string datos, Perfil perfil, bool copiaDeVersion)
    {
        var yaml = Escribir(perfil);
        Directory.CreateDirectory(Directorio(datos));
        EscrituraAtomica.Reemplazar(RutaDelPerfil(datos), yaml);
        if (copiaDeVersion)
        {
            var copia = RutaDeVersion(datos, perfil.Version, perfil.Generada);
            Directory.CreateDirectory(Path.GetDirectoryName(copia)!);
            EscrituraAtomica.Reemplazar(copia, yaml);
        }
    }

    /// <summary>Borra el perfil entero con su historial, sus generaciones y todo lo demás de Noa. No toca capturas, registros ni preguntas de Mira.</summary>
    public static void BorrarTodo(string datos)
    {
        var d = Directorio(datos);
        if (Directory.Exists(d))
        {
            Directory.Delete(d, recursive: true);
        }
    }

    public static string Escribir(Perfil p)
    {
        YNodo Textos(IEnumerable<string> xs) => new YLista([.. xs.Select(x => (YNodo)new YCitado(x))], EnLinea: true);
        YNodo Num(int n) => new YLiteral(n.ToString(CultureInfo.InvariantCulture));
        var rasgos = p.Rasgos.Select(r =>
        {
            var pares = new List<(string, YNodo)>
            {
                ("clave", new YLiteral(r.Clave)), ("apartado", new YLiteral(r.Apartado)), ("texto", new YCitado(r.Texto)),
            };
            if (r.Contexto is not null)
            {
                pares.Add(("contexto", new YCitado(r.Contexto)));
            }

            pares.Add(("certeza", new YLiteral(r.Certeza)));
            pares.Add(("nivel", new YLiteral(r.Nivel)));
            pares.Add(("sensible", new YLiteral(r.Sensible ? "true" : "false")));
            pares.Add(("fuentes", Num(r.Fuentes)));
            pares.Add(("semanas", Num(r.Semanas)));
            pares.Add(("estado", new YLiteral(r.Estado)));
            pares.Add(("origen", new YLiteral(r.Origen)));
            pares.Add(("oculto", new YLiteral(r.Oculto ? "true" : "false")));
            if (r.ComentarioDelUsuario is not null)
            {
                pares.Add(("comentario_usuario", new YCitado(r.ComentarioDelUsuario)));
            }

            if (r.EvidenciaAlDescartar is { } e)
            {
                pares.Add(("evidencia_al_descartar", Num(e)));
            }

            pares.Add(("version_creada", Num(r.VersionCreada)));
            pares.Add(("actualizado", new YInstante(r.Actualizado.ToString(Formato, CultureInfo.InvariantCulture))));
            pares.Add(("hipotesis", Textos(r.Hipotesis)));
            pares.Add(("evidencia", new YLista([.. r.Evidencia.Select(e => (YNodo)YMapa.EnLineaDe(
                ("fuente", new YLiteral(e.Fuente)), ("fecha", new YLiteral(e.Fecha)), ("cita", new YCitado(e.Cita))))], EnLinea: r.Evidencia.Count == 0)));
            pares.Add(("historial", new YLista([.. r.Historial.Select(h => (YNodo)YMapa.EnLineaDe(
                ("fecha", new YInstante(h.Fecha.ToString(Formato, CultureInfo.InvariantCulture))), ("accion", new YLiteral(h.Accion)), ("origen", new YLiteral(h.Origen)),
                ("detalle", h.Detalle is null ? new YLiteral("ninguno") : new YCitado(h.Detalle))))], EnLinea: r.Historial.Count == 0)));
            return (YNodo)YMapa.De([.. pares]);
        }).ToList();
        var recomendaciones = p.Recomendaciones.Select(r => (YNodo)YMapa.De(
            ("id", new YLiteral(r.Id)), ("texto", new YCitado(r.Texto)), ("certeza", new YLiteral(Certezas.Recomendacion)),
            ("rasgos", new YLista([.. r.Rasgos.Select(x => (YNodo)new YLiteral(x))], EnLinea: true)),
            ("estado", new YLiteral(r.Estado)), ("oculta", new YLiteral(r.Oculta ? "true" : "false")))).ToList();
        var raiz = YMapa.De(
            ("tipo", new YLiteral("perfil")),
            ("version", Num(p.Version)),
            ("generada_el", new YInstante(p.Generada.ToString(Formato, CultureInfo.InvariantCulture))),
            ("generacion", new YLiteral(p.Generacion.Length == 0 ? "ninguna" : p.Generacion)),
            ("registros_vistos", Num(p.RegistrosVistos)),
            ("apartados_sin_datos", new YLista([.. p.ApartadosSinDatos.Select(x => (YNodo)new YLiteral(x))], EnLinea: true)),
            ("apartados_ocultos", new YLista([.. p.ApartadosOcultos.Select(x => (YNodo)new YLiteral(x))], EnLinea: true)),
            ("rasgos", new YLista(rasgos, EnLinea: rasgos.Count == 0)),
            ("recomendaciones", new YLista(recomendaciones, EnLinea: recomendaciones.Count == 0)));
        return YamlEscritor.Escribir(raiz);
    }

    public static Perfil Analizar(string yaml)
    {
        var m = YamlLector.LeerMapa(yaml);
        DateTimeOffset F(object? v) => DateTimeOffset.Parse((string)v!, CultureInfo.InvariantCulture);
        int N(object? v) => int.Parse((string?)v ?? "0", CultureInfo.InvariantCulture);
        bool B(object? v) => v as string == "true";
        List<string> L(object? v) => (v as List<object?> ?? []).OfType<string>().ToList();
        string? T(object? v) => v is string s && s is not ("ninguno" or "ninguna") ? s : null;
        var rasgos = (m.GetValueOrDefault("rasgos") as List<object?> ?? []).OfType<Dictionary<string, object?>>().Select(r => new Rasgo(
            (string)r["clave"]!, (string)r["apartado"]!, (string)r["texto"]!, T(r.GetValueOrDefault("contexto")), (string)r["certeza"]!, (string)r["nivel"]!, B(r.GetValueOrDefault("sensible")),
            (r.GetValueOrDefault("evidencia") as List<object?> ?? []).OfType<Dictionary<string, object?>>().Select(e => new EvidenciaDeRasgo((string)e["fuente"]!, (string)e["fecha"]!, (string)e["cita"]!)).ToList(),
            N(r.GetValueOrDefault("fuentes")), N(r.GetValueOrDefault("semanas")), L(r.GetValueOrDefault("hipotesis")),
            (string)r["estado"]!, (string)r["origen"]!, B(r.GetValueOrDefault("oculto")), T(r.GetValueOrDefault("comentario_usuario")),
            r.GetValueOrDefault("evidencia_al_descartar") is string ed ? int.Parse(ed, CultureInfo.InvariantCulture) : null,
            N(r.GetValueOrDefault("version_creada")), F(r["actualizado"]),
            (r.GetValueOrDefault("historial") as List<object?> ?? []).OfType<Dictionary<string, object?>>().Select(h => new EntradaDeRasgo(F(h["fecha"]), (string)h["accion"]!, (string)h["origen"]!, T(h.GetValueOrDefault("detalle")))).ToList())).ToList();
        var recomendaciones = (m.GetValueOrDefault("recomendaciones") as List<object?> ?? []).OfType<Dictionary<string, object?>>().Select(r => new Recomendacion(
            (string)r["id"]!, (string)r["texto"]!, L(r.GetValueOrDefault("rasgos")), (string)r["estado"]!, B(r.GetValueOrDefault("oculta")))).ToList();
        return new Perfil(N(m["version"]), F(m["generada_el"]), T(m.GetValueOrDefault("generacion")) ?? string.Empty, N(m.GetValueOrDefault("registros_vistos")),
            rasgos, recomendaciones, L(m.GetValueOrDefault("apartados_sin_datos")), L(m.GetValueOrDefault("apartados_ocultos")));
    }

    // ------------------------------------------------------------------ generaciones

    public static void GuardarGeneracion(string datos, Generacion g)
    {
        YNodo Num(long n) => new YLiteral(n.ToString(CultureInfo.InvariantCulture));
        var pares = new List<(string, YNodo)>
        {
            ("id", new YLiteral(g.Id)), ("tipo", new YLiteral("generacion")),
            ("fecha", new YInstante(g.Fecha.ToString(Formato, CultureInfo.InvariantCulture))),
            ("motivo", new YLiteral(g.Motivo)), ("resultado", new YLiteral(g.Resultado)),
            ("detalle", g.Detalle is null ? new YLiteral("ninguno") : new YCitado(g.Detalle)),
            ("proveedor", g.Proveedor is null ? new YLiteral("ninguno") : new YLiteral(g.Proveedor)),
            ("modelo", g.Modelo is null ? new YLiteral("ninguno") : new YLiteral(g.Modelo)),
            ("registros_enviados", Num(g.RegistrosEnviados)), ("registros_excluidos", Num(g.RegistrosExcluidos)),
            ("rasgos_aceptados", Num(g.RasgosAceptados)), ("rasgos_descartados", Num(g.RasgosDescartados)),
            ("descartes", YMapa.EnLineaDe([.. g.Descartes.OrderBy(d => d.Key, StringComparer.Ordinal).Select(d => (d.Key, (YNodo)Num(d.Value)))])),
            ("uso", YMapa.EnLineaDe(("tokens_entrada", Num(g.TokensEntrada)), ("tokens_salida", Num(g.TokensSalida)),
                ("coste_estimado_usd", g.CosteUsd is null ? new YLiteral("desconocido") : new YCitado(g.CosteUsd.Value.ToString("0.000000", CultureInfo.InvariantCulture))))),
            ("version", g.Version is null ? new YLiteral("ninguna") : Num(g.Version.Value)),
        };
        var ruta = RutaDeGeneracion(datos, g.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        EscrituraAtomica.Reemplazar(ruta, YamlEscritor.Escribir(YMapa.De([.. pares])));
    }

    public static IReadOnlyList<Generacion> Generaciones(string datos)
    {
        var carpeta = Path.Combine(Directorio(datos), "generaciones");
        if (!Directory.Exists(carpeta))
        {
            return [];
        }

        var lista = new List<Generacion>();
        foreach (var f in Directory.EnumerateFiles(carpeta, "gen-*.yaml").Order(StringComparer.Ordinal))
        {
            try
            {
                var m = YamlLector.LeerMapa(File.ReadAllText(f, System.Text.Encoding.UTF8));
                long N(object? v) => long.Parse((string?)v ?? "0", CultureInfo.InvariantCulture);
                string? T(object? v) => v is string s && s is not ("ninguno" or "ninguna") ? s : null;
                var uso = m.GetValueOrDefault("uso") as Dictionary<string, object?> ?? [];
                lista.Add(new Generacion(
                    (string)m["id"]!, DateTimeOffset.Parse((string)m["fecha"]!, CultureInfo.InvariantCulture), (string)m["motivo"]!, (string)m["resultado"]!, T(m.GetValueOrDefault("detalle")),
                    T(m.GetValueOrDefault("proveedor")), T(m.GetValueOrDefault("modelo")), (int)N(m.GetValueOrDefault("registros_enviados")), (int)N(m.GetValueOrDefault("registros_excluidos")),
                    (int)N(m.GetValueOrDefault("rasgos_aceptados")), (int)N(m.GetValueOrDefault("rasgos_descartados")),
                    (m.GetValueOrDefault("descartes") as Dictionary<string, object?> ?? []).ToDictionary(d => d.Key, d => (int)N(d.Value)),
                    N(uso.GetValueOrDefault("tokens_entrada")), N(uso.GetValueOrDefault("tokens_salida")),
                    decimal.TryParse(uso.GetValueOrDefault("coste_estimado_usd") as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var c) ? c : null,
                    int.TryParse(m.GetValueOrDefault("version") as string, CultureInfo.InvariantCulture, out var v) ? v : null));
            }
            catch (Exception e) when (e is FormatException or KeyNotFoundException or InvalidCastException or YamlDotNet.Core.YamlException)
            {
                // constancia ilegible: se ignora
            }
        }

        return lista;
    }
}
