using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Orumi.Core.Ficheros;

namespace Orumi.Core.Validacion;

/// <summary>
/// Comprueba los ficheros de <c>orumi/</c> contra el formato (<c>docs/formato-de-ficheros.md</c>) y las reglas
/// de validación del contrato de Mira (<c>docs/agente-mira.md</c> §5). No modifica nada.
/// </summary>
public static partial class Validador
{
    private static readonly string[] Certezas = ["dicho", "confirmado", "interpretado", "hipotesis", "recomendacion"];
    private static readonly string[] Clases = ["actividad", "evento", "idea", "decision", "problema", "otro"];
    private static readonly string[] EstadosPregunta = ["pendiente", "respondida", "no_recuerda", "no_relevante", "cerrada_por_limite"];
    private static readonly string[] TiposEntidad = ["organizacion", "sistema", "producto", "otro"];
    private static readonly string[] TiposRelacion = ["surge_de", "continua", "relacionado_con"];
    private static readonly string[] CamposConCerteza =
        ["fecha", "hora", "duracion", "contexto", "personas", "lugares", "proyectos", "herramientas", "acciones", "resultados", "problemas", "ideas"];
    private static readonly string[] ClavesCaptura =
        ["id", "tipo", "comunicada_el", "origen", "huella", "contexto_automatico", "fecha_acontecimiento", "etiqueta"];

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")] private static partial Regex FechaRx();
    [GeneratedRegex(@"^([01]\d|2[0-3]):[0-5]\d$")] private static partial Regex HoraRx();
    [GeneratedRegex(@"\[\[([^\]]+)\]\]")] private static partial Regex WikilinkRx();
    [GeneratedRegex(@"^(cap-[a-z0-9-]+)#f\d+:([a-z]+):\d+$")] private static partial Regex ClaveRx();

    public static bool EsClaseValida(string clase) => Clases.Contains(clase);

    public static IReadOnlyList<Incidencia> Validar(Almacen almacen)
    {
        var errores = new List<Incidencia>(almacen.Problemas);
        var ids = new Dictionary<string, Documento>();

        foreach (var d in almacen.Documentos)
        {
            void E(string m) => errores.Add(new Incidencia(d.Ruta, m));

            if (d.Id is null)
            {
                E("falta el id");
                continue;
            }

            if (!ids.TryAdd(d.Id, d))
            {
                E($"identificador duplicado {d.Id}");
            }

            if (Path.GetFileNameWithoutExtension(d.Ruta) != d.Id)
            {
                E("el nombre del fichero no coincide con el id");
            }

            var esperado = Identificadores.DirectorioDe(d.Id);
            if (esperado is null)
            {
                E("el identificador no tiene un formato válido");
            }
            else if (!d.Ruta.StartsWith(esperado + "/", StringComparison.Ordinal))
            {
                E($"ruta esperada {esperado}/");
            }
        }

        Documento? Doc(string tipo, string? id) => id is not null && ids.TryGetValue(id, out var d) && d.Tipo == tipo ? d : null;

        var capturas = almacen.DeTipo("captura").Where(d => d.Id is not null).ToList();
        var registros = almacen.DeTipo("registro").Where(d => d.Id is not null).ToList();
        var preguntas = almacen.DeTipo("pregunta").Where(d => d.Id is not null).ToList();
        var lotes = almacen.DeTipo("lote").Where(d => d.Id is not null).ToList();

        foreach (var d in almacen.Documentos.Where(d => d.Tipo is not ("captura" or "registro" or "pregunta" or "lote")))
        {
            errores.Add(new Incidencia(d.Ruta, $"tipo desconocido '{d.Tipo}'"));
        }

        foreach (var c in capturas)
        {
            ValidarCaptura(c, errores);
        }

        var claves = new Dictionary<string, string>();
        foreach (var r in registros)
        {
            ValidarRegistro(r, Doc, claves, errores);
        }

        foreach (var p in preguntas)
        {
            ValidarPregunta(p, Doc, errores);
        }

        var procesadas = new HashSet<string>();
        var creados = new List<string>();
        foreach (var l in lotes)
        {
            ValidarLote(l, Doc, procesadas, creados, errores);
        }

        foreach (var c in capturas.Where(c => !procesadas.Contains(c.Id!)))
        {
            errores.Add(new Incidencia(c.Ruta, "captura sin lote"));
        }

        foreach (var r in registros.Where(r => !creados.Contains(r.Id!)))
        {
            errores.Add(new Incidencia(r.Ruta, "registro no creado por ningún lote"));
        }

        if (creados.Distinct().Count() != creados.Count)
        {
            errores.Add(new Incidencia("lotes", "hay registros creados por más de un lote"));
        }

        return errores;
    }

