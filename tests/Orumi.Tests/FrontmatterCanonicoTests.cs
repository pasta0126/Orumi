using Orumi.Core.Ficheros;

namespace Orumi.Tests;

public class FrontmatterCanonicoTests
{
    public static IEnumerable<object[]> Ficheros()
    {
        var a = Almacen.Cargar(Ayudantes.Ejemplos());
        foreach (var d in a.Documentos.Where(d => d.Tipo is "registro" or "pregunta" or "lote"))
        {
            yield return [d.Ruta];
        }
    }

    [Theory]
    [MemberData(nameof(Ficheros))]
    public void Reescribir_un_fichero_de_los_ejemplos_conserva_su_contenido(string ruta)
    {
        var original = Documento.Analizar(ruta, File.ReadAllText(Path.Combine(Ayudantes.Ejemplos(), ruta)));
        var texto = FrontmatterCanonico.Escribir(original.Frontmatter, original.Cuerpo, original.Tipo!);
        var reescrito = Documento.Analizar(ruta, texto);

        Assert.Equal(Normalizar(original.Frontmatter), Normalizar(reescrito.Frontmatter));
        Assert.Equal(original.Cuerpo, reescrito.Cuerpo);
    }

    [Fact]
    public void El_conjunto_reescrito_sigue_validando_sin_errores()
    {
        var copia = Ayudantes.CopiarEjemplos();
        try
        {
            var almacen = Almacen.Cargar(copia);
            foreach (var d in almacen.Documentos.Where(d => d.Tipo is "registro" or "pregunta" or "lote"))
            {
                File.WriteAllText(Path.Combine(copia, d.Ruta), FrontmatterCanonico.Escribir(d.Frontmatter, d.Cuerpo, d.Tipo!));
            }

            Assert.Empty(Orumi.Core.Validacion.Validador.Validar(Almacen.Cargar(copia)));
        }
        finally
        {
            Directory.Delete(copia, true);
        }
    }

    private static string Normalizar(object? v) => v switch
    {
        Dictionary<string, object?> m => "{" + string.Join(",", m.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + Normalizar(p.Value))) + "}",
        List<object?> l => "[" + string.Join(",", l.Select(Normalizar)) + "]",
        _ => v?.ToString() ?? "null",
    };
}
