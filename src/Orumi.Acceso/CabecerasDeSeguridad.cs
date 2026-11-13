using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Orumi.Acceso;

public static class CabecerasDeSeguridad
{
    /// <summary>
    /// Cabeceras de endurecimiento para todas las respuestas. <paramref name="origenAuth"/> es el origen del proveedor de identidad
    /// al que la página debe poder conectar. Con <paramref name="sinCache"/> las respuestas de la API no se almacenan nunca.
    /// </summary>
    public static IApplicationBuilder UsarCabecerasDeSeguridad(this IApplicationBuilder app, string origenAuth, bool sinCache = true) =>
        app.Use(async (contexto, siguiente) =>
        {
            var h = contexto.Response.Headers;
            h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            h["Content-Security-Policy"] =
                $"default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self' {origenAuth}; " +
                "frame-ancestors 'none'; base-uri 'none'; form-action 'none'; object-src 'none'";
            h["X-Content-Type-Options"] = "nosniff";
            h["Referrer-Policy"] = "no-referrer";
            h["Permissions-Policy"] = "camera=(), geolocation=(), payment=(), usb=()";
            h["Cross-Origin-Opener-Policy"] = "same-origin";
            h["Cross-Origin-Resource-Policy"] = "same-origin";
            h["X-Frame-Options"] = "DENY";
            if (sinCache && contexto.Request.Path.StartsWithSegments("/api"))
            {
                h["Cache-Control"] = "no-store";
                h["Pragma"] = "no-cache";
            }

            await siguiente();
        });

    /// <summary>true si la petición de escritura viene del propio sitio (cabecera Origin igual al host) o no trae Origin (no es un navegador).</summary>
    public static bool OrigenPropio(HttpRequest r)
    {
        var origen = r.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origen))
        {
            return true; // los navegadores siempre envían Origin en las escrituras entre sitios; la autenticación Bearer cubre el resto
        }

        return Uri.TryCreate(origen, UriKind.Absolute, out var u)
               && string.Equals(u.Authority, r.Host.Value, StringComparison.OrdinalIgnoreCase);
    }
}