    // ---------------------------------------------------------------- capturas

    private static void ValidarCaptura(Documento d, List<Incidencia> errores)
    {
        void E(string m) => errores.Add(new Incidencia(d.Ruta, m));
        var fm = d.Frontmatter;

        if (!Identificadores.Captura.IsMatch(d.Id!))
        {
            E("id de captura mal formado");
        }

        if (Texto(fm, "origen") is not ("texto" or "voz"))
        {
            E("origen inválido (texto | voz)");
        }

        if (Texto(fm, "huella") != Huella.De(d.Cuerpo))
        {
            E("la huella no coincide con el cuerpo");
        }

        var extra = fm.Keys.Except(ClavesCaptura).ToList();
        if (extra.Count > 0)
        {
            E($"claves no permitidas: {string.Join(", ", extra)}");
        }

        if (fm.TryGetValue("contexto_automatico", out var ctx) && ctx is Dictionary<string, object?> m && m.Keys.Except(["dispositivo"]).Any())
        {
            E("contexto_automatico solo admite dispositivo");
        }

        var comunicada = Texto(fm, "comunicada_el");
        if (!TryInstante(comunicada, out _))
        {
            E("comunicada_el no es un instante ISO 8601");
        }
        else if (!d.Id!.StartsWith("cap-" + comunicada![..10].Replace("-", "") + "-" + comunicada.Substring(11, 5).Replace(":", ""), StringComparison.Ordinal))
        {
            E("el id no coincide con comunicada_el");
        }

        if (Texto(fm, "fecha_acontecimiento") is { } fa && !FechaRx().IsMatch(fa))
        {
            E("fecha_acontecimiento no es una fecha ISO");
        }
    }

    // ---------------------------------------------------------------- registros

