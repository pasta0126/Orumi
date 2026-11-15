using System.Net;
using Orumi.Agente;
using Orumi.Agente.Clima;
using Orumi.Cli;
using Orumi.Core.Capturas;
using Orumi.Core.Ficheros;

namespace Orumi.Tests;

/// <summary>Geocodificador de pruebas: nunca sale a la red. Devuelve lo que se le haya configurado y anota los nombres que recibe.</summary>
internal sealed class GeocodificadorFalso(Dictionary<string, CandidatoDeCiudad[]>? respuestas = null) : IGeocodificador
{
    public List<string> Consultas { get; } = [];

    public bool Falla { get; set; }

    public Task<IReadOnlyList<CandidatoDeCiudad>> BuscarAsync(string nombre, CancellationToken ct = default)
    {
        Consultas.Add(nombre);
        if (Falla)
        {
            throw new ClimaNoDisponibleException("caído");
        }

        IReadOnlyList<CandidatoDeCiudad> r = respuestas is not null && respuestas.TryGetValue(Ciudad.Normalizar(nombre), out var c) ? c : [];
        return Task.FromResult(r);
    }
}

public class ClimaTests : IDisposable
{
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "orumi-clima-" + Guid.NewGuid().ToString("N")[..8]);
    private static readonly DateTimeOffset Ahora = new(2026, 11, 20, 23, 0, 0, TimeSpan.FromHours(1));
    private static readonly DateOnly Hoy = new(2026, 11, 20);

    public ClimaTests() => Directory.CreateDirectory(_datos);

    public void Dispose() => Directory.Delete(_datos, true);

    private static ConfiguracionDeClima Config(string extra = "") =>
        Configuracion.Leer("clima:\n  ciudad_por_defecto: Barcelona\n" + extra).Clima!;

    private (string Captura, string Registro) CapturaYRegistro(string fecha, string? ciudad = null, string? estado = null, string? desde = null, string? hasta = null)
    {
        var c = CreadorDeCapturas.Crear(_datos, "Salí a caminar por la tarde.", Ahora, ciudad: ciudad);
        var idReg = "reg-" + Guid.NewGuid().ToString("N")[..8];
        var ruta = Path.Combine(_datos, "registros", "2026", "11", idReg + ".md");
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllText(ruta,
            $"---\nid: {idReg}\ntipo: registro\nclase: actividad\ntitulo: Paseo\nfuentes:\n  - captura: {c.Id}\n    rol: origen\n" +
            $"fecha: {{desde: {desde ?? fecha}, hasta: {hasta ?? fecha}}}\n" + (estado is null ? "" : $"estado: {estado}\n") + "---\nPaseo.\n");
        return (c.Id, idReg);
    }

    // ---------------------------------------------------------------- configuración y ciudades

    [Fact]
    public void Sin_la_seccion_clima_no_hay_clima_configurado()
    {
        Assert.Null(Configuracion.Leer("proveedor:\n  nombre: x\n").Clima);
    }

    [Fact]
    public void La_seccion_clima_vacia_toma_los_valores_iniciales_acordados()
    {
        var c = Configuracion.Leer("clima: {}\n").Clima!;
        Assert.Equal("Barcelona", c.CiudadPorDefecto);
        Assert.Equal(6, c.Umbrales.UvAlto);
        Assert.Equal(1, c.Umbrales.LluviaMmDia);
        Assert.Equal(new TimeOnly(8, 30), c.Laboral.Desde);
        Assert.Equal(new TimeOnly(18, 30), c.Laboral.Hasta);
        Assert.Contains(c.Ciudades, x => x.Clave == "segur-de-calafell");
        Assert.Equal("open-meteo", c.Fuente);
    }

    [Fact]
    public void Las_coordenadas_se_redondean_a_un_decimal_aunque_la_configuracion_sea_precisa()
    {
        var c = Configuracion.Leer("clima:\n  ciudades:\n    Girona:\n      lat: 41.9794\n      lon: 2.8214\n").Clima!;
        var girona = c.Buscar("girona")!;
        Assert.Equal(42.0, girona.LatitudRedondeada);
        Assert.Equal(2.8, girona.LongitudRedondeada);
    }

    [Theory]
    [InlineData(null, "Barcelona")]
    [InlineData("", "Barcelona")]
    [InlineData("segur de calafell", "Segur de Calafell")]
    [InlineData("  SEGUR DE CALAFELL ", "Segur de Calafell")]
    public void Sin_ciudad_se_usa_la_de_por_defecto_y_las_conocidas_se_reconocen_sin_acentos_ni_mayusculas(string? dicha, string esperada)
    {
        var r = Config().Resolver(dicha);
        Assert.True(r.Reconocida);
        Assert.Equal(esperada, r.Ciudad!.Nombre);
    }

    [Fact]
    public void Una_ciudad_desconocida_no_se_supone()
    {
        var r = Config().Resolver("Lleida");
        Assert.False(r.Reconocida);
        Assert.Equal("Lleida", r.NoReconocida);
    }

    // ---------------------------------------------------------------- formato

    private static ClimaDia Dia(DateOnly fecha, double? uv = 2.8) => new(
        fecha, "Barcelona", "open-meteo", 41.4, 2.2, "Europe/Madrid", Ahora,
        new ResumenDelDia(uv, "07:12", "17:35", 36000, 26993.5, 0.4, 9.1, 17.3, 68, 40),
        Enumerable.Range(0, 24).Select(h => new ClimaHora(h, 10 + h / 4.0, 70, h == 15 ? 0.4 : 0, 40, h is > 7 and < 18 ? 200 : 0, h is > 8 and < 17 ? 3000 : 0, uv is null ? null : h is > 8 and < 17 ? 2.5 : 0)).ToList());

    [Fact]
    public void El_clima_se_escribe_y_se_lee_igual_y_lo_desconocido_es_explicito()
    {
        var original = Dia(new DateOnly(2026, 11, 14), uv: null);
        var yaml = FormatoDeClima.Escribir(original);
        Assert.Contains("desconocido", yaml);
        var leido = FormatoDeClima.Leer(yaml);

        Assert.Equal(original.Fecha, leido.Fecha);
        Assert.Equal("Barcelona", leido.Ciudad);
        Assert.Equal("07:12", leido.Dia.Amanecer);
        Assert.Null(leido.Dia.UvMaximo);
        Assert.False(leido.TieneUv);
        Assert.Equal(24, leido.Horas.Count);
        Assert.Equal(original.Horas[15].Precipitacion, leido.Horas[15].Precipitacion);
        Assert.Equal(26993.5, leido.Dia.SolSegundos);
    }

    [Fact]
    public void La_ruta_incluye_la_fecha_y_la_ciudad_sin_acentos()
    {
        var ruta = FormatoDeClima.Ruta("/d", new DateOnly(2026, 11, 14), new Ciudad("Segur de Calafell", 41.3, 1.6)).Replace('\\', '/');
        Assert.Equal("/d/clima/2026/11/2026-11-14-segur-de-calafell.yaml", ruta);
    }

    // ---------------------------------------------------------------- cliente Open-Meteo

    private sealed class ManejadorFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Peticiones { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Peticiones.Add(request);
            return Task.FromResult(responder(request));
        }
    }

    private static string Json(bool conUv, params string[] dias)
    {
        var tiempos = dias.SelectMany(d => Enumerable.Range(0, 24).Select(h => $"\"{d}T{h:00}:00\""));
        string Serie(Func<int, string> v) => "[" + string.Join(",", dias.SelectMany(_ => Enumerable.Range(0, 24).Select(v))) + "]";
        var uv = conUv ? $",\"uv_index\":{Serie(h => h is > 8 and < 17 ? "2.5" : "0")}" : ",\"uv_index\":" + Serie(_ => "null");
        var diario = $"\"daily\":{{\"time\":[{string.Join(",", dias.Select(d => $"\"{d}\""))}]," +
                     $"\"sunrise\":[{string.Join(",", dias.Select(d => $"\"{d}T07:12\""))}],\"sunset\":[{string.Join(",", dias.Select(d => $"\"{d}T17:35\""))}]," +
                     $"\"daylight_duration\":[{string.Join(",", dias.Select(_ => "36000"))}],\"sunshine_duration\":[{string.Join(",", dias.Select(_ => "26000"))}]," +
                     $"\"precipitation_sum\":[{string.Join(",", dias.Select(_ => "0.4"))}]" + (conUv ? $",\"uv_index_max\":[{string.Join(",", dias.Select(_ => "3.05"))}]" : "") + "}";
        return "{\"hourly\":{\"time\":[" + string.Join(",", tiempos) + "]," +
               $"\"temperature_2m\":{Serie(h => (10 + h / 4.0).ToString(System.Globalization.CultureInfo.InvariantCulture))}," +
               $"\"relative_humidity_2m\":{Serie(_ => "70")},\"precipitation\":{Serie(_ => "0")},\"cloud_cover\":{Serie(_ => "40")}," +
               $"\"shortwave_radiation\":{Serie(_ => "100")},\"sunshine_duration\":{Serie(h => h is > 8 and < 17 ? "3000" : "0")}{uv}}}," + diario + "}";
    }

    [Fact]
    public async Task La_peticion_solo_lleva_coordenadas_redondeadas_fechas_y_variables()
    {
        var manejador = new ManejadorFalso(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json(true, "2026-11-19", "2026-11-20")) });
        var proveedor = new ProveedorOpenMeteo(new HttpClient(manejador), Config());
        var ciudad = Config().PorDefecto;

        var dias = await proveedor.ObtenerAsync(ciudad, new DateOnly(2026, 11, 19), new DateOnly(2026, 11, 20), Hoy, Ahora);

        var p = Assert.Single(manejador.Peticiones);
        Assert.Equal(HttpMethod.Get, p.Method);
        Assert.Equal("api.open-meteo.com", p.RequestUri!.Host);
        var consulta = p.RequestUri.Query;
        Assert.Contains("latitude=41.4", consulta);
        Assert.Contains("longitude=2.2", consulta);
        Assert.Contains("past_days=1", consulta);
        Assert.Contains("uv_index", consulta);
        // Nada más que ubicación redondeada, variables, zona horaria y tamaño de ventana: sin texto, identidad ni cabeceras propias.
        var claves = consulta.TrimStart('?').Split('&').Select(x => x.Split('=')[0]).ToHashSet();
        Assert.Subset(new HashSet<string> { "latitude", "longitude", "hourly", "daily", "timezone", "past_days", "forecast_days" }, claves);
        Assert.Empty(p.Headers);
        Assert.Equal(2, dias.Count);
        Assert.Equal(3.05, dias[0].Dia.UvMaximo);
        Assert.Equal(24, dias[0].Horas.Count);
    }

    [Fact]
    public async Task Un_dia_antiguo_usa_el_historico_y_su_uv_queda_desconocido_sin_estimarse()
    {
        var manejador = new ManejadorFalso(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json(false, "2026-06-01")) });
        var proveedor = new ProveedorOpenMeteo(new HttpClient(manejador), Config());

        var dias = await proveedor.ObtenerAsync(Config().PorDefecto, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1), Hoy, Ahora);

        var p = Assert.Single(manejador.Peticiones);
        Assert.Equal("archive-api.open-meteo.com", p.RequestUri!.Host);
        Assert.DoesNotContain("uv_index", p.RequestUri.Query);
        var d = Assert.Single(dias);
        Assert.Null(d.Dia.UvMaximo);
        Assert.All(d.Horas, h => Assert.Null(h.Uv));
        Assert.False(d.TieneUv);
        Assert.NotNull(d.Dia.TemperaturaMaxima);
    }

    [Fact]
    public async Task Un_rango_que_cruza_la_ventana_hace_dos_peticiones()
    {
        var n = 0;
        var manejador = new ManejadorFalso(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(n++ == 0 ? Json(true, "2026-11-20") : Json(false, "2026-08-10")),
        });
        var proveedor = new ProveedorOpenMeteo(new HttpClient(manejador), Config());

        var dias = await proveedor.ObtenerAsync(Config().PorDefecto, new DateOnly(2026, 8, 10), new DateOnly(2026, 11, 20), Hoy, Ahora);

        Assert.Equal(2, manejador.Peticiones.Count);
        Assert.Contains(dias, d => d.Fecha == new DateOnly(2026, 11, 20) && d.TieneUv);
        Assert.Contains(dias, d => d.Fecha == new DateOnly(2026, 8, 10) && !d.TieneUv);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Un_error_del_servicio_es_una_excepcion_controlada(HttpStatusCode codigo)
    {
        var proveedor = new ProveedorOpenMeteo(new HttpClient(new ManejadorFalso(_ => new HttpResponseMessage(codigo))), Config());
        await Assert.ThrowsAsync<ClimaNoDisponibleException>(() =>
            proveedor.ObtenerAsync(Config().PorDefecto, Hoy, Hoy, Hoy, Ahora));
    }

    [Fact]
    public async Task Una_respuesta_sin_datos_horarios_es_una_excepcion_controlada()
    {
        var proveedor = new ProveedorOpenMeteo(new HttpClient(new ManejadorFalso(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") })), Config());
        await Assert.ThrowsAsync<ClimaNoDisponibleException>(() => proveedor.ObtenerAsync(Config().PorDefecto, Hoy, Hoy, Hoy, Ahora));
    }

    // ---------------------------------------------------------------- planificación y obtención

    private sealed class ProveedorFalso : IProveedorDeClima
    {
        public List<(string Ciudad, DateOnly Desde, DateOnly Hasta)> Llamadas { get; } = [];

        public bool Falla { get; set; }

        public Task<IReadOnlyList<ClimaDia>> ObtenerAsync(Ciudad ciudad, DateOnly desde, DateOnly hasta, DateOnly hoy, DateTimeOffset ahora, CancellationToken cancelacion = default)
        {
            Llamadas.Add((ciudad.Nombre, desde, hasta));
            if (Falla)
            {
                throw new ClimaNoDisponibleException("caído");
            }

            IReadOnlyList<ClimaDia> dias = [.. Enumerable.Range(0, hasta.DayNumber - desde.DayNumber + 1)
                .Select(i => Dia(desde.AddDays(i)) with { Ciudad = ciudad.Nombre, Obtenido = ahora, Latitud = ciudad.LatitudRedondeada, Longitud = ciudad.LongitudRedondeada })];
            return Task.FromResult(dias);
        }
    }

    [Fact]
    public void La_ciudad_de_cada_registro_es_la_de_su_captura_o_la_de_por_defecto_y_lo_eliminado_no_cuenta()
    {
        CapturaYRegistro("2026-11-14");
        CapturaYRegistro("2026-11-15", ciudad: "Segur de Calafell");
        CapturaYRegistro("2026-11-16", ciudad: "Lleida");
        CapturaYRegistro("2026-11-17", estado: "eliminado");
        CapturaYRegistro("2026-11-18", desde: "2026-11-18", hasta: "2026-11-19"); // rango impreciso: sin día exacto

        var plan = ObtencionDeClima.Planificar(Almacen.Cargar(_datos), Config());

        Assert.Equal([("2026-11-14", "Barcelona"), ("2026-11-15", "Segur de Calafell")], plan.Necesarios.Select(n => (n.Fecha.ToString("yyyy-MM-dd"), n.Ciudad.Nombre)).ToArray());
        var sin = Assert.Single(plan.SinReconocer);
        Assert.Equal("Lleida", sin.Ciudad);
    }

    [Fact]
    public async Task Se_obtiene_solo_lo_que_falta_y_un_dia_de_otra_ciudad_se_pide_aparte()
    {
        CapturaYRegistro("2026-11-14");
        CapturaYRegistro("2026-11-15");
        CapturaYRegistro("2026-11-15", ciudad: "Segur de Calafell");
        var proveedor = new ProveedorFalso();

        var r = await ObtencionDeClima.EjecutarAsync(_datos, Almacen.Cargar(_datos), Config(), proveedor, Hoy, Ahora);

        Assert.Equal(3, r.Obtenidos);
        Assert.Equal(2, proveedor.Llamadas.Count);
        Assert.True(File.Exists(FormatoDeClima.Ruta(_datos, new DateOnly(2026, 11, 15), Config().Buscar("Segur de Calafell")!)));

        // Segunda ejecución al día siguiente: ya está todo guardado y no se vuelve a pedir nada.
        var manana = Ahora.AddDays(1);
        var otro = new ProveedorFalso();
        var r2 = await ObtencionDeClima.EjecutarAsync(_datos, Almacen.Cargar(_datos), Config(), otro, Hoy.AddDays(1), manana);
        Assert.Equal(0, r2.Obtenidos);
        Assert.Equal(3, r2.YaGuardados);
        Assert.Empty(otro.Llamadas);
    }

    [Fact]
    public async Task Un_dia_guardado_el_mismo_dia_se_refresca_porque_esta_incompleto()
    {
        CapturaYRegistro("2026-11-20");
        var p1 = new ProveedorFalso();
        await ObtencionDeClima.EjecutarAsync(_datos, Almacen.Cargar(_datos), Config(), p1, Hoy, Ahora);

        var p2 = new ProveedorFalso();
        var r = await ObtencionDeClima.EjecutarAsync(_datos, Almacen.Cargar(_datos), Config(), p2, Hoy, Ahora.AddHours(1));

        Assert.Equal(1, r.Obtenidos);
        Assert.Single(p2.Llamadas);
    }

    [Fact]
    public async Task Si_el_servicio_cae_no_se_lanza_nada_no_se_escribe_nada_y_se_informa()
    {
        CapturaYRegistro("2026-11-14");

        var r = await ObtencionDeClima.EjecutarAsync(_datos, Almacen.Cargar(_datos), Config(), new ProveedorFalso { Falla = true }, Hoy, Ahora);

        Assert.Equal(0, r.Obtenidos);
        Assert.Single(r.Fallos);
        Assert.False(Directory.Exists(Path.Combine(_datos, "clima")));
    }

    [Fact]
    public async Task El_clima_nunca_modifica_capturas_ni_registros()
    {
        var (captura, _) = CapturaYRegistro("2026-11-14");
        var antes = Directory.GetFiles(_datos, "*.md", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllText);

        await ObtencionDeClima.EjecutarAsync(_datos, Almacen.Cargar(_datos), Config(), new ProveedorFalso(), Hoy, Ahora);

        foreach (var (ruta, contenido) in antes)
        {
            Assert.Equal(contenido, File.ReadAllText(ruta));
        }

        Assert.NotNull(captura);
    }

    [Fact]
    public async Task Al_interpretar_una_captura_el_modelo_no_recibe_datos_meteorologicos()
    {
        using var e = new EntornoMira();
        var cap = CreadorDeCapturas.Crear(e.Datos, "Hoy he salido a caminar con Marta por la tarde.", e.Ahora, ciudad: "Segur de Calafell");
        var ruta = FormatoDeClima.Ruta(e.Datos, new DateOnly(2026, 11, 16), Config().PorDefecto);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllText(ruta, FormatoDeClima.Escribir(Dia(new DateOnly(2026, 11, 16))));
        e.Respuesta = _ => Datos.Resp([Datos.Registro("Hoy he salido a caminar con Marta por la tarde.", "actividad", "Caminar con Marta")]);

        await e.Procesar();

        var s = Assert.Single(e.Proveedor.Solicitudes);
        var enviado = System.Text.Json.JsonSerializer.Serialize(s).ToLowerInvariant();
        foreach (var prohibido in new[] { "clima", "temperatura", "humedad", "uv", "precipitacion", "nubosidad", "open-meteo", "segur" })
        {
            Assert.DoesNotContain(prohibido, enviado.Replace("\"texto\"", string.Empty).Replace("hoy he salido a caminar con marta por la tarde.", string.Empty));
        }

        Assert.NotNull(cap);
    }

    // ---------------------------------------------------------------- comando

    private (Comandos, StringWriter, StringWriter, ProveedorFalso) Cli(string? config, Func<DateTimeOffset>? reloj = null)
    {
        var salida = new StringWriter();
        var error = new StringWriter();
        var proveedor = new ProveedorFalso();
        string? rutaConfig = null;
        if (config is not null)
        {
            rutaConfig = Path.Combine(_datos, "orumi.yaml");
            File.WriteAllText(rutaConfig, config);
        }

        var cmd = new Comandos(() => new MemoryStream(), salida, error, reloj ?? (() => Ahora),
            k => k == Comandos.VariableConfiguracion ? rutaConfig : null, proveedorDeClimaDePrueba: _ => proveedor, geocodificadorDePrueba: () => new GeocodificadorFalso());
        return (cmd, salida, error, proveedor);
    }

    [Fact]
    public void Sin_la_seccion_clima_el_comando_no_contacta_con_nada()
    {
        CapturaYRegistro("2026-11-14");
        var (cmd, salida, _, proveedor) = Cli("proveedor:\n  nombre: x\n");

        Assert.Equal(0, cmd.Ejecutar(["clima", "--datos", _datos]));

        Assert.Contains("no está activado", salida.ToString());
        Assert.Empty(proveedor.Llamadas);
    }

    [Fact]
    public void El_comando_obtiene_el_clima_y_avisa_de_las_ciudades_sin_reconocer()
    {
        CapturaYRegistro("2026-11-14");
        CapturaYRegistro("2026-11-15", ciudad: "Lleida");
        var (cmd, salida, _, proveedor) = Cli("clima:\n  ciudad_por_defecto: Barcelona\n");

        Assert.Equal(0, cmd.Ejecutar(["clima", "--datos", _datos]));

        Assert.Contains("1 día(s) obtenido(s)", salida.ToString());
        Assert.Contains("Lleida", salida.ToString());
        Assert.Single(proveedor.Llamadas);
    }

    [Fact]
    public void Procesar_completa_el_clima_si_esta_activado_y_no_cambia_el_resultado_de_mira()
    {
        CapturaYRegistro("2026-11-14");
        var salida = new StringWriter();
        var error = new StringWriter();
        var clima = new ProveedorFalso();
        var config = Path.Combine(_datos, "orumi.yaml");
        File.WriteAllText(config, "datos: " + _datos + "\nproveedor:\n  nombre: x\nclima: {}\n");
        var cmd = new Comandos(() => new MemoryStream(), salida, error, () => Ahora,
            k => k == Comandos.VariableConfiguracion ? config : null,
            proveedorDePrueba: () => new Orumi.Agente.Proveedores.ProveedorSimulado(_ => RespuestaExtraccion.SinHechos),
            proveedorDeClimaDePrueba: _ => clima);

        var codigo = cmd.Ejecutar(["procesar"]);

        Assert.Equal(0, codigo);
        Assert.Contains("Clima: 1 día(s) obtenido(s)", salida.ToString());
        Assert.Single(clima.Llamadas);
    }

    [Fact]
    public void Un_fallo_del_servicio_no_hace_fallar_el_comando()
    {
        CapturaYRegistro("2026-11-14");
        var (cmd, _, error, proveedor) = Cli("clima: {}\n");
        proveedor.Falla = true;

        Assert.Equal(0, cmd.Ejecutar(["clima", "--datos", _datos]));

        Assert.Contains("Clima pendiente", error.ToString());
    }
}
