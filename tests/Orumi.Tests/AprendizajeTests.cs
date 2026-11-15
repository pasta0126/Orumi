using Orumi.Agente;
using Orumi.Agente.Proveedores;
using Orumi.Cli;
using Orumi.Core.Aprendizaje;
using Orumi.Core.Correcciones;
using Orumi.Core.Ficheros;
using Orumi.Core.Validacion;

namespace Orumi.Tests;

/// <summary>Aprendizaje de Mira a partir de los cierres de preguntas (spec <c>aprendizaje-de-mira</c>, X64).</summary>
public class AprendizajeTests : IDisposable
{
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "orumi-apr-" + Guid.NewGuid().ToString("N")[..8]);
    private static readonly DateTimeOffset Ahora = new(2026, 11, 20, 10, 0, 0, TimeSpan.FromHours(1));
    private int _n;

    public AprendizajeTests() => Directory.CreateDirectory(_datos);

    public void Dispose() => Directory.Delete(_datos, true);

    private string Registro(string clase = "actividad")
    {
        var id = $"reg-20261110-r{++_n:000}";
        var ruta = Path.Combine(_datos, "registros", "2026", "11", id + ".md");
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllText(ruta, $"---\nid: {id}\ntipo: registro\nclase: {clase}\ntitulo: R\n---\nR.\n");
        return id;
    }

    /// <summary>Una pregunta ya cerrada (o pendiente) sobre un registro.</summary>
    private string Pregunta(string registro, string campo, string estado, string? cerrada = null, string? texto = null, string? clasePregunta = null)
    {
        var id = $"preg-20261110-p{++_n:000}";
        var ruta = Path.Combine(_datos, "preguntas", "2026", "11", id + ".md");
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        var cierre = estado == "pendiente" ? "" : $"cerrada_el: {cerrada}\nrespuesta: {{texto: \"{texto ?? "No es relevante."}\", respondida_el: {cerrada}}}\n";
        File.WriteAllText(ruta,
            $"---\nid: {id}\ntipo: pregunta\nregistro: {registro}\notros_registros: []\ncampo: {campo}\n" + (clasePregunta is null ? "" : $"clase_pregunta: \"{clasePregunta}\"\n") +
            $"texto: \"¿…?\"\nestado: {estado}\nvalor: media\ncreada_el: 2026-11-10T03:00:00+01:00\n{cierre}---\nP.\n");
        return id;
    }

    private string Irrelevante(string campo = "duracion", string clase = "actividad", string cuando = "2026-11-18T10:00:00+01:00") =>
        Pregunta(Registro(clase), campo, "no_relevante", cuando);

    private static UmbralesDeAprendizaje U => new();

    // ---------------------------------------------------------------- cuándo se aprende

    [Fact]
    public void Con_menos_de_tres_cierres_o_en_un_solo_dia_no_se_crea_ninguna_regla()
    {
        Irrelevante(cuando: "2026-11-18T10:00:00+01:00");
        Irrelevante(cuando: "2026-11-19T10:00:00+01:00");
        MotorDeAprendizaje.Actualizar(_datos, Ahora);
        Assert.Empty(MotorDeAprendizaje.Listar(_datos));

        Irrelevante(cuando: "2026-11-19T18:00:00+01:00"); // tres cierres, pero en dos días: ya cumple
        Assert.Single(MotorDeAprendizaje.Actualizar(_datos, Ahora).Nuevas);
    }

    [Fact]
    public void Tres_cierres_en_un_mismo_dia_no_bastan()
    {
        foreach (var h in new[] { "09", "10", "11" })
        {
            Irrelevante(cuando: $"2026-11-18T{h}:00:00+01:00");
        }

        MotorDeAprendizaje.Actualizar(_datos, Ahora);

        Assert.Empty(MotorDeAprendizaje.Listar(_datos));
    }

    [Fact]
    public void Tres_cierres_en_dos_dias_crean_una_regla_visible_con_su_evidencia()
    {
        var ids = new[]
        {
            Irrelevante(cuando: "2026-11-17T10:00:00+01:00"), Irrelevante(cuando: "2026-11-18T10:00:00+01:00"), Irrelevante(cuando: "2026-11-18T12:00:00+01:00"),
        };

        var r = MotorDeAprendizaje.Actualizar(_datos, Ahora);

        Assert.Equal(["apr-duracion-actividad"], r.Nuevas);
        var regla = Assert.Single(MotorDeAprendizaje.Listar(_datos));
        Assert.True(regla.EstaActiva);
        Assert.Equal("duracion:actividad", regla.Clase);
        Assert.Equal(ids.Order(), regla.Evidencia.Order());
        Assert.Contains("«duracion»", regla.Descripcion);
        Assert.Contains("3 veces en 2 días", regla.Descripcion);
        Assert.Equal("creada", regla.Historial[0].Accion);
        Assert.True(File.Exists(Path.Combine(_datos, "aprendizajes", "apr-duracion-actividad.yaml")));
        Assert.Contains("duracion:actividad", MotorDeAprendizaje.ClasesRebajadas(_datos));
    }

    [Fact]
    public void Es_por_clase_no_por_campo_ni_por_clase_de_registro_sueltos()
    {
        Irrelevante("duracion", "actividad", "2026-11-17T10:00:00+01:00");
        Irrelevante("duracion", "idea", "2026-11-18T10:00:00+01:00");
        Irrelevante("personas", "actividad", "2026-11-19T10:00:00+01:00");

        MotorDeAprendizaje.Actualizar(_datos, Ahora);

        Assert.Empty(MotorDeAprendizaje.Listar(_datos));
    }

    [Fact]
    public void Una_pregunta_antigua_sin_clase_escrita_se_clasifica_por_su_campo_y_su_registro()
    {
        var reg = Registro("problema");
        var id = Pregunta(reg, "resultados", "pendiente");
        var doc = Almacen.Cargar(_datos).DeTipo("pregunta").Single(p => p.Id == id);
        Assert.Equal("resultados:problema", ClaseDePregunta.De(doc, Almacen.Cargar(_datos)));

        var con = Pregunta(reg, "resultados", "pendiente", clasePregunta: "contraste:hip-uv");
        Assert.Equal("contraste:hip-uv", ClaseDePregunta.De(Almacen.Cargar(_datos).DeTipo("pregunta").Single(p => p.Id == con), Almacen.Cargar(_datos)));
    }

    // ---------------------------------------------------------------- lo que no enseña

    [Fact]
    public void Los_cierres_del_sistema_y_no_sucedio_y_no_lo_recuerdo_no_enseñan_a_callar()
    {
        for (var i = 0; i < 4; i++)
        {
            var d = 15 + i;
            Pregunta(Registro(), "duracion", "no_sucedio", $"2026-11-{d}T10:00:00+01:00", "No sucedió.");
            Pregunta(Registro(), "duracion", "no_recuerda", $"2026-11-{d}T11:00:00+01:00", "No lo recuerdo.");
            Pregunta(Registro(), "duracion", "no_relevante", $"2026-11-{d}T12:00:00+01:00", "El registro se eliminó."); // cierre del sistema
        }

        MotorDeAprendizaje.Actualizar(_datos, Ahora);

        Assert.Empty(MotorDeAprendizaje.Listar(_datos));
        Assert.Empty(MotorDeAprendizaje.Preferencias(_datos));
    }

    // ---------------------------------------------------------------- restablecer y reactivar

    private void ReglaActiva()
    {
        Irrelevante(cuando: "2026-11-16T10:00:00+01:00");
        Irrelevante(cuando: "2026-11-17T10:00:00+01:00");
        Irrelevante(cuando: "2026-11-18T10:00:00+01:00");
        MotorDeAprendizaje.Actualizar(_datos, Ahora);
    }

    [Fact]
    public void Una_respuesta_con_datos_restablece_la_prioridad_de_la_clase()
    {
        ReglaActiva();
        Pregunta(Registro(), "duracion", "respondida", "2026-11-19T10:00:00+01:00", "unas dos horas");

        var r = MotorDeAprendizaje.Actualizar(_datos, Ahora);

        Assert.Equal(["apr-duracion-actividad"], r.Restablecidas);
        Assert.Equal(ReglaAprendida.Restablecida, MotorDeAprendizaje.Leer(_datos, "apr-duracion-actividad")!.Estado);
        Assert.Empty(MotorDeAprendizaje.ClasesRebajadas(_datos));
    }

    [Fact]
    public void Tras_restablecerse_solo_vuelve_con_evidencia_nueva_suficiente()
    {
        ReglaActiva();
        Pregunta(Registro(), "duracion", "respondida", "2026-11-19T10:00:00+01:00", "unas dos horas");
        MotorDeAprendizaje.Actualizar(_datos, Ahora);

        Irrelevante(cuando: "2026-11-19T12:00:00+01:00");
        Irrelevante(cuando: "2026-11-20T08:00:00+01:00");
        Assert.Empty(MotorDeAprendizaje.Actualizar(_datos, Ahora).Reactivadas); // los cierres viejos ya no cuentan

        Irrelevante(cuando: "2026-11-20T09:00:00+01:00");
        var r = MotorDeAprendizaje.Actualizar(_datos, Ahora);

        Assert.Equal(["apr-duracion-actividad"], r.Reactivadas);
        Assert.True(MotorDeAprendizaje.Leer(_datos, "apr-duracion-actividad")!.EstaActiva);
    }

    // ---------------------------------------------------------------- control del usuario

    [Fact]
    public void Lo_que_el_usuario_desactiva_no_lo_reactiva_Mira_aunque_haya_mas_evidencia()
    {
        ReglaActiva();
        MotorDeAprendizaje.Desactivar(_datos, "apr-duracion-actividad", Ahora);
        Irrelevante(cuando: "2026-11-20T09:00:00+01:00");

        MotorDeAprendizaje.Actualizar(_datos, Ahora);

        var r = MotorDeAprendizaje.Leer(_datos, "apr-duracion-actividad")!;
        Assert.Equal(ReglaAprendida.Desactivada, r.Estado);
        Assert.Equal("usuario", r.Historial[^1].Origen);
        Assert.Empty(MotorDeAprendizaje.ClasesRebajadas(_datos));
        Assert.Empty(MotorDeAprendizaje.Preferencias(_datos));
    }

    [Fact]
    public void Lo_que_el_usuario_activa_a_mano_no_lo_restablece_Mira()
    {
        ReglaActiva();
        MotorDeAprendizaje.Desactivar(_datos, "apr-duracion-actividad", Ahora);
        MotorDeAprendizaje.Activar(_datos, "apr-duracion-actividad", Ahora);
        Pregunta(Registro(), "duracion", "respondida", "2026-11-19T10:00:00+01:00", "dos horas");

        MotorDeAprendizaje.Actualizar(_datos, Ahora);

        Assert.True(MotorDeAprendizaje.Leer(_datos, "apr-duracion-actividad")!.EstaActiva);
    }

    [Fact]
    public void Borrar_oculta_la_regla_y_solo_vuelve_con_evidencia_posterior_al_borrado()
    {
        ReglaActiva();
        MotorDeAprendizaje.Borrar(_datos, "apr-duracion-actividad", Ahora);

        MotorDeAprendizaje.Actualizar(_datos, Ahora);
        Assert.Empty(MotorDeAprendizaje.Listar(_datos)); // no reaparece con la misma evidencia
        Assert.Single(MotorDeAprendizaje.Listar(_datos, incluirBorradas: true));

        Irrelevante(cuando: "2026-11-21T09:00:00+01:00");
        Irrelevante(cuando: "2026-11-22T09:00:00+01:00");
        Irrelevante(cuando: "2026-11-22T10:00:00+01:00");
        var r = MotorDeAprendizaje.Actualizar(_datos, Ahora.AddDays(3));

        Assert.Equal(["apr-duracion-actividad"], r.Reactivadas);
        Assert.Single(MotorDeAprendizaje.Listar(_datos));
    }

    [Fact]
    public void Un_aprendizaje_inexistente_da_un_error_claro()
    {
        Assert.Throws<AprendizajeNoEncontradoException>(() => MotorDeAprendizaje.Desactivar(_datos, "apr-nada", Ahora));
    }

    // ---------------------------------------------------------------- «no sucedió»

    [Fact]
    public void No_sucedio_cierra_la_pregunta_sin_tocar_el_registro_y_el_fichero_sigue_siendo_valido()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy llamada con Rosa.");
        e.Respuesta = _ => Datos.Resp([Datos.Registro("Hoy llamada con Rosa.", "evento", "Llamada con Rosa")], new PreguntaCandidata("duracion", "¿Cuánto duró?", "media", RegistroIndice: 0));
        e.Procesar().GetAwaiter().GetResult();
        var pregunta = e.Preguntas().Single();
        var antes = File.ReadAllText(Path.Combine(e.Datos, e.Registros().Single().Ruta));

        var r = new Operaciones(e.Datos, () => Ahora).CerrarSinDato(pregunta.Id!, "no_sucedio");

        Assert.Contains("no_sucedio", r.Mensaje);
        var cerrada = e.Preguntas().Single();
        Assert.Equal("no_sucedio", cerrada.Frontmatter["estado"]);
        Assert.Equal("No sucedió.", ((Dictionary<string, object?>)cerrada.Frontmatter["respuesta"]!)["texto"]);
        Assert.Equal(antes, File.ReadAllText(Path.Combine(e.Datos, e.Registros().Single().Ruta)));
        Assert.DoesNotContain(Validador.Validar(Almacen.Cargar(e.Datos)), i => i.Mensaje.Contains("estado inválido") || i.Mensaje.Contains("sin respuesta"));

        // No se repite la misma pregunta sobre el mismo dato al reprocesar.
        e.Ahora = e.Ahora.AddDays(1);
        e.Capturar("Otra nota sin relación.");
        e.Respuesta = _ => Datos.Resp([Datos.Registro("Otra nota sin relación.", "evento", "Otra")]);
        Assert.Single(e.Preguntas());
    }

    [Fact]
    public void Una_pregunta_solo_se_cierra_sin_dato_con_un_estado_conocido()
    {
        var reg = Registro();
        var id = Pregunta(reg, "duracion", "pendiente");
        Assert.Throws<OperacionInvalidaException>(() => new Operaciones(_datos, () => Ahora).CerrarSinDato(id, "inventado"));
    }

    [Fact]
    public void La_linea_de_comandos_cierra_como_no_sucedio_y_gestiona_los_aprendizajes()
    {
        var reg = Registro();
        var id = Pregunta(reg, "duracion", "pendiente");
        var salida = new StringWriter();
        var error = new StringWriter();
        var cmd = new Comandos(() => new MemoryStream(), salida, error, () => Ahora, _ => null);

        Assert.Equal(0, cmd.Ejecutar(["responder", id, "--no-sucedio", "--datos", _datos]));
        Assert.Equal("no_sucedio", Almacen.Cargar(_datos).DeTipo("pregunta").Single().Frontmatter["estado"]);

        ReglaActiva();
        Assert.Equal(0, cmd.Ejecutar(["aprendizajes", "listar", "--datos", _datos]));
        Assert.Contains("apr-duracion-actividad", salida.ToString());
        Assert.Contains("evidencia:", salida.ToString());
        Assert.Equal(0, cmd.Ejecutar(["aprendizajes", "desactivar", "apr-duracion-actividad", "--datos", _datos]));
        Assert.Equal(ReglaAprendida.Desactivada, MotorDeAprendizaje.Leer(_datos, "apr-duracion-actividad")!.Estado);
        Assert.Equal(2, cmd.Ejecutar(["aprendizajes", "borrar", "apr-nada", "--datos", _datos]));
    }

    // ---------------------------------------------------------------- aplicación: orden y prompt

    private static PreguntaCandidata P(int registro, string campo, string valor) => new(campo, "¿…?", valor, RegistroIndice: registro);

    [Fact]
    public async Task Una_clase_rebajada_pasa_por_detras_de_las_demas_aunque_valga_mas()
    {
        using var e = new EntornoMira { Opciones = new OpcionesProcesador(MaximoPendientes: 1) };
        e.Capturar("Hoy llamada con Rosa.");
        // Regla activa para duracion:evento.
        var d = e.Datos;
        for (var i = 0; i < 3; i++)
        {
            var reg = $"reg-20261110-z{i:000}";
            var rp = Path.Combine(d, "registros", "2026", "11", reg + ".md");
            Directory.CreateDirectory(Path.GetDirectoryName(rp)!);
            File.WriteAllText(rp, $"---\nid: {reg}\ntipo: registro\nclase: evento\ntitulo: R\n---\nR.\n");
            var pp = Path.Combine(d, "preguntas", "2026", "11", $"preg-20261110-z{i:000}.md");
            Directory.CreateDirectory(Path.GetDirectoryName(pp)!);
            var dia = 17 + i;
            File.WriteAllText(pp, $"---\nid: preg-20261110-z{i:000}\ntipo: pregunta\nregistro: {reg}\notros_registros: []\ncampo: duracion\ntexto: \"¿…?\"\nestado: no_relevante\nvalor: media\ncreada_el: 2026-11-10T03:00:00+01:00\ncerrada_el: 2026-11-{dia}T10:00:00+01:00\nrespuesta: {{texto: \"No es relevante.\", respondida_el: 2026-11-{dia}T10:00:00+01:00}}\n---\nP.\n");
        }

        e.Ahora = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.FromHours(1));
        e.Respuesta = _ => Datos.Resp([Datos.Registro("Hoy llamada con Rosa.", "evento", "Llamada con Rosa")], P(0, "duracion", "alta"), P(0, "personas", "baja"));

        await e.Procesar();

        var nuevas = e.Preguntas().Where(p => p.Frontmatter["estado"] as string == "pendiente").ToList();
        var unica = Assert.Single(nuevas);
        Assert.Equal("personas", unica.Frontmatter["campo"]); // la de clase rebajada quedó por detrás
        Assert.Equal("personas:evento", unica.Frontmatter["clase_pregunta"]);
    }

    [Fact]
    public async Task Una_clase_rebajada_no_se_prohibe_si_hay_cupo_se_pregunta()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy llamada con Rosa.");
        for (var i = 0; i < 3; i++)
        {
            var reg = $"reg-20261110-y{i:000}";
            var rp = Path.Combine(e.Datos, "registros", "2026", "11", reg + ".md");
            Directory.CreateDirectory(Path.GetDirectoryName(rp)!);
            File.WriteAllText(rp, $"---\nid: {reg}\ntipo: registro\nclase: evento\ntitulo: R\n---\nR.\n");
            var pp = Path.Combine(e.Datos, "preguntas", "2026", "11", $"preg-20261110-y{i:000}.md");
            Directory.CreateDirectory(Path.GetDirectoryName(pp)!);
            var dia = 17 + i;
            File.WriteAllText(pp, $"---\nid: preg-20261110-y{i:000}\ntipo: pregunta\nregistro: {reg}\notros_registros: []\ncampo: duracion\ntexto: \"¿…?\"\nestado: no_relevante\nvalor: media\ncreada_el: 2026-11-10T03:00:00+01:00\ncerrada_el: 2026-11-{dia}T10:00:00+01:00\nrespuesta: {{texto: \"No es relevante.\", respondida_el: 2026-11-{dia}T10:00:00+01:00}}\n---\nP.\n");
        }

        e.Ahora = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.FromHours(1));
        e.Respuesta = _ => Datos.Resp([Datos.Registro("Hoy llamada con Rosa.", "evento", "Llamada con Rosa")], P(0, "duracion", "alta"));

        var r = await e.Procesar();

        Assert.Single(r.PreguntasCreadas);
        Assert.Contains("duracion:evento", MotorDeAprendizaje.ClasesRebajadas(e.Datos));
    }

    [Fact]
    public async Task Las_preferencias_van_al_modelo_solo_como_descripcion_de_la_regla_sin_textos_del_usuario()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy llamada con Rosa por el contrato secreto.");
        var reglaActiva = new ReglaAprendida("apr-duracion-evento", "duracion:evento", ReglaAprendida.Activa,
            "Preguntar menos por «duracion» en registros de clase «evento»: el usuario lo ha cerrado como no relevante 3 veces en 2 días distintos.",
            ["preg-20261110-aaaa"], Ahora, Ahora, false, null, [new EntradaDeAprendizaje(Ahora, "creada", "mira", null)]);
        MotorDeAprendizaje.Guardar(e.Datos, reglaActiva);
        e.Respuesta = _ => RespuestaExtraccion.SinHechos;

        await e.Procesar();

        var s = Assert.Single(e.Proveedor.Solicitudes);
        Assert.Equal([reglaActiva.Descripcion], s.Preferencias);
        var mensaje = ProveedorAnthropic.Mensaje(s);
        Assert.Contains("preferencias_del_usuario", mensaje);
        Assert.DoesNotContain("preg-20261110-aaaa", mensaje); // ni ids de preguntas ni textos de cierres
        Assert.Contains("no te autorizan a inventar", ProveedorAnthropic.Instrucciones);
    }

    [Fact]
    public void Sin_reglas_el_mensaje_al_modelo_no_incluye_preferencias()
    {
        var s = new SolicitudExtraccion("cap-1", "x", Ahora, null, null, [], []);
        Assert.DoesNotContain("preferencias_del_usuario", ProveedorAnthropic.Mensaje(s));
    }

    // ---------------------------------------------------------------- panel

    [Fact]
    public void El_panel_lista_los_aprendizajes_con_su_evidencia_y_permite_desactivar_activar_y_borrar()
    {
        ReglaActiva();
        var repo = new Orumi.Panel.RepositorioDeDatos(_datos, () => DateTimeOffset.UtcNow, TimeSpan.Zero);
        var escritura = new Orumi.Panel.EscrituraDelPanel(repo, () => Ahora, _ => { });
        var json = System.Text.Json.JsonSerializer.Serialize(new Orumi.Panel.Lectura(repo).Aprendizajes());
        Assert.Contains("apr-duracion-actividad", json);
        Assert.Contains("\"estado\":\"activa\"", json);

        var r = escritura.Ejecutar("aprendizaje-desactivar", "apr-duracion-actividad", "a@b.c", op => op.CambiarAprendizaje("apr-duracion-actividad", "desactivar"));
        Assert.Equal(200, r.Estado);
        Assert.Contains("\"estado\":\"desactivada\"", System.Text.Json.JsonSerializer.Serialize(new Orumi.Panel.Lectura(repo).Aprendizajes()));

        Assert.Equal(200, escritura.Ejecutar("aprendizaje-activar", "apr-duracion-actividad", null, op => op.CambiarAprendizaje("apr-duracion-actividad", "activar")).Estado);
        Assert.Equal(200, escritura.Ejecutar("aprendizaje-borrar", "apr-duracion-actividad", null, op => op.CambiarAprendizaje("apr-duracion-actividad", "borrar")).Estado);
        Assert.DoesNotContain("apr-duracion-actividad", System.Text.Json.JsonSerializer.Serialize(new Orumi.Panel.Lectura(repo).Aprendizajes()));
    }

    [Theory]
    [InlineData("apr-nada", "desactivar", 404)]
    [InlineData("apr-x", "inventar", 400)]
    [InlineData("../etc/passwd", "borrar", 400)]
    [InlineData("APR-MAYUS", "borrar", 400)]
    public void El_panel_rechaza_identificadores_o_acciones_invalidos_sin_modificar_nada(string id, string accion, int estado)
    {
        ReglaActiva();
        var antes = File.ReadAllText(MotorDeAprendizaje.Ruta(_datos, "apr-duracion-actividad"));
        var repo = new Orumi.Panel.RepositorioDeDatos(_datos, () => DateTimeOffset.UtcNow, TimeSpan.Zero);
        var escritura = new Orumi.Panel.EscrituraDelPanel(repo, () => Ahora, _ => { });

        var r = escritura.Ejecutar("aprendizaje", id, null, op => op.CambiarAprendizaje(id, accion));

        Assert.Equal(estado, r.Estado);
        Assert.Equal(antes, File.ReadAllText(MotorDeAprendizaje.Ruta(_datos, "apr-duracion-actividad")));
    }

    [Fact]
    public void Una_escritura_del_panel_se_rechaza_con_409_mientras_Mira_procesa()
    {
        ReglaActiva();
        var repo = new Orumi.Panel.RepositorioDeDatos(_datos, () => DateTimeOffset.UtcNow, TimeSpan.Zero);
        var escritura = new Orumi.Panel.EscrituraDelPanel(repo, () => Ahora, _ => { });
        using var bloqueo = BloqueoDeEjecucion.Adquirir(_datos);

        var r = escritura.Ejecutar("aprendizaje-borrar", "apr-duracion-actividad", null, op => op.CambiarAprendizaje("apr-duracion-actividad", "borrar"));

        Assert.Equal(409, r.Estado);
        Assert.True(MotorDeAprendizaje.Leer(_datos, "apr-duracion-actividad")!.EstaActiva);
    }
}
