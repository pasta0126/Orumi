using Orumi.Agente;
using Orumi.Cli;
using Orumi.Core.Correcciones;
using Orumi.Core.Ficheros;

namespace Orumi.Tests;

public class CorreccionesTests
{
    private static Dictionary<string, object?> Mapa(object? o) => (Dictionary<string, object?>)o!;

    private static List<object?> Lista(object? o) => (List<object?>)o!;

    private static Operaciones Ops(EntornoMira e) => new(e.Datos, () => e.Ahora.AddHours(1));

    /// <summary>Dos capturas, dos registros y una pregunta de duración sobre el primero.</summary>
    private static async Task<(string R1, string R2, string P)> Preparar(EntornoMira e)
    {
        e.Capturar("Hoy llamé a Marta por el presupuesto.", e.Ahora);
        e.Capturar("Hoy comí con Pau.", e.Ahora.AddMinutes(1));
        e.Respuesta = s => s.Texto.Contains("Marta")
            ? Datos.Resp(
                [Datos.Registro(s.Texto, "actividad", "Llamada con Marta", campos: new() { ["personas"] = new List<object?> { "Marta" } }, certeza: new() { ["personas"] = "dicho" })],
                new PreguntaCandidata("duracion", "¿Cuánto duró la llamada con Marta?", "media", RegistroIndice: 0))
            : Datos.Resp([Datos.Registro(s.Texto, "actividad", "Comida con Pau")]);
        await e.Procesar();
        var r1 = e.Registros().Single(r => (string)r.Frontmatter["titulo"]! == "Llamada con Marta").Id!;
        var r2 = e.Registros().Single(r => (string)r.Frontmatter["titulo"]! == "Comida con Pau").Id!;
        return (r1, r2, e.Preguntas().Single().Id!);
    }

    private static Dictionary<string, string> Instantanea(EntornoMira e) =>
        Directory.EnumerateFiles(e.Datos, "*.md", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllText);

    // ---------------------------------------------------------------- responder

    [Fact]
    public async Task Responder_una_duracion_confirma_el_dato_deja_historial_y_evidencia_y_no_toca_las_capturas()
    {
        using var e = new EntornoMira();
        var (r1, _, p) = await Preparar(e);
        var capturas = Instantanea(e).Where(x => x.Key.Contains("capturas")).ToDictionary(x => x.Key, x => x.Value);

        Ops(e).Responder(p, "1h30");

        var fm = e.Doc(r1).Frontmatter;
        Assert.Equal("90", Mapa(fm["duracion"])["minutos"]);
        Assert.Equal("1h30", Mapa(fm["duracion"])["expresion"]);
        Assert.Equal("confirmado", Mapa(fm["certeza"])["duracion"]);
        var h = Mapa(Lista(fm["historial"]).Last());
        Assert.Equal("usuario", h["origen"]);
        Assert.Equal("duracion", h["campo"]);
        Assert.Equal(p, h["pregunta"]);
        var pf = e.Doc(p).Frontmatter;
        Assert.Equal("respondida", pf["estado"]);
        Assert.Equal("1h30", Mapa(pf["respuesta"])["texto"]);
        Assert.True(pf.ContainsKey("cerrada_el"));
        Assert.Empty(e.Validar());
        Assert.Equal(capturas, Instantanea(e).Where(x => x.Key.Contains("capturas")).ToDictionary(x => x.Key, x => x.Value));
    }

    [Theory]
    [InlineData("90", "90")]
    [InlineData("90m", "90")]
    [InlineData("2h", "120")]
    [InlineData("1h30", "90")]
    [InlineData("1h 15 min", "75")]
    public void Las_duraciones_se_interpretan_en_minutos(string texto, string minutos) =>
        Assert.Equal(minutos, Mapa(ValoresDeCampo.Analizar("duracion", texto, null))["minutos"]);

