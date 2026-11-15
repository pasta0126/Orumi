using Orumi.Core.Correcciones;
using Orumi.Panel;

namespace Orumi.Tests;

/// <summary>Respuestas rápidas (spec <c>respuestas-rapidas</c>): botones con la respuesta cerrada ya fijada.</summary>
public class RespuestasRapidasTests
{
    private static async Task<(EntornoMira E, string Pregunta, string Antiguo, string Nuevo)> Coincidencia()
    {
        var e = new EntornoMira();
        e.Capturar("El jueves hablé con Laura de las facturas.", e.Ahora);
        e.Respuesta = _ => Datos.Resp([Datos.Registro("hablé con Laura de las facturas.", "evento", "Reunión con Laura sobre facturas", dia: "2026-11-12",
            campos: new() { ["personas"] = new List<object?> { "Laura" } }, certeza: new() { ["personas"] = "dicho" })]);
        await e.Procesar();
        var antiguo = e.Registros().Single().Id!;
        e.Ahora = e.Ahora.AddDays(2);
        e.Capturar("He vuelto a hablar con Laura de las facturas.", e.Ahora);
        e.Respuesta = s => Datos.Resp([Datos.Registro("He vuelto a hablar con Laura de las facturas.", "evento", "Hablar de nuevo con Laura",
            campos: new() { ["personas"] = new List<object?> { "Laura" } }, certeza: new() { ["personas"] = "dicho" }, duda: s.Relacionados.Single().Id)]);
        await e.Procesar();
        var nuevo = e.Registros().Single(r => r.Id != antiguo).Id!;
        return (e, e.Preguntas().Single().Id!, antiguo, nuevo);
    }

    private static PreguntaConContexto Pendiente(EntornoMira e, string id) => PreguntasConContexto.Pendientes(e.Cargar()).Single(p => p.Id == id);

    [Fact]
    public async Task Una_coincidencia_dudosa_ofrece_es_el_mismo_relacionados_y_son_distintos_sin_escribir_nada()
    {
        var (e, pregunta, _, _) = await Coincidencia();
        using var _e = e;

        var opciones = Pendiente(e, pregunta).Opciones!;

        Assert.Equal(["Es el mismo", "Relacionados", "Son distintos"], opciones.Select(o => o.Etiqueta).ToArray());
        Assert.Equal(["misma", "misma", "distinta"], opciones.Select(o => o.Valor).ToArray());
        Assert.Equal([true, false, false], opciones.Select(o => o.Fusiona).ToArray());
    }

    [Fact]
    public async Task Cada_opcion_equivale_a_escribir_la_respuesta_misma_o_distinta()
    {
        var (e, pregunta, antiguo, nuevo) = await Coincidencia();
        using var _e = e;
        var op = new Operaciones(e.Datos, () => e.Ahora);

        op.Responder(pregunta, Pendiente(e, pregunta).Opciones![2].Valor!); // «Son distintos»

        Assert.Equal("respondida", (string)e.Doc(pregunta).Frontmatter["estado"]!);
        Assert.Empty(e.Registros().Where(r => r.Frontmatter.ContainsKey("estado")));
        Assert.Equal(2, e.Registros().Count(r => r.Id == antiguo || r.Id == nuevo));
    }

    [Fact]
    public async Task Es_el_mismo_fusiona_en_el_mas_antiguo_y_se_puede_deshacer_de_inmediato()
    {
        var (e, pregunta, antiguo, nuevo) = await Coincidencia();
        using var _e = e;
        var antesDelAntiguo = e.Leer(antiguo);
        var antesDelNuevo = e.Leer(nuevo);
        var antesDeLaPregunta = e.Leer(pregunta);
        var escritura = new EscrituraDelPanel(new RepositorioDeDatos(e.Datos, () => DateTimeOffset.UtcNow, TimeSpan.Zero), () => e.Ahora, _ => { });

        var r = escritura.EjecutarConDeshacer("responder-y-fusionar", pregunta, "yo@ejemplo.com", op =>
        {
            var (destino, origen) = RespuestasRapidas.DestinoDeLaFusion(e.Cargar(), pregunta);
            return [op.Responder(pregunta, "misma"), op.Fusionar(destino, origen)];
        });

        Assert.Equal(200, r.Estado);
        Assert.Equal("fusionado", (string)e.Doc(nuevo).Frontmatter["estado"]!); // el más antiguo es el destino
        Assert.Equal(antiguo, (string)e.Doc(nuevo).Frontmatter["fusionado_en"]!);
        Assert.NotNull(r.Deshacer);

        var deshecho = escritura.Deshacer(r.Deshacer!, "yo@ejemplo.com");

        Assert.Equal(200, deshecho.Estado);
        Assert.Equal(antesDelAntiguo, e.Leer(antiguo));
        Assert.Equal(antesDelNuevo, e.Leer(nuevo));
        Assert.Equal(antesDeLaPregunta, e.Leer(pregunta));
        Assert.Equal(404, escritura.Deshacer(r.Deshacer!, null).Estado); // la ficha se gasta
    }

