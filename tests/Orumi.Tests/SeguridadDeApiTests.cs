using Orumi.Acceso;

namespace Orumi.Tests;

public class SeguridadDeApiTests
{
    private DateTimeOffset _ahora = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private sealed class Verificador : IVerificadorDeSesion
    {
        public int Consultas { get; private set; }

        public bool Caido { get; set; }

        public Task<IdentidadDeSesion?> VerificarAsync(string token, CancellationToken ct)
        {
            Consultas++;
            if (Caido)
            {
                throw new HttpRequestException("caído");
            }

            return Task.FromResult<IdentidadDeSesion?>(token switch
            {
                "yo" => new IdentidadDeSesion("yo@ejemplo.com", true),
                "otro" => new IdentidadDeSesion("otro@ejemplo.com", true),
                _ => null,
            });
        }
    }

    private (GuardianDeApi Guardian, Verificador Verificador) Montar(LimitesDePeticiones? limites = null)
    {
        var v = new Verificador();
        var acceso = new AccesoPorSesion(v, ["yo@ejemplo.com"], () => _ahora, maxFallos: 3);
        return (new GuardianDeApi(acceso, new PoliticaDeLimites(limites ?? new LimitesDePeticiones(), () => _ahora), ["/api/config"]), v);
    }

    private static Task<DecisionDeGuardian> Peticion(GuardianDeApi g, string metodo, string ruta, string? bearer = "yo", string ip = "1.1.1.1",
        bool origenPropio = true, string? tipo = "application/json") =>
        g.EvaluarAsync(metodo, ruta, origenPropio, tipo, bearer, ip, CancellationToken.None);

    // ---------------------------------------------------------------- limitador

    [Fact]
    public void La_ventana_deslizante_permite_el_maximo_y_libera_al_pasar_el_tiempo()
    {
        var l = new LimitadorDePeticiones(3, TimeSpan.FromMinutes(1), () => _ahora);
        Assert.True(l.Intentar("a").Permitido);
        _ahora = _ahora.AddSeconds(20);
        Assert.True(l.Intentar("a").Permitido);
        Assert.True(l.Intentar("a").Permitido);

        var bloqueada = l.Intentar("a");
        Assert.False(bloqueada.Permitido);
        Assert.Equal(TimeSpan.FromSeconds(40), bloqueada.Espera); // la más antigua sale a los 60 s

        _ahora = _ahora.AddSeconds(41); // sale la primera marca, no las otras dos
        Assert.True(l.Intentar("a").Permitido);
        Assert.False(l.Intentar("a").Permitido);
    }

    [Fact]
    public void Cada_clave_tiene_su_propia_cuenta()
    {
        var l = new LimitadorDePeticiones(1, TimeSpan.FromMinutes(1), () => _ahora);
        Assert.True(l.Intentar("a").Permitido);
        Assert.False(l.Intentar("a").Permitido);
        Assert.True(l.Intentar("b").Permitido);
    }

    [Fact]
    public void El_estado_esta_acotado_y_no_crece_sin_limite()
    {
        var l = new LimitadorDePeticiones(5, TimeSpan.FromMinutes(1), () => _ahora, maximoDeClaves: 10);
        for (var i = 0; i < 10; i++)
        {
            Assert.True(l.Intentar("ip" + i).Permitido);
        }

        Assert.False(l.Intentar("nueva").Permitido); // demasiadas claves distintas a la vez
        _ahora = _ahora.AddMinutes(2); // las antiguas caducan y se purgan
        Assert.True(l.Intentar("nueva").Permitido);
    }

    [Fact]
    public void Los_limites_se_leen_del_entorno_y_los_valores_invalidos_usan_el_defecto()
    {
        var l = LimitesDePeticiones.DesdeEntorno(n => n switch { "ORUMI_LIMITE_IP" => "7", "ORUMI_LIMITE_GLOBAL" => "abc", "ORUMI_LIMITE_USUARIO" => "-3", _ => null });
        Assert.Equal(7, l.PorIp);
        Assert.Equal(600, l.Global);
        Assert.Equal(240, l.PorUsuario);
    }

    // ---------------------------------------------------------------- guardián

    [Fact]
    public async Task Una_sesion_valida_pasa_y_devuelve_el_correo()
    {
        var (g, _) = Montar();
        var d = await Peticion(g, "GET", "/api/registros");
        Assert.True(d.Sigue);
        Assert.Equal("yo@ejemplo.com", d.Correo);
    }

    [Theory]
    [InlineData("/api/registros")]
    [InlineData("/api/lo-que-sea-nuevo")]
    [InlineData("/api/registros/reg-20261113-aaaa")]
    [InlineData("/api/capturas/cap-20261113-0900-abc")]
    [InlineData("/api/")]
    [InlineData("/API/Registros")]
    public async Task Toda_ruta_de_la_api_sin_sesion_se_deniega_por_defecto(string ruta)
    {
        var (g, _) = Montar();
        Assert.Equal(401, (await Peticion(g, "GET", ruta, bearer: null)).Estado);
        Assert.Equal(401, (await Peticion(g, "GET", ruta, bearer: "falso", ip: "2.2.2.2")).Estado);
    }

    [Fact]
    public async Task Solo_la_configuracion_publica_y_la_carcasa_estatica_no_exigen_sesion()
    {
        var (g, v) = Montar();
        Assert.True((await Peticion(g, "GET", "/api/config", bearer: null)).Sigue);
        Assert.True((await Peticion(g, "GET", "/api/config/", bearer: null)).Sigue);
        Assert.True((await Peticion(g, "GET", "/", bearer: null)).Sigue);
        Assert.True((await Peticion(g, "GET", "/app.js", bearer: null)).Sigue);
        Assert.Equal(0, v.Consultas);
        // la lista blanca solo vale para lecturas
        Assert.NotEqual(0, (await Peticion(g, "POST", "/api/config", bearer: null)).Estado);
    }

