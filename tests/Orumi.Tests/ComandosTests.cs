using System.Text;
using Orumi.Cli;
using Orumi.Core.Ficheros;

namespace Orumi.Tests;

public class ComandosTests : IDisposable
{
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "orumi-cli-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _salida = new();
    private readonly StringWriter _error = new();
    private static readonly DateTimeOffset Ahora = new(2026, 11, 16, 9, 0, 0, TimeSpan.FromHours(1));

    public void Dispose()
    {
        if (Directory.Exists(_datos))
        {
            Directory.Delete(_datos, true);
        }
    }

    private int Ejecutar(string stdin, params string[] args)
    {
        var c = new Comandos(
            () => new MemoryStream(new UTF8Encoding(false).GetBytes(stdin)),
            _salida,
            _error,
            () => Ahora,
            _ => null);
        return c.Ejecutar(args);
    }

    [Fact]
    public void Capturar_por_entrada_estandar_crea_la_captura_y_la_confirma()
    {
        var codigo = Ejecutar("Nota por stdin\ncon dos líneas\n", "capturar", "--datos", _datos);

        Assert.Equal(0, codigo);
        Assert.Contains("Captura creada: cap-20261116-0900-", _salida.ToString());
        var fichero = Directory.GetFiles(_datos, "*.md", SearchOption.AllDirectories).Single();
        Assert.Equal("Nota por stdin\ncon dos líneas\n", Documento.Analizar(fichero, File.ReadAllText(fichero)).Cuerpo);
    }

    [Fact]
    public void Capturar_con_texto_y_metadatos()
    {
        var codigo = Ejecutar("", "capturar", "--datos", _datos, "--texto", "Hola", "--fecha-acontecimiento", "2026-11-08", "--etiqueta", "casa");

        Assert.Equal(0, codigo);
        var f = Directory.GetFiles(_datos, "*.md", SearchOption.AllDirectories).Single();
        var fm = Documento.Analizar(f, File.ReadAllText(f)).Frontmatter;
        Assert.Equal("2026-11-08", fm["fecha_acontecimiento"]);
        Assert.Equal("casa", fm["etiqueta"]);
    }

    [Fact]
    public void Capturar_texto_vacio_falla_sin_crear_nada()
    {
        var codigo = Ejecutar("   \n", "capturar", "--datos", _datos);

        Assert.Equal(1, codigo);
        Assert.Contains("vacío", _error.ToString());
        Assert.False(Directory.Exists(_datos) && Directory.GetFiles(_datos, "*", SearchOption.AllDirectories).Length > 0);
    }

    [Fact]
    public void Sin_directorio_de_datos_pide_indicarlo()
    {
        var codigo = Ejecutar("x", "capturar");
        Assert.Equal(2, codigo);
        Assert.Contains("ORUMI_DATOS", _error.ToString());
    }

    [Fact]
    public void Validar_los_ejemplos_da_cero_errores()
    {
        var codigo = Ejecutar("", "validar", "--datos", Ayudantes.Ejemplos());
        Assert.Equal(0, codigo);
        Assert.Contains("OK: sin errores (5 capturas, 7 registros, 4 preguntas, 5 lotes)", _salida.ToString());
    }

    [Fact]
    public void Validar_informa_por_fichero_y_devuelve_error()
    {
        var copia = Ayudantes.CopiarEjemplos();
        try
        {
            Ayudantes.Reemplazar(Ayudantes.Buscar(copia, "cap-20261110-2140"), "estantería del salón", "estantería del comedor");
            var codigo = Ejecutar("", "validar", "--datos", copia);

            Assert.Equal(1, codigo);
            Assert.Contains("cap-20261110-2140-x9d.md: la huella no coincide con el cuerpo", _salida.ToString());
        }
        finally
        {
            Directory.Delete(copia, true);
        }
    }

    [Fact]
    public void Un_comando_desconocido_da_uso()
    {
        Assert.Equal(2, Ejecutar("", "volar"));
    }
}
