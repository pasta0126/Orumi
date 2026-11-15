using System.Net;
using Orumi.Agente;
using Orumi.Agente.Clima;
using Orumi.Core.Capturas;
using Orumi.Core.Correcciones;
using Orumi.Core.Ficheros;

namespace Orumi.Tests;

/// <summary>Ciudades que ORUMI no conoce: geocodificación, pregunta y corrección (X67).</summary>
public class CiudadesTests : IDisposable
{
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "orumi-ciu-" + Guid.NewGuid().ToString("N")[..8]);
    private static readonly DateTimeOffset Ahora = new(2026, 11, 20, 23, 0, 0, TimeSpan.FromHours(1));
    private static readonly DateOnly Hoy = new(2026, 11, 20);
    private static readonly ConfiguracionDeClima Config = ConfiguracionDeClima.Leer([])!;
    private int _n;

    public CiudadesTests() => Directory.CreateDirectory(_datos);

    public void Dispose() => Directory.Delete(_datos, true);

    private static CandidatoDeCiudad Lleida => new("Lleida", "Comunidad Autónoma de Cataluña", "España", "ES", 41.61674, 0.62218, 140797);

    private static CandidatoDeCiudad Cerca => new("Lleida", "Cataluña", "España", "ES", 41.6, 0.6, 140797);

    private (string Captura, string Registro) Nota(string fecha, string texto = "Un día cualquiera.", string? ciudad = null)
    {
        var c = CreadorDeCapturas.Crear(_datos, texto, Ahora.AddMinutes(_n++), ciudad: ciudad);
        var id = "reg-" + Guid.NewGuid().ToString("N")[..8];
        var ruta = Path.Combine(_datos, "registros", "2026", "11", id + ".md");
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllText(ruta, $"---\nid: {id}\ntipo: registro\nclase: actividad\ntitulo: Nota\nfuentes:\n  - captura: {c.Id}\n    rol: origen\nfecha: {{desde: {fecha}, hasta: {fecha}}}\n---\nX.\n");
        return (c.Id, id);
    }

    private sealed class Clima1 : IProveedorDeClima
    {
        public List<(string Ciudad, double Lat, double Lon)> Llamadas { get; } = [];

        public Task<IReadOnlyList<ClimaDia>> ObtenerAsync(Ciudad ciudad, DateOnly desde, DateOnly hasta, DateOnly hoy, DateTimeOffset ahora, CancellationToken cancelacion = default)
        {
            Llamadas.Add((ciudad.Nombre, ciudad.LatitudRedondeada, ciudad.LongitudRedondeada));
            IReadOnlyList<ClimaDia> dias = [.. Enumerable.Range(0, hasta.DayNumber - desde.DayNumber + 1).Select(i => new ClimaDia(
                desde.AddDays(i), ciudad.Nombre, "open-meteo", ciudad.LatitudRedondeada, ciudad.LongitudRedondeada, "Europe/Madrid", ahora,
                new ResumenDelDia(3, "07:00", "17:00", 36000, 20000, 0, 10, 18, 70, 40), Enumerable.Range(0, 24).Select(h => new ClimaHora(h, 14, 70, 0, 40, 0, 0, 3)).ToList()))];
            return Task.FromResult(dias);
        }
    }

    private Task<ResultadoDeClima> Ejecutar(Clima1 clima, GeocodificadorFalso geo, DateTimeOffset? ahora = null, int cupo = 5) =>
        ObtencionDeClima.EjecutarAsync(_datos, Almacen.Cargar(_datos), Config, clima, Hoy, ahora ?? Ahora, geocodificador: geo, cupoDePreguntas: cupo);

    private static GeocodificadorFalso Geo(params (string Nombre, CandidatoDeCiudad[] Candidatos)[] r) =>
        new(r.ToDictionary(x => Ciudad.Normalizar(x.Nombre), x => x.Candidatos));

    private List<Orumi.Core.Ficheros.Documento> PreguntasDeCiudad() => Almacen.Cargar(_datos).DeTipo("pregunta").Where(p => p.Frontmatter.GetValueOrDefault("campo") as string == "ciudad").ToList();

    private void Cerrar(string pregunta, string estado, string texto)
    {
        var ruta = Directory.GetFiles(_datos, pregunta + ".md", SearchOption.AllDirectories).Single();
        File.WriteAllText(ruta, File.ReadAllText(ruta).Replace("estado: pendiente", $"estado: {estado}") .Replace("---\nPregunta", $"cerrada_el: 2026-11-21T09:00:00+01:00\nrespuesta: {{texto: \"{texto}\", respondida_el: 2026-11-21T09:00:00+01:00}}\n---\nPregunta"));
    }

    // ---------------------------------------------------------------- geocodificador

    [Fact]
    public void Solo_se_aceptan_lugares_habitados_y_los_aeropuertos_se_descartan()
    {
        const string json = "{\"results\":[{\"name\":\"Lleida\",\"admin1\":\"Comunidad Autónoma de Cataluña\",\"country\":\"España\",\"country_code\":\"ES\",\"latitude\":41.61674,\"longitude\":0.62218,\"population\":140797,\"feature_code\":\"PPLA2\"},"
                            + "{\"name\":\"Aeropuerto de Lleida-Alguaire\",\"country_code\":\"ES\",\"latitude\":41.7,\"longitude\":0.5,\"feature_code\":\"AIRP\"}]}";

        var c = Assert.Single(GeocodificadorOpenMeteo.Analizar(json));

        Assert.Equal("Lleida (Comunidad Autónoma de Cataluña, ES)", c.Descripcion);
        Assert.Equal(140797, c.Poblacion);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"results\":[]}")]
    [InlineData("{\"results\":[{\"name\":\"X\",\"latitude\":999,\"longitude\":0,\"feature_code\":\"PPL\"}]}")]
    public void Una_respuesta_sin_resultados_validos_es_una_lista_vacia(string json) => Assert.Empty(GeocodificadorOpenMeteo.Analizar(json));

    [Fact]
    public async Task La_peticion_de_geocodificacion_solo_lleva_el_nombre_de_la_ciudad()
    {
        var manejador = new ManejadorGeo(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        var g = new GeocodificadorOpenMeteo(new HttpClient(manejador));

        await g.BuscarAsync("Sant Joan de Vilatorrada");

        var p = Assert.Single(manejador.Peticiones);
        Assert.Equal("geocoding-api.open-meteo.com", p.RequestUri!.Host);
        var claves = p.RequestUri.Query.TrimStart('?').Split('&').Select(x => x.Split('=')[0]).ToHashSet();
        Assert.Subset(new HashSet<string> { "name", "count", "language", "format" }, claves);
        Assert.Contains("name=Sant%20Joan%20de%20Vilatorrada", p.RequestUri.Query);
        Assert.Empty(p.Headers);
    }

    private sealed class ManejadorGeo(Func<HttpRequestMessage, HttpResponseMessage> r) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Peticiones { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Peticiones.Add(request);
            return Task.FromResult(r(request));
        }
    }

    [Fact]
    public async Task Un_error_del_geocodificador_es_una_excepcion_controlada()
    {
        var g = new GeocodificadorOpenMeteo(new HttpClient(new ManejadorGeo(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError))));
        await Assert.ThrowsAsync<ClimaNoDisponibleException>(() => g.BuscarAsync("Lleida"));
    }

    [Fact]
    public async Task Un_nombre_vacio_o_enorme_ni_siquiera_sale_a_la_red()
    {
        var manejador = new ManejadorGeo(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var g = new GeocodificadorOpenMeteo(new HttpClient(manejador));

        Assert.Empty(await g.BuscarAsync("  "));
        Assert.Empty(await g.BuscarAsync(new string('a', 80)));
        Assert.Empty(manejador.Peticiones);
    }

    // ---------------------------------------------------------------- cuándo está clara

    [Fact]
    public void Una_ciudad_esta_clara_si_es_unica_o_domina_y_con_dudas_no_se_supone()
    {
        var valencia = new[] { new CandidatoDeCiudad("Valencia", "CV", "España", "ES", 39.5, -0.4, 824340), new CandidatoDeCiudad("Valencia", "Carabobo", "Venezuela", "VE", 10.2, -68.0, 1619470) };
        var andorra = new[] { new CandidatoDeCiudad("Andorra", "Aragón", "España", "ES", 41.0, -0.4, 7890), new CandidatoDeCiudad("Andorra", null, "Andorra", "AD", 42.55, 1.58, 82904) };
        var salou = new[] { new CandidatoDeCiudad("Salou", "Cataluña", "España", "ES", 41.08, 1.14, 26775), new CandidatoDeCiudad("Salou", "Borgou", "Benín", "BJ", 9.5, 3.0, null) };

        Assert.Same(Lleida.Nombre, ElegirCiudad.Clara([Lleida])!.Nombre);
        Assert.Equal("Salou", ElegirCiudad.Clara(salou)!.Nombre);
        Assert.Null(ElegirCiudad.Clara(valencia));
        Assert.Null(ElegirCiudad.Clara(andorra));
        Assert.Null(ElegirCiudad.Clara([]));
        Assert.Null(ElegirCiudad.Clara([new CandidatoDeCiudad("X", null, null, null, 1, 1, null), new CandidatoDeCiudad("X", null, null, null, 2, 2, null)])); // sin población no hay base
    }

    // ---------------------------------------------------------------- ciudad clara: se busca ya y se pregunta después

    [Fact]
    public async Task Si_la_ciudad_esta_clara_se_usa_su_clima_ya_y_se_pregunta_para_confirmar()
    {
        var (_, reg) = Nota("2026-11-18", ciudad: "Lleida");
        var geo = Geo(("Lleida", [Lleida]));
        var clima = new Clima1();

        var r = await Ejecutar(clima, geo);

        Assert.Equal(["Lleida"], geo.Consultas); // solo el nombre que escribió el usuario
        Assert.Equal(["Lleida"], r.Ciudades!.Aprendidas);
        Assert.Equal(("Lleida", 41.6, 0.6), Assert.Single(clima.Llamadas));
        Assert.Empty(r.SinReconocer);
        var p = Assert.Single(PreguntasDeCiudad());
        Assert.Equal("confirmar", p.Frontmatter["ciudad_tipo"]);
        Assert.Equal(reg, p.Frontmatter["registro"]);
        Assert.Contains("creo que es Lleida (Comunidad Autónoma de Cataluña, ES). Ya estoy usando su clima. ¿Es correcta?", (string)p.Frontmatter["texto"]!);
        var d = CiudadesAprendidasStore.Leer(_datos).Ciudades.Single();
        Assert.Equal((41.6, 0.6, CiudadAprendida.PorConfirmar, "geocodificada"), (d.Latitud, d.Longitud, d.Estado, d.Origen)); // coordenadas ya redondeadas
        Assert.True(File.Exists(FormatoDeClima.Ruta(_datos, new DateOnly(2026, 11, 18), new Ciudad("Lleida", 41.6, 0.6))));
    }

    [Fact]
    public async Task Una_ciudad_ya_aprendida_no_vuelve_a_geocodificarse_ni_a_preguntarse()
    {
        Nota("2026-11-18", ciudad: "Lleida");
        var geo = Geo(("Lleida", [Lleida]));
        await Ejecutar(new Clima1(), geo);

        var segunda = await Ejecutar(new Clima1(), geo, Ahora.AddDays(1));

        Assert.Single(geo.Consultas);
        Assert.Empty(segunda.Ciudades!.PreguntasCreadas);
        Assert.Single(PreguntasDeCiudad());
    }

    [Fact]
    public async Task Responder_si_confirma_la_ciudad_y_no_se_vuelve_a_consultar()
    {
        Nota("2026-11-18", ciudad: "Lleida");
        var geo = Geo(("Lleida", [Lleida]));
        await Ejecutar(new Clima1(), geo);
        Cerrar((string)PreguntasDeCiudad().Single().Id!, "respondida", "Sí, es esa");

        var r = await Ejecutar(new Clima1(), geo, Ahora.AddDays(1));

        Assert.Single(r.Ciudades!.RespuestasAplicadas);
        Assert.Equal(CiudadAprendida.Confirmada, CiudadesAprendidasStore.Leer(_datos).Ciudades.Single().Estado);
        Assert.Single(geo.Consultas);
        Assert.Single(PreguntasDeCiudad());
        // Aplicada una vez: no se reprocesa en cada noche.
        var otra = await Ejecutar(new Clima1(), geo, Ahora.AddDays(2));
        Assert.Empty(otra.Ciudades!.RespuestasAplicadas);
    }

    [Fact]
    public async Task Si_el_usuario_corrige_se_corrige_la_ciudad_y_se_vuelve_a_pedir_el_clima_del_sitio_correcto()
    {
        Nota("2026-11-18", ciudad: "Lleida");
        var lleidaEs = new CandidatoDeCiudad("Lleida", "Cataluña", "España", "ES", 41.6, 0.6, 140797);
        var otraLleida = new CandidatoDeCiudad("Lleida", "Rio Grande do Sul", "Brasil", "BR", -29.5, -51.9, 15000);
        var geo = Geo(("Lleida", [otraLleida]), ("Lleida, Cataluña", [lleidaEs]));
        var clima1 = new Clima1();
        await Ejecutar(clima1, geo); // el geocodificador la tenía clara... pero se equivocó
        Assert.Equal(-29.5, clima1.Llamadas.Single().Lat);
        Cerrar((string)PreguntasDeCiudad().Single().Id!, "respondida", "Lleida, Cataluña");

        var clima2 = new Clima1();
        var r = await Ejecutar(clima2, geo, Ahora.AddDays(1));

        var d = CiudadesAprendidasStore.Leer(_datos).Ciudades.Single();
        Assert.Equal((41.6, 0.6, CiudadAprendida.Confirmada, "usuario"), (d.Latitud, d.Longitud, d.Estado, d.Origen));
        Assert.Equal(("Lleida", 41.6, 0.6), Assert.Single(clima2.Llamadas)); // el clima del sitio equivocado se sustituye
        Assert.Equal(1, r.Obtenidos);
    }

    [Fact]
    public async Task Responder_no_descarta_la_ciudad_y_Mira_pregunta_cual_es()
    {
        Nota("2026-11-18", ciudad: "Lleida");
        var geo = Geo(("Lleida", [Lleida]));
        await Ejecutar(new Clima1(), geo);
        Cerrar((string)PreguntasDeCiudad().Single().Id!, "respondida", "No");

        var clima = new Clima1();
        var r = await Ejecutar(clima, geo, Ahora.AddDays(1));

        Assert.Equal(CiudadAprendida.Rechazada, CiudadesAprendidasStore.Leer(_datos).Ciudades.Single().Estado);
        Assert.Empty(clima.Llamadas);                      // ya no se usa esa ubicación
        Assert.Single(r.Ciudades!.PreguntasCreadas);
        var nueva = PreguntasDeCiudad().Single(p => p.Frontmatter["estado"] as string == "pendiente");
        Assert.Equal("elegir", nueva.Frontmatter["ciudad_tipo"]);
        Assert.Contains("no es esa", (string)nueva.Frontmatter["texto"]!);
        Assert.Single(r.SinReconocer.Select(s => s.Ciudad).Distinct());
    }

    // ---------------------------------------------------------------- ciudad dudosa: se pregunta y no se consulta

    [Fact]
    public async Task Con_dudas_no_se_supone_se_pregunta_con_las_opciones_y_no_se_consulta_el_clima()
    {
        Nota("2026-11-18", ciudad: "Valencia");
        var geo = Geo(("Valencia", [new CandidatoDeCiudad("Valencia", "Comunidad Valenciana", "España", "ES", 39.5, -0.4, 824340), new CandidatoDeCiudad("Valencia", "Carabobo", "Venezuela", "VE", 10.2, -68.0, 1619470)]));
        var clima = new Clima1();

        var r = await Ejecutar(clima, geo);

        Assert.Empty(clima.Llamadas);
        Assert.Empty(CiudadesAprendidasStore.Leer(_datos).Ciudades);
        var p = Assert.Single(PreguntasDeCiudad());
        Assert.Equal("elegir", p.Frontmatter["ciudad_tipo"]);
        Assert.Contains("No tengo clara la ciudad «Valencia»", (string)p.Frontmatter["texto"]!);
        Assert.Contains("Valencia (Comunidad Valenciana, ES), o Valencia (Carabobo, VE)", (string)p.Frontmatter["texto"]!);
        Assert.Contains("Hasta entonces no consulto su clima", (string)p.Frontmatter["texto"]!);
        Assert.Single(r.SinReconocer);
    }

    [Fact]
    public async Task Si_no_se_encuentra_la_ciudad_se_pregunta_sin_inventar()
    {
        Nota("2026-11-18", ciudad: "Xyzzyq");

        var r = await Ejecutar(new Clima1(), Geo());

        Assert.Contains("No la encuentro", (string)PreguntasDeCiudad().Single().Frontmatter["texto"]!);
        Assert.Single(r.SinReconocer);
    }

    [Fact]
    public async Task Responder_con_la_ciudad_exacta_resuelve_la_pregunta_de_eleccion()
    {
        Nota("2026-11-18", ciudad: "Valencia");
        var geo = Geo(("Valencia", [new CandidatoDeCiudad("Valencia", "CV", "España", "ES", 39.5, -0.4, 824340), new CandidatoDeCiudad("Valencia", "Carabobo", "Venezuela", "VE", 10.2, -68.0, 1619470)]),
            ("Valencia, España", [new CandidatoDeCiudad("Valencia", "CV", "España", "ES", 39.47, -0.38, 824340)]));
        await Ejecutar(new Clima1(), geo);
        Cerrar((string)PreguntasDeCiudad().Single().Id!, "respondida", "Valencia, España");

        var clima = new Clima1();
        var r = await Ejecutar(clima, geo, Ahora.AddDays(1));

        Assert.Equal(("Valencia", 39.5, -0.4), Assert.Single(clima.Llamadas));
        Assert.Empty(r.SinReconocer);
        Assert.Equal(CiudadAprendida.Confirmada, CiudadesAprendidasStore.Leer(_datos).Ciudades.Single().Estado);
    }

    [Theory]
    [InlineData("no_relevante")]
    [InlineData("no_recuerda")]
    public async Task Si_el_usuario_cierra_la_pregunta_sin_contestar_no_se_insiste(string cierre)
    {
        Nota("2026-11-18", ciudad: "Xyzzyq");
        var geo = Geo();
        await Ejecutar(new Clima1(), geo);
        Cerrar((string)PreguntasDeCiudad().Single().Id!, cierre, "no");

        var r = await Ejecutar(new Clima1(), geo, Ahora.AddDays(1));

        Assert.Empty(r.Ciudades!.PreguntasCreadas);
        Assert.Single(PreguntasDeCiudad());
        Assert.Single(r.SinReconocer); // sigue sin ubicar, sin molestar
    }

    // ---------------------------------------------------------------- robustez

    [Fact]
    public async Task Si_el_geocodificador_cae_no_se_pregunta_ni_se_supone_y_se_reintenta()
    {
        Nota("2026-11-18", ciudad: "Lleida");
        var geo = Geo(("Lleida", [Lleida]));
        geo.Falla = true;

        var r = await Ejecutar(new Clima1(), geo);

        Assert.Single(r.Ciudades!.Avisos);
        Assert.Empty(PreguntasDeCiudad());
        Assert.Empty(CiudadesAprendidasStore.Leer(_datos).Ciudades);
        geo.Falla = false;
        Assert.Equal(["Lleida"], (await Ejecutar(new Clima1(), geo, Ahora.AddDays(1))).Ciudades!.Aprendidas);
    }

    [Fact]
    public async Task Si_Mira_ya_tiene_su_maximo_de_preguntas_pendientes_usa_la_ciudad_y_pregunta_mas_tarde()
    {
        Nota("2026-11-18", ciudad: "Lleida");
        var geo = Geo(("Lleida", [Lleida]));
        var clima = new Clima1();

        var r = await Ejecutar(clima, geo, cupo: 0);

        Assert.Single(clima.Llamadas);
        Assert.Empty(r.Ciudades!.PreguntasCreadas);
        var luego = await Ejecutar(new Clima1(), geo, Ahora.AddDays(1), cupo: 5);
        Assert.Single(luego.Ciudades!.PreguntasCreadas);
    }

    [Fact]
    public async Task Sin_geocodificador_el_comportamiento_es_el_de_antes_no_se_consulta_ni_se_pregunta_nada()
    {
        Nota("2026-11-18", ciudad: "Lleida");
        var clima = new Clima1();

        var r = await ObtencionDeClima.EjecutarAsync(_datos, Almacen.Cargar(_datos), Config, clima, Hoy, Ahora);

        Assert.Empty(clima.Llamadas);
        Assert.Single(r.SinReconocer);
        Assert.Null(r.Ciudades);
        Assert.Empty(PreguntasDeCiudad());
    }

    // ---------------------------------------------------------------- notas que mencionan otra ciudad

    [Fact]
    public async Task Una_nota_sin_ciudad_que_menciona_una_conocida_genera_una_pregunta_y_no_cambia_nada_sola()
    {
        var (cap, _) = Nota("2026-11-18", "Hoy hemos ido a comer a Segur de Calafell con mis padres.");
        var clima = new Clima1();

        var r = await Ejecutar(clima, Geo());

        var p = Assert.Single(PreguntasDeCiudad());
        Assert.Equal("mencion", p.Frontmatter["ciudad_tipo"]);
        Assert.Equal(cap, p.Frontmatter["ciudad_captura"]);
        Assert.Contains("aparece «Segur de Calafell», pero la nota no indica ciudad y se ha tomado como Barcelona. ¿Estabas en Segur de Calafell?", (string)p.Frontmatter["texto"]!);
        Assert.Equal("Barcelona", clima.Llamadas.Single().Ciudad); // sigue siendo Barcelona hasta que conteste
        Assert.Empty(CiudadesAprendidasStore.Leer(_datos).Notas);
        Assert.Single(r.Ciudades!.PreguntasCreadas);
    }

    [Fact]
    public async Task Responder_si_fija_la_ciudad_de_esa_nota_y_su_clima_pasa_a_ser_el_de_alli()
    {
        var (cap, _) = Nota("2026-11-18", "Hoy hemos ido a comer a Segur de Calafell con mis padres.");
        var geo = Geo();
        await Ejecutar(new Clima1(), geo);
        Cerrar((string)PreguntasDeCiudad().Single().Id!, "respondida", "Sí");

        var clima = new Clima1();
        await Ejecutar(clima, geo, Ahora.AddDays(1));

        Assert.Equal(cap, CiudadesAprendidasStore.Leer(_datos).Notas.Single().Captura);
        Assert.Equal("Segur de Calafell", clima.Llamadas.Single().Ciudad);
        Assert.Equal("Segur de Calafell", Config.ConAprendidas(_datos).ResolverDeCaptura(cap, null).Ciudad!.Nombre);
    }

    [Fact]
    public async Task Responder_no_deja_la_nota_como_estaba_y_no_se_vuelve_a_preguntar()
    {
        Nota("2026-11-18", "Hoy hemos ido a comer a Segur de Calafell con mis padres.");
        var geo = Geo();
        await Ejecutar(new Clima1(), geo);
        Cerrar((string)PreguntasDeCiudad().Single().Id!, "respondida", "No, era desde casa");

        var clima = new Clima1();
        var r = await Ejecutar(clima, geo, Ahora.AddDays(1));

        Assert.Empty(CiudadesAprendidasStore.Leer(_datos).Notas);
        Assert.Empty(clima.Llamadas); // sigue siendo Barcelona y su clima ya estaba guardado
        Assert.True(File.Exists(FormatoDeClima.Ruta(_datos, new DateOnly(2026, 11, 18), Config.PorDefecto)));
        Assert.Empty(r.Ciudades!.PreguntasCreadas);
    }

    [Fact]
    public async Task No_se_pregunta_si_la_nota_ya_indica_ciudad_o_solo_menciona_la_ciudad_por_defecto()
    {
        Nota("2026-11-18", "Fuimos a Segur de Calafell.", ciudad: "Segur de Calafell");
        Nota("2026-11-19", "Hoy en Barcelona todo normal.");

        await Ejecutar(new Clima1(), Geo());

        Assert.Empty(PreguntasDeCiudad());
    }

    [Fact]
    public async Task Responder_con_la_operacion_del_usuario_a_una_pregunta_de_ciudad_la_cierra_sin_tocar_el_registro()
    {
        Nota("2026-11-18", ciudad: "Lleida");
        var geo = Geo(("Lleida", [Lleida]));
        await Ejecutar(new Clima1(), geo);
        var pregunta = PreguntasDeCiudad().Single();

        var r = new Orumi.Core.Correcciones.Operaciones(_datos, () => Ahora).Responder((string)pregunta.Id!, "Sí");

        Assert.Contains("respondida", r.Mensaje);
        var cerrada = Orumi.Core.Ficheros.Almacen.Cargar(_datos).DeTipo("pregunta").Single(p => p.Id == pregunta.Id);
        Assert.Equal("respondida", cerrada.Frontmatter["estado"]);
    }
}
