using System.Text.Json;
using System.Text.RegularExpressions;
using Orumi.Agente;
using Orumi.Panel;

namespace Orumi.Tests;

public class PanelTests
{
    private static string Json(object o) => JsonSerializer.Serialize(o, new JsonSerializerOptions(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    /// <summary>Dos capturas, tres registros (uno con entidad y duración dicha) y una pregunta de duración.</summary>
    private static async Task<(string R1, string R2, string P)> Preparar(EntornoMira e)
    {
        e.Capturar("Hoy llamé a Marta, de Reformas Duero, por el presupuesto.", e.Ahora);
        e.Capturar("Hoy comí con Pau.", e.Ahora.AddMinutes(1));
        e.Respuesta = s => s.Texto.Contains("Marta")
            ? Datos.Resp(
                [Datos.Registro(s.Texto, "actividad", "Llamada con Marta", campos: new()
                    {
                        ["personas"] = new List<object?> { "Marta" },
                        ["hora"] = new Dictionary<string, object?> { ["expresion"] = "a las 9", ["valor"] = "09:00", ["aproximada"] = "false" },
                    },
                    certeza: new() { ["personas"] = "dicho", ["hora"] = "interpretado" })],
                new PreguntaCandidata("duracion", "¿Cuánto duró la llamada con Marta?", "media", RegistroIndice: 0))
            : Datos.Resp([Datos.Registro(s.Texto, "evento", "Comida con Pau", dia: "2026-11-15")]);
        await e.Procesar();
        return (e.Registros().Single(r => (string)r.Frontmatter["titulo"]! == "Llamada con Marta").Id!,
            e.Registros().Single(r => (string)r.Frontmatter["titulo"]! == "Comida con Pau").Id!, e.Preguntas().Single().Id!);
    }

    private static (Lectura L, RepositorioDeDatos R) Leer(EntornoMira e)
    {
        var r = new RepositorioDeDatos(e.Datos, () => DateTimeOffset.UtcNow, TimeSpan.Zero);
        return (new Lectura(r), r);
    }

    // ---------------------------------------------------------------- lectura

    [Fact]
    public async Task Los_dias_cuentan_los_registros_de_cada_fecha()
    {
        using var e = new EntornoMira();
        await Preparar(e);
        var (l, _) = Leer(e);

        var json = Json(l.Dias("2026-11-01", "2026-11-30"));

        Assert.Contains("\"fecha\":\"2026-11-16\",\"total\":1", json);
        Assert.Contains("\"fecha\":\"2026-11-15\",\"total\":1", json);
        Assert.DoesNotContain("2026-11-14", json);
    }

    [Theory]
    [InlineData(null, "2026-11-30")]
    [InlineData("ayer", "2026-11-30")]
    [InlineData("2026-11-30", "2026-11-01")]
    [InlineData("2020-01-01", "2026-11-30")]
    public async Task Un_rango_de_dias_invalido_se_rechaza(string? desde, string? hasta)
    {
        using var e = new EntornoMira();
        var (l, _) = Leer(e);
        Assert.Throws<SolicitudInvalidaException>(() => l.Dias(desde, hasta));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Los_registros_se_filtran_por_fecha_clase_texto_y_se_ordenan_por_hora()
    {
        using var e = new EntornoMira();
        await Preparar(e);
        var (l, _) = Leer(e);

        Assert.Equal(2, l.Registros(new FiltroDeRegistros()).Total);
        Assert.Equal("Comida con Pau", l.Registros(new FiltroDeRegistros(Desde: "2026-11-15", Hasta: "2026-11-15")).Registros.Single().Titulo);
        Assert.Equal("Comida con Pau", l.Registros(new FiltroDeRegistros(Clase: "evento")).Registros.Single().Titulo);
        Assert.Equal("Llamada con Marta", l.Registros(new FiltroDeRegistros(Q: "MARTA")).Registros.Single().Titulo);
        Assert.Equal("Llamada con Marta", l.Registros(new FiltroDeRegistros(Q: "llamada")).Registros.Single().Titulo);
        Assert.Empty(l.Registros(new FiltroDeRegistros(Q: "inexistente")).Registros);
        var asc = l.Registros(new FiltroDeRegistros(Ascendente: true)).Registros;
        Assert.Equal("Comida con Pau", asc[0].Titulo); // 15 antes que 16
    }

    [Fact]
    public async Task Se_filtra_por_certeza_entidad_y_proyecto_combinando_filtros()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        new Orumi.Core.Correcciones.Operaciones(e.Datos, () => e.Ahora).Corregir(r1, "entidades", "Reformas Duero:organizacion");
        new Orumi.Core.Correcciones.Operaciones(e.Datos, () => e.Ahora).Corregir(r1, "proyectos", "Reforma");
        var (l, _) = Leer(e);

        Assert.Equal("Llamada con Marta", l.Registros(new FiltroDeRegistros(Entidad: "reformas  duero".Replace("  ", " "))).Registros.Single().Titulo);
        Assert.Equal("Llamada con Marta", l.Registros(new FiltroDeRegistros(Proyecto: "reforma")).Registros.Single().Titulo);
        Assert.Equal("Llamada con Marta", l.Registros(new FiltroDeRegistros(Certeza: "confirmado")).Registros.Single().Titulo);
        Assert.Equal("Llamada con Marta", l.Registros(new FiltroDeRegistros(Certeza: "interpretado", Campo: "hora")).Registros.Single().Titulo);
        Assert.Empty(l.Registros(new FiltroDeRegistros(Entidad: "reformas duero", Desde: "2026-11-15", Hasta: "2026-11-15")).Registros);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Una_paginacion_fuera_de_limites_se_rechaza(int pagina, int limite)
    {
        using var e = new EntornoMira();
        var (l, _) = Leer(e);
        Assert.Throws<SolicitudInvalidaException>(() => l.Registros(new FiltroDeRegistros(Pagina: pagina, Limite: limite)));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Una_busqueda_demasiado_larga_o_una_certeza_desconocida_se_rechazan()
    {
        using var e = new EntornoMira();
        var (l, _) = Leer(e);
        Assert.Throws<SolicitudInvalidaException>(() => l.Registros(new FiltroDeRegistros(Q: new string('a', Lectura.LongitudMaximaDeBusqueda + 1))));
        Assert.Throws<SolicitudInvalidaException>(() => l.Registros(new FiltroDeRegistros(Certeza: "seguro")));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task La_paginacion_reparte_los_resultados()
    {
        using var e = new EntornoMira();
        await Preparar(e);
        var (l, _) = Leer(e);
        var p1 = l.Registros(new FiltroDeRegistros(Pagina: 1, Limite: 1));
        var p2 = l.Registros(new FiltroDeRegistros(Pagina: 2, Limite: 1));
        Assert.Equal(2, p1.Total);
        Assert.NotEqual(p1.Registros.Single().Id, p2.Registros.Single().Id);
    }

    [Fact]
    public async Task La_ficha_muestra_certezas_cita_historial_y_datos_desconocidos()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        var (l, _) = Leer(e);

        var json = Json(l.Ficha(r1));

        Assert.Contains("\"titulo\":\"Llamada con Marta\"", json);
        Assert.Contains("\"campo\":\"personas\"", json);
        Assert.Contains("\"certeza\":\"dicho\"", json);
        Assert.Contains("Hoy llamé a Marta", json); // la captura de origen, en solo lectura
        Assert.Contains("\"cita\":", json);
        Assert.Contains("\"historial\":", json);
    }

    [Fact]
    public async Task Un_campo_desconocido_se_muestra_como_desconocido_nunca_vacio()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        e.Escribir(r1, fm =>
        {
            fm["duracion"] = "desconocido";
        });
        var (l, _) = Leer(e);

        var json = Json(l.Ficha(r1));

        Assert.Contains("\"campo\":\"duracion\",\"valor\":\"desconocido\",\"texto\":\"desconocido\",\"desconocido\":true", json);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("reg-1")]
    [InlineData("")]
    [InlineData("cap-20261113-0900-abc")]
    public async Task Un_identificador_con_formato_invalido_se_rechaza_antes_de_tocar_ficheros(string id)
    {
        using var e = new EntornoMira();
        var (l, _) = Leer(e);
        Assert.Throws<SolicitudInvalidaException>(() => l.Ficha(id));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Un_registro_o_captura_inexistente_da_no_encontrado()
    {
        using var e = new EntornoMira();
        var (l, _) = Leer(e);
        Assert.Throws<NoEncontradoException>(() => l.Ficha("reg-20990101-zzzz"));
        Assert.Throws<NoEncontradoException>(() => l.Captura("cap-20990101-0000-zzz"));
        Assert.Throws<SolicitudInvalidaException>(() => l.Captura("x"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task La_captura_se_sirve_en_solo_lectura_con_su_texto_literal()
    {
        using var e = new EntornoMira();
        await Preparar(e);
        var id = e.Cargar().DeTipo("captura").First().Id!;
        var (l, _) = Leer(e);
        Assert.Contains("Hoy ", Json(l.Captura(id)));
    }

    [Fact]
    public async Task Las_preguntas_pendientes_traen_su_contexto()
    {
        using var e = new EntornoMira();
        var (r1, _, p) = await Preparar(e);
        var (l, _) = Leer(e);

        var q = l.PreguntasPendientes().Single();

        Assert.Equal(p, q.Id);
        Assert.Equal("duracion", q.Campo);
        Assert.Equal(r1, q.Registro.Id);
        Assert.Equal("desconocido", q.Registro.ValorActual);
    }

    [Fact]
    public async Task El_estado_resume_capturas_registros_y_preguntas()
    {
        using var e = new EntornoMira();
        await Preparar(e);
        var (l, _) = Leer(e);
        var json = Json(l.Estado());
        Assert.Contains("\"capturas\":2", json);
        Assert.Contains("\"registros\":2", json);
        Assert.Contains("\"preguntasPendientes\":1", json);
        Assert.Contains("\"pendientesDeProcesar\":0", json);
    }

    // ---------------------------------------------------------------- datos siempre al día

    [Fact]
    public async Task Un_cambio_externo_se_ve_en_la_siguiente_consulta_sin_reiniciar()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        var (l, _) = Leer(e);
        Assert.Equal("Llamada con Marta", l.Registros(new FiltroDeRegistros(Q: "marta")).Registros.Single().Titulo);

        new Orumi.Core.Correcciones.Operaciones(e.Datos, () => e.Ahora).Corregir(r1, "titulo", "Llamada con Marta y Pau"); // como si fuera la línea de comandos

        Assert.Equal("Llamada con Marta y Pau", l.Registros(new FiltroDeRegistros(Q: "marta")).Registros.Single().Titulo);
    }

    [Fact]
    public async Task Dentro_de_la_vigencia_no_se_vuelve_a_leer_el_disco()
    {
        using var e = new EntornoMira();
        await Preparar(e);
        var ahora = DateTimeOffset.UtcNow;
        var repo = new RepositorioDeDatos(e.Datos, () => ahora, TimeSpan.FromSeconds(30));
        var primero = repo.Actual();
        e.Capturar("Otra nota nueva.", e.Ahora.AddHours(3));

        Assert.Same(primero, repo.Actual()); // misma instancia: no se recarga dentro de la vigencia
        ahora = ahora.AddSeconds(31);
        Assert.NotSame(primero, repo.Actual());
    }

    // ---------------------------------------------------------------- registros eliminados y fusionados

    [Fact]
    public async Task Los_eliminados_no_salen_en_dias_busquedas_ni_recuentos_pero_si_en_la_papelera()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        new Orumi.Core.Correcciones.Operaciones(e.Datos, () => e.Ahora).Eliminar(r1);
        var (l, _) = Leer(e);

        Assert.DoesNotContain(l.Registros(new FiltroDeRegistros()).Registros, x => x.Id == r1);
        Assert.Equal(1, l.Registros(new FiltroDeRegistros()).Total);
        Assert.DoesNotContain("2026-11-16", Json(l.Dias("2026-11-01", "2026-11-30")));
        var estado = Json(l.Estado());
        Assert.Contains("\"registros\":1", estado);
        Assert.Contains("\"enPapelera\":1", estado);
        Assert.Contains(r1, Json(l.Papelera()));
        Assert.Empty(l.PreguntasPendientes()); // sus preguntas se cerraron

        var ficha = Json(l.Ficha(r1));
        Assert.Contains("\"estado\":\"eliminado\"", ficha);
        Assert.Contains("\"eliminadoEl\":", ficha);
    }

    [Fact]
    public async Task Un_registro_fusionado_indica_donde_esta_y_no_sale_en_las_vistas()
    {
        using var e = new EntornoMira();
        var (r1, r2, _) = await Preparar(e);
        new Orumi.Core.Correcciones.Operaciones(e.Datos, () => e.Ahora).Fusionar(r1, r2);
        var (l, _) = Leer(e);

        var ficha = Json(l.Ficha(r2));
        Assert.Contains("\"estado\":\"fusionado\"", ficha);
        Assert.Contains($"\"fusionadoEn\":{{\"id\":\"{r1}\"", ficha);
        Assert.DoesNotContain(l.Registros(new FiltroDeRegistros()).Registros, x => x.Id == r2);
        Assert.DoesNotContain(r2, Json(l.Papelera())); // fusionado no es papelera
    }

    [Fact]
    public async Task Eliminar_y_restaurar_desde_el_panel_pasan_por_el_bloqueo_y_la_auditoria()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        var auditoria = new List<string>();
        var w = Escritor(e, auditoria);

        Assert.Equal(200, w.Ejecutar("eliminar", r1, "yo@ejemplo.com", op => op.Eliminar(r1)).Estado);
        Assert.Equal(400, w.Ejecutar("eliminar", r1, null, op => op.Eliminar(r1)).Estado); // ya eliminado
        Assert.Equal(200, w.Ejecutar("restaurar", r1, null, op => op.Restaurar(r1)).Estado);
        Assert.Equal(3, auditoria.Count);
        Assert.Contains("operacion=eliminar", auditoria[0]);
        Assert.DoesNotContain("yo@ejemplo.com", auditoria[0]);
    }

    // ---------------------------------------------------------------- escritura

    private static EscrituraDelPanel Escritor(EntornoMira e, List<string> auditoria) =>
        new(new RepositorioDeDatos(e.Datos, () => DateTimeOffset.UtcNow, TimeSpan.Zero), () => e.Ahora, auditoria.Add);

    [Fact]
    public async Task Responder_desde_el_panel_confirma_el_dato_y_deja_auditoria_sin_valores()
    {
        using var e = new EntornoMira();
        var (r1, _, p) = await Preparar(e);
        var auditoria = new List<string>();

        var r = Escritor(e, auditoria).Ejecutar("responder", p, "yo@ejemplo.com", op => op.Responder(p, "1h30"));

        Assert.Equal(200, r.Estado);
        Assert.Equal("90", ((Dictionary<string, object?>)e.Doc(r1).Frontmatter["duracion"]!)["minutos"]);
        var linea = Assert.Single(auditoria);
        Assert.Contains("operacion=responder", linea);
        Assert.Contains($"id={p}", linea);
        Assert.Contains("resultado=ok", linea);
        Assert.DoesNotContain("1h30", linea);
        Assert.DoesNotContain("yo@ejemplo.com", linea);
    }

    [Fact]
    public async Task Un_valor_invalido_da_400_un_id_inexistente_404_y_no_se_modifica_nada()
    {
        using var e = new EntornoMira();
        var (_, _, p) = await Preparar(e);
        var antes = Directory.EnumerateFiles(e.Datos, "*.md", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllText);
        var w = Escritor(e, []);

        Assert.Equal(400, w.Ejecutar("responder", p, null, op => op.Responder(p, "mucho rato")).Estado);
        Assert.Equal(404, w.Ejecutar("responder", "preg-20990101-zzzz", null, op => op.Responder("preg-20990101-zzzz", "1h")).Estado);
        Assert.Equal(antes, Directory.EnumerateFiles(e.Datos, "*.md", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllText));
    }

    [Fact]
    public async Task Mientras_mira_procesa_la_escritura_da_409_y_no_modifica_nada()
    {
        using var e = new EntornoMira();
        var (_, _, p) = await Preparar(e);
        var auditoria = new List<string>();
        var antes = e.Leer(p);

        using (BloqueoDeEjecucion.Adquirir(e.Datos))
        {
            var r = Escritor(e, auditoria).Ejecutar("responder", p, null, op => op.Responder(p, "1h"));
            Assert.Equal(409, r.Estado);
            Assert.Contains("Mira está procesando", r.Error);
        }

        Assert.Equal(antes, e.Leer(p));
        Assert.Contains("resultado=rechazada-409", Assert.Single(auditoria));
    }

    [Fact]
    public async Task Corregir_y_rechazar_desde_el_panel_dejan_el_historial_y_las_capturas_intactas()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        var capturas = Directory.EnumerateFiles(Path.Combine(e.Datos, "capturas"), "*.md", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllText);
        var w = Escritor(e, []);

        Assert.Equal(200, w.Ejecutar("corregir", r1, null, op => op.Corregir(r1, "personas", "Marta, Pau")).Estado);
        Assert.Equal(200, w.Ejecutar("rechazar", r1, null, op => op.Rechazar(r1, "hora")).Estado); // la hora era «interpretado»
        Assert.Equal(400, w.Ejecutar("rechazar", r1, null, op => op.Rechazar(r1, "personas")).Estado); // ahora es «confirmado»

        Assert.Equal("desconocido", e.Doc(r1).Frontmatter["hora"]);
        Assert.Equal(capturas, Directory.EnumerateFiles(Path.Combine(e.Datos, "capturas"), "*.md", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllText));
        Assert.Empty(e.Validar().Where(i => !i.Mensaje.Contains("sin lote")));
    }

    // ---------------------------------------------------------------- superficie de la API

    private static string CodigoDelPanel()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Orumi.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return File.ReadAllText(Path.Combine(dir!, "src", "Orumi.Panel", "Program.cs"));
    }

    [Fact]
    public void La_api_no_ofrece_borrar_ni_sustituir_y_no_hay_operaciones_sobre_capturas()
    {
        var codigo = CodigoDelPanel();
        Assert.DoesNotMatch(@"app\.Map(Put|Delete|Patch)\(", codigo);
        var rutas = Regex.Matches(codigo, @"app\.Map(Get|Post)\(""(/api[^""]*)""").Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToList();
        Assert.NotEmpty(rutas);
        Assert.DoesNotContain(rutas, r => r.Item1 == "Post" && r.Item2.Contains("capturas"));
        // toda escritura es una de las operaciones conocidas (ninguna sobre capturas)
        Assert.Equal(
            ["/api/preguntas/{id}/cerrar", "/api/preguntas/{id}/responder", "/api/registros/{id}/corregir", "/api/registros/{id}/deshacer", "/api/registros/{id}/eliminar",
                "/api/registros/{id}/fusionar", "/api/registros/{id}/rechazar", "/api/registros/{id}/restaurar", "/api/registros/{id}/separar"],
            rutas.Where(r => r.Item1 == "Post").Select(r => r.Item2).Order().ToArray());
    }
}
