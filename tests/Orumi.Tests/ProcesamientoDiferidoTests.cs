using System.Text.Json;
using Orumi.Agente;
using Orumi.Agente.Proveedores;
using Orumi.Cli;
using Orumi.Core.Ficheros;

namespace Orumi.Tests;

public class ProcesamientoDiferidoTests
{
    private static string J(string s) => JsonSerializer.Serialize(s);

    private static string Json(string fragmento, string titulo, string clase = "actividad") => JsonSerializer.Serialize(new
    {
        registros = new[]
        {
            new
            {
                fragmento,
                clase,
                titulo,
                descripcion = titulo,
                campos = new { fecha = new { expresion = "hoy", desde = "2026-11-16", hasta = "2026-11-16" } },
                certeza = new { fecha = "interpretado" },
            },
        },
        preguntas = Array.Empty<object>(),
    });

    /// <summary>Un resultado correcto por captura: un registro cuyo fragmento es todo el texto de la nota.</summary>
    private static ResultadoLote Correcto(SolicitudExtraccion s, long entrada = 1000, long salida = 500) =>
        new(s.CapturaId, TipoResultado.Correcto, Json(s.Texto, s.Texto.TrimEnd('.')), new UsoDeTokens(entrada, salida));

    private static ProveedorPorLotesSimulado Simulado(int consultas = 0) => new(s => Correcto(s), consultas);

    private static Dictionary<string, object?> Mapa(object? o) => (Dictionary<string, object?>)o!;

    private static List<object?> Lista(object? o) => (List<object?>)o!;

    // ---------------------------------------------------------------- envío y recogida

    [Fact]
    public async Task Las_pendientes_se_envian_como_un_unico_lote_y_se_recogen_con_la_misma_validacion()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy llamé a Marta.", e.Ahora);
        e.Capturar("Hoy comí con Pau.", e.Ahora.AddMinutes(1));
        var lotes = Simulado();

        var r = await e.ProcesarPorLotes(lotes);

        var envio = Assert.Single(lotes.Envios);
        Assert.Equal(2, envio.Count); // un único lote con las dos capturas
        Assert.Equal(2, r.Procesadas.Count);
        Assert.Equal(2, e.Registros().Count);
        Assert.Single(r.Envios);
        Assert.Equal(0, r.EnviosAbiertos);

