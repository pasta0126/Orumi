using Microsoft.AspNetCore.HttpOverrides;
using Orumi.Core.Capturas;
using Orumi.Core.Ficheros;
using Orumi.Web;

var builder = WebApplication.CreateBuilder(args);
var datos = Environment.GetEnvironmentVariable("ORUMI_DATOS");
var token = Environment.GetEnvironmentVariable("ORUMI_WEB_TOKEN");
if (string.IsNullOrWhiteSpace(datos) || !Directory.Exists(datos))
{
    Console.Error.WriteLine("Falta ORUMI_DATOS o el directorio no existe.");
    return 2;
}

AccesoPorToken acceso;
try
{
    acceso = new AccesoPorToken(token ?? string.Empty, () => DateTimeOffset.UtcNow);
}
catch (ArgumentException e)
{
    Console.Error.WriteLine(e.Message);
    return 2;
}

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    // Solo se alcanza a través de Traefik (red interna de Docker): se confía en su cabecera X-Forwarded-For.
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});
builder.Logging.SetMinimumLevel(LogLevel.Warning); // nunca se registra el contenido de las notas

var app = builder.Build();
app.UseForwardedHeaders();
TimeZoneInfo zona;
try
{
    zona = TimeZoneInfo.FindSystemTimeZoneById(Environment.GetEnvironmentVariable("TZ") ?? "Europe/Madrid");
}
catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
{
    zona = TimeZoneInfo.Local; // sin datos de zona horaria en la imagen: hora local del contenedor
}
var servicio = new ServicioDeCaptura(datos, () => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zona));

app.Use(async (contexto, siguiente) =>
{
    contexto.Response.Headers["X-Content-Type-Options"] = "nosniff";
    contexto.Response.Headers["Referrer-Policy"] = "no-referrer";
    contexto.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'";
    await siguiente();
});

IResult? Rechazo(HttpContext c)
{
    var cabecera = c.Request.Headers.Authorization.ToString();
    var presentado = cabecera.StartsWith("Bearer ", StringComparison.Ordinal) ? cabecera["Bearer ".Length..] : null;
    return acceso.Comprobar(presentado, c.Connection.RemoteIpAddress?.ToString() ?? "desconocido") switch
    {
        ResultadoDeAcceso.Permitido => null,
        ResultadoDeAcceso.Bloqueado => Results.StatusCode(StatusCodes.Status429TooManyRequests),
        _ => Results.Unauthorized(),
    };
}

app.MapGet("/api/comprobar", (HttpContext c) => Rechazo(c) ?? Results.NoContent());

app.MapPost("/api/capturas", (HttpContext c, SolicitudDeCaptura solicitud) =>
{
    if (Rechazo(c) is { } rechazo)
    {
        return rechazo;
    }

    try
    {
        var r = servicio.Crear(solicitud);
        return r.Repetida ? Results.Ok(r) : Results.Created($"/api/capturas/{r.Id}", r);
    }
    catch (CapturaInvalidaException e)
    {
        return Results.BadRequest(new { error = e.Message });
    }
    catch (EscrituraFallidaException)
    {
        return Results.Problem("No se pudo guardar la nota. Reinténtalo.", statusCode: 503);
    }
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.Run("http://0.0.0.0:8080");
return 0;
