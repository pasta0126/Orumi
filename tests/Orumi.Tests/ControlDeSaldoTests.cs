using System.Text;
using Orumi.Agente;
using Orumi.Agente.Proveedores;
using Orumi.Cli;
using static Orumi.Tests.Datos;

namespace Orumi.Tests;

public class ControlDeSaldoTests
{
    private static Dictionary<string, object?> Mapa(object? o) => (Dictionary<string, object?>)o!;

    private static List<object?> Lista(object? o) => (List<object?>)o!;

    private static string UnicoLote(EntornoMira e) => e.Cargar().DeTipo("lote").OrderBy(l => l.Id).Last().Id!;

    // ---------------------------------------------------------------- clasificación

    [Theory]
    [InlineData(402, null, null, null, CategoriaError.SaldoAgotado)]
    [InlineData(400, "invalid_request_error", null, "Your credit balance is too low to access the Anthropic API. Please go to Plans & Billing to upgrade or purchase credits.", CategoriaError.SaldoAgotado)]
    [InlineData(400, "billing_error", null, "Problema de facturación", CategoriaError.SaldoAgotado)]
    [InlineData(429, "rate_limit_error", "enforced_spend_limit_reached", "You have reached your API usage limits: your organization has crossed its monthly API usage threshold", CategoriaError.LimiteDeGasto)]
    [InlineData(400, "invalid_request_error", null, "You have reached your specified API usage limits. You will regain access on 2026-12-01 at 00:00 UTC.", CategoriaError.LimiteDeGasto)]
    [InlineData(400, "invalid_request_error", null, "You have reached your specified workspace API usage limits.", CategoriaError.LimiteDeGasto)]
    [InlineData(401, "authentication_error", null, "invalid x-api-key", CategoriaError.ClaveInvalida)]
    [InlineData(429, "rate_limit_error", null, "Number of requests has exceeded your rate limit", CategoriaError.Otro)]
    [InlineData(500, "api_error", null, "Internal server error", CategoriaError.Otro)]
    [InlineData(529, "overloaded_error", null, "Overloaded", CategoriaError.Otro)]
    [InlineData(400, "invalid_request_error", null, "messages.0.content: field required", CategoriaError.Otro)]
    public void Clasifica_los_errores_del_proveedor(int estado, string? tipo, string? codigo, string? mensaje, CategoriaError esperado)
    {
        Assert.Equal(esperado, ClasificadorDeErrores.Clasificar(estado, tipo, codigo, mensaje));
    }

    [Fact]
    public void La_clasificacion_no_distingue_mayusculas_y_acepta_datos_ausentes()
    {
        Assert.Equal(CategoriaError.SaldoAgotado, ClasificadorDeErrores.Clasificar(null, null, null, "YOUR CREDIT BALANCE IS TOO LOW"));
        Assert.Equal(CategoriaError.Otro, ClasificadorDeErrores.Clasificar(null, null, null, null));
    }

    [Fact]
    public void El_adaptador_de_anthropic_lee_tipo_codigo_y_mensaje_del_cuerpo_de_error()
    {
        var (tipo, codigo, mensaje) = ProveedorAnthropic_LeerError("""{"type":"error","error":{"type":"rate_limit_error","message":"límite","details":{"error_code":"enforced_spend_limit_reached"}},"request_id":"req_1"}""");
        Assert.Equal("rate_limit_error", tipo);
        Assert.Equal("enforced_spend_limit_reached", codigo);
        Assert.Equal("límite", mensaje);
        Assert.Equal((null, null, "texto de la excepción"), ProveedorAnthropic_LeerError("esto no es json", "texto de la excepción"));
    }

