using System.Globalization;
using Npgsql;
using Orumi.Core.Ficheros;
using Orumi.Core.Texto;
using Orumi.Core.Validacion;

namespace Orumi.Indice;

public sealed record InformeIndexado(
    IReadOnlyDictionary<string, long> Filas,
    IReadOnlyList<Incidencia> Omitidos,
    IReadOnlyList<Incidencia> Referencias,
    IReadOnlyList<Incidencia> Graves,
    bool Sustituido,
    string? Motivo);

/// <summary>
/// Regenera por completo el índice derivado desde los ficheros de <c>orumi/</c> (spec <c>validacion-e-indice</c>):
/// mismo resultado con los mismos ficheros, un fichero ilegible se omite y se lista, y el índice vigente solo se sustituye
/// cuando el nuevo se ha construido bien. Es de solo lectura sobre los ficheros.
/// </summary>
public static class Indexador
{
    private static readonly string[] Tablas =
    [
        "captura", "registro", "registro_dato", "registro_entidad", "registro_certeza", "fuente", "entrada", "relacion",
        "conflicto", "rechazo", "historial", "pregunta", "lote", "lote_captura",
    ];

    private static readonly string[] CamposDeTexto = ["personas", "lugares", "proyectos", "herramientas", "acciones", "resultados", "problemas", "ideas", "contexto"];

    private static readonly string[] MensajesGraves = ["la huella no coincide con el cuerpo", "identificador duplicado"];

    public static async Task<InformeIndexado> ReindexarAsync(
        string datos, string conexion, string esquemaVivo = Esquema.Vivo, bool forzar = false,
        Action<string>? hito = null, CancellationToken ct = default)
    {
        Esquema.Validar(esquemaVivo);
        var almacen = Almacen.Cargar(datos);
        var incidencias = Validador.Validar(almacen);
        var omitidos = incidencias.Where(i => i.Mensaje.StartsWith("No se puede leer", StringComparison.Ordinal)).ToList();
        var graves = incidencias.Where(i => MensajesGraves.Any(g => i.Mensaje.StartsWith(g, StringComparison.Ordinal))).ToList();
        var referencias = incidencias.Except(omitidos).Except(graves).ToList();

        if (graves.Count > 0 && !forzar)
        {
            return new InformeIndexado(new Dictionary<string, long>(), omitidos, referencias, graves, false,
                "Hay problemas graves (captura alterada o identificadores duplicados): el índice vigente no se ha sustituido. Corrige los ficheros o usa --forzar.");
        }

        var nuevo = esquemaVivo + "_nuevo";
        var anterior = esquemaVivo + "_anterior";

        await using var con = new NpgsqlConnection(conexion);
        await con.OpenAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);

        await Ejecutar(con, tx, $"DROP SCHEMA IF EXISTS {nuevo} CASCADE; DROP SCHEMA IF EXISTS {anterior} CASCADE;", ct);
        await Ejecutar(con, tx, Esquema.Crear(nuevo), ct);
        await Cargar(con, tx, nuevo, almacen, ct);

        hito?.Invoke("construido");

        var filas = new Dictionary<string, long>();
        foreach (var t in Tablas)
        {
            await using var c = new NpgsqlCommand($"SELECT count(*) FROM {nuevo}.{t}", con, tx);
            filas[t] = (long)(await c.ExecuteScalarAsync(ct))!;
        }

        // Sustitución en la misma transacción: si algo falla, el índice vigente queda como estaba.
        await using (var existe = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = @s)", con, tx))
        {
            existe.Parameters.AddWithValue("s", esquemaVivo);
            if ((bool)(await existe.ExecuteScalarAsync(ct))!)
            {
                await Ejecutar(con, tx, $"ALTER SCHEMA {esquemaVivo} RENAME TO {anterior};", ct);
            }
        }

        await Ejecutar(con, tx, $"ALTER SCHEMA {nuevo} RENAME TO {esquemaVivo}; DROP SCHEMA IF EXISTS {anterior} CASCADE;", ct);
        await tx.CommitAsync(ct);

