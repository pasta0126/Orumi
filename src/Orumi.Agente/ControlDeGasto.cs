using System.Globalization;
using Orumi.Core.Ficheros;

namespace Orumi.Agente;

public enum CategoriaError { Otro, SaldoAgotado, LimiteDeGasto, ClaveInvalida }

/// <summary>Error del proveedor que detiene la ejecución (saldo, límite de gasto o clave). Los demás errores se tratan por captura.</summary>
public sealed class ProveedorException(CategoriaError categoria, string mensaje) : Exception(mensaje)
{
    public CategoriaError Categoria { get; } = categoria;
}

/// <summary>Clasificación de errores del proveedor, independiente del proveedor concreto (spec <c>control-del-saldo</c>).</summary>
public static class ClasificadorDeErrores
{
    public static CategoriaError Clasificar(int? estado, string? tipo, string? codigo, string? mensaje)
    {
        var m = (mensaje ?? string.Empty).ToLowerInvariant();

        // Un límite de gasto (el del usuario o el del nivel de uso) se distingue de un saldo agotado.
        if (string.Equals(codigo, "enforced_spend_limit_reached", StringComparison.OrdinalIgnoreCase)
            || m.Contains("reached your specified", StringComparison.Ordinal)
            || m.Contains("reached your api usage limits", StringComparison.Ordinal)
            || m.Contains("monthly api usage threshold", StringComparison.Ordinal))
        {
            return CategoriaError.LimiteDeGasto;
        }

        if (estado == 402
            || string.Equals(tipo, "billing_error", StringComparison.OrdinalIgnoreCase)
            || m.Contains("credit balance", StringComparison.Ordinal)
            || (m.Contains("credit", StringComparison.Ordinal) && (m.Contains("too low", StringComparison.Ordinal) || m.Contains("insufficient", StringComparison.Ordinal)))
            || m.Contains("purchase credits", StringComparison.Ordinal))
        {
            return CategoriaError.SaldoAgotado;
        }

        if (estado == 401 || string.Equals(tipo, "authentication_error", StringComparison.OrdinalIgnoreCase))
        {
            return CategoriaError.ClaveInvalida;
        }

        return CategoriaError.Otro;
    }

    public static string Etiqueta(CategoriaError c) => c switch
    {
        CategoriaError.SaldoAgotado => "saldo_agotado",
        CategoriaError.LimiteDeGasto => "limite_de_gasto",
        CategoriaError.ClaveInvalida => "clave_invalida",
        _ => "otro",
    };

    public static bool EsFatal(string? etiqueta) => etiqueta is "saldo_agotado" or "limite_de_gasto" or "clave_invalida";

    /// <summary>Qué ha pasado y qué hacer. Sin la clave ni contenido de las notas.</summary>
    public static string Explicacion(string etiqueta) => etiqueta switch
    {
        "saldo_agotado" => "Saldo de créditos agotado: recarga créditos en la consola del proveedor (Facturación). Las capturas siguen pendientes y se procesarán en la siguiente ejecución.",
        "limite_de_gasto" => "Límite de gasto del proveedor alcanzado: súbelo en la consola (Límites de gasto) o espera a que se renueve. Las capturas siguen pendientes.",
        "clave_invalida" => "La clave de API no es válida (revocada, caducada o mal escrita): crea otra en la consola y guárdala en orumi.env. Las capturas siguen pendientes.",
        _ => "Error del proveedor.",
    };

    public static string Explicacion(CategoriaError c) => Explicacion(Etiqueta(c));
}

public sealed record UsoDeTokens(long Entrada, long Salida);

/// <summary>Precios por millón de tokens, de la configuración (nunca inventados: sin ellos el coste es desconocido).</summary>
public sealed record Precios(decimal EntradaPorMillonUsd, decimal SalidaPorMillonUsd, decimal FactorLote = 0.5m)
{
    /// <summary>Precios efectivos con el descuento del procesamiento diferido (50 % por defecto).</summary>
    public Precios ConDescuentoDeLote() => this with { EntradaPorMillonUsd = EntradaPorMillonUsd * FactorLote, SalidaPorMillonUsd = SalidaPorMillonUsd * FactorLote };

