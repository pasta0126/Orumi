using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Orumi.Acceso;

/// <summary>Lo que debe responder el guardián. <see cref="Estado"/> es 0 si la petición puede seguir.</summary>
public sealed record DecisionDeGuardian(int Estado, string? Correo = null, TimeSpan? Espera = null)
{
    public bool Sigue => Estado == 0;
}

/// <summary>
/// Puerta única de la API. Deniega por defecto: toda ruta bajo <c>/api</c> exige sesión salvo la lista blanca explícita, de modo que
/// una ruta nueva queda protegida sin hacer nada. Orden: límites por IP y globales (antes de tocar al proveedor de identidad),
/// origen y tipo de contenido de las escrituras, autenticación y límite por usuario.
/// </summary>
public sealed class GuardianDeApi(AccesoPorSesion acceso, PoliticaDeLimites limites, IReadOnlyCollection<string> rutasPublicas)
{
    private static readonly string[] MetodosDeLectura = ["GET", "HEAD", "OPTIONS"];

    public static bool EsEscritura(string metodo) => !MetodosDeLectura.Contains(metodo.ToUpperInvariant());

    public async Task<DecisionDeGuardian> EvaluarAsync(
        string metodo, string ruta, bool origenPropio, string? tipoDeContenido, string? bearer, string ip, CancellationToken ct)
    {
        if (!ruta.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            return new DecisionDeGuardian(0); // carcasa estática: sin datos
        }

        var escritura = EsEscritura(metodo);
        var porOrigen = limites.PorOrigen(ip, escritura);
        if (!porOrigen.Permitido)
        {
            return new DecisionDeGuardian(429, Espera: porOrigen.Espera);
        }

        if (rutasPublicas.Contains(ruta.TrimEnd('/'), StringComparer.OrdinalIgnoreCase) && !escritura)
        {
            return new DecisionDeGuardian(0);
        }

        if (escritura)
        {
            if (!origenPropio)
            {
                return new DecisionDeGuardian(403);
            }

            if (tipoDeContenido is null || !tipoDeContenido.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                return new DecisionDeGuardian(415);
            }
        }

        DecisionDeAcceso decision;
        try
        {
            decision = await acceso.ComprobarConIdentidadAsync(bearer, ip, ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new DecisionDeGuardian(503);
        }

        switch (decision.Resultado)
        {
            case ResultadoDeAcceso.Bloqueado:
                return new DecisionDeGuardian(429, Espera: TimeSpan.FromMinutes(15));
            case ResultadoDeAcceso.NoAutorizado:
                return new DecisionDeGuardian(403);
            case ResultadoDeAcceso.Denegado:
                return new DecisionDeGuardian(401);
        }

        var porUsuario = limites.PorUsuario(decision.Correo!, escritura);
        return porUsuario.Permitido
            ? new DecisionDeGuardian(0, decision.Correo)
            : new DecisionDeGuardian(429, decision.Correo, porUsuario.Espera);
    }
}

public static class GuardianDeApiExtensiones
{
    public const string ClaveDeCorreo = "orumi.correo";

    public static IApplicationBuilder UsarGuardianDeApi(this IApplicationBuilder app, GuardianDeApi guardian, IReadOnlyCollection<string>? origenesPermitidos = null) =>
        app.Use(async (c, siguiente) =>
        {
            var cabecera = c.Request.Headers.Authorization.ToString();
            var bearer = cabecera.StartsWith("Bearer ", StringComparison.Ordinal) ? cabecera["Bearer ".Length..] : null;
            var d = await guardian.EvaluarAsync(
                c.Request.Method, c.Request.Path.Value ?? "/", CabecerasDeSeguridad.OrigenPropio(c.Request, origenesPermitidos), c.Request.ContentType, bearer,
                c.Connection.RemoteIpAddress?.ToString() ?? "desconocido", c.RequestAborted);
            if (d.Sigue)
            {
                if (d.Correo is not null)
                {
                    c.Items[ClaveDeCorreo] = d.Correo;
                }

                await siguiente();
                return;
            }

            c.Response.StatusCode = d.Estado;
            if (d.Estado == 429 && d.Espera is { } espera)
            {
                c.Response.Headers.RetryAfter = Math.Ceiling(espera.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            }

            // Cuerpo mínimo y genérico: nunca se explica el motivo exacto del rechazo.
            await c.Response.WriteAsJsonAsync(new { error = Mensaje(d.Estado) });
        });

    private static string Mensaje(int estado) => estado switch
    {
        401 => "Inicia sesión.",
        403 => "No tienes acceso.",
        415 => "Tipo de contenido no admitido.",
        429 => "Demasiadas peticiones. Espera un momento.",
        503 => "El servicio de acceso no responde. Reinténtalo en unos minutos.",
        _ => "Petición rechazada.",
    };

    /// <summary>Errores internos genéricos: nunca se devuelven trazas, rutas ni mensajes de excepción.</summary>
    public static IApplicationBuilder UsarErroresGenericos(this IApplicationBuilder app) =>
        app.UseExceptionHandler(a => a.Run(async c =>
        {
            c.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await c.Response.WriteAsJsonAsync(new { error = "Error interno." });
        }));
}

/// <summary>Auditoría de escrituras: operación, identificador y resultado. Nunca valores ni textos de notas.</summary>
public static class Auditoria
{
    public static string Enmascarar(string? correo)
    {
        if (string.IsNullOrEmpty(correo) || !correo.Contains('@'))
        {
            return "?";
        }

        var partes = correo.Split('@', 2);
        return $"{partes[0][0]}***@{partes[1]}";
    }

    public static string Linea(DateTimeOffset cuando, string? correo, string operacion, string? id, string resultado) =>
        $"{cuando:yyyy-MM-ddTHH:mm:sszzz} auditoria usuario={Enmascarar(correo)} operacion={operacion} id={id ?? "-"} resultado={resultado}";
}