    [Fact]
    public async Task Una_cuenta_ajena_recibe_403()
    {
        var (g, _) = Montar();
        Assert.Equal(403, (await Peticion(g, "GET", "/api/registros", bearer: "otro")).Estado);
    }

    [Fact]
    public async Task Una_escritura_desde_otro_origen_o_con_otro_tipo_de_contenido_se_rechaza_antes_de_autenticar()
    {
        var (g, v) = Montar();
        Assert.Equal(403, (await Peticion(g, "POST", "/api/registros/x/corregir", origenPropio: false)).Estado);
        Assert.Equal(415, (await Peticion(g, "POST", "/api/registros/x/corregir", tipo: "text/plain")).Estado);
        Assert.Equal(415, (await Peticion(g, "POST", "/api/registros/x/corregir", tipo: null)).Estado);
        Assert.Equal(0, v.Consultas);
        Assert.True((await Peticion(g, "POST", "/api/registros/x/corregir")).Sigue);
    }

    [Fact]
    public async Task El_exceso_por_ip_da_429_con_espera_sin_afectar_a_otras_ip_y_sin_llegar_al_proveedor()
    {
        var (g, v) = Montar(new LimitesDePeticiones(PorIp: 3));
        for (var i = 0; i < 3; i++)
        {
            Assert.True((await Peticion(g, "GET", "/api/registros", ip: "9.9.9.9")).Sigue);
        }

        var consultasAntes = v.Consultas;
        var d = await Peticion(g, "GET", "/api/registros", bearer: "falso", ip: "9.9.9.9");
        Assert.Equal(429, d.Estado);
        Assert.True(d.Espera > TimeSpan.Zero);
        Assert.Equal(consultasAntes, v.Consultas); // lo rechazado por límite no consume al proveedor de identidad
        Assert.True((await Peticion(g, "GET", "/api/registros", ip: "8.8.8.8")).Sigue);

        _ahora = _ahora.AddMinutes(1).AddSeconds(1);
        Assert.True((await Peticion(g, "GET", "/api/registros", ip: "9.9.9.9")).Sigue);
    }

    [Fact]
    public async Task Las_escrituras_tienen_un_limite_mas_estricto_que_las_lecturas()
    {
        var (g, _) = Montar(new LimitesDePeticiones(PorIp: 100, PorIpEscritura: 2));
        Assert.True((await Peticion(g, "POST", "/api/x")).Sigue);
        Assert.True((await Peticion(g, "POST", "/api/x")).Sigue);
        Assert.Equal(429, (await Peticion(g, "POST", "/api/x")).Estado);
        Assert.True((await Peticion(g, "GET", "/api/registros")).Sigue); // las lecturas siguen
    }

    [Fact]
    public async Task El_limite_por_usuario_actua_aunque_cambie_la_ip()
    {
        var (g, _) = Montar(new LimitesDePeticiones(PorUsuario: 2));
        Assert.True((await Peticion(g, "GET", "/api/registros", ip: "1.1.1.1")).Sigue);
        Assert.True((await Peticion(g, "GET", "/api/registros", ip: "2.2.2.2")).Sigue);
        Assert.Equal(429, (await Peticion(g, "GET", "/api/registros", ip: "3.3.3.3")).Estado);
    }

    [Fact]
    public async Task El_tope_global_protege_el_servidor_aunque_haya_muchas_ip()
    {
        var (g, _) = Montar(new LimitesDePeticiones(Global: 3));
        for (var i = 0; i < 3; i++)
        {
            Assert.True((await Peticion(g, "GET", "/api/registros", ip: "10.0.0." + i)).Sigue);
        }

        Assert.Equal(429, (await Peticion(g, "GET", "/api/registros", ip: "10.0.0.99")).Estado);
    }

    [Fact]
    public async Task Los_fallos_de_acceso_repetidos_bloquean_la_ip_incluso_con_sesion_valida()
    {
        var (g, _) = Montar();
        Assert.Equal(401, (await Peticion(g, "GET", "/api/registros", bearer: "mal", ip: "7.7.7.7")).Estado);
        Assert.Equal(401, (await Peticion(g, "GET", "/api/registros", bearer: "mal", ip: "7.7.7.7")).Estado);
        Assert.Equal(429, (await Peticion(g, "GET", "/api/registros", bearer: "mal", ip: "7.7.7.7")).Estado);
        Assert.Equal(429, (await Peticion(g, "GET", "/api/registros", bearer: "yo", ip: "7.7.7.7")).Estado);
    }

    [Fact]
    public async Task Si_el_proveedor_de_identidad_cae_responde_503()
    {
        var (g, v) = Montar();
        v.Caido = true;
        Assert.Equal(503, (await Peticion(g, "GET", "/api/registros")).Estado);
    }

    // ---------------------------------------------------------------- auditoría

    [Theory]
    [InlineData("pasta0126@gmail.com", "p***@gmail.com")]
    [InlineData(null, "?")]
    [InlineData("sinarroba", "?")]
    public void El_correo_se_enmascara_en_la_auditoria(string? correo, string esperado) => Assert.Equal(esperado, Auditoria.Enmascarar(correo));

    [Fact]
    public void La_linea_de_auditoria_no_contiene_el_correo_completo_ni_valores()
    {
        var linea = Auditoria.Linea(_ahora, "yo@ejemplo.com", "corregir", "reg-20261113-aaaa", "ok");
        Assert.Contains("usuario=y***@ejemplo.com", linea);
        Assert.DoesNotContain("yo@ejemplo.com", linea);
        Assert.Contains("operacion=corregir id=reg-20261113-aaaa resultado=ok", linea);
    }
}