    public decimal Coste(long entrada, long salida) =>
        (entrada * EntradaPorMillonUsd + salida * SalidaPorMillonUsd) / 1_000_000m;
}

/// <summary>Saldo comprado y fecha desde la que se suma el gasto; avisa al llegar al porcentaje indicado.</summary>
public sealed record Presupuesto(decimal SaldoUsd, DateOnly Desde, int AvisoPorcentaje = 90);

public static class Formato
{
    public static string Usd(decimal d) => d.ToString("0.000000", CultureInfo.InvariantCulture);

    /// <summary>Para mostrar al usuario: tres decimales por debajo de 1 US$ (el gasto por nota es de milésimas), dos desde ahí.</summary>
    public static string Usd2(decimal d) => d.ToString(d < 1m ? "0.000" : "0.00", CultureInfo.InvariantCulture);
}

public static class ControlDeGasto
{
    /// <summary>Suma el coste estimado de los lotes ejecutados desde la fecha del presupuesto.</summary>
    public static decimal GastoEstimado(IEnumerable<Documento> lotes, DateOnly desde)
    {
        decimal total = 0;
        foreach (var l in lotes)
        {
            if (!DateTimeOffset.TryParse(l.Frontmatter.GetValueOrDefault("ejecutado_el") as string, CultureInfo.InvariantCulture, DateTimeStyles.None, out var cuando)
                || DateOnly.FromDateTime(cuando.DateTime) < desde)
            {
                continue;
            }

            if (l.Frontmatter.GetValueOrDefault("uso") is Dictionary<string, object?> uso
                && decimal.TryParse(uso.GetValueOrDefault("coste_estimado_usd") as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var c))
            {
                total += c;
            }
        }

        return total;
    }

    /// <summary>Aviso por gasto estimado, o null si aún no se alcanza el umbral. Solo avisa: nunca detiene el procesamiento.</summary>
    public static string? AvisoDePresupuesto(decimal gastado, Presupuesto p)
    {
        if (p.SaldoUsd <= 0)
        {
            return null;
        }

        var porcentaje = gastado / p.SaldoUsd * 100m;
        if (gastado > p.SaldoUsd)
        {
            return $"El gasto estimado ({Formato.Usd2(gastado)} US$) supera el saldo configurado ({Formato.Usd2(p.SaldoUsd)} US$): el saldo real puede estar agotado; revisa la consola del proveedor.";
        }

        if (porcentaje >= p.AvisoPorcentaje)
        {
            return $"Gasto estimado {Formato.Usd2(gastado)} de {Formato.Usd2(p.SaldoUsd)} US$ ({porcentaje:0}%): quedan unos {Formato.Usd2(p.SaldoUsd - gastado)} US$. Recarga pronto (es una estimación: compárala con la consola).";
        }

        return null;
    }
}

public sealed record InfoLote(string Id, DateTimeOffset? EjecutadoEl, int Procesadas, int Problemas, string? TipoFatal);

public sealed record EstadoMira(
    int Pendientes, InfoLote? UltimoLote, decimal? GastoEstimado, Presupuesto? Presupuesto, IReadOnlyList<string> Avisos,
    int EnviosAbiertos = 0, int CapturasEnEnvio = 0);

