using Orumi.Agente;
using Orumi.Agente.Etiquetado;
using Orumi.Agente.Repaso;
using Orumi.Core.Aprendizaje;
using Orumi.Core.Correcciones;

namespace Orumi.Tests;

/// <summary>Repaso de los registros existentes (spec <c>repaso-de-registros</c>).</summary>
public class RepasoTests
{
    private static async Task<EntornoMira> Con(params (string Texto, string Clase, string Titulo)[] notas)
    {
        var e = new EntornoMira();
        foreach (var (texto, clase, titulo) in notas)
        {
            e.Capturar(texto, e.Ahora);
            e.Ahora = e.Ahora.AddMinutes(1);
            e.Respuesta = _ => Datos.Resp([Datos.Registro(texto, clase, titulo)]);
            await e.Procesar();
        }

        return e;
    }

    private static RepasoDeRegistros Repaso(EntornoMira e, ConfiguracionDeRepaso? c = null, IProveedorDeTexto? p = null, Configuracion? conf = null) =>
        new(e.Datos, () => e.Ahora, c ?? new ConfiguracionDeRepaso(), p, conf, _ => "clave-de-prueba");

    private sealed class ProveedorQueEnvia(Func<string> respuesta) : IProveedorDeTexto
    {
        public int Llamadas { get; private set; }

        public string Nombre => "anthropic";

        public string Modelo => "m";

        public bool EnviaDatosAFuera => true;

        public Task<RespuestaDeTexto> CompletarAsync(string sistema, string mensaje, CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult(new RespuestaDeTexto(respuesta(), null));
        }
    }

    [Fact]
    public async Task La_capa_determinista_pregunta_los_campos_estructurales_desconocidos_sin_modelo_y_no_toca_los_registros()
    {
        using var e = await Con(("Hoy he revisado el servidor.", "actividad", "Revisión del servidor"));
        var antes = e.Registros().ToDictionary(r => r.Id!, r => File.ReadAllText(Path.Combine(e.Datos, r.Ruta)));
        var p = new ProveedorDeTextoSimulado((_, _) => "{\"preguntas\":[]}");

        var r = await Repaso(e).EjecutarAsync(soloDeterminista: true);

        Assert.False(r.UsoModelo);
        Assert.Equal(5, r.Creadas); // hora, duración, personas, lugares y resultados de una actividad
        Assert.Empty(p.Peticiones);
        Assert.Equal(antes, e.Registros().ToDictionary(x => x.Id!, x => File.ReadAllText(Path.Combine(e.Datos, x.Ruta))));
        Assert.All(e.Preguntas(), q =>
        {
            Assert.Equal("pendiente", q.Frontmatter["estado"]);
            Assert.Equal(AgentesDePregunta.Mira, AgentesDePregunta.De(q.Frontmatter));
            Assert.Contains("Revisión del servidor", (string)q.Frontmatter["texto"]!);
        });
        Assert.Empty(e.Validar());
        Assert.True(File.Exists(Path.Combine(e.Datos, "analisis", "repaso", "2026", "11", r.Constancia + ".yaml")));
    }

    [Fact]
    public async Task Un_registro_sin_huecos_no_genera_ninguna_pregunta()
    {
        using var e = new EntornoMira();
        e.Capturar("Todo dicho.", e.Ahora);
        e.Respuesta = _ => Datos.Resp([Datos.Registro("Todo dicho.", "evento", "Comida con Pau", campos: new()
        {
            ["hora"] = new Dictionary<string, object?> { ["expresion"] = "a las 14", ["valor"] = "14:00" },
            ["duracion"] = new Dictionary<string, object?> { ["expresion"] = "una hora", ["minutos"] = "60" },
            ["personas"] = new List<object?> { "Pau" },
            ["lugares"] = new List<object?> { "el centro" },
        }, certeza: new() { ["hora"] = "dicho", ["duracion"] = "dicho", ["personas"] = "dicho", ["lugares"] = "dicho" })]);
        await e.Procesar();

        var r = await Repaso(e).EjecutarAsync(soloDeterminista: true);

        Assert.Equal(0, r.Creadas);
        Assert.Empty(e.Preguntas());
    }

