using Orumi.Agente;
using Orumi.Core.Ficheros;
using static Orumi.Tests.Datos;

namespace Orumi.Tests;

public class ProcesadorTests
{
    private static List<object?> Lista(Dictionary<string, object?> fm, string clave) => (List<object?>)fm[clave]!;

    private static Dictionary<string, object?> Mapa(object? o) => (Dictionary<string, object?>)o!;

    // ---------------------------------------------------------------- C1 cobertura completa

    [Fact]
    public async Task C1_Una_nota_mezclada_genera_un_registro_por_hecho_sin_filtrar_lo_cotidiano()
    {
        using var e = new EntornoMira();
        const string texto = "Esta mañana he revisado el contrato de Ferrer con Pau. Luego me he tomado un cortado y una tostada. Ya he puesto la lavadora y he sacado al perro.";
        var cap = e.Capturar(texto);
        e.Respuesta = _ => Resp([
            Registro("Esta mañana he revisado el contrato de Ferrer con Pau.", "actividad", "Revisar el contrato de Ferrer con Pau",
                campos: new() { ["personas"] = new List<object?> { "Pau" }, ["entidades"] = new List<object?> { new Dictionary<string, object?> { ["nombre"] = "Ferrer", ["tipo"] = "organizacion" } } },
                certeza: new() { ["personas"] = "dicho", ["entidades"] = "dicho" }),
            Registro("Luego me he tomado un cortado y una tostada.", "actividad", "Desayuno: cortado y tostada"),
            Registro("Ya he puesto la lavadora", "actividad", "Poner la lavadora"),
            Registro("he sacado al perro.", "actividad", "Sacar al perro"),
        ]);

        var r = await e.Procesar();

        Assert.Equal(4, r.RegistrosCreados.Count);
        Assert.Equal(4, e.Registros().Count);
        Assert.Empty(e.Validar());
        var claves = e.Registros().Select(d => (string)d.Frontmatter["clave_extraccion"]!).Order().ToList();
        Assert.Equal([$"{cap.Id}#f1:actividad:1", $"{cap.Id}#f2:actividad:1", $"{cap.Id}#f3:actividad:1", $"{cap.Id}#f4:actividad:1"], claves);
        Assert.NotNull(r.LoteId);
    }

    // ---------------------------------------------------------------- captura intacta y mínimo necesario

    [Fact]
    public async Task Procesar_no_modifica_la_captura_y_solo_envia_lo_minimo()
    {
        using var e = new EntornoMira();
        var cap = e.Capturar("Hoy he hablado con Marta.", fechaAcontecimiento: "2026-11-15", etiqueta: "trabajo");
        var antes = File.ReadAllBytes(cap.Ruta);
        e.Respuesta = _ => Resp([Registro("Hoy he hablado con Marta.", "evento", "Hablar con Marta")]);

        await e.Procesar();

        Assert.Equal(antes, File.ReadAllBytes(cap.Ruta));
        var s = Assert.Single(e.Proveedor.Solicitudes);
        Assert.Equal("Hoy he hablado con Marta.", s.Texto);
        Assert.Equal("2026-11-15", s.FechaAcontecimiento); // C12: metadatos del usuario
        Assert.Equal("trabajo", s.Etiqueta);
        Assert.Empty(s.ClavesExistentes);
        Assert.Empty(s.Relacionados);
    }

    // ---------------------------------------------------------------- C2 fechas y horas

    [Fact]
    public async Task C2_Una_hora_aproximada_sin_valor_no_se_guarda_y_la_captura_sigue_pendiente()
    {
        using var e = new EntornoMira();
        e.Capturar("Ayer sobre las 18 hablé con Rosa.");
        e.Respuesta = _ => Resp([Registro("Ayer sobre las 18 hablé con Rosa.", "evento", "Llamada con Rosa", dia: "2026-11-15",
            campos: new() { ["hora"] = new Dictionary<string, object?> { ["expresion"] = "sobre las 18", ["aproximada"] = "true" } },
            certeza: new() { ["hora"] = "dicho" })]);

        var r = await e.Procesar();

        Assert.Empty(r.Procesadas);
        Assert.Single(r.Problemas);
        Assert.Contains("aproximada sin valor", r.Problemas[0].Motivo);
        Assert.Empty(e.Registros());
        Assert.Equal(1, (await e.Procesar()).Pendientes); // sigue pendiente
    }

