using System.Globalization;
using Orumi.Core.Yaml;

namespace Orumi.Agente;

public sealed record GarantiaDePrivacidad(bool Declarada, DateOnly? VerificadaEl, string? Fuente, int CaducidadDias);

public sealed record Configuracion(
    string Datos,
    string Proveedor,
    string Modelo,
    GarantiaDePrivacidad Garantia,
    string Horario,
    int VentanaRelacionadosDias,
    int MaximoPendientes,
    bool ExigirVolumen = false,
    Precios? Precios = null,
    Presupuesto? Presupuesto = null)
{
    public const int CaducidadPorDefecto = 180;

    /// <summary>Lee la configuración (YAML, sin secretos). Los valores ausentes toman los del diseño.</summary>
    public static Configuracion Leer(string yaml)
    {
        var raiz = YamlLector.LeerMapa(yaml);
        string Texto(Dictionary<string, object?> m, string k, string porDefecto = "") =>
            m.TryGetValue(k, out var v) && v is string s ? s : porDefecto;
        Dictionary<string, object?> Mapa(Dictionary<string, object?> m, string k) =>
            m.TryGetValue(k, out var v) && v is Dictionary<string, object?> d ? d : [];
        int Entero(Dictionary<string, object?> m, string k, int porDefecto) =>
            m.TryGetValue(k, out var v) && v is string s && int.TryParse(s, CultureInfo.InvariantCulture, out var n) ? n : porDefecto;

        var proveedor = Mapa(raiz, "proveedor");
        var garantia = Mapa(proveedor, "garantia_no_reutilizacion");
        var lote = Mapa(raiz, "lote");
        var preguntas = Mapa(raiz, "preguntas");

        DateOnly? verificada = DateOnly.TryParseExact(Texto(garantia, "verificada_el"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)
            ? f
            : null;

        return new Configuracion(
            Texto(raiz, "datos"),
            Texto(proveedor, "nombre"),
            Texto(proveedor, "modelo"),
            new GarantiaDePrivacidad(Texto(garantia, "declarada") == "true", verificada, garantia.GetValueOrDefault("fuente") as string, Entero(garantia, "caducidad_dias", CaducidadPorDefecto)),
            Texto(lote, "horario", "03:00"),
            Entero(lote, "ventana_relacionados_dias", 14),
            Entero(preguntas, "maximo_pendientes", 5),
            Texto(Mapa(raiz, "volumen"), "exigir") == "true",
            LeerPrecios(Mapa(raiz, "precios")),
            LeerPresupuesto(Mapa(raiz, "presupuesto")));
    }

    private static Precios? LeerPrecios(Dictionary<string, object?> m)
    {
        return Decimal(m, "entrada_por_millon_usd") is { } e && Decimal(m, "salida_por_millon_usd") is { } s ? new Precios(e, s) : null;
    }

    private static Presupuesto? LeerPresupuesto(Dictionary<string, object?> m)
    {
        if (Decimal(m, "saldo_usd") is not { } saldo
            || !DateOnly.TryParseExact(m.GetValueOrDefault("desde") as string, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var desde))
        {
            return null;
        }

        var aviso = int.TryParse(m.GetValueOrDefault("aviso_porcentaje") as string, CultureInfo.InvariantCulture, out var a) ? a : 90;
        return new Presupuesto(saldo, desde, aviso);
    }

    private static decimal? Decimal(Dictionary<string, object?> m, string clave) =>
        decimal.TryParse(m.GetValueOrDefault(clave) as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
}
