using Orumi.Agente;
using Orumi.Core.Correcciones;
using Orumi.Core.Ficheros;

namespace Orumi.Tests;

public class GestionDeRegistrosTests
{
    private static Dictionary<string, object?> Mapa(object? o) => (Dictionary<string, object?>)o!;

    private static List<object?> Lista(object? o) => (List<object?>)o!;

    private static Operaciones Ops(EntornoMira e) => new(e.Datos, () => e.Ahora.AddHours(1));

    private static IEnumerable<string> Errores(EntornoMira e) => e.Validar().Where(i => !i.Mensaje.Contains("sin lote")).Select(i => i.ToString());

    /// <summary>Dos registros (Marta con hora y personas, Pau) y una pregunta de duración sobre el primero.</summary>
    private static async Task<(string R1, string R2, string P)> Preparar(EntornoMira e)
    {
        e.Capturar("Hoy llamé a Marta por el presupuesto.", e.Ahora);
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
            : Datos.Resp([Datos.Registro(s.Texto, "evento", "Comida con Pau", campos: new() { ["personas"] = new List<object?> { "Pau" }, ["lugares"] = new List<object?> { "Bar" } }, certeza: new() { ["personas"] = "dicho", ["lugares"] = "interpretado" })]);
        await e.Procesar();
        return (e.Registros().Single(r => (string)r.Frontmatter["titulo"]! == "Llamada con Marta").Id!, e.Registros().Single(r => (string)r.Frontmatter["titulo"]! == "Comida con Pau").Id!, e.Preguntas().Single().Id!);
    }

    private static Dictionary<string, string> Instantanea(EntornoMira e) =>
        Directory.EnumerateFiles(e.Datos, "*.md", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllText);

    // ---------------------------------------------------------------- deshacer

    [Fact]
    public async Task Deshacer_una_correccion_devuelve_el_valor_y_la_certeza_anteriores_y_lo_anota()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        var ops = Ops(e);
        ops.Corregir(r1, "personas", "Marta, Pau");
        Assert.Equal("confirmado", Mapa(e.Doc(r1).Frontmatter["certeza"])["personas"]);

        ops.Deshacer(r1, "personas");