        var lote = e.Doc(r.LoteId!).Frontmatter;
        Assert.Equal("lotes", lote["modo"]);
        Assert.Equal(r.Envios, Lista(lote["envios"]).Cast<string>().ToList());
        var doc = e.Doc(r.Envios[0]).Frontmatter;
        Assert.Equal("recogido", doc["estado"]);
        Assert.Equal(r.LoteId, doc["lote"]);
        Assert.Empty(e.Validar()); // incluida la validación del fichero de envío
    }

    [Fact]
    public async Task Sin_pendientes_no_se_envia_nada()
    {
        using var e = new EntornoMira();
        var lotes = Simulado();

        var r = await e.ProcesarPorLotes(lotes);

        Assert.Empty(lotes.Envios);
        Assert.Null(r.LoteId);
        Assert.Empty(Directory.GetFiles(e.Datos, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Un_envio_abierto_no_se_reenvia_y_se_recoge_en_la_siguiente_ejecucion()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy llamé a Marta.");
        var lotes = Simulado(consultas: 6); // la 1.ª ejecución consulta 5 veces, la 2.ª una más (6) y solo la 3.ª lo encuentra terminado

        var r1 = await e.ProcesarPorLotes(lotes);
        Assert.Equal(1, r1.EnviosAbiertos);
        Assert.Empty(r1.Procesadas);
        Assert.Empty(e.Registros());
        var envio = e.Doc(r1.Envios[0]);
        Assert.Equal("abierto", envio.Frontmatter["estado"]);

        // Otra ejecución mientras sigue en curso: no se vuelve a enviar esa captura.
        e.Ahora = e.Ahora.AddMinutes(10);
        var r2 = await e.ProcesarPorLotes(lotes, espera: TimeSpan.Zero);
        Assert.Single(lotes.Envios);

        // Cuando el proveedor termina, se recoge.
        e.Ahora = e.Ahora.AddHours(1);
        var r3 = await e.ProcesarPorLotes(lotes);
        Assert.Single(lotes.Envios);
        Assert.Single(r3.Procesadas);
        Assert.Single(e.Registros());
        Assert.Equal("recogido", e.Doc(envio.Id!).Frontmatter["estado"]);
        Assert.Empty(e.Validar());
        _ = r2;
    }

    [Fact]
    public async Task Tras_una_interrupcion_despues_de_enviar_la_siguiente_ejecucion_recoge_sin_reenviar()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy llamé a Marta.");
        var lotes = Simulado();

        await Assert.ThrowsAsync<InvalidOperationException>(() => e.ProcesarPorLotes(lotes, hito: h =>
        {
            if (h == "enviado")
            {
                throw new InvalidOperationException("caída simulada");
            }
        }));
        Assert.Single(lotes.Envios);
        Assert.Equal("abierto", e.Cargar().DeTipo("envio").Single().Frontmatter["estado"]);

        e.Ahora = e.Ahora.AddMinutes(5);
        var r = await e.ProcesarPorLotes(lotes);

        Assert.Single(lotes.Envios); // no se reenvía
        Assert.Single(r.Procesadas);
        Assert.Single(e.Registros());
        Assert.Empty(e.Validar());
    }

    [Fact]
    public async Task Tras_una_interrupcion_antes_de_escribir_el_lote_se_recoge_de_nuevo_sin_duplicar()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy llamé a Marta.");
        var lotes = Simulado();

        await Assert.ThrowsAsync<InvalidOperationException>(() => e.ProcesarPorLotes(lotes, hito: h =>
        {
            if (h == "antes_del_lote")
            {
                throw new InvalidOperationException("caída simulada");
            }
        }));
        Assert.Single(e.Registros()); // los registros ya se guardaron, pero no hay lote ni envío marcado
        Assert.Empty(e.Cargar().DeTipo("lote"));

        e.Ahora = e.Ahora.AddMinutes(5);
        var r = await e.ProcesarPorLotes(lotes);

        Assert.Single(e.Registros()); // sin duplicados
        Assert.Single(r.Procesadas);
        Assert.Equal("recogido", e.Cargar().DeTipo("envio").Single().Frontmatter["estado"]);
        Assert.Empty(e.Validar());
    }

    // ---------------------------------------------------------------- errores por captura

    [Fact]
    public async Task Un_error_una_cancelacion_o_una_expiracion_dejan_pendiente_solo_esa_captura()
    {
        using var e = new EntornoMira();
        e.Capturar("Buena nota.", e.Ahora);
        e.Capturar("Nota con error.", e.Ahora.AddMinutes(1));
        e.Capturar("Nota cancelada.", e.Ahora.AddMinutes(2));
        e.Capturar("Nota expirada.", e.Ahora.AddMinutes(3));
        var lotes = new ProveedorPorLotesSimulado(s => s.Texto switch
        {
            "Nota con error." => new ResultadoLote(s.CapturaId, TipoResultado.Error, Detalle: "api_error"),
            "Nota cancelada." => new ResultadoLote(s.CapturaId, TipoResultado.Cancelado),
            "Nota expirada." => new ResultadoLote(s.CapturaId, TipoResultado.Expirado),
            _ => Correcto(s),
        });

        var r = await e.ProcesarPorLotes(lotes);

        Assert.Single(r.Procesadas);
        Assert.Equal(3, r.Problemas.Count);
        Assert.Contains(r.Problemas, p => p.Motivo.Contains("error"));
        Assert.Contains(r.Problemas, p => p.Motivo.Contains("canceló"));
        Assert.Contains(r.Problemas, p => p.Motivo.Contains("expiró"));
        Assert.Equal("recogido", e.Cargar().DeTipo("envio").Single().Frontmatter["estado"]);
        Assert.Equal(3, (await e.ProcesarPorLotes(Simulado())).Procesadas.Count); // al reintentar, las tres pendientes se envían de nuevo
    }

    [Fact]
    public async Task Una_salida_no_valida_no_guarda_nada_queda_pendiente_y_su_consumo_cuenta()
    {
        using var e = new EntornoMira { Opciones = new OpcionesProcesador(Precios: new Precios(2m, 10m)) };
        e.Capturar("Texto real.");
        var lotes = new ProveedorPorLotesSimulado(s => new ResultadoLote(s.CapturaId, TipoResultado.Correcto, Json("Texto inventado.", "X"), new UsoDeTokens(2000, 1000)));

        var r = await e.ProcesarPorLotes(lotes);

        Assert.Empty(r.Procesadas);
        Assert.Empty(e.Registros());
        Assert.Contains("fragmento", Assert.Single(r.Problemas).Motivo);
        Assert.Equal("2000", Mapa(e.Doc(r.LoteId!).Frontmatter["uso"])["tokens_entrada"]);
    }

    [Fact]
    public async Task Una_respuesta_que_no_es_json_se_rechaza_indicando_el_campo_sin_contenido()
    {
        using var e = new EntornoMira();
        e.Capturar("Nota secreta.");
        var lotes = new ProveedorPorLotesSimulado(s => new ResultadoLote(s.CapturaId, TipoResultado.Correcto, "esto no es json", new UsoDeTokens(10, 10)));

        var r = await e.ProcesarPorLotes(lotes);

        Assert.Contains("formato esperado", Assert.Single(r.Problemas).Motivo);
        Assert.DoesNotContain("secreta", e.Leer(r.LoteId!));
    }

    // ---------------------------------------------------------------- saldo, límite, clave y privacidad

    [Fact]
    public async Task Un_saldo_agotado_al_enviar_no_crea_el_envio_y_deja_las_capturas_pendientes()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy llamé a Marta.");
        var lotes = Simulado();
        lotes.ErrorAlEnviar = new ProveedorException(CategoriaError.SaldoAgotado, "x");

        var r = await e.ProcesarPorLotes(lotes);

        Assert.Equal(CategoriaError.SaldoAgotado, r.Fatal);
        Assert.Empty(e.Cargar().DeTipo("envio"));
        Assert.Contains("tipo: saldo_agotado", e.Leer(r.LoteId!));
        Assert.Equal(1, ServicioDeEstado.Calcular(e.Datos, null).Pendientes);
    }

    [Fact]
    public async Task Un_error_de_saldo_al_recoger_corta_y_deja_el_envio_abierto_para_reintentarlo()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy llamé a Marta.");
        var lotes = new ProveedorPorLotesSimulado(s => new ResultadoLote(s.CapturaId, TipoResultado.Error, Categoria: CategoriaError.SaldoAgotado));

        var r = await e.ProcesarPorLotes(lotes);

        Assert.Equal(CategoriaError.SaldoAgotado, r.Fatal);
        Assert.Equal("abierto", e.Cargar().DeTipo("envio").Single().Frontmatter["estado"]);
        Assert.Empty(e.Registros());
    }

    private sealed class LotesDeFuera : IProveedorPorLotes
    {
        public int Envios { get; private set; }

        public string Nombre => "anthropic";

        public string Modelo => "claude-sonnet-5-5";

        public bool EnviaDatosAFuera => true;

        public Task<string> EnviarAsync(IReadOnlyList<(string Id, SolicitudExtraccion Solicitud)> p, CancellationToken ct)
        {
            Envios++;
            return Task.FromResult("msgbatch_x");
        }

        public Task<EstadoLote> ConsultarAsync(string id, CancellationToken ct) => Task.FromResult(new EstadoLote(true));

        public Task<IReadOnlyList<ResultadoLote>> RecogerAsync(string id, CancellationToken ct) => Task.FromResult<IReadOnlyList<ResultadoLote>>([]);
    }

    [Fact]
    public async Task Sin_garantia_de_privacidad_no_se_envia_nada_tambien_por_lotes()
    {
        using var e = new EntornoMira();
        e.Capturar("Nota privada.");
        var fuera = new LotesDeFuera();
        var config = Configuracion.Leer("proveedor:\n  nombre: anthropic\n  modelo: claude-sonnet-5-5\n");

        var r = await e.ProcesarPorLotes(fuera, configuracion: config);

        Assert.True(r.Detenida);
        Assert.Equal(0, fuera.Envios);
        Assert.Empty(e.Cargar().DeTipo("envio"));
        Assert.Contains("proveedor: anthropic", e.Leer(r.LoteId!));
    }

    // ---------------------------------------------------------------- coste con descuento

    [Fact]
    public async Task El_coste_estimado_del_lote_diferido_aplica_el_descuento()
    {
        using var e = new EntornoMira { Opciones = new OpcionesProcesador(Precios: new Precios(2m, 10m)) }; // factor de lote 0,5 por defecto
        e.Capturar("Hoy llamé a Marta.");
        var r = await e.ProcesarPorLotes(Simulado());

        var captura = Mapa(Lista(e.Doc(r.LoteId!).Frontmatter["capturas"]).Single());
        Assert.Equal("0.003500", captura["coste_estimado_usd"]); // (1000·2 + 500·10) / 1M · 0,5
        Assert.Equal("0.003500", Mapa(e.Doc(r.LoteId!).Frontmatter["uso"])["coste_estimado_usd"]);

        // En modo inmediato el mismo uso cuesta el doble.
        using var i = new EntornoMira { Opciones = new OpcionesProcesador(Precios: new Precios(2m, 10m)) };
        i.Capturar("Hoy llamé a Marta.");
        i.Respuesta = s => Datos.Resp([Datos.Registro(s.Texto, "actividad", "Llamada")]) with { Uso = new UsoDeTokens(1000, 500) };
        var ri = await i.Procesar();
        Assert.Equal("0.007000", Mapa(i.Doc(ri.LoteId!).Frontmatter["uso"])["coste_estimado_usd"]);
        Assert.Equal("inmediato", i.Doc(ri.LoteId!).Frontmatter["modo"]);
    }

    // ---------------------------------------------------------------- validación del fichero de envío

    [Fact]
    public async Task El_validador_detecta_un_envio_mal_formado()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy llamé a Marta.");
        var r = await e.ProcesarPorLotes(Simulado(consultas: 1000));
        var id = r.Envios[0];

        e.Escribir(id, fm =>
        {
            fm["estado"] = "casi";
            fm["lote_proveedor"] = "";
            ((Dictionary<string, object?>)Lista(fm["capturas"])[0]!)["captura"] = "cap-20991231-0000-zzz";
        });

        var mensajes = e.Validar().Select(i => i.Mensaje).ToList();
        Assert.Contains(mensajes, m => m.Contains("estado inválido"));
        Assert.Contains(mensajes, m => m.Contains("no indica el lote del proveedor"));
        Assert.Contains(mensajes, m => m.Contains("captura inexistente"));
    }

    [Fact]
    public async Task Un_envio_recogido_sin_lote_se_detecta()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy llamé a Marta.");
        var r = await e.ProcesarPorLotes(Simulado(consultas: 1000));
        e.Escribir(r.Envios[0], fm =>
        {
            fm["estado"] = "recogido";
            fm["recogido_el"] = "2026-11-17T03:00:00+01:00";
            fm["lote"] = "lote-20991231-0000";
        });

        Assert.Contains(e.Validar(), i => i.Mensaje.Contains("recogido sin lote existente"));
    }

    // ---------------------------------------------------------------- estado y configuración

    [Fact]
    public async Task El_estado_distingue_las_capturas_en_un_envio_abierto_de_las_pendientes()
    {
        using var e = new EntornoMira();
        e.Capturar("Primera nota.", e.Ahora);
        e.Capturar("Segunda nota.", e.Ahora.AddMinutes(1));
        await e.ProcesarPorLotes(Simulado(consultas: 1000));
        e.Capturar("Tercera nota, aún sin enviar.", e.Ahora.AddDays(1));

        var estado = ServicioDeEstado.Calcular(e.Datos, null);

        Assert.Equal(1, estado.EnviosAbiertos);
        Assert.Equal(2, estado.CapturasEnEnvio);
        Assert.Equal(1, estado.Pendientes);

        var salida = new StringWriter();
        new Comandos(() => new MemoryStream(), salida, new StringWriter(), () => e.Ahora, _ => null).Ejecutar(["estado", "--datos", e.Datos]);
        Assert.Contains("1 envío(s) abierto(s)", salida.ToString());
        Assert.Contains("2 captura(s)", salida.ToString());
    }

    [Fact]
    public void La_configuracion_lee_el_modo_la_espera_y_el_factor_de_lote()
    {
        var c = Configuracion.Leer("procesamiento:\n  modo: lotes\n  espera_maxima_minutos: 45\n  intervalo_segundos: 30\nprecios:\n  entrada_por_millon_usd: 2\n  salida_por_millon_usd: 10\n  factor_lote: 0.5\n");
        Assert.Equal(new Procesamiento("lotes", 45, 30), c.Procesamiento);
        Assert.True(c.Procesamiento!.PorLotes);
        Assert.Equal(0.5m, c.Precios!.FactorLote);

        var sin = Configuracion.Leer("proveedor:\n  nombre: x\n");
        Assert.False(sin.Procesamiento!.PorLotes); // por defecto, inmediato
        Assert.Equal(90, sin.Procesamiento.EsperaMaximaMinutos);
    }

    // ---------------------------------------------------------------- línea de comandos

    private (int Codigo, string Salida, string Error) Cli(EntornoMira e, Func<IProveedorIA?>? inmediato, Func<IProveedorPorLotes?>? lotes, string config, params string[] args)
    {
        var s = new StringWriter();
        var er = new StringWriter();
        var codigo = new Comandos(() => new MemoryStream(), s, er, () => e.Ahora, _ => null, inmediato, lotes)
            .Ejecutar(["procesar", "--datos", e.Datos, "--config", config, .. args]);
        return (codigo, s.ToString(), er.ToString());
    }

    private static string ConfigLotes()
    {
        var f = Path.Combine(Path.GetTempPath(), "orumi-lotes-" + Guid.NewGuid().ToString("N")[..6] + ".yaml");
        File.WriteAllText(f, "proveedor:\n  nombre: simulado\nprocesamiento:\n  modo: lotes\n  espera_maxima_minutos: 0\n  intervalo_segundos: 1\n");
        return f;
    }

    [Fact]
    public void Procesar_por_lotes_desde_la_linea_de_comandos_usa_el_proveedor_por_lotes()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy llamé a Marta.");
        var lotes = Simulado();
        var config = ConfigLotes();
        try
        {
            var (codigo, salida, _) = Cli(e, null, () => lotes, config);

            Assert.Equal(0, codigo);
            Assert.Contains("Modo por lotes (con descuento): 1 envío(s) creado(s)", salida);
            Assert.Single(lotes.Envios);
            Assert.Single(e.Registros());
        }
        finally
        {
            File.Delete(config);
        }
    }

    [Fact]
    public void Con_inmediato_se_usa_el_proveedor_normal_aunque_la_configuracion_diga_lotes()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy llamé a Marta.");
        var lotes = Simulado();
        var inmediato = new ProveedorSimulado(s => Datos.Resp([Datos.Registro(s.Texto, "actividad", "Llamada")]));
        var config = ConfigLotes();
        try
        {
            var (codigo, salida, _) = Cli(e, () => inmediato, () => lotes, config, "--inmediato", "si");

            Assert.Equal(0, codigo);
            Assert.DoesNotContain("Modo por lotes", salida);
            Assert.Empty(lotes.Envios);
            Assert.Single(inmediato.Solicitudes);
        }
        finally
        {
            File.Delete(config);
        }
    }

    [Fact]
    public void Procesar_por_lotes_con_saldo_agotado_termina_con_el_codigo_7()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy llamé a Marta.");
        var lotes = Simulado();
        lotes.ErrorAlEnviar = new ProveedorException(CategoriaError.SaldoAgotado, "x");
        var config = ConfigLotes();
        try
        {
            var (codigo, _, error) = Cli(e, null, () => lotes, config);
            Assert.Equal(7, codigo);
            Assert.Contains("Saldo de créditos agotado", error);
        }
        finally
        {
            File.Delete(config);
        }
    }
}
