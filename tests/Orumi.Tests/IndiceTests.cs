using Npgsql;
using Orumi.Cli;
using Orumi.Core.Ficheros;
using Orumi.Indice;

namespace Orumi.Tests;

/// <summary>Pruebas que necesitan PostgreSQL: se saltan salvo que exista <c>ORUMI_TEST_DATABASE_URL</c> (se ejecutan en el servidor).</summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public const string Variable = "ORUMI_TEST_DATABASE_URL";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
        {
            Skip = $"Sin {Variable}: estas pruebas se ejecutan contra PostgreSQL en el servidor.";
        }
    }
}

public class IndiceTests : IDisposable
{
    private readonly string _esquema = "test_" + Guid.NewGuid().ToString("N")[..8];
    private readonly string _copia = Ayudantes.CopiarEjemplos();
    private string Conexion => Orumi.Indice.Conexion.DesdeUrl(Environment.GetEnvironmentVariable(PostgresFactAttribute.Variable) ?? "postgres://x:y@h/db");

    public void Dispose()
    {
        Directory.Delete(_copia, true);
        if (Environment.GetEnvironmentVariable(PostgresFactAttribute.Variable) is not null)
        {
            using var con = new NpgsqlConnection(Conexion);
            con.Open();
            using var cmd = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {_esquema}, {_esquema}_nuevo, {_esquema}_anterior CASCADE", con);
            cmd.ExecuteNonQuery();
        }
    }

    private Task<InformeIndexado> Reindexar(bool forzar = false, Action<string>? hito = null, string? datos = null) =>
        Indexador.ReindexarAsync(datos ?? _copia, Conexion, _esquema, forzar, hito);

    private Consultas Consultas() => new(Conexion, _esquema);

    private async Task<string> Huella()
    {
        await using var con = new NpgsqlConnection(Conexion);
        await con.OpenAsync();
        var partes = new List<string>();
        foreach (var t in new[] { "captura", "registro", "registro_dato", "registro_entidad", "registro_certeza", "fuente", "entrada", "relacion", "conflicto", "rechazo", "historial", "pregunta", "lote", "lote_captura" })
        {
            await using var cmd = new NpgsqlCommand($"SELECT coalesce(md5(string_agg(x::text, '|' ORDER BY x::text)), '-') FROM {_esquema}.{t} x", con);
            partes.Add(t + "=" + await cmd.ExecuteScalarAsync());
        }

        return string.Join(";", partes);
    }

    private static List<string> Ids(IEnumerable<FilaRegistro> filas) => filas.Select(f => f.Id).Order().ToList();

    // ---------------------------------------------------------------- no necesitan base de datos

    [Fact]
    public void Los_nombres_de_esquema_solo_admiten_identificadores_simples()
    {
        Assert.Equal("indice", Esquema.Validar("indice"));
        Assert.Throws<ArgumentException>(() => Esquema.Validar("indice; DROP SCHEMA public"));
        Assert.Throws<ArgumentException>(() => Esquema.Validar("Indice"));
    }

    [Fact]
    public void La_url_de_postgres_se_convierte_en_cadena_de_conexion()
    {
        var c = Orumi.Indice.Conexion.DesdeUrl("postgres://orumi_app:abc123@postgres:5432/orumi");
        Assert.Contains("Host=postgres", c);
        Assert.Contains("Username=orumi_app", c);
        Assert.Contains("Database=orumi", c);
        Assert.Contains("SSL Mode=Disable", c);
        var b = new NpgsqlConnectionStringBuilder(c); // la cadena debe ser válida para Npgsql, también sin GSS (evita cargar libgssapi en la imagen)
        Assert.Equal(Npgsql.GssEncryptionMode.Disable, b.GssEncryptionMode);
        Assert.Throws<ArgumentException>(() => Orumi.Indice.Conexion.DesdeUrl("no es una url"));
    }

    // ---------------------------------------------------------------- registros eliminados y fusionados