    private static void ValidarRegistro(
        Documento d, Func<string, string?, Documento?> doc, Dictionary<string, string> claves, List<Incidencia> errores)
    {
        void E(string m) => errores.Add(new Incidencia(d.Ruta, m));
        var fm = d.Frontmatter;
        var id = d.Id!;

        if (!Identificadores.Registro.IsMatch(id))
        {
            E("id de registro mal formado");
        }

        foreach (var k in new[] { "id", "tipo", "clase", "titulo", "comunicada_el", "fuentes", "fecha", "procedencia", "clave_extraccion", "certeza" })
        {
            if (!fm.ContainsKey(k))
            {
                E($"falta {k}");
            }
        }

        if (Texto(fm, "clase") is not { } clase || !Clases.Contains(clase))
        {
            E("clase inválida");
        }

        if (string.IsNullOrWhiteSpace(d.Cuerpo))
        {
            E("sin descripción");
        }

        var certeza = fm.TryGetValue("certeza", out var cz) && cz is Dictionary<string, object?> cm ? cm : new Dictionary<string, object?>();

        // certeza: valores válidos y campos existentes
        foreach (var (k, v) in certeza)
        {
            if (v is not string s || !Certezas.Contains(s))
            {
                E($"certeza inválida {k}={v}");
            }

            var partes = k.Split('.');
            var existe = fm.TryGetValue(partes[0], out var campo)
                         && (partes.Length == 1 || campo is Dictionary<string, object?> sub && sub.ContainsKey(partes[1]));
            if (!existe)
            {
                E($"certeza de un campo inexistente {k}");
            }
        }

        foreach (var campo in CamposConCerteza.Where(fm.ContainsKey))
        {
            var desconocido = Texto(fm, campo) == "desconocido";
            if (!desconocido && !certeza.ContainsKey(campo))
            {
                E($"{campo} sin certeza");
            }

            if (desconocido && certeza.ContainsKey(campo))
            {
                E($"{campo} desconocido con certeza");
            }
        }

        ValidarHora(fm, certeza, E);
        ValidarEntidades(fm, certeza, E);
        ValidarMultidia(fm, certeza, E, doc);

        if (fm.TryGetValue("fecha", out var fecha) && fecha is Dictionary<string, object?> f)
        {
            var desde = Texto(f, "desde");
            var hasta = Texto(f, "hasta");
            if (desde is null || hasta is null || !FechaRx().IsMatch(desde) || !FechaRx().IsMatch(hasta))
            {
                E("fecha: desde y hasta deben ser fechas ISO");
            }
            else if (string.CompareOrdinal(desde, hasta) > 0)
            {
                E("fecha desde > hasta");
            }
        }

        // fuentes y fragmentos
        string? origen = null;
        if (fm.TryGetValue("fuentes", out var fu) && fu is List<object?> fuentes && fuentes.Count > 0)
        {
            if (fuentes[0] is not Dictionary<string, object?> primera || Texto(primera, "rol") != "origen")
            {
                E("la primera fuente no es el origen");
            }

            foreach (var item in fuentes.OfType<Dictionary<string, object?>>())
            {
                var capId = Texto(item, "captura");
                var cap = doc("captura", capId);
                if (cap is null)
                {
                    E($"captura inexistente {capId}");
                    continue;
                }

                origen ??= capId;
                if (item.TryGetValue("fragmento", out var frObj) && frObj is Dictionary<string, object?> fr)
                {
                    ValidarFragmento(fr, cap.Cuerpo, E);
                }
            }
        }
        else
        {
            E("fuentes vacías");
        }

        if (origen is not null && doc("captura", origen) is { } capOrigen)
        {
            if (!MismoInstante(Texto(fm, "comunicada_el"), Texto(capOrigen.Frontmatter, "comunicada_el")))
            {
                E("comunicada_el distinta de la de la captura de origen");
            }
        }

        // clave de extracción
        var clave = Texto(fm, "clave_extraccion");
        var mc = clave is null ? null : ClaveRx().Match(clave);
        if (mc is null || !mc.Success)
        {
            E("clave_extraccion mal formada");
        }
        else
        {
            if (origen is not null && mc.Groups[1].Value != origen)
            {
                E("clave_extraccion no empieza por la captura de origen");
            }

            if (Texto(fm, "clase") != mc.Groups[2].Value)
            {
                E("clave_extraccion no coincide con la clase");
            }

            if (claves.TryGetValue(clave!, out var otro))
            {
                E($"clave_extraccion duplicada con {otro}");
            }
            else
            {
                claves[clave!] = id;
            }
        }

        // relaciones
        foreach (var r in Lista(fm, "relaciones"))
        {
            if (doc("registro", Texto(r, "destino")) is null)
            {
                E($"relación a un registro inexistente {Texto(r, "destino")}");
            }

            if (Texto(r, "tipo") is not { } tr || !TiposRelacion.Contains(tr))
            {
                E("relación con tipo inválido");
            }

            if (Texto(r, "certeza") is not { } cr || !Certezas.Contains(cr))
            {
                E("relación sin certeza válida");
            }
        }

        // conflictos (simétricos y con pregunta)
        foreach (var c in Lista(fm, "conflictos"))
        {
            var con = doc("registro", Texto(c, "con"));
            if (con is null)
            {
                E($"conflicto con un registro inexistente {Texto(c, "con")}");
                continue;
            }

            var simetrico = Lista(con.Frontmatter, "conflictos").Any(x =>
                Texto(x, "con") == id && Texto(x, "campo") == Texto(c, "campo") && Texto(x, "pregunta") == Texto(c, "pregunta"));
            if (!simetrico)
            {
                E("conflicto no simétrico");
            }

            if (doc("pregunta", Texto(c, "pregunta")) is null)
            {
                E($"pregunta inexistente {Texto(c, "pregunta")}");
            }

            var campoConflicto = Texto(c, "campo");
            if (campoConflicto is null || !fm.ContainsKey(campoConflicto) || !con.Frontmatter.ContainsKey(campoConflicto))
            {
                E("conflicto sin dos valores visibles");
            }

            if (!d.Cuerpo.Contains($"[[{Texto(c, "con")}]]", StringComparison.Ordinal))
            {
                E("conflicto sin wikilink en el cuerpo");
            }
        }

        // rechazos
        var historial = Lista(fm, "historial");
        foreach (var rz in Lista(fm, "rechazos"))
        {
            var campo = Texto(rz, "campo");
            if (campo is not null && fm.TryGetValue(campo, out var actual) && actual is string s && s == Texto(rz, "valor"))
            {
                E("el valor rechazado sigue en el campo");
            }

            if (!historial.Any(h => Texto(h, "origen") == "usuario" && Texto(h, "campo") == campo
                                    && h.TryGetValue("anterior", out var ant) && ant as string == Texto(rz, "valor")))
            {
                E("rechazo sin entrada de historial del usuario");
            }
        }

        // historial
        var fechas = historial.Select(h => Texto(h, "fecha")).ToList();
        if (fechas.Any(f => !TryInstante(f, out _)))
        {
            E("historial con una fecha que no es un instante ISO");
        }
        else if (!fechas.SequenceEqual(fechas.OrderBy(f => DateTimeOffset.Parse(f!, CultureInfo.InvariantCulture))))
        {
            E("historial no cronológico");
        }

        foreach (var h in historial)
        {
            if (Texto(h, "origen") is not ("usuario" or "mira"))
            {
                E("historial: origen inválido");
            }

            if (Texto(h, "pregunta") is { } pq && doc("pregunta", pq) is null)
            {
                E("historial: pregunta inexistente");
            }
        }

        // lo corregido por el usuario queda confirmado (o desconocido)
        foreach (var h in historial.Where(h => Texto(h, "origen") == "usuario"))
        {
            var campo = Texto(h, "campo");
            if (campo is null)
            {
                continue;
            }

            var confirmado = certeza.TryGetValue(campo, out var cc) && cc as string == "confirmado"
                             || campo == "entidades" && Lista(fm, "entidades").Any(e => Texto(e, "certeza") == "confirmado")
                             || fm.TryGetValue(campo, out var val) && val as string == "desconocido";
            if (!confirmado)
            {
                E($"campo corregido por el usuario ({campo}) sin certeza confirmado");
            }
        }

        // cuerpo
        foreach (Match m in WikilinkRx().Matches(d.Cuerpo))
        {
            if (doc("captura", m.Groups[1].Value) is null && doc("registro", m.Groups[1].Value) is null
                && doc("pregunta", m.Groups[1].Value) is null)
            {
                E($"wikilink roto {m.Groups[1].Value}");
            }
        }

        if (origen is not null && !d.Cuerpo.Contains($"[[{origen}]]", StringComparison.Ordinal))
        {
            E("el cuerpo no enlaza con la captura de origen");
        }

        // procedencia
        if (fm.TryGetValue("procedencia", out var pr) && pr is Dictionary<string, object?> proc)
        {
            if (!TryInstante(Texto(proc, "generado_el"), out _))
            {
                E("procedencia.generado_el no es un instante ISO");
            }

            if (Texto(proc, "ejecucion") is not ("lote" or "peticion"))
            {
                E("procedencia.ejecucion inválida (lote | peticion)");
            }
        }
    }