        var fm = e.Doc(r1).Frontmatter;
        Assert.Equal(["Marta"], Lista(fm["personas"]).Cast<string>());
        Assert.Equal("dicho", Mapa(fm["certeza"])["personas"]); // la certeza que tenía antes de corregir
        var ultima = Mapa(Lista(fm["historial"]).Last());
        Assert.Equal("usuario", ultima["origen"]);
        Assert.True(ultima.ContainsKey("deshace"));
        Assert.Empty(Errores(e));
    }

    [Fact]
    public async Task Sin_cambios_del_usuario_no_hay_nada_que_deshacer_y_no_se_modifica_nada()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        var antes = Instantanea(e);

        Assert.Contains("que deshacer", Assert.Throws<OperacionInvalidaException>(() => Ops(e).Deshacer(r1)).Message);
        Assert.Contains("no admite", Assert.Throws<OperacionInvalidaException>(() => Ops(e).Deshacer(r1, "clase")).Message);
        Assert.Equal(antes, Instantanea(e));
    }

    [Fact]
    public async Task Se_pueden_deshacer_varios_pasos_de_uno_en_uno_y_cada_campo_por_separado()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        var ops = Ops(e);
        ops.Corregir(r1, "personas", "Marta, Pau");
        ops.Corregir(r1, "titulo", "Llamada larga");
        ops.Corregir(r1, "personas", "Pau");

        ops.Deshacer(r1, "titulo");
        Assert.Equal("Llamada con Marta", e.Doc(r1).Frontmatter["titulo"]);
        Assert.False(Mapa(e.Doc(r1).Frontmatter["certeza"]).ContainsKey("titulo")); // el título nunca tuvo certeza

        ops.Deshacer(r1, "personas");
        Assert.Equal(["Marta", "Pau"], Lista(e.Doc(r1).Frontmatter["personas"]).Cast<string>());
        ops.Deshacer(r1, "personas");
        Assert.Equal(["Marta"], Lista(e.Doc(r1).Frontmatter["personas"]).Cast<string>());
        Assert.Throws<OperacionInvalidaException>(() => ops.Deshacer(r1, "personas")); // ya no queda nada
        Assert.Empty(Errores(e));
    }

    [Fact]
    public async Task Deshacer_un_rechazo_devuelve_la_interpretacion_y_la_saca_de_los_rechazos()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        var ops = Ops(e);
        ops.Rechazar(r1, "hora");
        Assert.Equal("desconocido", e.Doc(r1).Frontmatter["hora"]);

        ops.Deshacer(r1);

        var fm = e.Doc(r1).Frontmatter;
        Assert.Equal("09:00", Mapa(fm["hora"])["valor"]);
        Assert.Equal("interpretado", Mapa(fm["certeza"])["hora"]);
        Assert.Empty(Lista(fm["rechazos"]));
        Assert.Empty(Errores(e));
    }

    [Fact]
    public async Task Deshacer_una_correccion_de_entidades_devuelve_las_anteriores()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        var ops = Ops(e);
        ops.Corregir(r1, "entidades", "Nuvia:organizacion");
        ops.Deshacer(r1, "entidades");

        Assert.Equal("desconocido", e.Doc(r1).Frontmatter.GetValueOrDefault("entidades") ?? "desconocido");
        Assert.Empty(Errores(e));
    }

    [Fact]
    public async Task Una_correccion_antigua_sin_certeza_anterior_vuelve_como_interpretado_y_se_avisa()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        Ops(e).Corregir(r1, "personas", "Pau");
        e.Escribir(r1, fm => ((Dictionary<string, object?>)Lista(fm["historial"]).Last()!).Remove("certeza_anterior"));

        var r = Ops(e).Deshacer(r1, "personas");

        Assert.Contains("interpretado", r.Mensaje);
        Assert.Equal("interpretado", Mapa(e.Doc(r1).Frontmatter["certeza"])["personas"]);
        Assert.Empty(Errores(e));
    }

    // ---------------------------------------------------------------- eliminar y restaurar

    [Fact]
    public async Task Eliminar_deja_una_tumba_cierra_sus_preguntas_y_no_toca_las_capturas()
    {
        using var e = new EntornoMira();
        var (r1, _, p) = await Preparar(e);
        var capturas = Instantanea(e).Where(x => x.Key.Contains("capturas")).ToDictionary(x => x.Key, x => x.Value);

        Ops(e).Eliminar(r1);

        var fm = e.Doc(r1).Frontmatter;
        Assert.Equal("eliminado", fm["estado"]);
        Assert.True(fm.ContainsKey("eliminado_el"));
        Assert.Equal("no_relevante", e.Doc(p).Frontmatter["estado"]);
        Assert.Equal(capturas, Instantanea(e).Where(x => x.Key.Contains("capturas")).ToDictionary(x => x.Key, x => x.Value));
        Assert.Equal(2, e.Registros().Count); // el fichero y su clave se conservan
        Assert.Empty(Errores(e));
    }

    [Fact]
    public async Task Restaurar_devuelve_el_registro_a_activo_con_su_contenido()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        Ops(e).Eliminar(r1);

        Ops(e).Restaurar(r1);

        var fm = e.Doc(r1).Frontmatter;
        Assert.False(fm.ContainsKey("estado"));
        Assert.False(fm.ContainsKey("eliminado_el"));
        Assert.Equal("Llamada con Marta", fm["titulo"]);
        Assert.Empty(Errores(e));
    }

    [Fact]
    public async Task Eliminar_o_restaurar_donde_no_procede_se_rechaza_sin_modificar_nada()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        var antes = Instantanea(e);
        Assert.Contains("no está eliminado", Assert.Throws<OperacionInvalidaException>(() => Ops(e).Restaurar(r1)).Message);
        Assert.Contains("No existe", Assert.Throws<OperacionInvalidaException>(() => Ops(e).Eliminar("reg-20990101-zzzz")).Message);
        Assert.Equal(antes, Instantanea(e));

        Ops(e).Eliminar(r1);
        Assert.Contains("eliminado", Assert.Throws<OperacionInvalidaException>(() => Ops(e).Eliminar(r1)).Message);
        Assert.Contains("eliminado", Assert.Throws<OperacionInvalidaException>(() => Ops(e).Corregir(r1, "titulo", "x")).Message);
        Assert.Contains("eliminado", Assert.Throws<OperacionInvalidaException>(() => Ops(e).Rechazar(r1, "hora")).Message);
    }

    private static void MarcarConflicto(EntornoMira e, string r1, string r2, string p)
    {
        e.Escribir(p, fm => { fm["campo"] = "personas"; fm["otros_registros"] = new List<object?> { r2 }; });
        e.Escribir(r1, fm => fm["conflictos"] = new List<object?> { new Dictionary<string, object?> { ["campo"] = "personas", ["con"] = r2, ["pregunta"] = p, ["estado"] = "abierto" } });
        e.Escribir(r2, fm => fm["conflictos"] = new List<object?> { new Dictionary<string, object?> { ["campo"] = "personas", ["con"] = r1, ["pregunta"] = p, ["estado"] = "abierto" } });
        File.AppendAllText(Path.Combine(e.Datos, e.Doc(r1).Ruta), $"\n## Relacionados\n- [[{r2}]]\n");
        File.AppendAllText(Path.Combine(e.Datos, e.Doc(r2).Ruta), $"\n## Relacionados\n- [[{r1}]]\n");
    }

    [Fact]
    public async Task Eliminar_un_registro_con_un_conflicto_lo_resuelve_en_el_otro_y_cierra_la_pregunta()
    {
        using var e = new EntornoMira();
        var (r1, r2, p) = await Preparar(e);
        MarcarConflicto(e, r1, r2, p);
        Assert.Empty(Errores(e));

        Ops(e).Eliminar(r1);

        Assert.Equal("resuelto", Mapa(Lista(e.Doc(r2).Frontmatter["conflictos"]).Single())["estado"]);
        Assert.Equal("resuelto", Mapa(Lista(e.Doc(r1).Frontmatter["conflictos"]).Single())["estado"]);
        Assert.Equal("no_relevante", e.Doc(p).Frontmatter["estado"]);
        Assert.Empty(Errores(e));
    }

    // ---------------------------------------------------------------- fusionar

    [Fact]
    public async Task Fusionar_une_fuentes_entradas_y_datos_y_deja_una_tumba()
    {
        using var e = new EntornoMira();
        var (r1, r2, _) = await Preparar(e);

        Ops(e).Fusionar(r1, r2);

        var a = e.Doc(r1).Frontmatter;
        Assert.Equal(2, Lista(a["fuentes"]).Count);
        Assert.Equal("ampliacion", Mapa(Lista(a["fuentes"])[1])["rol"]);
        Assert.Empty(Lista(a["entradas"])); // los registros de un solo día no llevan entradas
        Assert.Equal(["Bar"], Lista(a["lugares"]).Cast<string>()); // el destino no tenía lugares
        Assert.Equal("interpretado", Mapa(a["certeza"])["lugares"]);
        Assert.Equal(["Marta"], Lista(a["personas"]).Cast<string>()); // en conflicto de valores gana el destino
        Assert.Contains(Lista(a["historial"]).Select(Mapa), h => (string)h["campo"]! == "fusion" && ((string)h["nuevo"]!).Contains("personas") && ((string)h["nuevo"]!).Contains("Pau"));

        var b = e.Doc(r2).Frontmatter;
        Assert.Equal("fusionado", b["estado"]);
        Assert.Equal(r1, b["fusionado_en"]);
        Assert.Contains($"[[{e.Cargar().DeTipo("captura").Last().Id}]]", e.Doc(r1).Cuerpo);
        Assert.Empty(Errores(e));
    }

    [Fact]
    public async Task Fusionar_cierra_las_preguntas_del_origen_y_reapunta_las_tumbas_anteriores()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy llamé a Marta.", e.Ahora);
        e.Capturar("Hoy comí con Pau.", e.Ahora.AddMinutes(1));
        e.Capturar("Hoy cené con Ana.", e.Ahora.AddMinutes(2));
        e.Respuesta = s => Datos.Resp([Datos.Registro(s.Texto, "actividad", s.Texto.TrimEnd('.'))], new PreguntaCandidata("duracion", "¿Cuánto duró?", "media", RegistroIndice: 0));
        await e.Procesar();
        var ids = e.Registros().OrderBy(r => (string)r.Frontmatter["titulo"]!).Select(r => r.Id!).ToList(); // Hoy cené, Hoy comí, Hoy llamé
        var (c, b, a) = (ids[0], ids[1], ids[2]);

        Ops(e).Fusionar(b, a); // a → b
        Ops(e).Fusionar(c, b); // b → c: la tumba de a ahora apunta a c

        Assert.Equal(c, e.Doc(a).Frontmatter["fusionado_en"]);
        Assert.Equal(c, e.Doc(b).Frontmatter["fusionado_en"]);
        Assert.All(e.Preguntas().Where(p => (string)p.Frontmatter["registro"]! != c), p => Assert.NotEqual("pendiente", p.Frontmatter["estado"]));
        Assert.Equal(3, Lista(e.Doc(c).Frontmatter["fuentes"]).Count);
        Assert.Empty(Errores(e));
    }

    [Fact]
    public async Task Fusionar_un_registro_consigo_mismo_o_uno_no_activo_se_rechaza_sin_modificar_nada()
    {
        using var e = new EntornoMira();
        var (r1, r2, _) = await Preparar(e);
        Ops(e).Eliminar(r2);
        var antes = Instantanea(e);

        Assert.Contains("el mismo", Assert.Throws<OperacionInvalidaException>(() => Ops(e).Fusionar(r1, r1)).Message);
        Assert.Contains("eliminado", Assert.Throws<OperacionInvalidaException>(() => Ops(e).Fusionar(r1, r2)).Message);
        Assert.Contains("eliminado", Assert.Throws<OperacionInvalidaException>(() => Ops(e).Fusionar(r2, r1)).Message);
        Assert.Equal(antes, Instantanea(e));
    }

    // ---------------------------------------------------------------- separar

    private static async Task<(string R, string Captura2)> PrepararAmpliacion(EntornoMira e)
    {
        e.Ahora = new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.FromHours(1));
        e.Capturar("Hoy he empezado a pintar el pasillo, aún no he terminado.");
        e.Respuesta = _ => Datos.Resp([Datos.Registro("Hoy he empezado a pintar el pasillo, aún no he terminado.", "actividad", "Pintar el pasillo", dia: "2026-11-13",
            multidia: new MultidiaExtraido("abierto", null, null, "dicho", "interpretado"))]);
        await e.Procesar();
        var r = e.Registros().Single().Id!;
        e.Ahora = new DateTimeOffset(2026, 11, 16, 18, 0, 0, TimeSpan.FromHours(1));
        var c2 = e.Capturar("Hoy he terminado de pintar el pasillo.").Id;
        e.Respuesta = s => Datos.Resp([Datos.Registro("Hoy he terminado de pintar el pasillo.", "actividad", "Pintar el pasillo", dia: null, amplia: s.Relacionados.Single().Id,
            multidia: new MultidiaExtraido("cerrado", "2026-11-16", "2026-11-16", "dicho", "interpretado"))]);
        await e.Procesar();
        return (r, c2);
    }

    [Fact]
    public async Task Separar_una_ampliacion_crea_un_registro_nuevo_y_la_retira_del_original()
    {
        using var e = new EntornoMira();
        var (r, c2) = await PrepararAmpliacion(e);
        Assert.Equal(2, Lista(e.Doc(r).Frontmatter["fuentes"]).Count);

        var res = Ops(e).Separar(r, c2, "Terminar de pintar el pasillo");

        var original = e.Doc(r).Frontmatter;
        Assert.Single(Lista(original["fuentes"]));
        Assert.DoesNotContain(Lista(original["entradas"]).Select(Mapa), x => (string)x["captura"]! == c2);
        Assert.Equal([c2], Lista(original["separados"]).Cast<string>());
        Assert.DoesNotContain($"[[{c2}]]", e.Doc(r).Cuerpo);

        var nuevo = e.Registros().Single(x => x.Id != r);
        Assert.Equal("Terminar de pintar el pasillo", nuevo.Frontmatter["titulo"]);
        Assert.Single(Lista(original["entradas"]));
        Assert.Single(Lista(nuevo.Frontmatter["entradas"]));
        Assert.Equal(c2, Mapa(Lista(nuevo.Frontmatter["fuentes"])[0])["captura"]);
        Assert.Equal("origen", Mapa(Lista(nuevo.Frontmatter["fuentes"])[0])["rol"]);
        Assert.StartsWith(c2 + "#f", (string)nuevo.Frontmatter["clave_extraccion"]!);
        Assert.Contains(nuevo.Id!, res.Mensaje);
        Assert.Empty(Errores(e));
    }

    [Fact]
    public async Task No_se_puede_separar_el_origen_ni_una_captura_que_no_es_fuente()
    {
        using var e = new EntornoMira();
        var (r, _) = await PrepararAmpliacion(e);
        var origen = Mapa(Lista(e.Doc(r).Frontmatter["fuentes"])[0])["captura"]!.ToString()!;
        var antes = Instantanea(e);

        Assert.Contains("origen", Assert.Throws<OperacionInvalidaException>(() => Ops(e).Separar(r, origen)).Message);
        Assert.Contains("no figura", Assert.Throws<OperacionInvalidaException>(() => Ops(e).Separar(r, e.Capturar("Otra nota distinta.", e.Ahora.AddDays(3)).Id)).Message);
        Assert.Contains("No existe", Assert.Throws<OperacionInvalidaException>(() => Ops(e).Separar(r, "cap-20990101-0000-zzz")).Message);
        Assert.Equal(antes.Where(x => !x.Key.Contains("capturas")).ToDictionary(x => x.Key, x => x.Value),
            Instantanea(e).Where(x => !x.Key.Contains("capturas")).ToDictionary(x => x.Key, x => x.Value));
    }

    // ---------------------------------------------------------------- integridad

    [Fact]
    public async Task Si_falla_una_escritura_a_medias_se_devuelve_todo_a_como_estaba()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var e = new EntornoMira();
        var (r1, _, p) = await Preparar(e);
        var antes = Instantanea(e);
        var dirRegistros = Path.GetDirectoryName(Path.Combine(e.Datos, e.Doc(r1).Ruta))!;
        var modo = File.GetUnixFileMode(dirRegistros);
        File.SetUnixFileMode(dirRegistros, UnixFileMode.UserRead | UnixFileMode.UserExecute); // la pregunta se puede escribir, el registro no
        try
        {
            Assert.ThrowsAny<Exception>(() => Ops(e).Eliminar(r1));
        }
        finally
        {
            File.SetUnixFileMode(dirRegistros, modo);
        }

        Assert.Equal(antes, Instantanea(e)); // la pregunta ya cerrada se restauró
        Assert.Equal("pendiente", e.Doc(p).Frontmatter["estado"]);
    }

    [Fact]
    public async Task Mientras_mira_procesa_las_operaciones_de_gestion_no_se_pueden_ejecutar_desde_el_panel()
    {
        using var e = new EntornoMira();
        var (r1, _, _) = await Preparar(e);
        var antes = Instantanea(e);
        var w = new Orumi.Panel.EscrituraDelPanel(new Orumi.Panel.RepositorioDeDatos(e.Datos, () => DateTimeOffset.UtcNow, TimeSpan.Zero), () => e.Ahora, _ => { });

        using (BloqueoDeEjecucion.Adquirir(e.Datos))
        {
            Assert.Equal(409, w.Ejecutar("eliminar", r1, null, op => op.Eliminar(r1)).Estado);
        }

        Assert.Equal(antes, Instantanea(e));
    }
}