    [PostgresFact]
    public async Task Los_registros_eliminados_y_fusionados_no_aparecen_en_las_consultas()
    {
        await Reindexar();
        var antes = (await Consultas().PorClase("actividad")).Count;
        var ejemplo = (await Consultas().PorClase("actividad")).First();

        new Orumi.Core.Correcciones.Operaciones(_copia, () => DateTimeOffset.Now).Eliminar(ejemplo.Id);
        await Reindexar(forzar: true);

        var despues = await Consultas().PorClase("actividad");
        Assert.Equal(antes - 1, despues.Count);
        Assert.DoesNotContain(despues, f => f.Id == ejemplo.Id);
        Assert.DoesNotContain(await Consultas().PorFecha(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), f => f.Id == ejemplo.Id);
    }

    // ---------------------------------------------------------------- 3.2 regenerar

    [PostgresFact]
    public async Task Los_ejemplos_regeneran_el_indice_completo()
    {
        var r = await Reindexar();

        Assert.True(r.Sustituido);
        Assert.Equal(5, r.Filas["captura"]);
        Assert.Equal(7, r.Filas["registro"]);
        Assert.Equal(4, r.Filas["pregunta"]);
        Assert.Equal(5, r.Filas["lote"]);
        Assert.Empty(r.Omitidos);
        Assert.Empty(r.Graves);

        var c = Consultas();
        var conflictos = await c.ConflictosAbiertos();
        Assert.Equal(2, conflictos.Count); // una fila por cada lado: un único conflicto entre dos registros
        Assert.Single(conflictos.Select(f => f.PreguntaId).Distinct());
        Assert.Single(await c.PreguntasPendientes());
        Assert.Empty(await c.CapturasSinProcesar());
    }

    [PostgresFact]
    public async Task Regenerar_dos_veces_da_el_mismo_indice()
    {
        await Reindexar();
        var primero = await Huella();
        await Reindexar();
        Assert.Equal(primero, await Huella());
    }

    [PostgresFact]
    public async Task Un_indice_borrado_se_regenera_sin_perdida()
    {
        await Reindexar();
        var antes = await Huella();
        await using (var con = new NpgsqlConnection(Conexion))
        {
            await con.OpenAsync();
            await using var cmd = new NpgsqlCommand($"DROP SCHEMA {_esquema} CASCADE", con);
            await cmd.ExecuteNonQueryAsync();
        }

        var r = await Reindexar();

        Assert.True(r.Sustituido);
        Assert.Equal(antes, await Huella());
    }

    [PostgresFact]
    public async Task Un_fichero_ilegible_se_omite_se_lista_y_el_resto_se_indexa()
    {
        File.WriteAllText(Path.Combine(_copia, "capturas", "2026", "11", "cap-20261199-0000-zzz.md"), "esto no es un fichero de ORUMI");

        var r = await Reindexar();

        Assert.True(r.Sustituido);
        Assert.Single(r.Omitidos);
        Assert.Contains("cap-20261199-0000-zzz.md", r.Omitidos[0].Fichero);
        Assert.Equal(7, r.Filas["registro"]);
        Assert.Equal(5, r.Filas["captura"]);
    }

    [PostgresFact]
    public async Task Una_captura_alterada_impide_sustituir_el_indice_salvo_que_se_fuerce()
    {
        Ayudantes.Reemplazar(Ayudantes.Buscar(_copia, "cap-20261110-2140"), "estantería del salón", "estantería del comedor");

        var r = await Reindexar();
        Assert.False(r.Sustituido);
        Assert.Single(r.Graves);

        await using (var con = new NpgsqlConnection(Conexion))
        {
            await con.OpenAsync();
            await using var existe = new NpgsqlCommand("SELECT count(*) FROM information_schema.schemata WHERE schema_name = @s", con);
            existe.Parameters.AddWithValue("s", _esquema);
            Assert.Equal(0L, await existe.ExecuteScalarAsync()); // no se ha creado ni sustituido nada
        }

        var forzado = await Reindexar(forzar: true);
        Assert.True(forzado.Sustituido);
    }

