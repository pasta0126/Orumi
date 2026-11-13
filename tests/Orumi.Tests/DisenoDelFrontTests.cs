using System.Text.RegularExpressions;

namespace Orumi.Tests;

/// <summary>Reglas verificables de la spec diseno-del-front sobre los ficheros de las dos interfaces web.</summary>
public partial class DisenoDelFrontTests
{
    private static readonly string Raiz = BuscarRaiz();

    private static string BuscarRaiz()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Orumi.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir!;
    }

    private static readonly string[] Interfaces = [Path.Combine("src", "Orumi.Panel", "wwwroot"), Path.Combine("src", "Orumi.Web", "wwwroot")];

    private static IEnumerable<string> Ficheros(params string[] extensiones) =>
        Interfaces.SelectMany(i => Directory.EnumerateFiles(Path.Combine(Raiz, i)))
            .Where(f => extensiones.Contains(Path.GetExtension(f)) && !Path.GetFileName(f).StartsWith("dev-")); // dev-*: utilidades locales no versionadas

    private static string Nombre(string f) => Path.GetRelativePath(Raiz, f);

    // Emojis: planos suplementarios 1F000–1FAFF (pares sustitutos D83C–D83E) y los símbolos, dingbats y selectores del plano básico.
    [GeneratedRegex(@"[\uD83C-\uD83E][\uDC00-\uDFFF]|[\u2600-\u27BF\u2300-\u23FF\u2B00-\u2BFF\uFE0F\u200D\u20E3]")] private static partial Regex EmojiRx();
    [GeneratedRegex(@"(^|\s)[:;]-?[)(DP](\s|$)")] private static partial Regex EmoticonoRx();
    [GeneratedRegex(@"\sstyle\s*=", RegexOptions.IgnoreCase)] private static partial Regex EstiloEnLineaRx();
    [GeneratedRegex(@"<style", RegexOptions.IgnoreCase)] private static partial Regex BloqueEstiloRx();
    [GeneratedRegex(@"<script(?![^>]*\bsrc\s*=)", RegexOptions.IgnoreCase)] private static partial Regex ScriptEnLineaRx();
    [GeneratedRegex(@"\son[a-z]+\s*=", RegexOptions.IgnoreCase)] private static partial Regex ManejadorEnLineaRx();
    [GeneratedRegex(@"(https?:)?//[a-z0-9.-]+\.[a-z]{2,}", RegexOptions.IgnoreCase)] private static partial Regex RecursoExternoRx();

    [Fact]
    public void Ningun_fichero_de_la_interfaz_contiene_emojis_ni_emoticonos()
    {
        foreach (var f in Ficheros(".html", ".css", ".js", ".svg", ".webmanifest"))
        {
            var t = File.ReadAllText(f);
            Assert.False(EmojiRx().IsMatch(t), $"{Nombre(f)} contiene un emoji: {EmojiRx().Match(t).Value}");
            Assert.False(EmoticonoRx().IsMatch(t), $"{Nombre(f)} contiene un emoticono: {EmoticonoRx().Match(t).Value.Trim()}");
        }
    }

    [Fact]
    public void Los_html_no_tienen_estilos_scripts_ni_manejadores_en_linea_ni_cargan_recursos_externos()
    {
        foreach (var f in Ficheros(".html"))
        {
            var t = File.ReadAllText(f);
            Assert.False(EstiloEnLineaRx().IsMatch(t), $"{Nombre(f)}: atributo style en línea");
            Assert.False(BloqueEstiloRx().IsMatch(t), $"{Nombre(f)}: bloque <style>");
            Assert.False(ScriptEnLineaRx().IsMatch(t), $"{Nombre(f)}: script en línea");
            Assert.False(ManejadorEnLineaRx().IsMatch(t), $"{Nombre(f)}: manejador de evento en línea");
            Assert.False(RecursoExternoRx().IsMatch(t), $"{Nombre(f)}: recurso externo {RecursoExternoRx().Match(t).Value}");
        }
    }

    [Fact]
    public void Las_hojas_de_estilo_no_importan_ni_cargan_nada_de_fuera()
    {
        foreach (var f in Ficheros(".css"))
        {
            var t = File.ReadAllText(f);
            Assert.DoesNotContain("@import", t);
            Assert.False(RecursoExternoRx().IsMatch(t), $"{Nombre(f)}: recurso externo {RecursoExternoRx().Match(t).Value}");
        }
    }

    [Fact]
    public void El_javascript_no_inserta_html_ni_estilos_en_linea()
    {
        string[] prohibidos = ["innerHTML", "outerHTML", "insertAdjacentHTML", "document.write", "eval(", "new Function(", "setAttribute('style'", "setAttribute(\"style\""];
        foreach (var f in Ficheros(".js"))
        {
            var t = File.ReadAllText(f);
            foreach (var p in prohibidos)
            {
                Assert.False(t.Contains(p, StringComparison.Ordinal), $"{Nombre(f)} usa {p}");
            }
        }
    }

    [Theory]
    [InlineData("ui.css")]
    [InlineData("iconos.js")]
    [InlineData("tema.js")]
    public void Los_ficheros_del_sistema_de_diseno_son_identicos_en_panel_y_captura(string fichero)
    {
        var a = File.ReadAllText(Path.Combine(Raiz, Interfaces[0], fichero));
        var b = File.ReadAllText(Path.Combine(Raiz, Interfaces[1], fichero));
        Assert.True(a == b, $"{fichero} difiere entre el panel y la captura: copia el del panel al otro proyecto.");
    }

    [Fact]
    public void El_sistema_de_diseno_respeta_el_movimiento_reducido_y_ofrece_ambos_temas()
    {
        var css = File.ReadAllText(Path.Combine(Raiz, Interfaces[0], "ui.css"));
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css);
        Assert.Contains("animation: none !important", css);
        Assert.Contains(":root[data-tema=\"light\"]", css);
        Assert.Contains("@media (prefers-color-scheme: light)", css);
        Assert.Contains(":focus-visible", css);
    }

    [Fact]
    public void Todos_los_iconos_que_se_usan_existen()
    {
        var js = File.ReadAllText(Path.Combine(Raiz, Interfaces[0], "iconos.js"));
        var definidos = Regex.Matches(js, @"^\s{4}([a-z]+): \[", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToHashSet();
        Assert.True(definidos.Count > 40);

        var usados = new HashSet<string>();
        foreach (var f in Ficheros(".js", ".html").Where(f => !f.EndsWith("iconos.js")))
        {
            var t = File.ReadAllText(f);
            foreach (Match m in Regex.Matches(t, @"\b(?:ic|Icono)\('([a-z]+)'")) { usados.Add(m.Groups[1].Value); }
            foreach (Match m in Regex.Matches(t, @"data-icono=""([a-z]+)""")) { usados.Add(m.Groups[1].Value); }
            foreach (Match m in Regex.Matches(t, @"(?:'|\s)(?:ic|boton)\([^)]*?,\s*'([a-z]+)'")) { usados.Add(m.Groups[1].Value); }
        }

        // Los mapas de clases, certezas y campos se pintan con un icono del mismo nombre.
        var panel = File.ReadAllText(Path.Combine(Raiz, Interfaces[0], "panel.js"));
        foreach (var mapa in new[] { "CAMPOS", "CLASES", "CERTEZAS" })
        {
            var m = Regex.Match(panel, $@"const {mapa} = \{{(.*?)\}};", RegexOptions.Singleline);
            Assert.True(m.Success, $"No se encuentra {mapa} en panel.js");
            foreach (Match k in Regex.Matches(m.Groups[1].Value, @"(\w+):\s*'")) { usados.Add(k.Groups[1].Value); }
        }

        var faltan = usados.Where(u => !definidos.Contains(u)).OrderBy(u => u).ToList();
        Assert.True(faltan.Count == 0, "Iconos usados pero no definidos: " + string.Join(", ", faltan));
    }
}