    private static void ValidarFragmento(Dictionary<string, object?> fr, string cuerpo, Action<string> e)
    {
        var runes = cuerpo.EnumerateRunes().ToList();
        if (!int.TryParse(Texto(fr, "desde"), out var desde) || !int.TryParse(Texto(fr, "hasta"), out var hasta))
        {
            e("fragmento sin desde y hasta numéricos");
            return;
        }

        if (desde < 0 || desde >= hasta || hasta > runes.Count)
        {
            e("fragmento fuera de rango");
            return;
        }

        if (Texto(fr, "cita") is { } cita)
        {
            var trozo = string.Concat(runes.Skip(desde).Take(hasta - desde).Select(r => r.ToString()));
            if (!trozo.StartsWith(cita, StringComparison.Ordinal))
            {
                e("la cita no es el inicio del fragmento");
            }
        }
    }

    private static void ValidarHora(Dictionary<string, object?> fm, Dictionary<string, object?> certeza, Action<string> e)
    {
        if (!fm.TryGetValue("hora", out var h) || h is not Dictionary<string, object?> hora)
        {
            return;
        }

        if (hora.Keys.Except(["expresion", "valor", "aproximada"]).Any())
        {
            e("hora con claves no permitidas");
        }

        if (Texto(hora, "expresion") is null)
        {
            e("hora sin expresion");
        }

        var valor = Texto(hora, "valor");
        if (valor is not null && !HoraRx().IsMatch(valor))
        {
            e("hora.valor no es HH:MM");
        }

        if (Texto(hora, "aproximada") == "true" && valor is null)
        {
            e("hora aproximada sin valor");
        }

        if (!certeza.ContainsKey("hora"))
        {
            e("hora sin certeza");
        }

        if (fm.TryGetValue("fecha", out var f) && f is Dictionary<string, object?> fecha && Texto(fecha, "desde") != Texto(fecha, "hasta"))
        {
            e("hora con una fecha de varios días");
        }
    }