        return new InformeIndexado(filas, omitidos, referencias, graves, true, null);
    }

    // ------------------------------------------------------------------ carga

    private static async Task Cargar(NpgsqlConnection con, NpgsqlTransaction tx, string s, Almacen almacen, CancellationToken ct)
    {
        async Task Fila(string tabla, params (string Columna, object? Valor)[] valores)
        {
            var columnas = string.Join(", ", valores.Select(v => v.Columna));
            var parametros = string.Join(", ", valores.Select((_, i) => "@p" + i));
            await using var cmd = new NpgsqlCommand($"INSERT INTO {s}.{tabla} ({columnas}) VALUES ({parametros})", con, tx);
            for (var i = 0; i < valores.Length; i++)
            {
                cmd.Parameters.AddWithValue("p" + i, Valor(valores[i].Valor));
            }

            await cmd.ExecuteNonQueryAsync(ct);
        }

        await Fila("meta", ("clave", "generado_el"), ("valor", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));

        foreach (var d in almacen.DeTipo("captura").Where(d => d.Id is not null))
        {
            var fm = d.Frontmatter;
            await Fila("captura",
                ("id", d.Id), ("ruta", d.Ruta), ("comunicada_el", Instante(Txt(fm, "comunicada_el"))), ("origen", Txt(fm, "origen")),
                ("fecha_acontecimiento", Fecha(Txt(fm, "fecha_acontecimiento"))), ("etiqueta", Txt(fm, "etiqueta")),
                ("dispositivo", Txt(Mapa(fm, "contexto_automatico"), "dispositivo")), ("huella_ok", Txt(fm, "huella") == Huella.De(d.Cuerpo)));
        }

        foreach (var d in almacen.DeTipo("registro").Where(d => d.Id is not null))
        {
            await CargarRegistro(Fila, d);
        }

        foreach (var d in almacen.DeTipo("pregunta").Where(d => d.Id is not null))
        {
            var fm = d.Frontmatter;
            await Fila("pregunta",
                ("id", d.Id), ("ruta", d.Ruta), ("registro_id", Txt(fm, "registro")), ("campo", Txt(fm, "campo")), ("texto", Txt(fm, "texto")),
                ("estado", Txt(fm, "estado")), ("valor", Txt(fm, "valor")), ("creada_el", Instante(Txt(fm, "creada_el"))), ("cerrada_el", Instante(Txt(fm, "cerrada_el"))));
        }

        foreach (var d in almacen.DeTipo("lote").Where(d => d.Id is not null))
        {
            var fm = d.Frontmatter;
            var agente = Mapa(fm, "agente");
            await Fila("lote",
                ("id", d.Id), ("ejecutado_el", Instante(Txt(fm, "ejecutado_el"))), ("ejecucion", Txt(fm, "ejecucion")),
                ("version_agente", Txt(agente, "version")), ("modelo", Txt(agente, "modelo")), ("proveedor", Txt(fm, "proveedor")));
            foreach (var c in Lista(fm, "capturas"))
            {
                var captura = Txt(c, "captura");
                var creados = Textos(c, "registros_creados");
                var actualizados = Textos(c, "registros_actualizados");
                foreach (var r in creados)
                {
                    await Fila("lote_captura", ("lote_id", d.Id), ("captura_id", captura), ("registro_id", r), ("accion", "creado"));
                }

                foreach (var r in actualizados)
                {
                    await Fila("lote_captura", ("lote_id", d.Id), ("captura_id", captura), ("registro_id", r), ("accion", "actualizado"));
                }

                if (creados.Count + actualizados.Count == 0)
                {
                    await Fila("lote_captura", ("lote_id", d.Id), ("captura_id", captura), ("registro_id", null), ("accion", "procesada"));
                }
            }
        }
    }

    private static async Task CargarRegistro(Func<string, (string, object?)[], Task> fila, Documento d)
    {
        Task F(string t, params (string, object?)[] v) => fila(t, v);
        var fm = d.Frontmatter;
        var id = d.Id!;
        var fecha = fm.GetValueOrDefault("fecha") as Dictionary<string, object?>;
        var hora = fm.GetValueOrDefault("hora") as Dictionary<string, object?>;
        var duracion = fm.GetValueOrDefault("duracion") as Dictionary<string, object?>;
        var multidia = fm.GetValueOrDefault("multidia") as Dictionary<string, object?>;
        var fin = multidia?.GetValueOrDefault("fin") as Dictionary<string, object?>;
        var proc = Mapa(fm, "procedencia");
        var gen = Mapa(proc, "generado_por");

        await F("registro",
            ("id", id), ("ruta", d.Ruta), ("clase", Txt(fm, "clase")), ("titulo", Txt(fm, "titulo")), ("comunicada_el", Instante(Txt(fm, "comunicada_el"))),
            ("fecha_expresion", Txt(fecha, "expresion")), ("fecha_desde", Fecha(Txt(fecha, "desde"))), ("fecha_hasta", Fecha(Txt(fecha, "hasta"))),
            ("hora_expresion", Txt(hora, "expresion")), ("hora_valor", Hora(Txt(hora, "valor"))), ("hora_aproximada", hora is null ? null : Txt(hora, "aproximada") == "true"),
            ("duracion_expresion", Txt(duracion, "expresion")), ("duracion_minutos", Entero(Txt(duracion, "minutos"))),
            ("multidia_estado", Txt(multidia, "estado")), ("multidia_fin_desde", Fecha(Txt(fin, "desde"))), ("multidia_fin_hasta", Fecha(Txt(fin, "hasta"))),
            ("clave_extraccion", Txt(fm, "clave_extraccion")), ("estado", Txt(fm, "estado") ?? "activo"), ("version_agente", Txt(gen, "version")), ("modelo", Txt(gen, "modelo")),
            ("generado_el", Instante(Txt(proc, "generado_el"))), ("actualizado_el", Instante(Txt(proc, "actualizado_el"))));

        var certeza = Mapa(fm, "certeza");
        foreach (var (campo, c) in certeza)
        {
            await F("registro_certeza", ("registro_id", id), ("campo", campo), ("certeza", c as string));
        }

        foreach (var campo in CamposDeTexto)
        {
            foreach (var valor in ValoresDe(fm.GetValueOrDefault(campo)).Where(v => v != "desconocido"))
            {
                await F("registro_dato", ("registro_id", id), ("campo", campo), ("valor", valor),
                    ("valor_normalizado", Normalizacion.SinAcentos(valor)), ("certeza", certeza.GetValueOrDefault(campo) as string));
            }
        }

        foreach (var e in Lista(fm, "entidades"))
        {
            var nombre = Txt(e, "nombre");
            if (string.IsNullOrWhiteSpace(nombre))
            {
                continue;
            }

            await F("registro_entidad", ("registro_id", id), ("nombre", nombre), ("nombre_normalizado", Normalizacion.SinAcentos(nombre)),
                ("tipo", Txt(e, "tipo")), ("certeza", Txt(e, "certeza") ?? certeza.GetValueOrDefault("entidades") as string));
        }

        foreach (var f in Lista(fm, "fuentes"))
        {
            var fr = f.GetValueOrDefault("fragmento") as Dictionary<string, object?>;
            await F("fuente", ("registro_id", id), ("captura_id", Txt(f, "captura")), ("rol", Txt(f, "rol")),
                ("fragmento_desde", Entero(Txt(fr, "desde"))), ("fragmento_hasta", Entero(Txt(fr, "hasta"))));
        }

        foreach (var en in Lista(fm, "entradas"))
        {
            await F("entrada", ("registro_id", id), ("fecha", Fecha(Txt(en, "fecha"))), ("captura_id", Txt(en, "captura")), ("resumen", Txt(en, "resumen")));
        }

        foreach (var r in Lista(fm, "relaciones"))
        {
            await F("relacion", ("origen_id", id), ("destino_id", Txt(r, "destino")), ("tipo", Txt(r, "tipo")), ("certeza", Txt(r, "certeza")),
                ("evidencia", r.GetValueOrDefault("evidencia") is List<object?> ev ? string.Join(",", ev.OfType<string>()) : null));
        }

        foreach (var c in Lista(fm, "conflictos"))
        {
            await F("conflicto", ("registro_id", id), ("con_registro_id", Txt(c, "con")), ("campo", Txt(c, "campo")),
                ("pregunta_id", Txt(c, "pregunta")), ("estado", Txt(c, "estado")));
        }

        foreach (var r in Lista(fm, "rechazos"))
        {
            await F("rechazo", ("registro_id", id), ("campo", Txt(r, "campo")), ("valor", Txt(r, "valor")), ("rechazada_el", Instante(Txt(r, "rechazada_el"))));
        }

        var orden = 0;
        foreach (var h in Lista(fm, "historial"))
        {
            await F("historial", ("registro_id", id), ("orden", orden++), ("fecha", Instante(Txt(h, "fecha"))), ("origen", Txt(h, "origen")),
                ("campo", Txt(h, "campo")), ("pregunta_id", Txt(h, "pregunta")), ("captura_id", Txt(h, "captura")));
        }
    }

    // ------------------------------------------------------------------ utilidades

    private static async Task Ejecutar(NpgsqlConnection con, NpgsqlTransaction tx, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, con, tx);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static object Valor(object? v) => v switch
    {
        null => DBNull.Value,
        DateTimeOffset o => o.ToUniversalTime(),
        _ => v,
    };

    private static string? Txt(Dictionary<string, object?>? m, string k) => m is not null && m.TryGetValue(k, out var v) ? v as string : null;

    private static Dictionary<string, object?> Mapa(Dictionary<string, object?> m, string k) =>
        m.TryGetValue(k, out var v) && v is Dictionary<string, object?> d ? d : [];

    private static List<Dictionary<string, object?>> Lista(Dictionary<string, object?> m, string k) =>
        m.TryGetValue(k, out var v) && v is List<object?> l ? l.OfType<Dictionary<string, object?>>().ToList() : [];

    private static List<string> Textos(Dictionary<string, object?> m, string k) =>
        m.TryGetValue(k, out var v) && v is List<object?> l ? l.OfType<string>().ToList() : [];

    private static IEnumerable<string> ValoresDe(object? v) => v switch
    {
        string s => [s],
        List<object?> l => l.OfType<string>(),
        _ => [],
    };

    private static DateTimeOffset? Instante(string? t) =>
        t is not null && DateTimeOffset.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out var v) ? v : null;

    private static DateOnly? Fecha(string? t) =>
        t is not null && DateOnly.TryParseExact(t, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var v) ? v : null;

    private static TimeOnly? Hora(string? t) =>
        t is not null && TimeOnly.TryParseExact(t, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var v) ? v : null;

    private static int? Entero(string? t) => int.TryParse(t, CultureInfo.InvariantCulture, out var v) ? v : null;
}
