using Orumi.Core.Capturas;
using Orumi.Core.Ficheros;
using Orumi.Web;

namespace Orumi.Tests;

public class CapturaWebTests : IDisposable
{
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "orumi-web-" + Guid.NewGuid().ToString("N")[..8]);
    private DateTimeOffset _ahora = new(2026, 11, 16, 9, 0, 0, TimeSpan.FromHours(1));

    public CapturaWebTests() => Directory.CreateDirectory(_datos);

    public void Dispose() => Directory.Delete(_datos, true);

    private ServicioDeCaptura Servicio() => new(_datos, () => _ahora);

    private static SolicitudDeCaptura Nota(string? texto = "Hoy llamé a Marta.", bool voz = false, string? fecha = null, string? etiqueta = null, string? dispositivo = null, string? id = null) =>
        new(texto, voz, fecha, etiqueta, dispositivo, id ?? Guid.NewGuid().ToString());

    private Documento Capturada(string id) => Almacen.Cargar(_datos).DeTipo("captura").Single(d => d.Id == id);

    // ---------------------------------------------------------------- creación

    [Fact]
    public void Una_nota_escrita_crea_una_captura_con_origen_texto()
    {
        var r = Servicio().Crear(Nota());

        var c = Capturada(r.Id);
        Assert.False(r.Repetida);
        Assert.Equal("texto", c.Frontmatter["origen"]);
        Assert.Equal("Hoy llamé a Marta.", c.Cuerpo);
        Assert.Equal(Huella.De("Hoy llamé a Marta."), c.Frontmatter["huella"]);
    }

    [Fact]
    public void Una_nota_dictada_crea_una_captura_con_origen_voz_y_el_texto_revisado()
    {
        var r = Servicio().Crear(Nota("Texto tras revisarlo.", voz: true, dispositivo: "web-movil"));

        var c = Capturada(r.Id);
        Assert.Equal("voz", c.Frontmatter["origen"]);
        Assert.Equal("Texto tras revisarlo.", c.Cuerpo);
        Assert.Equal("web-movil", ((Dictionary<string, object?>)c.Frontmatter["contexto_automatico"]!)["dispositivo"]);
    }

    [Fact]
    public void Los_metadatos_opcionales_se_guardan_y_un_dispositivo_desconocido_se_normaliza()
    {
        var r = Servicio().Crear(Nota(fecha: "2026-11-08", etiqueta: " casa ", dispositivo: "x; rm -rf"));

        var c = Capturada(r.Id);
        Assert.Equal("2026-11-08", c.Frontmatter["fecha_acontecimiento"]);
        Assert.Equal("casa", c.Frontmatter["etiqueta"]);
        Assert.Equal("web", ((Dictionary<string, object?>)c.Frontmatter["contexto_automatico"]!)["dispositivo"]);
        Assert.Empty(Orumi.Core.Validacion.Validador.Validar(Almacen.Cargar(_datos)).Where(i => !i.Mensaje.Contains("sin lote")));
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("   \n ", null)]
    [InlineData(null, null)]
    [InlineData("Una nota.", "ayer")]
    [InlineData("Una nota.", "2026-13-40")]
    public void Una_nota_vacia_o_con_fecha_invalida_no_crea_nada(string? texto, string? fecha)
    {
        Assert.Throws<CapturaInvalidaException>(() => Servicio().Crear(Nota(texto, fecha: fecha)));
        Assert.Empty(Directory.GetFiles(_datos, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Sin_un_identificador_de_envio_valido_no_se_crea_nada()
    {
        Assert.Throws<CapturaInvalidaException>(() => Servicio().Crear(Nota(id: "../../etc/passwd")));
        Assert.Throws<CapturaInvalidaException>(() => Servicio().Crear(new SolicitudDeCaptura("Hola.", false, null, null, null, null)));
        Assert.Empty(Directory.GetFiles(_datos, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Una_nota_demasiado_larga_se_rechaza()
    {
        Assert.Throws<CapturaInvalidaException>(() => Servicio().Crear(Nota(new string('a', ServicioDeCaptura.LongitudMaxima + 1))));
    }

    // ---------------------------------------------------------------- idempotencia

    [Fact]
    public void Reenviar_el_mismo_envio_devuelve_la_misma_captura_sin_crear_otra()
    {
        var id = Guid.NewGuid().ToString();
        var primera = Servicio().Crear(Nota(id: id));
        _ahora = _ahora.AddMinutes(5);
        var segunda = Servicio().Crear(Nota(id: id));

        Assert.Equal(primera.Id, segunda.Id);
        Assert.True(segunda.Repetida);
        Assert.Single(Almacen.Cargar(_datos).DeTipo("captura"));
    }

    [Fact]
    public void Dos_envios_distintos_crean_dos_capturas_y_la_marca_de_envio_no_la_ve_el_resto_del_sistema()
    {
        Servicio().Crear(Nota("Primera."));
        Servicio().Crear(Nota("Segunda."));

        Assert.Equal(2, Almacen.Cargar(_datos).DeTipo("captura").Count());
        Assert.Empty(Orumi.Core.Validacion.Validador.Validar(Almacen.Cargar(_datos)).Where(i => !i.Mensaje.Contains("sin lote")));
    }

    // ---------------------------------------------------------------- acceso

    private static AccesoPorToken Acceso(Func<DateTimeOffset> reloj) => new(new string('t', 40), reloj, maxFallos: 3, bloqueo: TimeSpan.FromMinutes(15));

    [Fact]
    public void Con_el_token_correcto_se_permite_y_con_otro_se_deniega()
    {
        var a = Acceso(() => _ahora);
        Assert.Equal(ResultadoDeAcceso.Permitido, a.Comprobar(new string('t', 40), "1.1.1.1"));
        Assert.Equal(ResultadoDeAcceso.Denegado, a.Comprobar("otro", "1.1.1.1"));
        Assert.Equal(ResultadoDeAcceso.Denegado, a.Comprobar(null, "1.1.1.1"));
    }

    [Fact]
    public void Tras_varios_fallos_se_bloquea_el_origen_incluso_con_el_token_correcto_y_luego_se_libera()
    {
        var a = Acceso(() => _ahora);
        Assert.Equal(ResultadoDeAcceso.Denegado, a.Comprobar("x", "2.2.2.2"));
        Assert.Equal(ResultadoDeAcceso.Denegado, a.Comprobar("x", "2.2.2.2"));
        Assert.Equal(ResultadoDeAcceso.Bloqueado, a.Comprobar("x", "2.2.2.2"));
        Assert.Equal(ResultadoDeAcceso.Bloqueado, a.Comprobar(new string('t', 40), "2.2.2.2"));
        Assert.Equal(ResultadoDeAcceso.Permitido, a.Comprobar(new string('t', 40), "3.3.3.3")); // otro origen no afectado

        _ahora = _ahora.AddMinutes(16);
        Assert.Equal(ResultadoDeAcceso.Permitido, a.Comprobar(new string('t', 40), "2.2.2.2"));
    }

    [Fact]
    public void Un_acierto_reinicia_la_cuenta_de_fallos()
    {
        var a = Acceso(() => _ahora);
        a.Comprobar("x", "4.4.4.4");
        a.Comprobar("x", "4.4.4.4");
        a.Comprobar(new string('t', 40), "4.4.4.4");
        Assert.Equal(ResultadoDeAcceso.Denegado, a.Comprobar("x", "4.4.4.4"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("corto")]
    public void Un_token_demasiado_corto_no_se_admite(string token) =>
        Assert.Throws<ArgumentException>(() => new AccesoPorToken(token, () => DateTimeOffset.UtcNow));

    // ---------------------------------------------------------------- alcance mínimo

    [Fact]
    public void El_servicio_solo_expone_la_creacion_de_capturas()
    {
        var publicos = typeof(ServicioDeCaptura).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(m => m.Name).ToList();
        Assert.Equal(["Crear"], publicos);
    }
}