    private static void ValidarEntidades(Dictionary<string, object?> fm, Dictionary<string, object?> certeza, Action<string> e)
    {
        if (!fm.TryGetValue("entidades", out var en) || en is not List<object?> lista)
        {
            return;
        }

        var porCampo = certeza.ContainsKey("entidades");
        foreach (var x in lista.OfType<Dictionary<string, object?>>())
        {
            if (string.IsNullOrWhiteSpace(Texto(x, "nombre")))
            {
                e("entidad sin nombre");
            }

            if (Texto(x, "tipo") is { } t && !TiposEntidad.Contains(t))
            {
                e("entidad con tipo inválido");
            }

            if (x.Keys.Except(["nombre", "tipo", "certeza"]).Any())
            {
                e("entidad con claves no permitidas");
            }

            if (porCampo)
            {
                if (x.ContainsKey("certeza"))
                {
                    e("entidad con certeza propia y también en el mapa de certeza");
                }
            }
            else if (Texto(x, "certeza") is not { } c || !Certezas.Contains(c))
            {
                e("entidad sin certeza");
            }
        }
    }

    private static void ValidarMultidia(
        Dictionary<string, object?> fm, Dictionary<string, object?> certeza, Action<string> e, Func<string, string?, Documento?> doc)
    {
        if (!fm.TryGetValue("multidia", out var m) || m is not Dictionary<string, object?> md)
        {
            return;
        }

        var estado = Texto(md, "estado");
        var finDesconocido = Texto(md, "fin") == "desconocido";
        if (!certeza.ContainsKey("multidia.estado"))
        {
            e("multidia.estado sin certeza");
        }

        if (estado is not ("abierto" or "cerrado"))
        {
            e("multidia.estado inválido");
        }

        if (estado == "cerrado" && finDesconocido)
        {
            e("cerrado sin fin");
        }

        if (estado == "abierto" && !finDesconocido)
        {
            e("abierto con fin");
        }

        var entradas = Lista(fm, "entradas");
        if (entradas.Count == 0)
        {
            e("multidia sin entradas");
        }

        foreach (var en in entradas)
        {
            if (doc("captura", Texto(en, "captura")) is null)
            {
                e($"entrada con captura inexistente {Texto(en, "captura")}");
            }
        }
    }

    // ---------------------------------------------------------------- preguntas y lotes