    [Fact]
    public async Task Deshacer_no_pisa_lo_que_cambio_despues_y_caduca_al_pasar_el_minuto()
    {
        var (e, pregunta, antiguo, _) = await Coincidencia();
        using var _e = e;
        var ahora = e.Ahora;
        var escritura = new EscrituraDelPanel(new RepositorioDeDatos(e.Datos, () => DateTimeOffset.UtcNow, TimeSpan.Zero), () => ahora, _ => { });
        var r = escritura.EjecutarConDeshacer("responder-y-fusionar", pregunta, null, op =>
        {
            var (destino, origen) = RespuestasRapidas.DestinoDeLaFusion(e.Cargar(), pregunta);
            return [op.Responder(pregunta, "misma"), op.Fusionar(destino, origen)];
        });
        File.AppendAllText(Path.Combine(e.Datos, e.Doc(antiguo).Ruta), "\nEditado a mano después.\n");

        var conCambios = escritura.Deshacer(r.Deshacer!, null);

        Assert.Equal(400, conCambios.Estado);
        Assert.Contains("Editado a mano", e.Leer(antiguo));

        var r2 = escritura.EjecutarConDeshacer("otra", null, null, op => [op.Responder(pregunta, "misma")]); // ya respondida: 400, sin ficha
        Assert.Null(r2.Deshacer);
        ahora = ahora.AddSeconds(EscrituraDelPanel.SegundosParaDeshacer + 1);
        Assert.Equal(404, escritura.Deshacer("0000000000000000", null).Estado);
    }

    [Fact]
    public async Task Una_pregunta_que_no_es_coincidencia_no_se_puede_fusionar()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy comí con Pau.", e.Ahora);
        e.Respuesta = _ => Datos.Resp([Datos.Registro("Hoy comí con Pau.", "evento", "Comida con Pau")], new Orumi.Agente.PreguntaCandidata("duracion", "¿Cuánto duró?", "media", RegistroIndice: 0));
        await e.Procesar();