/// <summary>Estado de Mira derivado de los ficheros (sin estado propio): lee solo los lotes y los nombres de las capturas.</summary>
public static class ServicioDeEstado
{
    public static EstadoMira Calcular(string datos, Configuracion? config)
    {
        var lotes = new List<Documento>();
        var dirLotes = Path.Combine(datos, "lotes");
        if (Directory.Exists(dirLotes))
        {
            foreach (var f in Directory.EnumerateFiles(dirLotes, "*.md", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
            {
                try
                {
                    lotes.Add(Documento.Analizar(Path.GetRelativePath(datos, f), File.ReadAllText(f)));
                }
                catch (Exception e) when (e is FormatException or YamlDotNet.Core.YamlException or IOException)
                {
                    // un lote ilegible no impide mostrar el estado
                }
            }
        }

        DateTimeOffset? Cuando(Documento d) =>
            DateTimeOffset.TryParse(d.Frontmatter.GetValueOrDefault("ejecutado_el") as string, CultureInfo.InvariantCulture, DateTimeStyles.None, out var v) ? v : null;

        var ordenados = lotes.OrderBy(l => Cuando(l) ?? DateTimeOffset.MinValue).ThenBy(l => l.Id, StringComparer.Ordinal).ToList();

        var procesadas = ordenados
            .SelectMany(l => l.Frontmatter.GetValueOrDefault("capturas") is List<object?> cs ? cs : [])
            .OfType<Dictionary<string, object?>>()
            .Select(c => c.GetValueOrDefault("captura") as string)
            .OfType<string>()
            .ToHashSet();
        // Envíos abiertos: capturas enviadas al proveedor cuyo lote aún no se ha recogido.
        var enEnvio = new HashSet<string>();
        var enviosAbiertos = 0;
        var dirEnvios = Path.Combine(datos, "envios");
        if (Directory.Exists(dirEnvios))
        {
            foreach (var f in Directory.EnumerateFiles(dirEnvios, "*.md", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
            {
                try
                {
                    var envio = Documento.Analizar(Path.GetRelativePath(datos, f), File.ReadAllText(f));
                    if (envio.Frontmatter.GetValueOrDefault("estado") as string != "abierto")
                    {
                        continue;
                    }

                    enviosAbiertos++;
                    foreach (var c in envio.Frontmatter.GetValueOrDefault("capturas") as List<object?> ?? [])
                    {
                        if (c is Dictionary<string, object?> m && m.GetValueOrDefault("captura") is string id)
                        {
                            enEnvio.Add(id);
                        }
                    }
                }
                catch (Exception e) when (e is FormatException or YamlDotNet.Core.YamlException or IOException)
                {
                    // un envío ilegible no impide mostrar el estado
                }
            }
        }

        var dirCapturas = Path.Combine(datos, "capturas");
        var sinLote = Directory.Exists(dirCapturas)
            ? Directory.EnumerateFiles(dirCapturas, "cap-*.md", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
                .Select(f => Path.GetFileNameWithoutExtension(f)).Where(id => !procesadas.Contains(id)).ToList()
            : [];
        var pendientes = sinLote.Count(id => !enEnvio.Contains(id));
        var capturasEnEnvio = sinLote.Count(id => enEnvio.Contains(id));

        InfoLote? ultimo = null;
        var avisos = new List<string>();
        if (ordenados.Count > 0)
        {
            var l = ordenados[^1];
            var problemas = l.Frontmatter.GetValueOrDefault("problemas") is List<object?> ps ? ps.OfType<Dictionary<string, object?>>().ToList() : [];
            var fatal = problemas.Select(p => p.GetValueOrDefault("tipo") as string).FirstOrDefault(ClasificadorDeErrores.EsFatal);
            var nCapturas = l.Frontmatter.GetValueOrDefault("capturas") is List<object?> cs2 ? cs2.Count : 0;
            ultimo = new InfoLote(l.Id ?? "?", Cuando(l), nCapturas, problemas.Count, fatal);
            if (fatal is not null)
            {
                avisos.Add($"{ClasificadorDeErrores.Explicacion(fatal)} (lote {l.Id})");
            }
        }

        decimal? gastado = null;
        if (config?.Presupuesto is { } p)
        {
            gastado = ControlDeGasto.GastoEstimado(ordenados, p.Desde);
            if (ControlDeGasto.AvisoDePresupuesto(gastado.Value, p) is { } aviso)
            {
                avisos.Add(aviso);
            }
        }

        return new EstadoMira(pendientes, ultimo, gastado, config?.Presupuesto, avisos, enviosAbiertos, capturasEnEnvio);
    }
}
