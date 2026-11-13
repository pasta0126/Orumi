using Orumi.Core.Capturas;
using Orumi.Core.Ficheros;
using Orumi.Core.Validacion;
using Orumi.Core.Yaml;

namespace Orumi.Tests;

public class CapturasTests : IDisposable
{
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "orumi-capt-" + Guid.NewGuid().ToString("N")[..8]);
    private static readonly DateTimeOffset Ahora = new(2026, 11, 16, 8, 15, 0, TimeSpan.FromHours(1));

    public CapturasTests() => Directory.CreateDirectory(_datos);

    public void Dispose() => Directory.Delete(_datos, true);

    private static string Leer(CapturaCreada c) => File.ReadAllText(c.Ruta);

    [Fact]
    public void Guarda_el_texto_literal_con_su_huella()
    {
        const string texto = "Esta es mi nota con erratas: copmiodo, un salto\n\ny espacios finales   ";
        var c = CreadorDeCapturas.Crear(_datos, texto, Ahora);

        var doc = Documento.Analizar(c.Ruta, Leer(c));
        Assert.Equal(texto, doc.Cuerpo);
        Assert.Equal(Huella.De(texto), doc.Frontmatter["huella"]);
        Assert.Equal("captura", doc.Tipo);
        Assert.Equal("texto", doc.Frontmatter["origen"]);
        Assert.Equal("2026-11-16T08:15:00+01:00", doc.Frontmatter["comunicada_el"]);
        Assert.StartsWith("cap-20261116-0815-", c.Id);
        Assert.EndsWith($"capturas/2026/11/{c.Id}.md", c.Ruta.Replace('\\', '/'));
    }

    [Fact]
    public void Conserva_saltos_crlf_y_texto_con_dos_puntos_y_comillas()
    {
        const string texto = "linea uno\r\nlinea \"dos\": tres\r\n---\r\ncuatro";
        var c = CreadorDeCapturas.Crear(_datos, texto, Ahora);
        Assert.Equal(texto, Documento.Analizar(c.Ruta, Leer(c)).Cuerpo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\t  ")]
    public void Rechaza_texto_vacio_sin_crear_ficheros(string texto)
    {
        Assert.Throws<CapturaInvalidaException>(() => CreadorDeCapturas.Crear(_datos, texto, Ahora));
        Assert.Empty(Directory.GetFiles(_datos, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Guarda_los_metadatos_opcionales_sin_cambiar_la_fecha_de_comunicacion()
    {
        var c = CreadorDeCapturas.Crear(_datos, "Del domingo", Ahora, fechaAcontecimiento: "2026-11-08", etiqueta: "casa: reforma");
        var fm = Documento.Analizar(c.Ruta, Leer(c)).Frontmatter;
        Assert.Equal("2026-11-08", fm["fecha_acontecimiento"]);
        Assert.Equal("casa: reforma", fm["etiqueta"]);
        Assert.Equal("2026-11-16T08:15:00+01:00", fm["comunicada_el"]);
    }

    [Fact]
    public void Rechaza_una_fecha_de_acontecimiento_mal_formada()
    {
        Assert.Throws<CapturaInvalidaException>(() =>
            CreadorDeCapturas.Crear(_datos, "x", Ahora, fechaAcontecimiento: "8 de noviembre"));
    }

    [Fact]
    public void No_guarda_ubicacion_ni_claves_ajenas()
    {
        var c = CreadorDeCapturas.Crear(_datos, "hola", Ahora, dispositivo: "movil");
        var fm = Documento.Analizar(c.Ruta, Leer(c)).Frontmatter;
        Assert.DoesNotContain(fm.Keys, k => k.Contains("ubicacion") || k.Contains("lugar") || k.Contains("gps"));
        Assert.Equal(["dispositivo"], ((Dictionary<string, object?>)fm["contexto_automatico"]!).Keys.ToArray());
    }

    [Fact]
    public void Ante_una_colision_de_identificador_genera_otro_y_no_toca_la_captura_existente()
    {
        var primera = CreadorDeCapturas.Crear(_datos, "primera", Ahora, generadorId: _ => "cap-20261116-0815-aaa");
        var original = Leer(primera);

        var llamadas = 0;
        var segunda = CreadorDeCapturas.Crear(_datos, "segunda", Ahora,
            generadorId: _ => llamadas++ == 0 ? "cap-20261116-0815-aaa" : "cap-20261116-0815-bbb");

        Assert.Equal("cap-20261116-0815-bbb", segunda.Id);
        Assert.Equal(original, Leer(primera));
        Assert.Equal("segunda", Documento.Analizar(segunda.Ruta, Leer(segunda)).Cuerpo);
    }

    [Fact]
    public void Una_captura_creada_pasa_la_validacion_de_captura()
    {
        CreadorDeCapturas.Crear(_datos, "nota", Ahora);
        var errores = Validador.Validar(Almacen.Cargar(_datos));
        // Sin lote todavía: lo único que se espera es «captura sin lote»; nada de formato.
        Assert.All(errores, i => Assert.Equal("captura sin lote", i.Mensaje));
    }

    [Fact]
    public void No_queda_ningun_temporal_tras_escribir()
    {
        CreadorDeCapturas.Crear(_datos, "nota", Ahora);
        Assert.DoesNotContain(Directory.GetFiles(_datos, "*.tmp", SearchOption.AllDirectories), _ => true);
    }

    [Fact]
    public void Un_fallo_de_escritura_no_deja_ficheros_ni_confirma()
    {
        var destino = Path.Combine(_datos, "capturas", "2026", "11");
        Directory.CreateDirectory(destino);
        File.SetUnixFileMode(destino, UnixFileMode.UserRead | UnixFileMode.UserExecute); // sin permiso de escritura
        try
        {
            Assert.Throws<EscrituraFallidaException>(() => CreadorDeCapturas.Crear(_datos, "nota", Ahora));
        }
        finally
        {
            File.SetUnixFileMode(destino, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Assert.Empty(Directory.GetFiles(_datos, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void La_escritura_atomica_no_sobrescribe()
    {
        var ruta = Path.Combine(_datos, "x", "fichero.md");
        EscrituraAtomica.EscribirNuevo(ruta, "uno");
        Assert.Throws<EscrituraFallidaException>(() => EscrituraAtomica.EscribirNuevo(ruta, "dos"));
        Assert.Equal("uno", File.ReadAllText(ruta));
    }
}

public class YamlEscritorTests
{
    private static Dictionary<string, object?> IdaYVuelta(YMapa m) => YamlLector.LeerMapa(YamlEscritor.Escribir(m));

    [Theory]
    [InlineData("Outlook caído: no salen los correos")]
    [InlineData("texto con # almohadilla y \"comillas\"")]
    [InlineData("true")]
    [InlineData("2026-11-13")]
    [InlineData("09:30")]
    [InlineData("12.5")]
    [InlineData("- empieza por guion")]
    [InlineData("termina en dos puntos:")]
    [InlineData("")]
    [InlineData(" espacios al borde ")]
    public void Los_textos_dificiles_se_leen_igual_que_se_escribieron(string texto)
    {
        var leido = IdaYVuelta(YMapa.De(("t", new YTexto(texto))));
        Assert.Equal(texto, leido["t"]);
    }

    [Theory]
    [InlineData("a, b")]
    [InlineData("a: b")]
    [InlineData("[x]")]
    [InlineData("llaves {x}")]
    public void Los_textos_dentro_de_mapas_en_linea_se_leen_igual(string texto)
    {
        var leido = IdaYVuelta(YMapa.De(("m", YMapa.EnLineaDe(("a", new YTexto(texto)), ("b", new YLiteral("1"))))));
        var m = (Dictionary<string, object?>)leido["m"]!;
        Assert.Equal(texto, m["a"]);
        Assert.Equal("1", m["b"]);
    }

    [Fact]
    public void Los_instantes_van_entre_comillas_solo_dentro_de_mapas_en_linea()
    {
        var yaml = YamlEscritor.Escribir(YMapa.De(
            ("a", new YInstante("2026-11-13T13:23:00+01:00")),
            ("b", YMapa.EnLineaDe(("c", new YInstante("2026-11-13T13:23:00+01:00"))))));
        Assert.Contains("a: 2026-11-13T13:23:00+01:00\n", yaml);
        Assert.Contains("b: {c: \"2026-11-13T13:23:00+01:00\"}", yaml);
        Assert.NotEmpty(YamlLector.LeerMapa(yaml));
    }

    [Fact]
    public void Listas_y_mapas_en_bloque_y_en_linea_se_leen_bien()
    {
        var m = IdaYVuelta(YMapa.De(
            ("lista", new YLista([new YLiteral("a"), new YTexto("b: c")], EnLinea: true)),
            ("fuentes", new YLista([
                YMapa.De(("captura", new YLiteral("cap-1")), ("fragmento", YMapa.EnLineaDe(("desde", new YLiteral("0")), ("hasta", new YLiteral("5"))))),
                YMapa.De(("captura", new YLiteral("cap-2"))),
            ])),
            ("vacia", new YLista([], EnLinea: true))));

        Assert.Equal(["a", "b: c"], ((List<object?>)m["lista"]!).Cast<string>().ToArray());
        var fuentes = (List<object?>)m["fuentes"]!;
        Assert.Equal(2, fuentes.Count);
        Assert.Equal("cap-1", ((Dictionary<string, object?>)fuentes[0]!)["captura"]);
        Assert.Empty((List<object?>)m["vacia"]!);
    }
}