        Assert.Throws<OperacionInvalidaException>(() => RespuestasRapidas.DestinoDeLaFusion(e.Cargar(), e.Preguntas().Single().Id!));
        Assert.Empty(Pendiente(e, e.Preguntas().Single().Id!).Opciones!); // pregunta abierta: sin botones inventados
    }

    [Fact]
    public async Task En_un_conflicto_hay_un_boton_por_valor_y_responder_con_uno_confirma_ese_valor_en_su_registro()
    {
        using var e = new EntornoMira();
        e.Capturar("El jueves pasado comí con Carla en el centro.", e.Ahora);
        e.Respuesta = _ => Datos.Resp([Datos.Registro("El jueves pasado comí con Carla en el centro.", "evento", "Comida con Carla", dia: "2026-11-12",
            campos: new() { ["personas"] = new List<object?> { "Carla" } }, certeza: new() { ["personas"] = "dicho" })]);
        await e.Procesar();
        var primero = e.Registros().Single().Id!;
        e.Ahora = e.Ahora.AddDays(1);
        e.Capturar("Ayer me acordé de la comida con Carla, fue el miércoles de la semana pasada.", e.Ahora);
        e.Respuesta = s => Datos.Resp([Datos.Registro("fue el miércoles de la semana pasada.", "evento", "Comida con Carla (otra fecha)", dia: "2026-11-11",
            campos: new() { ["personas"] = new List<object?> { "Carla" } }, certeza: new() { ["personas"] = "dicho" },
            enConflictoCon: s.Relacionados.Single().Id, conflictoCampo: "fecha")]);
        await e.Procesar();
        var pregunta = e.Preguntas().Single();

        var opciones = Pendiente(e, pregunta.Id!).Opciones!;

        Assert.Equal(2, opciones.Count);
        Assert.All(opciones, o => Assert.NotNull(o.Registro));
        var elegida = opciones.Single(o => o.Registro == primero);
        new Operaciones(e.Datos, () => e.Ahora).Responder(pregunta.Id!, elegida.Valor!, elegida.Registro);
        Assert.Equal("respondida", (string)e.Doc(pregunta.Id!).Frontmatter["estado"]!);
        Assert.Equal("confirmado", ((Dictionary<string, object?>)e.Doc(primero).Frontmatter["certeza"]!)["fecha"]);
    }

    [Fact]
    public void Las_preguntas_de_Noa_y_las_abiertas_no_llevan_opciones_y_las_de_ciudad_y_contraste_llevan_si_no()
    {
        var noa = new Dictionary<string, object?> { ["campo"] = "perfil", ["generada_por"] = new Dictionary<string, object?> { ["agente"] = "Noa" } };
        var ciudad = new Dictionary<string, object?> { ["campo"] = "ciudad", ["ciudad_tipo"] = "confirmar" };
        var elegir = new Dictionary<string, object?> { ["campo"] = "ciudad", ["ciudad_tipo"] = "elegir" };
        var contraste = new Dictionary<string, object?> { ["campo"] = "contraste", ["concepto"] = "dolor_de_cabeza" };
        var duracion = new Dictionary<string, object?> { ["campo"] = "contraste", ["concepto"] = RespuestasRapidas.ConceptoDeDuracion };

        Assert.Empty(RespuestasRapidas.De(noa, null, null, []));
        Assert.Equal(["Sí", "No"], RespuestasRapidas.De(ciudad, null, null, []).Select(o => o.Etiqueta).ToArray());
        Assert.Empty(RespuestasRapidas.De(elegir, null, null, []));
        var c = RespuestasRapidas.De(contraste, null, null, []);
        Assert.Equal((null, "no_sucedio"), (c[1].Valor, c[1].Cierre)); // «No» es «No sucedió»: ausente confirmado
        Assert.Empty(RespuestasRapidas.De(duracion, null, null, [])); // la duración se escribe
    }

    [Fact]
    public void Un_problema_con_pregunta_de_resolucion_ofrece_resuelto_o_sigue_abierto_y_las_demas_no()
    {
        var q = new Dictionary<string, object?> { ["campo"] = "resultados", ["texto"] = "¿Quedó resuelta la incidencia?" };

        var op = RespuestasRapidas.De(q, "problema", new RegistroImplicado("reg-20261116-aaaa", "Incidencia", "desconocido"), []);

        Assert.Equal(["Sí, quedó resuelto", "No, sigue abierto"], op.Select(o => o.Etiqueta).ToArray());
        Assert.Empty(RespuestasRapidas.De(q, "actividad", null, []));
        Assert.Empty(RespuestasRapidas.De(new Dictionary<string, object?> { ["campo"] = "resultados", ["texto"] = "¿Cómo acabó?" }, "problema", null, []));
    }

    [Fact]
    public async Task Responder_a_preguntas_de_ciudad_y_de_contraste_las_cierra_sin_error_y_sin_tocar_el_registro()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy comí con Pau.", e.Ahora);
        e.Respuesta = _ => Datos.Resp([Datos.Registro("Hoy comí con Pau.", "evento", "Comida con Pau")]);
        await e.Procesar();
        var reg = e.Registros().Single().Id!;
        var antes = e.Leer(reg);
        foreach (var campo in new[] { "ciudad", "contraste" })
        {
            var id = $"preg-20261116-{(campo == "ciudad" ? "ci01" : "co01")}";
            var ruta = Path.Combine(e.Datos, "preguntas", "2026", "11", id + ".md");
            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
            File.WriteAllText(ruta, $"---\nid: {id}\ntipo: pregunta\nregistro: {reg}\notros_registros: []\ncampo: {campo}\ntexto: \"¿Seguro?\"\nestado: pendiente\nvalor: media\ncreada_el: 2026-11-16T09:00:00+01:00\ngenerada_por: {{agente: Mira}}\n---\nPregunta.\n");
            var r = new Operaciones(e.Datos, () => e.Ahora).Responder(id, "Sí");
            Assert.Contains("respondida", r.Mensaje);
            Assert.Equal("respondida", (string)e.Doc(id).Frontmatter["estado"]!);
        }

        Assert.Equal(antes, e.Leer(reg));
    }

    [Fact]
    public async Task La_linea_de_comandos_responde_con_una_opcion_numerada_y_las_lista_en_pendientes()
    {
        var (e, pregunta, _, nuevo) = await Coincidencia();
        using var _e = e;
        var salida = new StringWriter();
        var error = new StringWriter();
        var comandos = new Orumi.Cli.Comandos(() => new MemoryStream(), salida, error, () => e.Ahora, _ => null);

        comandos.Ejecutar(["consultar", "pendientes", "--datos", e.Datos]);
        Assert.Contains("1) Es el mismo · 2) Relacionados · 3) Son distintos", salida.ToString());

        var codigo = comandos.Ejecutar(["responder", pregunta, "--opcion", "1", "--datos", e.Datos]);

        Assert.True(codigo == 0, error.ToString());
        Assert.Equal("fusionado", (string)e.Doc(nuevo).Frontmatter["estado"]!);
        Assert.Equal(2, comandos.Ejecutar(["responder", pregunta, "--opcion", "9", "--datos", e.Datos]));
    }
}
