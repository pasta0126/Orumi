using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Orumi.Acceso;

public static class CabecerasDeSeguridad
{
    /// <summary>
    /// Cabeceras de endurecimiento para todas las respuestas. <paramref name="origenAuth"/> es el origen del proveedor de identidad
    /// al que la página debe poder conectar. Con <paramref name="sinCache"/> las respuestas de la API no se almacenan nunca.
    /// </summary>
    public static IApplicationBuilder UsarCabecerasDeSeguridad(this IApplicationBuilder app, string origenAuth, bool sinCache = true, params string[] origenesDeConexion) =>
        app.Use(async (contexto, siguiente) =>
        {
            var conexiones = string.Join(' ', new[] { "'self'", origenAuth }.Concat(origenesDeConexion));
            var h = contexto.Response.Headers;
            h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            h["Content-Security-Policy"] =
                $"default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src {conexiones}; " +
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

    /// <summary>
    /// true si el origen es el propio sitio (Origin igual al host), uno de la lista cerrada de orígenes permitidos o no hay Origin
    /// (no es un navegador; la autenticación Bearer cubre el resto).
    /// </summary>
    public static bool OrigenPermitido(string? origen, string host, IReadOnlyCollection<string> permitidos)
    {
        if (string.IsNullOrEmpty(origen))
        {
            return true;
        }

        if (permitidos.Contains(origen.TrimEnd('/'), StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(origen, UriKind.Absolute, out var u) && string.Equals(u.Authority, host, StringComparison.OrdinalIgnoreCase);
    }

    public static bool OrigenPropio(HttpRequest r, IReadOnlyCollection<string>? permitidos = null) =>
        OrigenPermitido(r.Headers.Origin.ToString(), r.Host.Value, permitidos ?? []);

    /// <summary>
    /// CORS con lista cerrada: solo responde a un <c>Origin</c> de la lista, con el origen exacto (nunca <c>*</c>). Las peticiones previas
    /// (OPTIONS) de un origen permitido se resuelven aquí, sin autenticar y sin datos; las de otro origen reciben 403.
    /// </summary>
    public static IApplicationBuilder UsarCors(this IApplicationBuilder app, IReadOnlyCollection<string> origenesPermitidos) =>
        app.Use(async (c, siguiente) =>
        {
            var origen = c.Request.Headers.Origin.ToString().TrimEnd('/');
            if (!string.IsNullOrEmpty(origen) && c.Request.Path.StartsWithSegments("/api"))
            {
                var permitido = origenesPermitidos.Contains(origen, StringComparer.OrdinalIgnoreCase);
                if (permitido)
                {
                    c.Response.Headers["Access-Control-Allow-Origin"] = origen;
                    c.Response.Headers.Append("Vary", "Origin");
                }

                if (HttpMethods.IsOptions(c.Request.Method))
                {
                    if (!permitido)
                    {
                        c.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return;
                    }

                    c.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
                    c.Response.Headers["Access-Control-Allow-Headers"] = "Authorization, Content-Type";
                    c.Response.Headers["Access-Control-Max-Age"] = "600";
                    c.Response.StatusCode = StatusCodes.Status204NoContent;
                    return;
                }
            }

            await siguiente();
        });
}