    [Fact]
    public async Task El_repaso_no_repite_lo_pendiente_ni_lo_que_el_usuario_cerro()
    {
        using var e = await Con(("Hoy he revisado el servidor.", "actividad", "Revisión del servidor"));
        await Repaso(e).EjecutarAsync(soloDeterminista: true);
        var hora = e.Preguntas().Single(q => (string)q.Frontmatter["campo"]! == "hora");
        new Operaciones(e.Datos, () => e.Ahora).CerrarSinDato((string)hora.Frontmatter["id"]!, "no_relevante");

        e.Ahora = e.Ahora.AddDays(1);
        var segundo = await Repaso(e).EjecutarAsync(soloDeterminista: true);

        Assert.Equal(0, segundo.Creadas);
        Assert.Equal(5, e.Preguntas().Count);
    }

    [Fact]
    public async Task El_tope_de_creacion_reparte_las_preguntas_entre_noches_sin_perder_ninguna()
    {
        using var e = await Con(("Hoy he revisado el servidor.", "actividad", "Revisión del servidor"));
        var cfg = new ConfiguracionDeRepaso(MaximoNuevas: 3);

        var primera = await Repaso(e, cfg).EjecutarAsync(soloDeterminista: true);
        var deLaPrimera = e.Preguntas().Select(q => (string)q.Frontmatter["campo"]!).ToList();
        e.Ahora = e.Ahora.AddDays(1);
        var segunda = await Repaso(e, cfg).EjecutarAsync(soloDeterminista: true);
        e.Ahora = e.Ahora.AddDays(1);
        var tercera = await Repaso(e, cfg).EjecutarAsync(soloDeterminista: true);

        Assert.Equal((3, 2, 0), (primera.Creadas, segunda.Creadas, tercera.Creadas));
        Assert.Equal(5, e.Preguntas().Select(q => (string)q.Frontmatter["campo"]!).Distinct().Count());
        Assert.Contains("resultados", deLaPrimera); // la de mayor valor sale en la primera noche
    }

    [Fact]
    public async Task Con_el_repaso_desactivado_o_sin_tope_de_creacion_no_se_crea_nada()
    {
        using var e = await Con(("Hoy he revisado el servidor.", "actividad", "Revisión del servidor"));

        var r = await Repaso(e, new ConfiguracionDeRepaso(MaximoNuevas: 0)).EjecutarAsync(soloDeterminista: true);

        Assert.Equal(0, r.Creadas);
        Assert.Empty(e.Preguntas());
    }

    [Fact]
    public async Task Un_registro_eliminado_no_se_repasa()
    {
        using var e = await Con(("Hoy he revisado el servidor.", "actividad", "Revisión del servidor"));
        new Operaciones(e.Datos, () => e.Ahora).Eliminar(e.Registros().Single().Id!);

        var r = await Repaso(e).EjecutarAsync(soloDeterminista: true);

        Assert.Equal(0, r.Creadas);
    }

