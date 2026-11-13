using Npgsql;
using Orumi.Core.Texto;

namespace Orumi.Indice;

public sealed record FilaRegistro(string Id, string? Clase, string? Titulo, DateOnly? FechaDesde, DateOnly? FechaHasta, TimeOnly? Hora);

public sealed record FilaPregunta(string Id, string RegistroId, string? Campo, string? Texto, string Valor, string Estado);

public sealed record FilaConflicto(string RegistroId, string ConRegistroId, string? Campo, string? PreguntaId, string? Estado);

/// <summary>Consultas estructuradas sobre el índice derivado (spec <c>indice-y-respaldo</c> y <c>validacion-e-indice</c>). Solo lectura.</summary>
public sealed class Consultas(string conexion, string esquema = Esquema.Vivo)
{
    private readonly string _s = Esquema.Validar(esquema);

    private const string Columnas = "r.id, r.clase, r.titulo, r.fecha_desde, r.fecha_hasta, r.hora_valor";

    private async Task<List<FilaRegistro>> Registros(string sql, Action<NpgsqlParameterCollection> parametros, CancellationToken ct)
    {
        await using var con = new NpgsqlConnection(conexion);
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        parametros(cmd.Parameters);
        var lista = new List<FilaRegistro>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            lista.Add(new FilaRegistro(
                r.GetString(0),
                r.IsDBNull(1) ? null : r.GetString(1),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.IsDBNull(3) ? null : DateOnly.FromDateTime(r.GetDateTime(3)),
                r.IsDBNull(4) ? null : DateOnly.FromDateTime(r.GetDateTime(4)),
                r.IsDBNull(5) ? null : TimeOnly.FromTimeSpan(r.GetTimeSpan(5))));
        }

        return lista;
    }

    /// <summary>Registros cuyo rango de fechas se solapa con [desde, hasta].</summary>
    public Task<List<FilaRegistro>> PorFecha(DateOnly desde, DateOnly hasta, CancellationToken ct = default) =>
        Registros($"SELECT {Columnas} FROM {_s}.registro r WHERE r.fecha_desde <= @h AND r.fecha_hasta >= @d ORDER BY r.fecha_desde, r.id",
            p => { p.AddWithValue("d", desde); p.AddWithValue("h", hasta); }, ct);

    /// <summary>Registros de un día con hora dentro de la franja. Los que no tienen hora concreta no aparecen.</summary>
    public Task<List<FilaRegistro>> PorHora(DateOnly dia, TimeOnly desde, TimeOnly hasta, CancellationToken ct = default) =>
        Registros($"SELECT {Columnas} FROM {_s}.registro r WHERE r.fecha_desde <= @dia AND r.fecha_hasta >= @dia AND r.hora_valor BETWEEN @d AND @h ORDER BY r.hora_valor, r.id",
            p => { p.AddWithValue("dia", dia); p.AddWithValue("d", desde); p.AddWithValue("h", hasta); }, ct);

    public Task<List<FilaRegistro>> PorClase(string clase, CancellationToken ct = default) =>
        Registros($"SELECT {Columnas} FROM {_s}.registro r WHERE r.clase = @c ORDER BY r.fecha_desde, r.id", p => p.AddWithValue("c", clase), ct);

    /// <summary>Registros con algún dato de esa certeza (opcionalmente de un campo concreto).</summary>
    public Task<List<FilaRegistro>> PorCerteza(string certeza, string? campo = null, CancellationToken ct = default) =>
        Registros($"SELECT {Columnas} FROM {_s}.registro r WHERE r.id IN (SELECT registro_id FROM {_s}.registro_certeza WHERE certeza = @z AND (@c::text IS NULL OR campo = @c)) " +
                  $"OR r.id IN (SELECT registro_id FROM {_s}.registro_entidad WHERE certeza = @z AND (@c::text IS NULL OR @c = 'entidades')) ORDER BY r.fecha_desde, r.id",
            p => { p.AddWithValue("z", certeza); p.AddWithValue("c", (object?)campo ?? DBNull.Value); }, ct);

    /// <summary>Registros relacionados con uno dado, en cualquier sentido, y los que están en conflicto con él.</summary>
    public Task<List<FilaRegistro>> PorRelacion(string registroId, CancellationToken ct = default) =>
        Registros($"SELECT {Columnas} FROM {_s}.registro r WHERE r.id IN (" +
                  $"SELECT destino_id FROM {_s}.relacion WHERE origen_id = @id UNION SELECT origen_id FROM {_s}.relacion WHERE destino_id = @id " +
                  $"UNION SELECT con_registro_id FROM {_s}.conflicto WHERE registro_id = @id) ORDER BY r.fecha_desde, r.id",
            p => p.AddWithValue("id", registroId), ct);

    public Task<List<FilaRegistro>> PorProyecto(string proyecto, CancellationToken ct = default) =>
        Registros($"SELECT {Columnas} FROM {_s}.registro r WHERE r.id IN (SELECT registro_id FROM {_s}.registro_dato WHERE campo = 'proyectos' AND valor_normalizado = @p) ORDER BY r.fecha_desde, r.id",
            p => p.AddWithValue("p", Normalizacion.SinAcentos(proyecto)), ct);

    /// <summary>Registros que mencionan una entidad, escrita con o sin acentos ni mayúsculas.</summary>
    public Task<List<FilaRegistro>> PorEntidad(string entidad, CancellationToken ct = default) =>
        Registros($"SELECT {Columnas} FROM {_s}.registro r WHERE r.id IN (SELECT registro_id FROM {_s}.registro_entidad WHERE nombre_normalizado = @n) ORDER BY r.fecha_desde, r.id",
            p => p.AddWithValue("n", Normalizacion.SinAcentos(entidad)), ct);

    /// <summary>Preguntas pendientes, de más a menos valor.</summary>
    public async Task<List<FilaPregunta>> PreguntasPendientes(CancellationToken ct = default)
    {
        await using var con = new NpgsqlConnection(conexion);
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            $"SELECT id, registro_id, campo, texto, valor, estado FROM {_s}.pregunta WHERE estado = 'pendiente' " +
            "ORDER BY CASE valor WHEN 'alta' THEN 0 WHEN 'media' THEN 1 ELSE 2 END, creada_el, id", con);
        var lista = new List<FilaPregunta>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            lista.Add(new FilaPregunta(r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.GetString(4), r.GetString(5)));
        }

        return lista;
    }

    /// <summary>Capturas sin ninguna fila en un lote.</summary>
    public async Task<List<string>> CapturasSinProcesar(CancellationToken ct = default)
    {
        await using var con = new NpgsqlConnection(conexion);
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand($"SELECT id FROM {_s}.captura c WHERE NOT EXISTS (SELECT 1 FROM {_s}.lote_captura l WHERE l.captura_id = c.id) ORDER BY comunicada_el, id", con);
        var lista = new List<string>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            lista.Add(r.GetString(0));
        }

        return lista;
    }

    public async Task<List<FilaConflicto>> ConflictosAbiertos(CancellationToken ct = default)
    {
        await using var con = new NpgsqlConnection(conexion);
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand($"SELECT registro_id, con_registro_id, campo, pregunta_id, estado FROM {_s}.conflicto WHERE estado = 'abierto' ORDER BY registro_id, con_registro_id", con);
        var lista = new List<FilaConflicto>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            lista.Add(new FilaConflicto(r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4)));
        }

        return lista;
    }
}
