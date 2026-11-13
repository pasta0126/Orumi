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
    bool ExigirVolumen = false)
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
            Texto(Mapa(raiz, "volumen"), "exigir") == "true");
    }
}