    [Fact]
    public async Task La_capa_con_modelo_acepta_lo_valido_descarta_y_cuenta_lo_invalido_y_no_vuelve_a_enviar_lo_revisado()
    {
        using var e = await Con(("Hoy hemos llegado a acuerdos con Nuvia.", "evento", "Reunión con Nuvia"));
        var id = e.Registros().Single().Id!;
        var respuesta = "{\"preguntas\":[" +
            $"{{\"registro\":\"{id}\",\"campo\":\"contexto\",\"texto\":\"¿Qué os llevó a esa reunión con Nuvia?\",\"valor\":\"media\"}}," +
            $"{{\"registro\":\"{id}\",\"campo\":\"titulo\",\"texto\":\"¿Cómo se llama?\",\"valor\":\"baja\"}}," +
            $"{{\"registro\":\"{id}\",\"campo\":\"acciones\",\"texto\":\"Deberías anotar qué queda pendiente.\",\"valor\":\"alta\"}}," +
            "{\"registro\":\"reg-20990101-zzzz\",\"campo\":\"ideas\",\"texto\":\"¿Alguna idea?\",\"valor\":\"baja\"}]}";
        var p = new ProveedorDeTextoSimulado((_, _) => respuesta);

        var r = await Repaso(e, p: p).EjecutarAsync();
        var segunda = await Repaso(e, p: p).EjecutarAsync();

        Assert.True(r.UsoModelo);
        Assert.Contains(e.Preguntas(), q => (string)q.Frontmatter["texto"]! == "¿Qué os llevó a esa reunión con Nuvia?" && (string)q.Frontmatter["campo"]! == "contexto");
        Assert.Equal(3, r.Descartes.Values.Sum());
        Assert.Equal(1, r.Descartes[RepasoDeRegistros.CampoInvalido]);
        Assert.Equal(1, r.Descartes[RepasoDeRegistros.TonoInvalido]);
        Assert.Equal(1, r.Descartes[RepasoDeRegistros.RegistroInvalido]);
        Assert.Single(p.Peticiones); // el segundo repaso no vuelve a enviar el registro ya revisado
        Assert.False(segunda.UsoModelo);
        Assert.Empty(e.Validar());
        var enviado = p.Peticiones.Single().Mensaje;
        Assert.Contains("Hoy hemos llegado a acuerdos con Nuvia.", enviado);
        Assert.DoesNotContain("clima", enviado, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Una_salida_del_modelo_que_no_es_json_no_escribe_preguntas_del_modelo_y_se_avisa()
    {
        using var e = await Con(("Hoy hemos llegado a acuerdos con Nuvia.", "evento", "Reunión con Nuvia"));
        var p = new ProveedorDeTextoSimulado((_, _) => "esto no es json");

        var r = await Repaso(e, p: p).EjecutarAsync();

        Assert.Contains(r.Avisos, a => a.Contains("no es válida"));
        Assert.DoesNotContain(e.Preguntas(), q => (string)q.Frontmatter["campo"]! is "contexto" or "acciones" or "problemas" or "ideas");
    }

    [Fact]
    public async Task Sin_la_garantia_de_privacidad_no_se_envia_nada_y_solo_actua_la_capa_determinista()
    {
        using var e = await Con(("Hoy hemos llegado a acuerdos con Nuvia.", "evento", "Reunión con Nuvia"));
        var p = new ProveedorQueEnvia(() => "{\"preguntas\":[]}");
        var conf = Configuracion.Leer("proveedor:\n  nombre: anthropic\n  modelo: m\n");

        var r = await Repaso(e, p: p, conf: conf).EjecutarAsync();

        Assert.Equal(0, p.Llamadas);
        Assert.NotEmpty(r.Detenida);
        Assert.False(r.UsoModelo);
        Assert.True(r.Creadas > 0); // las deterministas sí
    }

    [Fact]
    public async Task Con_el_presupuesto_agotado_se_omite_la_capa_con_modelo_y_se_anota()
    {
        using var e = await Con(("Hoy hemos llegado a acuerdos con Nuvia.", "evento", "Reunión con Nuvia"));
        Directory.CreateDirectory(Path.Combine(e.Datos, "lotes", "2026", "11"));
        File.WriteAllText(Path.Combine(e.Datos, "lotes", "2026", "11", "lote-20261116-0300.md"),
            "---\nid: lote-20261116-0300\ntipo: lote\nejecutado_el: 2026-11-16T03:00:00+01:00\nuso: {tokens_entrada: 1, tokens_salida: 1, coste_estimado_usd: \"5.000000\"}\n---\n");
        var p = new ProveedorQueEnvia(() => "{\"preguntas\":[]}");
        var conf = Configuracion.Leer("proveedor:\n  nombre: anthropic\n  modelo: m\n  garantia_no_reutilizacion:\n    declarada: true\n    verificada_el: 2026-11-10\n    fuente: x\npresupuesto:\n  saldo_usd: 1\n  desde: 2026-11-01\n");

        var r = await Repaso(e, p: p, conf: conf).EjecutarAsync();

        Assert.Equal(0, p.Llamadas);
        Assert.Contains(r.Avisos, a => a.Contains("presupuesto"));
    }

    [Fact]
    public async Task Las_clases_que_el_usuario_cierra_como_no_relevantes_pasan_por_detras_pero_no_se_prohiben()
    {
        using var e = await Con(("Hoy he revisado el servidor.", "actividad", "Revisión del servidor"));
        MotorDeAprendizaje.Guardar(e.Datos, new ReglaAprendida(MotorDeAprendizaje.IdDe("resultados:actividad"), "resultados:actividad", ReglaAprendida.Activa,
            "Preguntar menos por resultados", ["preg-20261101-aaaa"], e.Ahora, e.Ahora, false, null, []));

        var primera = await Repaso(e, new ConfiguracionDeRepaso(MaximoNuevas: 1)).EjecutarAsync(soloDeterminista: true);

        Assert.Equal(1, primera.Creadas);
        Assert.NotEqual("resultados", (string)e.Preguntas().Single().Frontmatter["campo"]!); // la rebajada espera
        e.Ahora = e.Ahora.AddDays(1);
        await Repaso(e, new ConfiguracionDeRepaso(MaximoNuevas: 10)).EjecutarAsync(soloDeterminista: true);
        Assert.Contains(e.Preguntas(), q => (string)q.Frontmatter["campo"]! == "resultados"); // pero no se prohíbe
    }

    [Fact]
    public void La_configuracion_del_repaso_y_del_maximo_se_leen_con_sus_valores_por_defecto()
    {
        var sin = Configuracion.Leer("proveedor:\n  nombre: anthropic\n");
        var con = Configuracion.Leer("preguntas:\n  maximo_pendientes: 7\nrepaso:\n  maximo_nuevas: 4\n  activado: false\n  registros_por_llamada: 9\n");

        Assert.Null(sin.MaximoPendientes);
        Assert.Equal(new ConfiguracionDeRepaso(true, 20, 15), sin.Repaso);
        Assert.Equal(7, con.MaximoPendientes);
        Assert.Equal(new ConfiguracionDeRepaso(false, 4, 9), con.Repaso);
    }

    [Fact]
    public async Task La_linea_de_comandos_repasa_con_la_capa_determinista_y_lo_cuenta()
    {
        using var e = await Con(("Hoy he revisado el servidor.", "actividad", "Revisión del servidor"));
        var salida = new StringWriter();
        var comandos = new Orumi.Cli.Comandos(() => new MemoryStream(), salida, new StringWriter(), () => e.Ahora, _ => null);

        var error = new StringWriter();
        comandos = new Orumi.Cli.Comandos(() => new MemoryStream(), salida, error, () => e.Ahora, _ => null);
        var codigo = comandos.Ejecutar(["repasar", "--datos", e.Datos, "--solo-determinista", "si"]);

        Assert.True(codigo == 0, error.ToString());
        Assert.Contains("5 creada(s)", salida.ToString());
    }

    [Fact]
    public async Task El_panel_lanza_el_repaso_en_segundo_plano_responde_409_si_Mira_esta_ocupada_y_muestra_el_ultimo()
    {
        using var e = await Con(("Hoy he revisado el servidor.", "actividad", "Revisión del servidor"));
        var repo = new Orumi.Panel.RepositorioDeDatos(e.Datos, () => DateTimeOffset.UtcNow, TimeSpan.Zero);
        var panel = new Orumi.Panel.RepasoDelPanel(repo, () => e.Ahora, _ => { }, null, () => null, _ => null);

        using (var ocupada = Orumi.Agente.BloqueoDeEjecucion.Adquirir(e.Datos))
        {
            Assert.Equal(409, panel.Lanzar(null).Estado);
        }

        Assert.Equal(202, panel.Lanzar("yo@ejemplo.com").Estado);
        for (var i = 0; i < 200 && System.Text.Json.JsonSerializer.Serialize(panel.Ver()).Contains("\"enCurso\":true"); i++)
        {
            await Task.Delay(50);
        }

        Assert.Equal(5, e.Preguntas().Count);
        var json = System.Text.Json.JsonSerializer.Serialize(panel.Ver());
        Assert.Contains("\"creadas\":\"5\"", json);
    }
}