    private static (string?, string?, string?) ProveedorAnthropic_LeerError(string cuerpo, string? respaldo = null)
    {
        var m = typeof(Orumi.Agente.Proveedores.ProveedorAnthropic).GetMethod("LeerError", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return ((string?, string?, string?))m.Invoke(null, [cuerpo, respaldo])!;
    }

    // ---------------------------------------------------------------- detención y reanudación

    [Fact]
    public async Task Un_saldo_agotado_detiene_la_ejecucion_deja_todo_pendiente_y_lo_explica_en_el_lote()
    {
        using var e = new EntornoMira();
        e.Capturar("Primera nota privada de prueba.", e.Ahora);
        e.Capturar("Segunda nota privada de prueba.", e.Ahora.AddMinutes(1));
        e.Respuesta = _ => throw new ProveedorException(CategoriaError.SaldoAgotado, "x");

        var r = await e.Procesar();

        Assert.Equal(CategoriaError.SaldoAgotado, r.Fatal);
        Assert.Single(e.Proveedor.Solicitudes); // no se reintenta con la segunda
        Assert.Empty(r.Procesadas);
        Assert.Empty(e.Registros());
        var lote = e.Leer(r.LoteId!);
        Assert.Contains("tipo: saldo_agotado", lote);
        Assert.Contains("recarga créditos", lote);
        Assert.DoesNotContain("nota privada", lote); // sin contenido de las notas
        Assert.DoesNotContain("sk-", lote); // ni claves
        Assert.Equal(2, (await e.Procesar()).Pendientes);
    }

    [Theory]
    [InlineData(CategoriaError.LimiteDeGasto, "limite_de_gasto", "Límite de gasto")]
    [InlineData(CategoriaError.ClaveInvalida, "clave_invalida", "clave de API no es válida")]
    public async Task El_limite_de_gasto_y_la_clave_invalida_tambien_detienen_la_ejecucion(CategoriaError categoria, string etiqueta, string texto)
    {
        using var e = new EntornoMira();
        e.Capturar("Nota.");
        e.Respuesta = _ => throw new ProveedorException(categoria, "x");

        var r = await e.Procesar();

        Assert.Equal(categoria, r.Fatal);
        var lote = e.Leer(r.LoteId!);
        Assert.Contains($"tipo: {etiqueta}", lote);
        Assert.Contains(texto, lote);
        Assert.Equal(1, (await e.Procesar()).Pendientes);
    }

    [Fact]
    public async Task Tras_recargar_la_siguiente_ejecucion_procesa_lo_que_quedo_pendiente()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy he llamado a Marta.", e.Ahora);
        e.Capturar("Hoy he comido con Pau.", e.Ahora.AddMinutes(1));
        e.Respuesta = _ => throw new ProveedorException(CategoriaError.SaldoAgotado, "x");
        await e.Procesar();

        e.Ahora = e.Ahora.AddDays(1);
        e.Respuesta = s => Resp([Registro(s.Texto, "actividad", s.Texto.TrimEnd('.'))]);
        var r = await e.Procesar(); // el usuario recargó

        Assert.Null(r.Fatal);
        Assert.Equal(2, r.Procesadas.Count);
        Assert.Equal(2, e.Registros().Count);
    }

    [Fact]
    public async Task Un_error_que_no_es_de_saldo_solo_deja_pendiente_esa_captura()
    {
        using var e = new EntornoMira();
        e.Capturar("Primera.", e.Ahora);
        e.Capturar("Segunda.", e.Ahora.AddMinutes(1));
        e.Respuesta = s => s.Texto == "Primera." ? throw new InvalidOperationException("fallo puntual") : Resp([Registro("Segunda.", "actividad", "Segunda")]);

        var r = await e.Procesar();

        Assert.Null(r.Fatal);
        Assert.Single(r.Procesadas);
    }

    // ---------------------------------------------------------------- uso y coste

    private static readonly Precios Haiku = new(0.10m, 0.50m);

    [Fact]
    public async Task El_lote_registra_los_tokens_y_el_coste_estimado_por_captura_y_en_total()
    {
        using var e = new EntornoMira { Opciones = new OpcionesProcesador(Precios: Haiku) };
        e.Capturar("Hoy he llamado a Marta.");
        e.Respuesta = _ => Resp([Registro("Hoy he llamado a Marta.", "evento", "Llamada")]) with { Uso = new UsoDeTokens(1000, 500) };

        var r = await e.Procesar();

        var lote = e.Doc(r.LoteId!).Frontmatter;
        var captura = Mapa(Lista(lote["capturas"]).Single());
        Assert.Equal("1000", captura["tokens_entrada"]);
        Assert.Equal("500", captura["tokens_salida"]);
        Assert.Equal("0.000350", captura["coste_estimado_usd"]); // 1000/1M*0,10 + 500/1M*0,50
        var uso = Mapa(lote["uso"]);
        Assert.Equal("1000", uso["tokens_entrada"]);
        Assert.Equal("0.000350", uso["coste_estimado_usd"]);
        Assert.Empty(e.Validar());
    }

    [Fact]
    public async Task Sin_precios_el_coste_queda_desconocido_y_no_se_inventa()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy he llamado a Marta.");
        e.Respuesta = _ => Resp([Registro("Hoy he llamado a Marta.", "evento", "Llamada")]) with { Uso = new UsoDeTokens(1000, 500) };

        var r = await e.Procesar();