    [Theory]
    [InlineData("duracion", "mucho")]
    [InlineData("duracion", "0")]
    [InlineData("hora", "25:00")]
    [InlineData("hora", "tarde")]
    [InlineData("fecha", "ayer")]
    [InlineData("fecha", "2026-13-40")]
    [InlineData("fecha", "2026-11-10..2026-11-01")]
    [InlineData("entidades", "Acme:planeta")]
    [InlineData("personas", " , ")]
    public void Un_valor_no_valido_se_rechaza_con_el_formato_esperado(string campo, string valor)
    {
        var ex = Assert.Throws<OperacionInvalidaException>(() => ValoresDeCampo.Analizar(campo, valor, null));
        Assert.Contains("Se espera", ex.Message);
    }

    [Fact]
    public async Task Un_valor_no_valido_no_modifica_ningun_fichero()
    {
        using var e = new EntornoMira();
        var (_, _, p) = await Preparar(e);
        var antes = Instantanea(e);

        Assert.Throws<OperacionInvalidaException>(() => Ops(e).Responder(p, "mucho rato"));

        Assert.Equal(antes, Instantanea(e));
    }

    [Fact]
    public async Task Responder_una_fecha_conserva_la_expresion_original()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        e.Escribir(r1, fm => ((Dictionary<string, object?>)fm["fecha"]!)["expresion"] = "ayer");

        Ops(e).Corregir(r1, "fecha", "2026-11-10");