    private static void ValidarPregunta(Documento d, Func<string, string?, Documento?> doc, List<Incidencia> errores)
    {
        void E(string m) => errores.Add(new Incidencia(d.Ruta, m));
        var fm = d.Frontmatter;
        if (!Identificadores.Pregunta.IsMatch(d.Id!))
        {
            E("id de pregunta mal formado");
        }

        if (doc("registro", Texto(fm, "registro")) is null)
        {
            E("registro inexistente");
        }

        var estado = Texto(fm, "estado");
        if (estado is null || !EstadosPregunta.Contains(estado))
        {
            E("estado inválido");
        }

        if (estado is "respondida" or "no_recuerda" or "no_relevante" && !fm.ContainsKey("respuesta"))
        {
            E("pregunta cerrada sin respuesta");
        }

        if (estado == "pendiente" && (fm.ContainsKey("respuesta") || fm.ContainsKey("cerrada_el")))
        {
            E("pregunta pendiente con datos de cierre");
        }

        if (Texto(fm, "valor") is not ("alta" or "media" or "baja"))
        {
            E("valor inválido (alta | media | baja)");
        }

        foreach (var otro in ListaDeTextos(fm, "otros_registros"))
        {
            if (doc("registro", otro) is null)
            {
                E($"otro registro inexistente {otro}");
            }
        }
    }

    private static void ValidarLote(
        Documento d, Func<string, string?, Documento?> doc, HashSet<string> procesadas, List<string> creados, List<Incidencia> errores)
    {
        void E(string m) => errores.Add(new Incidencia(d.Ruta, m));
        if (!Identificadores.Lote.IsMatch(d.Id!))
        {
            E("id de lote mal formado");
        }

        if (Texto(d.Frontmatter, "ejecucion") is not ("lote" or "peticion"))
        {
            E("ejecucion inválida (lote | peticion)");
        }

        if (string.IsNullOrWhiteSpace(Texto(d.Frontmatter, "proveedor")))
        {
            E("el lote no indica el proveedor");
        }

        if (!(d.Frontmatter.TryGetValue("agente", out var ag) && ag is Dictionary<string, object?> agente && !string.IsNullOrWhiteSpace(Texto(agente, "modelo"))))
        {
            E("el lote no indica el modelo");
        }

        foreach (var c in Lista(d.Frontmatter, "capturas"))
        {
            var cap = Texto(c, "captura");
            if (doc("captura", cap) is null)
            {
                E($"captura inexistente {cap}");
            }
            else
            {
                procesadas.Add(cap!);
            }

            foreach (var r in ListaDeTextos(c, "registros_creados").Concat(ListaDeTextos(c, "registros_actualizados")))
            {
                if (doc("registro", r) is null)
                {
                    E($"registro inexistente {r}");
                }
            }

            foreach (var p in ListaDeTextos(c, "preguntas_creadas"))
            {
                if (doc("pregunta", p) is null)
                {
                    E($"pregunta inexistente {p}");
                }
            }

            creados.AddRange(ListaDeTextos(c, "registros_creados"));
        }
    }

    // ---------------------------------------------------------------- utilidades

    private static string? Texto(Dictionary<string, object?> mapa, string clave) =>
        mapa.TryGetValue(clave, out var v) ? v as string : null;

    private static List<Dictionary<string, object?>> Lista(Dictionary<string, object?> mapa, string clave) =>
        mapa.TryGetValue(clave, out var v) && v is List<object?> l ? l.OfType<Dictionary<string, object?>>().ToList() : [];

    private static List<string> ListaDeTextos(Dictionary<string, object?> mapa, string clave) =>
        mapa.TryGetValue(clave, out var v) && v is List<object?> l ? l.OfType<string>().ToList() : [];

    private static bool TryInstante(string? texto, out DateTimeOffset valor)
    {
        valor = default;
        return texto is not null
               && Regex.IsMatch(texto, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+-]\d{2}:\d{2}$")
               && DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out valor);
    }

    private static bool MismoInstante(string? a, string? b) =>
        TryInstante(a, out var x) && TryInstante(b, out var y) && x == y;
}