    [PostgresFact]
    public async Task Si_la_construccion_falla_el_indice_vigente_se_conserva()
    {
        await Reindexar();
        var antes = await Huella();
        File.Delete(Ayudantes.Buscar(_copia, "reg-20261111-d6y2")); // los ficheros cambian: el índice nuevo sería distinto

        await Assert.ThrowsAsync<InvalidOperationException>(() => Reindexar(hito: h =>
        {
            if (h == "construido")
            {
                throw new InvalidOperationException("fallo simulado");
            }
        }));

        Assert.Equal(antes, await Huella()); // el vigente sigue igual
        await using var con = new NpgsqlConnection(Conexion);
        await con.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM information_schema.schemata WHERE schema_name IN (@a, @b)", con);
        cmd.Parameters.AddWithValue("a", _esquema + "_nuevo");
        cmd.Parameters.AddWithValue("b", _esquema + "_anterior");
        Assert.Equal(0L, await cmd.ExecuteScalarAsync()); // sin restos
    }

    [PostgresFact]
    public async Task Reindexar_no_modifica_ningun_fichero()
    {
        string Instantanea() => string.Join("|", Directory.GetFiles(_copia, "*", SearchOption.AllDirectories).Order().Select(f => f + File.ReadAllText(f)));
        var antes = Instantanea();
        await Reindexar();
        Assert.Equal(antes, Instantanea());
    }

    // ---------------------------------------------------------------- 3.3 consultas

    [PostgresFact]
    public async Task Consulta_por_entidad_con_o_sin_acentos_y_mayusculas()
    {
        await Reindexar();
        var c = Consultas();
        var esperado = new[] { "reg-20261113-e4n7", "reg-20261113-w1h5" };

        Assert.Equal(esperado, Ids(await c.PorEntidad("Reformas Duero")));
        Assert.Equal(esperado, Ids(await c.PorEntidad("reformas duero")));
        Assert.Equal(esperado, Ids(await c.PorEntidad("REFORMAS DUERO")));
        Assert.Equal(["reg-20261110-q2x1"], Ids(await c.PorEntidad("duplicacy")));
        Assert.Empty(await c.PorEntidad("no existe"));
    }

    [PostgresFact]
    public async Task Consulta_por_certeza_devuelve_solo_registros_con_datos_de_esa_certeza()
    {
        await Reindexar();
        var c = Consultas();

        // `fecha` es interpretada en cinco registros; en los dos de la estantería la dio el usuario (dicho).
        Assert.Equal(
            ["reg-20261110-q2x1", "reg-20261110-r7m4", "reg-20261110-t5k8", "reg-20261113-e4n7", "reg-20261113-w1h5"],
            Ids(await c.PorCerteza("interpretado", "fecha")));
        Assert.Equal(["reg-20261111-a3f9", "reg-20261111-d6y2"], Ids(await c.PorCerteza("dicho", "fecha")));
        Assert.Contains("reg-20261110-q2x1", Ids(await c.PorCerteza("confirmado")));
        Assert.Contains("reg-20261110-q2x1", Ids(await c.PorCerteza("confirmado", "entidades"))); // certeza por elemento
    }

    [PostgresFact]
    public async Task Consulta_por_fecha_por_hora_por_clase_y_por_proyecto()
    {
        await Reindexar();
        var c = Consultas();

        var semana = Ids(await c.PorFecha(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8)));
        Assert.Contains("reg-20261113-e4n7", semana); // el rango «la semana pasada» se solapa
        Assert.Contains("reg-20261111-a3f9", semana); // domingo 8
        Assert.DoesNotContain("reg-20261110-q2x1", semana); // 9 de noviembre

        Assert.Equal(["reg-20261110-q2x1"], Ids(await c.PorHora(new DateOnly(2026, 11, 9), new TimeOnly(7, 0), new TimeOnly(8, 0))));
        Assert.Empty(await c.PorHora(new DateOnly(2026, 11, 12), new TimeOnly(0, 0), new TimeOnly(23, 59))); // «por la tarde»: sin hora concreta, no aparece