public class GestionPorLineaDeComandosTests
{
    private static (int Codigo, string Salida, string Error) Cli(EntornoMira e, params string[] args)
    {
        var s = new StringWriter();
        var er = new StringWriter();
        var codigo = new Orumi.Cli.Comandos(() => new MemoryStream(), s, er, () => e.Ahora, _ => null).Ejecutar([.. args, "--datos", e.Datos]);
        return (codigo, s.ToString(), er.ToString());
    }

    private static async Task<(string R1, string R2)> Preparar(EntornoMira e)
    {
        e.Capturar("Hoy llamé a Marta.", e.Ahora);
        e.Capturar("Hoy comí con Pau.", e.Ahora.AddMinutes(1));
        e.Respuesta = s => Datos.Resp([Datos.Registro(s.Texto, "actividad", s.Texto.Contains("Marta") ? "Llamada con Marta" : "Comida con Pau", campos: new() { ["personas"] = new List<object?> { s.Texto.Contains("Marta") ? "Marta" : "Pau" } }, certeza: new() { ["personas"] = "dicho" })]);
        await e.Procesar();
        return (e.Registros().Single(r => (string)r.Frontmatter["titulo"]! == "Llamada con Marta").Id!, e.Registros().Single(r => (string)r.Frontmatter["titulo"]! == "Comida con Pau").Id!);
    }

