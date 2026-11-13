namespace Orumi.Tests;

internal static class Ayudantes
{
    public static string RaizRepositorio()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Orumi.slnx")))
        {
            d = d.Parent;
        }

        return d?.FullName ?? throw new InvalidOperationException("No se encuentra la raíz del repositorio.");
    }

    public static string Ejemplos() => Path.Combine(RaizRepositorio(), "docs", "ejemplos", "orumi");

    /// <summary>Copia los ejemplos a un directorio temporal para mutarlos sin tocar el original.</summary>
    public static string CopiarEjemplos()
    {
        var destino = Path.Combine(Path.GetTempPath(), "orumi-tests-" + Guid.NewGuid().ToString("N")[..8]);
        CopiarDirectorio(Ejemplos(), destino);
        return destino;
    }

    private static void CopiarDirectorio(string origen, string destino)
    {
        Directory.CreateDirectory(destino);
        foreach (var f in Directory.GetFiles(origen))
        {
            File.Copy(f, Path.Combine(destino, Path.GetFileName(f)));
        }

        foreach (var d in Directory.GetDirectories(origen))
        {
            CopiarDirectorio(d, Path.Combine(destino, Path.GetFileName(d)));
        }
    }

    public static string Buscar(string raiz, string fragmentoNombre) =>
        Directory.GetFiles(raiz, "*.md", SearchOption.AllDirectories).Single(f => Path.GetFileName(f).Contains(fragmentoNombre));

    public static void Reemplazar(string fichero, string antes, string despues)
    {
        var t = File.ReadAllText(fichero);
        if (!t.Contains(antes))
        {
            throw new InvalidOperationException($"No se encuentra '{antes}' en {fichero}");
        }

        File.WriteAllText(fichero, t.Replace(antes, despues));
    }
}
