namespace Orumi.Agente;

/// <summary>
/// Comprobación previa a cada ejecución (spec <c>configuracion-del-proveedor-ia</c> y <c>privacidad-del-procesamiento</c>):
/// si el proveedor envía datos fuera, exige garantía declarada y vigente y clave presente. Falla cerrado.
/// </summary>
public static class ComprobadorDePrivacidad
{
    /// <summary>Variable de entorno de la clave de API de cada proveedor.</summary>
    public static string? VariableDeClave(string proveedor) => proveedor.ToLowerInvariant() switch
    {
        "anthropic" => "ANTHROPIC_API_KEY",
        _ => null,
    };

    /// <summary>Devuelve los motivos por los que NO se puede enviar nada; vacío si se puede continuar.</summary>
    public static IReadOnlyList<string> Comprobar(
        IProveedorIA proveedor, Configuracion configuracion, DateOnly hoy, Func<string, string?> entorno) =>
        Comprobar(proveedor.Nombre, proveedor.EnviaDatosAFuera, configuracion, hoy, entorno);

    public static IReadOnlyList<string> Comprobar(
        string nombreDelProveedor, bool enviaDatosAFuera, Configuracion configuracion, DateOnly hoy, Func<string, string?> entorno)
    {
        if (!enviaDatosAFuera)
        {
            return [];
        }

        var proveedor = new { Nombre = nombreDelProveedor };

        var motivos = new List<string>();
        if (!string.Equals(proveedor.Nombre, configuracion.Proveedor, StringComparison.OrdinalIgnoreCase))
        {
            motivos.Add($"El proveedor en uso ({proveedor.Nombre}) no coincide con el de la configuración ({configuracion.Proveedor}).");
        }

        var g = configuracion.Garantia;
        if (!g.Declarada)
        {
            motivos.Add("Falta la garantía de no reutilización de los datos declarada en la configuración.");
        }
        else if (g.VerificadaEl is null)
        {
            motivos.Add("La garantía de privacidad no tiene fecha de verificación.");
        }
        else if (hoy.DayNumber - g.VerificadaEl.Value.DayNumber > g.CaducidadDias)
        {
            motivos.Add($"La verificación de la garantía de privacidad ({g.VerificadaEl:yyyy-MM-dd}) tiene más de {g.CaducidadDias} días: hay que renovarla.");
        }
        else if (string.IsNullOrWhiteSpace(g.Fuente))
        {
            motivos.Add("La garantía de privacidad no indica su fuente.");
        }

        var variable = VariableDeClave(proveedor.Nombre);
        if (variable is null)
        {
            motivos.Add($"No se conoce la clave de API del proveedor «{proveedor.Nombre}».");
        }
        else if (string.IsNullOrWhiteSpace(entorno(variable)))
        {
            motivos.Add($"Falta la clave de API (variable de entorno {variable}).");
        }

        return motivos;
    }
}