    [Fact]
    public async Task Los_comandos_eliminan_restauran_deshacen_y_fusionan()
    {
        using var e = new EntornoMira();
        var (r1, r2) = await Preparar(e);

        Assert.Equal(0, Cli(e, "corregir", r1, "personas", "Marta,", "Pau").Codigo);
        Assert.Equal(0, Cli(e, "deshacer", r1, "personas").Codigo);
        Assert.Equal(["Marta"], ((List<object?>)e.Doc(r1).Frontmatter["personas"]!).Cast<string>());

        Assert.Equal(0, Cli(e, "eliminar", r2).Codigo);
        var (c, salida, _) = Cli(e, "consultar", "papelera");
        Assert.Equal(0, c);
        Assert.Contains(r2, salida);
        Assert.Equal(0, Cli(e, "restaurar", r2).Codigo);
        Assert.Contains("vacía", Cli(e, "consultar", "papelera").Salida);

        Assert.Equal(0, Cli(e, "fusionar", r1, r2).Codigo);
        Assert.Equal("fusionado", e.Doc(r2).Frontmatter["estado"]);
        Assert.Empty(e.Validar().Where(i => !i.Mensaje.Contains("sin lote")));
    }

    [Fact]
    public async Task Un_error_de_gestion_termina_con_el_codigo_2_y_sin_modificar_nada()
    {
        using var e = new EntornoMira();
        var (r1, _) = await Preparar(e);
        var (codigo, _, error) = Cli(e, "fusionar", r1, r1);
        Assert.Equal(2, codigo);
        Assert.Contains("No se ha modificado nada", error);
        Assert.Equal(2, Cli(e, "eliminar").Codigo); // faltan argumentos
    }
}