        var fecha = Mapa(e.Doc(r1).Frontmatter["fecha"]);
        Assert.Equal("ayer", fecha["expresion"]);
        Assert.Equal("2026-11-10", fecha["desde"]);
        Assert.Equal("2026-11-10", fecha["hasta"]);
        Assert.Equal("confirmado", Mapa(e.Doc(r1).Frontmatter["certeza"])["fecha"]);
        Assert.Empty(e.Validar());
    }

    [Fact]
    public async Task No_lo_recuerdo_y_no_relevante_cierran_la_pregunta_sin_tocar_el_registro()
    {
        using var e = new EntornoMira();
        var (r1, _, p) = await Preparar(e);
        var registro = File.ReadAllText(Path.Combine(e.Datos, e.Doc(r1).Ruta));

        Ops(e).CerrarSinDato(p, "no_recuerda");

        Assert.Equal("no_recuerda", e.Doc(p).Frontmatter["estado"]);
        Assert.Equal(registro, File.ReadAllText(Path.Combine(e.Datos, e.Doc(r1).Ruta)));
        Assert.Empty(e.Validar());
        Assert.Contains("ya está cerrada", Assert.Throws<OperacionInvalidaException>(() => Ops(e).CerrarSinDato(p, "no_relevante")).Message);
    }

    [Fact]
    public async Task Una_pregunta_ya_cerrada_o_inexistente_se_rechaza_sin_modificar_nada()
    {
        using var e = new EntornoMira();
        var (_, _, p) = await Preparar(e);
        Ops(e).Responder(p, "1h");
        var antes = Instantanea(e);

        Assert.Contains("ya está cerrada", Assert.Throws<OperacionInvalidaException>(() => Ops(e).Responder(p, "2h")).Message);
        Assert.Contains("No existe", Assert.Throws<OperacionInvalidaException>(() => Ops(e).Responder("preg-20990101-zzzz", "2h")).Message);
        Assert.Equal(antes, Instantanea(e));
    }

    // ---------------------------------------------------------------- corregir

    [Fact]
    public async Task Corregir_personas_y_titulo_deja_el_valor_anterior_en_el_historial()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);

        Ops(e).Corregir(r1, "personas", "Marta, Pau");
        Ops(e).Corregir(r1, "titulo", "Llamada con Marta y Pau");

        var fm = e.Doc(r1).Frontmatter;
        Assert.Equal(["Marta", "Pau"], Lista(fm["personas"]).Cast<string>());
        Assert.Equal("Llamada con Marta y Pau", fm["titulo"]);
        Assert.Equal("confirmado", Mapa(fm["certeza"])["personas"]);
        var historial = Lista(fm["historial"]).Select(Mapa).Where(h => (string)h["origen"]! == "usuario").ToList();
        Assert.Equal(["Marta"], Lista(historial[0]["anterior"]).Cast<string>());
        Assert.Equal("Llamada con Marta", historial[1]["anterior"]);
        Assert.Empty(e.Validar());
    }

    [Fact]
    public async Task Corregir_entidades_las_deja_confirmadas_una_a_una()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);

        Ops(e).Corregir(r1, "entidades", "Nuvia:organizacion, Outlook:sistema");

        var entidades = Lista(e.Doc(r1).Frontmatter["entidades"]).Select(Mapa).ToList();
        Assert.Equal(2, entidades.Count);
        Assert.All(entidades, x => Assert.Equal("confirmado", x["certeza"]));
        Assert.Empty(e.Validar());
    }

    [Theory]
    [InlineData("id")]
    [InlineData("fuentes")]
    [InlineData("procedencia")]
    [InlineData("clave_extraccion")]
    [InlineData("clase")]
    public async Task Los_campos_que_no_admiten_correccion_se_rechazan_sin_modificar_nada(string campo)
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        var antes = Instantanea(e);

        Assert.Contains("no se puede corregir", Assert.Throws<OperacionInvalidaException>(() => Ops(e).Corregir(r1, campo, "x")).Message);
        Assert.Equal(antes, Instantanea(e));
    }

    [Fact]
    public async Task Un_reprocesado_de_mira_respeta_lo_corregido_por_el_usuario()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        Ops(e).Corregir(r1, "personas", "Marta, Pau");
        var captura = e.Cargar().DeTipo("captura").Single(c => c.Cuerpo.Contains("Marta")).Id!;

        e.Respuesta = s => Datos.Resp([Datos.Registro(s.Texto, "actividad", "Llamada con Marta", campos: new() { ["personas"] = new List<object?> { "Marta" } }, certeza: new() { ["personas"] = "dicho" })]);
        e.Ahora = e.Ahora.AddDays(1);
        await e.Procesar(reprocesar: [captura]);

        var fm = e.Doc(r1).Frontmatter;
        Assert.Equal(["Marta", "Pau"], Lista(fm["personas"]).Cast<string>());
        Assert.Equal("confirmado", Mapa(fm["certeza"])["personas"]);
        Assert.Empty(e.Validar());
    }

    // ---------------------------------------------------------------- rechazar

    [Fact]
    public async Task Rechazar_una_interpretacion_la_guarda_en_rechazos_y_deja_el_campo_desconocido()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        e.Escribir(r1, fm => Mapa(fm["certeza"])["personas"] = "interpretado");

        Ops(e).Rechazar(r1, "personas");

        var fm = e.Doc(r1).Frontmatter;
        Assert.Equal("desconocido", fm["personas"]);
        Assert.False(Mapa(fm["certeza"]).ContainsKey("personas"));
        var rechazo = Mapa(Lista(fm["rechazos"]).Single());
        Assert.Equal("personas", rechazo["campo"]);
        Assert.Equal("Marta", rechazo["valor"]);
        Assert.Equal("interpretado", rechazo["certeza_original"]);
        Assert.Equal("usuario", Mapa(Lista(fm["historial"]).Last())["origen"]);
        Assert.Empty(e.Validar());
    }

    [Fact]
    public async Task Rechazar_un_dato_que_no_es_una_interpretacion_no_hace_nada()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e); // personas es «dicho»
        var antes = Instantanea(e);

        Assert.Contains("no es una interpretación", Assert.Throws<OperacionInvalidaException>(() => Ops(e).Rechazar(r1, "personas")).Message);
        Assert.Equal(antes, Instantanea(e));
    }

    // ---------------------------------------------------------------- conflictos y coincidencias

    private static void MarcarConflicto(EntornoMira e, string r1, string r2, string p)
    {
        e.Escribir(p, fm =>
        {
            fm["campo"] = "fecha";
            fm["otros_registros"] = new List<object?> { r2 };
        });
        e.Escribir(r1, fm => fm["conflictos"] = new List<object?> { new Dictionary<string, object?> { ["campo"] = "fecha", ["con"] = r2, ["pregunta"] = p, ["estado"] = "abierto" } });
        e.Escribir(r2, fm => fm["conflictos"] = new List<object?> { new Dictionary<string, object?> { ["campo"] = "fecha", ["con"] = r1, ["pregunta"] = p, ["estado"] = "abierto" } });
        // el conflicto exige el wikilink al otro registro en el cuerpo
        File.AppendAllText(Path.Combine(e.Datos, e.Doc(r1).Ruta), $"\n## Relacionados\n- [[{r2}]]\n");
        File.AppendAllText(Path.Combine(e.Datos, e.Doc(r2).Ruta), $"\n## Relacionados\n- [[{r1}]]\n");
    }

    [Fact]
    public async Task Responder_un_conflicto_aplica_el_valor_al_registro_indicado_y_lo_da_por_resuelto_en_ambos()
    {
        using var e = new EntornoMira();
        var (r1, r2, p) = await Preparar(e);
        MarcarConflicto(e, r1, r2, p);
        Assert.Empty(e.Validar());

        var r = Ops(e).Responder(p, "2026-11-12", registroDestino: r2);

        Assert.Equal("2026-11-12", Mapa(e.Doc(r2).Frontmatter["fecha"])["desde"]);
        Assert.Equal("2026-11-16", Mapa(e.Doc(r1).Frontmatter["fecha"])["desde"]); // la otra versión no cambia
        Assert.All([r1, r2], id => Assert.Equal("resuelto", Mapa(Lista(e.Doc(id).Frontmatter["conflictos"]).Single())["estado"]));
        Assert.Equal("respondida", e.Doc(p).Frontmatter["estado"]);
        Assert.Contains("la otra versión no se ha cambiado", r.Mensaje);
        Assert.Empty(e.Validar());
    }

    [Fact]
    public async Task Responder_un_conflicto_con_un_registro_ajeno_se_rechaza()
    {
        using var e = new EntornoMira();
        var (r1, r2, p) = await Preparar(e);
        MarcarConflicto(e, r1, r2, p);
        var antes = Instantanea(e);

        Assert.Throws<OperacionInvalidaException>(() => Ops(e).Responder(p, "2026-11-12", registroDestino: "reg-20990101-zzzz"));
        Assert.Equal(antes, Instantanea(e));
    }

    [Fact]
    public async Task Una_coincidencia_distinta_cierra_la_pregunta_y_deja_los_registros_intactos()
    {
        using var e = new EntornoMira();
        var (r1, r2, p) = await Preparar(e);
        e.Escribir(p, fm =>
        {
            fm["campo"] = "coincidencia";
            fm["otros_registros"] = new List<object?> { r2 };
        });
        var registros = Instantanea(e).Where(x => x.Key.Contains("registros")).ToDictionary(x => x.Key, x => x.Value);

        Ops(e).Responder(p, "distinta");

        Assert.Equal("respondida", e.Doc(p).Frontmatter["estado"]);
        Assert.Equal(registros, Instantanea(e).Where(x => x.Key.Contains("registros")).ToDictionary(x => x.Key, x => x.Value));
        _ = r1;
    }

    [Fact]
    public async Task Una_coincidencia_misma_relaciona_los_registros_sin_fusionarlos()
    {
        using var e = new EntornoMira();
        var (r1, r2, p) = await Preparar(e);
        e.Escribir(p, fm =>
        {
            fm["campo"] = "coincidencia";
            fm["otros_registros"] = new List<object?> { r2 };
        });

        Ops(e).Responder(p, "misma");

        var relacion = Mapa(Lista(e.Doc(r1).Frontmatter["relaciones"]).Single());
        Assert.Equal(("relacionado_con", r2, "confirmado"), ((string)relacion["tipo"]!, (string)relacion["destino"]!, (string)relacion["certeza"]!));
        Assert.Equal(2, e.Registros().Count);
        Assert.Empty(e.Validar());
        Assert.Throws<OperacionInvalidaException>(() => Ops(e).Responder(p, "quizá")); // ya cerrada; y «quizá» tampoco valdría
    }

    // ---------------------------------------------------------------- línea de comandos

    private static (int Codigo, string Salida, string Error) Cli(EntornoMira e, params string[] args)
    {
        var s = new StringWriter();
        var er = new StringWriter();
        var codigo = new Comandos(() => new MemoryStream(), s, er, () => e.Ahora, _ => null).Ejecutar([.. args, "--datos", e.Datos]);
        return (codigo, s.ToString(), er.ToString());
    }

    [Fact]
    public async Task Los_comandos_responden_corrigen_y_rechazan()
    {
        using var e = new EntornoMira();
        var (r1, _, p) = await Preparar(e);

        var (c1, s1, _) = Cli(e, "responder", p, "1h30");
        Assert.Equal(0, c1);
        Assert.Contains("respondida", s1);
        Assert.Equal(0, Cli(e, "corregir", r1, "personas", "Marta,", "Pau").Codigo); // el valor puede venir en varias palabras
        Assert.Equal(["Marta", "Pau"], Lista(e.Doc(r1).Frontmatter["personas"]).Cast<string>());
        Assert.Equal(2, Cli(e, "rechazar", r1, "personas").Codigo); // es «confirmado»
        Assert.Empty(e.Validar());
    }

    [Fact]
    public async Task Con_no_recuerdo_el_comando_cierra_la_pregunta()
    {
        using var e = new EntornoMira();
        var (_, _, p) = await Preparar(e);

        Assert.Equal(0, Cli(e, "responder", p, "--no-recuerdo").Codigo);
        Assert.Equal("no_recuerda", e.Doc(p).Frontmatter["estado"]);
    }

    [Fact]
    public async Task Un_error_de_valor_termina_con_codigo_2_y_sin_modificar_nada()
    {
        using var e = new EntornoMira();
        var (_, _, p) = await Preparar(e);
        var antes = Instantanea(e);

        var (codigo, _, error) = Cli(e, "responder", p, "mucho");

        Assert.Equal(2, codigo);
        Assert.Contains("No se ha modificado nada", error);
        Assert.Equal(antes, Instantanea(e));
    }

    [Fact]
    public async Task Mientras_mira_procesa_la_operacion_se_rechaza_con_el_codigo_3()
    {
        using var e = new EntornoMira();
        var (_, _, p) = await Preparar(e);
        var antes = Instantanea(e);

        using (BloqueoDeEjecucion.Adquirir(e.Datos))
        {
            var (codigo, _, error) = Cli(e, "responder", p, "1h");
            Assert.Equal(3, codigo);
            Assert.Contains("Mira está procesando", error);
        }

        Assert.Equal(antes, Instantanea(e));
    }

    [Fact]
    public async Task Los_pendientes_muestran_el_registro_el_campo_el_valor_actual_y_el_otro_registro()
    {
        using var e = new EntornoMira();
        var (r1, r2, p) = await Preparar(e);
        MarcarConflicto(e, r1, r2, p);

        var (codigo, salida, _) = Cli(e, "consultar", "pendientes");

        Assert.Equal(0, codigo);
        Assert.Contains(p, salida);
        Assert.Contains("registro: " + r1, salida);
        Assert.Contains("campo: fecha", salida);
        Assert.Contains("valor actual: 2026-11-16", salida);
        Assert.Contains("con: " + r2, salida);
        Assert.Contains("1 pregunta(s) pendiente(s).", salida);
    }
}