    [Fact]
    public async Task C2_Una_franja_sin_hora_concreta_se_conserva_sin_valor()
    {
        using var e = new EntornoMira();
        e.Capturar("El lunes de la semana pasada tuve médico por la mañana.");
        e.Respuesta = _ => Resp([Registro("tuve médico por la mañana.", "evento", "Médico", dia: "2026-11-09", expresion: "el lunes de la semana pasada",
            campos: new() { ["hora"] = new Dictionary<string, object?> { ["expresion"] = "por la mañana" } }, certeza: new() { ["hora"] = "dicho" })]);

        await e.Procesar();

        var hora = Mapa(e.Registros().Single().Frontmatter["hora"]);
        Assert.Equal("por la mañana", hora["expresion"]);
        Assert.False(hora.ContainsKey("valor"));
        Assert.Empty(e.Validar());
    }

    [Fact]
    public async Task Un_campo_desconocido_o_ausente_no_conserva_la_certeza_que_el_modelo_le_puso()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy he llamado a Marta.");
        e.Respuesta = _ => Resp([Registro("Hoy he llamado a Marta.", "evento", "Llamada a Marta",
            campos: new() { ["duracion"] = "desconocido", ["personas"] = new List<object?> { "Marta" } },
            certeza: new() { ["duracion"] = "dicho", ["personas"] = "dicho", ["lugares"] = "interpretado" })]);

        var r = await e.Procesar();