        Assert.Equal(7, (await c.PorClase("actividad")).Count + (await c.PorClase("evento")).Count + (await c.PorClase("idea")).Count
                        + (await c.PorClase("decision")).Count + (await c.PorClase("problema")).Count);
        Assert.Equal(["reg-20261110-q2x1"], Ids(await c.PorProyecto("Mantenimiento-Servidor")));
        Assert.Equal(["reg-20261113-e4n7", "reg-20261113-w1h5"], Ids(await c.PorProyecto("reforma")));
    }

    [PostgresFact]
    public async Task Consulta_por_relacion_y_preguntas_pendientes_ordenadas()
    {
        await Reindexar();
        var c = Consultas();

        Assert.Equal(["reg-20261110-r7m4"], Ids(await c.PorRelacion("reg-20261110-q2x1")));
        Assert.Equal(["reg-20261113-w1h5"], Ids(await c.PorRelacion("reg-20261113-e4n7"))); // el conflicto cuenta como relación

        var pendientes = await c.PreguntasPendientes();
        Assert.Equal("alta", Assert.Single(pendientes).Valor);
    }

    [PostgresFact]
    public async Task Las_capturas_sin_lote_aparecen_como_sin_procesar()
    {
        File.WriteAllText(Path.Combine(_copia, "capturas", "2026", "11", "cap-20261114-1000-nue.md"),
            "---\nid: cap-20261114-1000-nue\ntipo: captura\ncomunicada_el: 2026-11-14T10:00:00+01:00\norigen: texto\nhuella: sha256:" +
            "dummy\ncontexto_automatico:\n  dispositivo: terminal\n---\nnota nueva");

        await Reindexar(forzar: true); // la huella de este fichero no es correcta a propósito: no es lo que se prueba
        Assert.Equal(["cap-20261114-1000-nue"], await Consultas().CapturasSinProcesar());
    }

    // ---------------------------------------------------------------- CLI y procesamiento sin base de datos

    private int Ejecutar(Func<string, string?> entorno, out string salida, out string error, params string[] args)
    {
        var s = new StringWriter();
        var e = new StringWriter();
        var codigo = new Comandos(() => new MemoryStream(), s, e, () => new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.FromHours(1)), entorno).Ejecutar(args);
        salida = s.ToString();
        error = e.ToString();
        return codigo;
    }

    [Fact]
    public void Capturar_y_validar_funcionan_con_la_base_de_datos_caida_y_reindexar_falla_limpio()
    {
        var bogus = new Func<string, string?>(n => n == Orumi.Indice.Conexion.Variable ? "postgres://u:p@host-que-no-existe.invalid:5432/orumi" : null);
        var datos = Path.Combine(Path.GetTempPath(), "orumi-idx-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Assert.Equal(0, Ejecutar(bogus, out _, out _, "capturar", "--datos", datos, "--texto", "Hoy he llamado a Marta."));
            Assert.Equal(1, Ejecutar(bogus, out var v, out _, "validar", "--datos", datos)); // solo «captura sin lote»
            Assert.Contains("captura sin lote", v);

            var antes = Directory.GetFiles(datos, "*", SearchOption.AllDirectories).Length;
            var codigo = Ejecutar(bogus, out _, out var error, "reindexar", "--datos", datos);

            Assert.Equal(6, codigo);
            Assert.DoesNotContain("host-que-no-existe", error); // sin detalles de conexión
            Assert.DoesNotContain("Password", error);
            Assert.Equal(antes, Directory.GetFiles(datos, "*", SearchOption.AllDirectories).Length);
        }
        finally
        {
            Directory.Delete(datos, true);
        }
    }

    [Fact]
    public void Reindexar_sin_la_variable_de_la_base_pide_indicarla()
    {
        Assert.Equal(2, Ejecutar(_ => null, out _, out var error, "reindexar", "--datos", Path.GetTempPath()));
        Assert.Contains("ORUMI_DATABASE_URL", error);
    }
}