        var uso = Mapa(e.Doc(r.LoteId!).Frontmatter["uso"]);
        Assert.Equal("1000", uso["tokens_entrada"]);
        Assert.Equal("desconocido", uso["coste_estimado_usd"]);
    }

    [Fact]
    public async Task El_consumo_de_una_llamada_cuya_salida_no_se_pudo_guardar_tambien_cuenta()
    {
        using var e = new EntornoMira { Opciones = new OpcionesProcesador(Precios: Haiku) };
        e.Capturar("Texto real.");
        e.Respuesta = _ => Resp([Registro("Texto inventado.", "actividad", "X")]) with { Uso = new UsoDeTokens(2000, 1000) };

        var r = await e.Procesar();

        Assert.Empty(r.Procesadas); // la captura sigue pendiente, pero la llamada ya se cobró
        var uso = Mapa(e.Doc(r.LoteId!).Frontmatter["uso"]);
        Assert.Equal("2000", uso["tokens_entrada"]);
        Assert.Equal("1000", uso["tokens_salida"]);
    }

    // ---------------------------------------------------------------- presupuesto

    private static EntornoMira ConPresupuesto() => new()
    {
        Opciones = new OpcionesProcesador(Precios: new Precios(1m, 1m), Presupuesto: new Presupuesto(5m, new DateOnly(2026, 11, 16), 90)),
    };

    private static RespuestaExtraccion ConUso(string texto, long entrada, long salida) =>
        Resp([Registro(texto, "actividad", "Algo")]) with { Uso = new UsoDeTokens(entrada, salida) };

    [Fact]
    public async Task Avisa_al_llegar_al_90_por_ciento_y_al_superar_el_saldo_sin_detener_el_procesamiento()
    {
        using var e = ConPresupuesto();

        // 4,00 US$ estimados (80 %): todavía sin aviso.
        e.Capturar("Nota uno.", e.Ahora);
        e.Respuesta = _ => ConUso("Nota uno.", 2_000_000, 2_000_000);
        var r1 = await e.Procesar();
        Assert.Empty(r1.Avisos);
        Assert.DoesNotContain("avisos", e.Leer(r1.LoteId!));

        // +0,50 US$: 4,50 de 5,00 = 90 %: aviso.
        e.Ahora = e.Ahora.AddDays(1);
        e.Capturar("Nota dos.");
        e.Respuesta = _ => ConUso("Nota dos.", 250_000, 250_000);
        var r2 = await e.Procesar();
        var aviso = Assert.Single(r2.Avisos);
        Assert.Contains("4.50 de 5.00", aviso);
        Assert.Contains("(90%)", aviso);
        Assert.Contains("avisos", e.Leer(r2.LoteId!));

        // +1,00 US$: 5,50 > 5,00: aviso de que se supera, y se sigue procesando.
        e.Ahora = e.Ahora.AddDays(1);
        e.Capturar("Nota tres.");
        e.Respuesta = _ => ConUso("Nota tres.", 500_000, 500_000);
        var r3 = await e.Procesar();
        Assert.Contains("supera el saldo configurado", Assert.Single(r3.Avisos));
        Assert.Single(r3.Procesadas); // el aviso no detiene nada
        Assert.Empty(e.Validar());
    }

    [Fact]
    public async Task El_gasto_anterior_a_la_fecha_del_presupuesto_no_cuenta()
    {
        using var e = new EntornoMira
        {
            Opciones = new OpcionesProcesador(Precios: new Precios(1m, 1m), Presupuesto: new Presupuesto(5m, new DateOnly(2026, 11, 20), 90)),
        };
        e.Capturar("Nota antigua.");
        e.Respuesta = _ => ConUso("Nota antigua.", 4_000_000, 4_000_000); // 8 US$ el 16 de noviembre, antes del presupuesto
        await e.Procesar();

        var estado = ServicioDeEstado.Calcular(e.Datos, Configuracion.Leer("presupuesto:\n  saldo_usd: 5\n  desde: 2026-11-20\n"));
        Assert.Equal(0m, estado.GastoEstimado);
        Assert.Empty(estado.Avisos);
    }

    // ---------------------------------------------------------------- estado y avisos

    [Fact]
    public async Task El_estado_avisa_tras_un_saldo_agotado_y_el_aviso_desaparece_cuando_un_lote_termina_bien()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy he llamado a Marta.");
        e.Respuesta = _ => throw new ProveedorException(CategoriaError.SaldoAgotado, "x");
        await e.Procesar();

        var estado = ServicioDeEstado.Calcular(e.Datos, null);
        Assert.Equal(1, estado.Pendientes);
        Assert.Contains(estado.Avisos, a => a.Contains("Saldo de créditos agotado"));
        Assert.Equal("saldo_agotado", estado.UltimoLote!.TipoFatal);

        e.Ahora = e.Ahora.AddDays(1);
        e.Respuesta = s => Resp([Registro(s.Texto, "actividad", "Llamada")]);
        await e.Procesar();

        var despues = ServicioDeEstado.Calcular(e.Datos, null);
        Assert.Equal(0, despues.Pendientes);
        Assert.Empty(despues.Avisos);
        Assert.Null(despues.UltimoLote!.TipoFatal);
    }

    private int Cli(EntornoMira e, out string salida, out string error, Func<IProveedorIA?>? proveedor, params string[] args)
    {
        var s = new StringWriter();
        var er = new StringWriter();
        var codigo = new Comandos(() => new MemoryStream(new UTF8Encoding(false).GetBytes("nota")), s, er, () => e.Ahora, _ => null, proveedor).Ejecutar(args);
        salida = s.ToString();
        error = er.ToString();
        return codigo;
    }

    [Fact]
    public async Task Capturar_y_validar_muestran_un_aviso_breve_sin_bloquear()
    {
        using var e = new EntornoMira();
        e.Capturar("Primera nota.");
        e.Respuesta = _ => throw new ProveedorException(CategoriaError.SaldoAgotado, "x");
        await e.Procesar();

        var codigo = Cli(e, out var salida, out var error, null, "capturar", "--datos", e.Datos, "--texto", "Otra nota");

        Assert.Equal(0, codigo); // se captura con normalidad
        Assert.Contains("Captura creada", salida);
        Assert.Contains("⚠ Mira: Saldo de créditos agotado", error);
        Assert.DoesNotContain("Primera nota", error);

        Cli(e, out _, out var errorValidar, null, "validar", "--datos", e.Datos);
        Assert.Contains("⚠ Mira", errorValidar);
    }

    [Fact]
    public async Task El_comando_estado_muestra_pendientes_ultimo_lote_gasto_y_avisos()
    {
        using var e = ConPresupuesto();
        e.Capturar("Hoy he llamado a Marta.");
        e.Respuesta = _ => ConUso("Hoy he llamado a Marta.", 2_250_000, 2_250_000); // 4,50 US$
        await e.Procesar();
        e.Capturar("Otra nota pendiente.", e.Ahora.AddDays(1));
        var config = Path.Combine(Path.GetTempPath(), "orumi-estado-" + Guid.NewGuid().ToString("N")[..6] + ".yaml");
        File.WriteAllText(config, "proveedor:\n  nombre: simulado\nprecios:\n  entrada_por_millon_usd: 1\n  salida_por_millon_usd: 1\npresupuesto:\n  saldo_usd: 5.00\n  desde: 2026-11-16\n  aviso_porcentaje: 90\n");
        try
        {
            var codigo = Cli(e, out var salida, out _, null, "estado", "--datos", e.Datos, "--config", config);

            Assert.Equal(0, codigo);
            Assert.Contains("Capturas pendientes de procesar: 1", salida);
            Assert.Contains("Último lote: lote-", salida);
            Assert.Contains("Gasto estimado desde 2026-11-16: 4.50 de 5.00 US$ (90%", salida);
            Assert.Contains("⚠ Gasto estimado 4.50 de 5.00", salida);
        }
        finally
        {
            File.Delete(config);
        }
    }

    [Theory]
    [InlineData(CategoriaError.SaldoAgotado, 7)]
    [InlineData(CategoriaError.LimiteDeGasto, 7)]
    [InlineData(CategoriaError.ClaveInvalida, 8)]
    public void Procesar_termina_con_un_codigo_propio_si_el_proveedor_corta_por_saldo_limite_o_clave(CategoriaError categoria, int esperado)
    {
        using var e = new EntornoMira();
        e.Capturar("Nota.");
        var proveedor = new ProveedorSimulado(_ => throw new ProveedorException(categoria, "x"));

        var codigo = Cli(e, out _, out var error, () => proveedor, "procesar", "--datos", e.Datos);

        Assert.Equal(esperado, codigo);
        Assert.Contains(ClasificadorDeErrores.Explicacion(categoria), error);
    }

    [Fact]
    public void La_configuracion_lee_precios_y_presupuesto_y_aplica_el_90_por_defecto()
    {
        var c = Configuracion.Leer("precios:\n  entrada_por_millon_usd: 0.10\n  salida_por_millon_usd: 0.50\npresupuesto:\n  saldo_usd: 5.00\n  desde: 2026-11-13\n");

        Assert.Equal(new Precios(0.10m, 0.50m), c.Precios);
        Assert.Equal(new Presupuesto(5.00m, new DateOnly(2026, 11, 13), 90), c.Presupuesto);
        Assert.Null(Configuracion.Leer("proveedor:\n  nombre: x\n").Precios);
        Assert.Null(Configuracion.Leer("proveedor:\n  nombre: x\n").Presupuesto);
    }
}