        var fm = e.Registros().Single().Frontmatter;
        var certeza = Mapa(fm["certeza"]);
        Assert.False(certeza.ContainsKey("duracion")); // desconocido: sin certeza
        Assert.False(certeza.ContainsKey("lugares")); // ausente: sin certeza
        Assert.Equal("dicho", certeza["personas"]); // lo real se conserva
        Assert.Equal("desconocido", fm["duracion"]); // el dato no se rellena ni se inventa
        Assert.Single(r.RegistrosCreados);
        Assert.Empty(e.Validar());
    }

    // ---------------------------------------------------------------- C3 preguntas estructurales

    [Fact]
    public async Task C3_Solo_se_crean_preguntas_estructurales_y_se_descartan_las_de_contenido()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy reunión con Nuvia sobre el piloto. Hemos llegado a varios acuerdos. Después he estado con la incidencia del servidor de Nuvia.");
        e.Respuesta = _ => Resp([
            Registro("Hoy reunión con Nuvia sobre el piloto.", "evento", "Reunión con Nuvia sobre el piloto",
                campos: new() { ["duracion"] = "desconocido", ["resultados"] = "Hay acuerdos; no se indican" }, certeza: new() { ["resultados"] = "dicho" }),
            Registro("Después he estado con la incidencia del servidor de Nuvia.", "problema", "Incidencia del servidor de Nuvia",
                campos: new() { ["resultados"] = "desconocido" }),
        ],
            new PreguntaCandidata("resultados", "¿Quedó resuelta la incidencia del servidor de Nuvia?", "alta", RegistroIndice: 1),
            new PreguntaCandidata("duracion", "¿Cuánto duró la reunión con Nuvia?", "media", RegistroIndice: 0),
            new PreguntaCandidata("resultados", "¿Qué acuerdos tomasteis?", "alta", RegistroIndice: 0), // contenido sustantivo de una reunión
            new PreguntaCandidata("contexto", "¿Qué fallaba en el servidor?", "alta", RegistroIndice: 1)); // campo no estructural

        var r = await e.Procesar();

        Assert.Equal(2, r.PreguntasCreadas.Count);
        var textos = e.Preguntas().Select(p => (string)p.Frontmatter["texto"]!).ToList();
        Assert.Contains("¿Quedó resuelta la incidencia del servidor de Nuvia?", textos);
        Assert.Contains("¿Cuánto duró la reunión con Nuvia?", textos);
        Assert.DoesNotContain(textos, t => t.Contains("acuerdos") || t.Contains("fallaba"));
        Assert.Contains("preguntas_descartadas", e.Leer(r.LoteId!));
        Assert.All(e.Preguntas(), p => Assert.Equal("pendiente", p.Frontmatter["estado"]));
        Assert.Empty(e.Validar());
    }

    // ---------------------------------------------------------------- C4 contradicción

    [Fact]
    public async Task C4_Una_contradiccion_conserva_ambas_versiones_las_marca_y_pregunta()
    {
        using var e = new EntornoMira();
        e.Capturar("El jueves pasado comí con Carla en el centro.");
        e.Respuesta = _ => Resp([Registro("El jueves pasado comí con Carla en el centro.", "evento", "Comida con Carla", dia: "2026-11-12",
            campos: new() { ["personas"] = new List<object?> { "Carla" } }, certeza: new() { ["personas"] = "dicho" })]);
        await e.Procesar();
        var primero = e.Registros().Single();

        e.Ahora = e.Ahora.AddDays(1);
        e.Capturar("Ayer me acordé de la comida con Carla, fue el miércoles de la semana pasada.");
        e.Respuesta = s => Resp([Registro("fue el miércoles de la semana pasada.", "evento", "Comida con Carla (otra fecha)", dia: "2026-11-11",
            campos: new() { ["personas"] = new List<object?> { "Carla" } }, certeza: new() { ["personas"] = "dicho" },
            enConflictoCon: s.Relacionados.Single().Id, conflictoCampo: "fecha")]);

        var r = await e.Procesar();

        var registros = e.Registros();
        Assert.Equal(2, registros.Count);
        foreach (var reg in registros)
        {
            var c = Mapa(Lista(reg.Frontmatter, "conflictos").Single());
            Assert.Equal("fecha", c["campo"]);
            Assert.Equal("abierto", c["estado"]);
        }

        Assert.Equal("2026-11-12", Mapa(registros.Single(x => x.Id == primero.Id).Frontmatter["fecha"])["desde"]); // no se pisa
        var pregunta = Assert.Single(e.Preguntas());
        Assert.Equal("alta", pregunta.Frontmatter["valor"]);
        Assert.Equal("pendiente", pregunta.Frontmatter["estado"]);
        Assert.Contains(primero.Id!, r.RegistrosActualizados);
        Assert.Empty(e.Validar());
    }

    // ---------------------------------------------------------------- C5 coincidencia dudosa

    [Fact]
    public async Task C5_Una_coincidencia_dudosa_no_se_fusiona_y_deja_una_pregunta()
    {
        using var e = new EntornoMira();
        e.Capturar("El jueves hablé con Laura de las facturas.");
        e.Respuesta = _ => Resp([Registro("hablé con Laura de las facturas.", "evento", "Reunión con Laura sobre facturas", dia: "2026-11-12",
            campos: new() { ["personas"] = new List<object?> { "Laura" } }, certeza: new() { ["personas"] = "dicho" })]);
        await e.Procesar();
        var existente = e.Registros().Single();
        var antes = e.Leer(existente.Id!);

        e.Ahora = e.Ahora.AddDays(2);
        e.Capturar("He vuelto a hablar con Laura de las facturas.");
        e.Respuesta = s => Resp([Registro("He vuelto a hablar con Laura de las facturas.", "evento", "Hablar de nuevo con Laura de las facturas",
            campos: new() { ["personas"] = new List<object?> { "Laura" } }, certeza: new() { ["personas"] = "dicho" }, duda: s.Relacionados.Single().Id)]);

        await e.Procesar();

        Assert.Equal(2, e.Registros().Count);
        Assert.Equal(antes, e.Leer(existente.Id!)); // el existente queda intacto
        var p = Assert.Single(e.Preguntas());
        Assert.Equal("coincidencia", p.Frontmatter["campo"]);
        Assert.Equal("alta", p.Frontmatter["valor"]);
        Assert.Empty(e.Validar());
    }

    // ---------------------------------------------------------------- C6 continuación y cierre

    [Fact]
    public async Task C6_Una_continuacion_se_anade_al_mismo_registro_y_el_cierre_lo_marca_cerrado()
    {
        using var e = new EntornoMira();
        e.Ahora = new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.FromHours(1));
        e.Capturar("Hoy he empezado a pintar el pasillo, aún no he terminado.");
        e.Respuesta = _ => Resp([Registro("Hoy he empezado a pintar el pasillo, aún no he terminado.", "actividad", "Pintar el pasillo", dia: "2026-11-13",
            multidia: new MultidiaExtraido("abierto", null, null, "dicho", "interpretado"))]);
        await e.Procesar();
        var registro = e.Registros().Single();

        e.Ahora = new DateTimeOffset(2026, 11, 16, 18, 0, 0, TimeSpan.FromHours(1));
        e.Capturar("Hoy he terminado de pintar el pasillo.");
        e.Respuesta = s => Resp([Registro("Hoy he terminado de pintar el pasillo.", "actividad", "Pintar el pasillo", dia: null, amplia: s.Relacionados.Single().Id,
            multidia: new MultidiaExtraido("cerrado", "2026-11-16", "2026-11-16", "dicho", "interpretado"))]);

        var r = await e.Procesar();

        var actual = Assert.Single(e.Registros());
        Assert.Equal(registro.Id, actual.Id);
        Assert.Empty(r.RegistrosCreados);
        Assert.Equal(["origen", "ampliacion"], Lista(actual.Frontmatter, "fuentes").Select(f => (string)Mapa(f)["rol"]!).ToArray());
        Assert.Equal(2, Lista(actual.Frontmatter, "entradas").Count);
        Assert.Equal("cerrado", Mapa(actual.Frontmatter["multidia"])["estado"]);
        Assert.Contains(Lista(actual.Frontmatter, "historial"), h => (string)Mapa(h)["origen"]! == "mira" && (string)Mapa(h)["campo"]! == "multidia");
        Assert.Empty(e.Validar());
    }

    // ---------------------------------------------------------------- C7 reprocesado

    private static EntornoMira ConReunionConfirmada(out string capturaId, out string idReunion, out string idOtro)
    {
        var e = new EntornoMira();
        capturaId = e.Capturar("Hoy reunión con Nuvia. También he regado las plantas.").Id;
        var cap = capturaId;
        e.Respuesta = _ => Resp([
            Registro("Hoy reunión con Nuvia.", "evento", "Reunión con Nuvia",
                campos: new() { ["duracion"] = "desconocido", ["entidades"] = new List<object?> { new Dictionary<string, object?> { ["nombre"] = "Nuvia" } } },
                certeza: new() { ["entidades"] = "interpretado" }),
            Registro("También he regado las plantas.", "actividad", "Regar las plantas"),
        ]);
        e.Procesar().GetAwaiter().GetResult();
        var regs = e.Registros();
        idReunion = regs.Single(r => (string)r.Frontmatter["titulo"]! == "Reunión con Nuvia").Id!;
        idOtro = regs.Single(r => (string)r.Frontmatter["titulo"]! == "Regar las plantas").Id!;

        // El usuario corrige la duración y el nombre de la entidad.
        e.Escribir(idReunion, fm =>
        {
            fm["duracion"] = new Dictionary<string, object?> { ["expresion"] = "una hora y media", ["minutos"] = "90" };
            ((Dictionary<string, object?>)fm["certeza"]!)["duracion"] = "confirmado";
            fm["entidades"] = new List<object?> { new Dictionary<string, object?> { ["nombre"] = "Nuvia Labs", ["certeza"] = "confirmado" } };
            ((Dictionary<string, object?>)fm["certeza"]!).Remove("entidades");
            var h = (List<object?>)fm["historial"]!;
            h.Add(new Dictionary<string, object?> { ["fecha"] = "2026-11-16T20:00:00+01:00", ["origen"] = "usuario", ["campo"] = "duracion", ["anterior"] = "desconocido", ["nuevo"] = "90 min" });
            h.Add(new Dictionary<string, object?> { ["fecha"] = "2026-11-16T20:01:00+01:00", ["origen"] = "usuario", ["campo"] = "entidades", ["anterior"] = "Nuvia", ["nuevo"] = "Nuvia Labs" });
        });
        return e;
    }

    [Fact]
    public async Task C7_Reprocesar_respeta_lo_del_usuario_no_duplica_y_conserva_lo_no_reclamado()
    {
        using var e = ConReunionConfirmada(out var cap, out var idReunion, out var idOtro);
        Assert.Empty(e.Validar());
        var otroAntes = e.Leer(idOtro);

        // Una nueva versión reprocesa la captura: otra duración, otro nombre y solo reclama la reunión.
        e.Ahora = e.Ahora.AddDays(1);
        e.Opciones = new OpcionesProcesador(VersionMira: "0.2.0");
        e.Respuesta = s => Resp([Registro("Hoy reunión con Nuvia.", "evento", "Reunión con Nuvia Labs (nueva versión)",
            reclama: s.ClavesExistentes.Single(c => c.EndsWith("evento:1")),
            campos: new() { ["duracion"] = new Dictionary<string, object?> { ["expresion"] = "unas dos horas", ["minutos"] = "120" },
                            ["entidades"] = new List<object?> { new Dictionary<string, object?> { ["nombre"] = "Nuvia" } },
                            ["lugares"] = new List<object?> { "oficina" } },
            certeza: new() { ["duracion"] = "interpretado", ["entidades"] = "interpretado", ["lugares"] = "interpretado" })]);

        var r = await e.Procesar(reprocesar: [cap]);

        Assert.Equal(2, e.Registros().Count); // sin duplicados
        var reunion = e.Doc(idReunion);
        Assert.Equal("90", Mapa(reunion.Frontmatter["duracion"])["minutos"]); // conserva lo del usuario
        Assert.Equal("Nuvia Labs", Mapa(Lista(reunion.Frontmatter, "entidades").Single())["nombre"]);
        Assert.Equal(["oficina"], ((List<object?>)reunion.Frontmatter["lugares"]!).Cast<string>().ToArray()); // lo no tocado sí se actualiza
        Assert.Equal("Reunión con Nuvia Labs (nueva versión)", reunion.Frontmatter["titulo"]);
        var proc = Mapa(reunion.Frontmatter["procedencia"]);
        Assert.Equal("0.2.0", Mapa(proc["actualizado_por"])["version"]);
        Assert.Equal(otroAntes, e.Leer(idOtro)); // el no reclamado queda tal cual
        Assert.Contains(idReunion, r.RegistrosActualizados);
        Assert.Empty(r.RegistrosCreados);
        Assert.Empty(e.Validar());
    }

    [Fact]
    public async Task C7_Si_el_proveedor_no_reclama_la_clave_se_reconoce_por_fragmento_y_clase()
    {
        using var e = ConReunionConfirmada(out var cap, out _, out _);
        e.Respuesta = _ => Resp([
            Registro("Hoy reunión con Nuvia.", "evento", "Reunión con Nuvia"),
            Registro("También he regado las plantas.", "actividad", "Regar las plantas"),
        ]);

        var r = await e.Procesar(reprocesar: [cap]);

        Assert.Equal(2, e.Registros().Count);
        Assert.Empty(r.RegistrosCreados);
        Assert.Empty(e.Validar());
    }

    [Fact]
    public async Task Reprocesar_nunca_pierde_un_dato_conocido()
    {
        using var e = ConReunionConfirmada(out var cap, out var idReunion, out _);
        e.Respuesta = s => Resp([Registro("Hoy reunión con Nuvia.", "evento", "Reunión con Nuvia",
            campos: new() { ["duracion"] = "desconocido" })]);
        await e.Procesar(reprocesar: [cap]);
        Assert.Equal("90", Mapa(e.Doc(idReunion).Frontmatter["duracion"])["minutos"]);
    }

    // ---------------------------------------------------------------- C8 sin hechos

    [Fact]
    public async Task C8_Una_captura_sin_hechos_no_crea_registros_y_no_se_reprocesa()
    {
        using var e = new EntornoMira();
        var cap = e.Capturar("Probando, probando. ¿Se oye bien?");
        var antes = File.ReadAllBytes(cap.Ruta);

        var r = await e.Procesar();

        Assert.Empty(e.Registros());
        Assert.Empty(e.Preguntas());
        Assert.Equal([cap.Id], r.Procesadas);
        Assert.Equal(antes, File.ReadAllBytes(cap.Ruta));
        Assert.Contains("sin hechos registrables", e.Leer(r.LoteId!));
        Assert.Empty(e.Validar());

        await e.Procesar();
        Assert.Single(e.Proveedor.Solicitudes); // no se vuelve a procesar
    }

    // ---------------------------------------------------------------- C9 privacidad

    private sealed class ProveedorDeFuera : IProveedorIA
    {
        public int Llamadas { get; private set; }

        public string Nombre => "anthropic";

        public string Modelo => "claude-haiku-5-5";

        public bool EnviaDatosAFuera => true;

        public Task<RespuestaExtraccion> ExtraerAsync(SolicitudExtraccion s, CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult(RespuestaExtraccion.SinHechos);
        }
    }

    private static Configuracion Config(bool declarada = true, string verificada = "2026-11-01") => Configuracion.Leer($"""
        datos: /x
        proveedor:
          nombre: anthropic
          modelo: claude-haiku-5-5
          garantia_no_reutilizacion:
            declarada: {declarada.ToString().ToLowerInvariant()}
            verificada_el: {verificada}
            fuente: https://ejemplo.test/condiciones
            caducidad_dias: 180
        """);

    [Fact]
    public async Task C9_Sin_garantia_declarada_no_se_envia_nada_y_las_capturas_siguen_pendientes()
    {
        using var e = new EntornoMira();
        e.Capturar("Nota privada.");
        var fuera = new ProveedorDeFuera();

        var r = await e.Procesar(configuracion: Config(declarada: false), proveedor: fuera);

        Assert.True(r.Detenida);
        Assert.Equal(0, fuera.Llamadas);
        Assert.Empty(r.Procesadas);
        Assert.Contains(r.Problemas, p => p.Motivo.Contains("garantía"));
        var lote = e.Leer(r.LoteId!);
        Assert.Contains("proveedor: anthropic", lote);
        Assert.Contains("problemas", lote);
        Assert.Equal(1, (await e.Procesar(configuracion: Config(declarada: false), proveedor: fuera)).Pendientes);
    }

    [Fact]
    public void C9_La_verificacion_caducada_o_la_clave_ausente_tambien_bloquean()
    {
        var hoy = new DateOnly(2026, 11, 16);
        var p = new ProveedorDeFuera();
        Func<string, string?> conClave = _ => "clave";
        Func<string, string?> sinClave = _ => null;

        Assert.Empty(ComprobadorDePrivacidad.Comprobar(p, Config(), hoy, conClave));
        Assert.Contains(ComprobadorDePrivacidad.Comprobar(p, Config(verificada: "2026-01-01"), hoy, conClave), m => m.Contains("más de 180 días"));
        Assert.Contains(ComprobadorDePrivacidad.Comprobar(p, Config(), hoy, sinClave), m => m.Contains("ANTHROPIC_API_KEY"));
    }

    [Fact]
    public void La_clave_de_api_no_aparece_en_los_motivos()
    {
        var motivos = ComprobadorDePrivacidad.Comprobar(new ProveedorDeFuera(), Config(declarada: false), new DateOnly(2026, 11, 16), _ => "sk-secreta-123");
        Assert.DoesNotContain(motivos, m => m.Contains("sk-secreta-123"));
    }

    [Fact]
    public async Task El_proveedor_simulado_no_exige_garantia()
    {
        using var e = new EntornoMira();
        e.Capturar("Nota.");
        var r = await e.Procesar(configuracion: Config(declarada: false));
        Assert.False(r.Detenida);
    }

    // ---------------------------------------------------------------- C10 / C11 preguntas

    private static PreguntaCandidata P(int registro, string campo, string valor, string texto = "¿…?") => new(campo, texto, valor, RegistroIndice: registro);

    [Fact]
    public async Task C10_Solo_se_crean_las_cinco_preguntas_de_mayor_valor()
    {
        using var e = new EntornoMira();
        e.Capturar("A uno. B dos. C tres. D cuatro.");
        e.Respuesta = _ => Resp([
            Registro("A uno.", "actividad", "A"), Registro("B dos.", "actividad", "B"), Registro("C tres.", "actividad", "C"), Registro("D cuatro.", "actividad", "D"),
        ],
            P(0, "duracion", "baja"), P(0, "lugares", "baja"), P(1, "duracion", "media"), P(1, "personas", "media"),
            P(2, "duracion", "alta"), P(2, "hora", "alta"), P(3, "duracion", "media"), P(3, "proyectos", "baja"));

        var r = await e.Procesar();

        Assert.Equal(5, r.PreguntasCreadas.Count);
        Assert.Equal(3, e.Leer(r.LoteId!).Split("preguntas_descartadas: ")[1].TakeWhile(char.IsDigit).Aggregate(0, (a, c) => a * 10 + (c - '0')));
        var valores = e.Preguntas().Select(p => (string)p.Frontmatter["valor"]!).Order().ToList();
        Assert.Equal(["alta", "alta", "media", "media", "media"], valores);
    }

    [Fact]
    public async Task C10_Una_pregunta_alta_desplaza_a_una_pendiente_de_menor_valor()
    {
        using var e = new EntornoMira { Opciones = new OpcionesProcesador(MaximoPendientes: 2) };
        e.Capturar("Primera nota. Con más cosas.");
        e.Respuesta = _ => Resp([Registro("Primera nota.", "actividad", "Primera"), Registro("Con más cosas.", "actividad", "Segunda")],
            P(0, "duracion", "baja"), P(1, "duracion", "baja"));
        await e.Procesar();
        Assert.Equal(2, e.Preguntas().Count(p => (string)p.Frontmatter["estado"]! == "pendiente"));

        e.Ahora = e.Ahora.AddDays(1);
        e.Capturar("Tercera nota importante.");
        e.Respuesta = _ => Resp([Registro("Tercera nota importante.", "problema", "Tercera")], P(0, "resultados", "alta", "¿Quedó resuelto?"));
        var r = await e.Procesar();

        Assert.Single(r.PreguntasCerradasPorLimite);
        var estados = e.Preguntas().GroupBy(p => (string)p.Frontmatter["estado"]!).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(2, estados["pendiente"]);
        Assert.Equal(1, estados["cerrada_por_limite"]);
        Assert.Contains(e.Preguntas(), p => (string)p.Frontmatter["valor"]! == "alta" && (string)p.Frontmatter["estado"]! == "pendiente");
        Assert.Empty(e.Validar());
    }

    [Fact]
    public async Task C11_Tras_no_relevante_no_se_repite_ni_se_formula_otra_equivalente()
    {
        using var e = new EntornoMira();
        e.Capturar("Se me ha ocurrido usar cron.");
        e.Respuesta = _ => Resp([Registro("Se me ha ocurrido usar cron.", "idea", "Usar cron")], P(0, "proyectos", "baja", "¿A qué proyecto pertenece?"));
        await e.Procesar();
        var pregunta = e.Preguntas().Single();
        e.Escribir(pregunta.Id!, fm =>
        {
            fm["estado"] = "no_relevante";
            fm["cerrada_el"] = "2026-11-16T10:00:00+01:00";
            fm["respuesta"] = new Dictionary<string, object?> { ["texto"] = "No es relevante.", ["respondida_el"] = "2026-11-16T10:00:00+01:00" };
        });

        e.Ahora = e.Ahora.AddDays(1);
        e.Capturar("Otra idea suelta.");
        e.Respuesta = _ => Resp([Registro("Otra idea suelta.", "idea", "Otra idea")], P(0, "proyectos", "baja", "¿Y esta, de qué proyecto?"));
        var r = await e.Procesar();

        Assert.Empty(r.PreguntasCreadas); // misma clase y mismo campo: equivalente
        Assert.Single(e.Preguntas());
    }

    [Fact]
    public async Task Tras_no_lo_recuerdo_no_se_vuelve_a_preguntar_el_mismo_dato()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy llamada con Rosa.");
        e.Respuesta = _ => Resp([Registro("Hoy llamada con Rosa.", "evento", "Llamada con Rosa")], P(0, "duracion", "media"));
        await e.Procesar();
        var q = e.Preguntas().Single();
        e.Escribir(q.Id!, fm =>
        {
            fm["estado"] = "no_recuerda";
            fm["respuesta"] = new Dictionary<string, object?> { ["texto"] = "No lo recuerdo.", ["respondida_el"] = "2026-11-16T10:00:00+01:00" };
        });

        var id = e.Registros().Single().Id!;
        e.Ahora = e.Ahora.AddDays(1);
        e.Capturar("Sigo con lo de Rosa.");
        e.Respuesta = s => Resp([Registro("Sigo con lo de Rosa.", "actividad", "Seguir con Rosa", amplia: id)], new PreguntaCandidata("duracion", "¿Cuánto duró?", "media", RegistroId: id));
        var r = await e.Procesar();

        Assert.Empty(r.PreguntasCreadas);
    }

    // ---------------------------------------------------------------- salidas inválidas y fallos

    [Fact]
    public async Task Un_fragmento_que_no_esta_en_la_captura_deja_la_captura_pendiente_sin_escribir()
    {
        using var e = new EntornoMira();
        e.Capturar("Texto real.");
        e.Respuesta = _ => Resp([Registro("Texto inventado.", "actividad", "X")]);

        var r = await e.Procesar();

        Assert.Empty(e.Registros());
        Assert.Contains("fragmento", r.Problemas.Single().Motivo);
        Assert.Equal(1, (await e.Procesar()).Pendientes);
    }

    [Fact]
    public async Task Una_referencia_a_un_registro_no_ofrecido_se_rechaza()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy lo de siempre.");
        e.Respuesta = _ => Resp([Registro("Hoy lo de siempre.", "actividad", "Lo de siempre", amplia: "reg-20261101-zzzz")]);

        var r = await e.Procesar();

        Assert.Empty(e.Registros());
        Assert.Contains("no se ofreció", r.Problemas.Single().Motivo);
    }

    [Fact]
    public async Task Un_fallo_del_proveedor_en_una_captura_no_impide_procesar_las_demas_ni_filtra_contenido()
    {
        using var e = new EntornoMira();
        e.Capturar("Primera nota secreta.", e.Ahora);
        e.Capturar("Segunda nota.", e.Ahora.AddMinutes(1));
        e.Respuesta = s => s.Texto.Contains("secreta")
            ? throw new InvalidOperationException("fallo con contenido: Primera nota secreta.")
            : Resp([Registro("Segunda nota.", "actividad", "Segunda")]);

        var r = await e.Procesar();

        Assert.Single(r.Procesadas);
        var problema = Assert.Single(r.Problemas);
        Assert.DoesNotContain("secreta", problema.Motivo);
        Assert.Single(e.Registros());
        Assert.Equal(1, (await e.Procesar()).Pendientes);
    }

    [Fact]
    public async Task Las_capturas_se_procesan_en_orden_cronologico()
    {
        using var e = new EntornoMira();
        e.Capturar("Segunda en el tiempo.", e.Ahora.AddHours(2));
        e.Capturar("Primera en el tiempo.", e.Ahora);
        e.Respuesta = _ => RespuestaExtraccion.SinHechos;

        await e.Procesar();

        Assert.Equal(["Primera en el tiempo.", "Segunda en el tiempo."], e.Proveedor.Solicitudes.Select(s => s.Texto).ToArray());
    }

    // ---------------------------------------------------------------- recuperación

    [Fact]
    public async Task Tras_una_interrupcion_antes_del_lote_se_reintenta_sin_duplicar_registros()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy he hecho A. Luego B.");
        e.Respuesta = _ => Resp([Registro("Hoy he hecho A.", "actividad", "A"), Registro("Luego B.", "actividad", "B")]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => e.Procesar(hito: h =>
        {
            if (h == "antes_del_lote")
            {
                throw new InvalidOperationException("caída simulada");
            }
        }));

        Assert.Equal(2, e.Registros().Count); // los registros se guardaron
        Assert.Empty(e.Cargar().DeTipo("lote")); // pero la captura no figura en ningún lote

        var antes = e.Registros().Select(d => d.Id!).Order().ToList();
        var r = await e.Procesar(); // reintento, el proveedor no reclama las claves
        Assert.Equal(antes, e.Registros().Select(d => d.Id!).Order().ToList()); // mismos registros, sin duplicar
        Assert.Equal(antes, r.RegistrosCreados.Order().ToList()); // el lote adopta los huérfanos como creados
        Assert.Empty(r.RegistrosActualizados);
        Assert.Single(e.Cargar().DeTipo("lote"));
        Assert.Empty(e.Validar());
    }

    [Fact]
    public async Task Una_ejecucion_sin_capturas_pendientes_no_escribe_nada()
    {
        using var e = new EntornoMira();
        var r = await e.Procesar();
        Assert.Null(r.LoteId);
        Assert.Empty(Directory.GetFiles(e.Datos, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task El_lote_registra_proveedor_y_modelo_y_no_contiene_la_clave()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy nada.");
        var r = await e.Procesar();
        var lote = e.Leer(r.LoteId!);
        Assert.Contains("proveedor: simulado", lote);
        Assert.Contains("simulado-1", lote);
        Assert.DoesNotContain("sk-", lote);
    }

    // ---------------------------------------------------------------- relacionados

    [Fact]
    public async Task Solo_se_ofrecen_registros_recientes_que_comparten_algo_o_actividades_abiertas()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy llamé a Marta. Pinto el pasillo, sigo mañana. Fui al gimnasio.");
        e.Respuesta = _ => Resp([
            Registro("Hoy llamé a Marta.", "evento", "Llamada a Marta", campos: new() { ["personas"] = new List<object?> { "Marta" } }, certeza: new() { ["personas"] = "dicho" }),
            Registro("Pinto el pasillo, sigo mañana.", "actividad", "Pintar el pasillo", multidia: new MultidiaExtraido("abierto", null, null, "dicho", "interpretado")),
            Registro("Fui al gimnasio.", "actividad", "Gimnasio"),
        ]);
        await e.Procesar();

        e.Ahora = e.Ahora.AddDays(1);
        e.Capturar("Ayer hablé con MARTA otra vez.");
        e.Respuesta = _ => RespuestaExtraccion.SinHechos;
        await e.Procesar();

        var ofrecidos = e.Proveedor.Solicitudes.Last().Relacionados.Select(r => r.Titulo).Order().ToList();
        Assert.Equal(["Llamada a Marta", "Pintar el pasillo"], ofrecidos); // «Gimnasio» no comparte nada ni está abierto
    }

    [Fact]
    public async Task No_se_ofrecen_registros_fuera_de_la_ventana()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy llamé a Marta.");
        e.Respuesta = _ => Resp([Registro("Hoy llamé a Marta.", "evento", "Llamada a Marta", campos: new() { ["personas"] = new List<object?> { "Marta" } }, certeza: new() { ["personas"] = "dicho" })]);
        await e.Procesar();

        e.Ahora = e.Ahora.AddDays(30);
        e.Capturar("Hoy otra vez Marta.");
        e.Respuesta = _ => RespuestaExtraccion.SinHechos;
        await e.Procesar();

        Assert.Empty(e.Proveedor.Solicitudes.Last().Relacionados);
    }
}

public class BloqueoTests
{
    [Fact]
    public void Una_segunda_ejecucion_no_obtiene_el_bloqueo_y_se_libera_al_terminar()
    {
        var datos = Path.Combine(Path.GetTempPath(), "orumi-bloq-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            using (var primero = BloqueoDeEjecucion.Adquirir(datos))
            {
                Assert.NotNull(primero);
                Assert.Null(BloqueoDeEjecucion.Adquirir(datos));
            }

            using var otra = BloqueoDeEjecucion.Adquirir(datos);
            Assert.NotNull(otra); // liberado sin intervención manual
        }
        finally
        {
            Directory.Delete(datos, true);
        }
    }
}
