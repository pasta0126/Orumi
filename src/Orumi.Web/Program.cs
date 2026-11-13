using Microsoft.AspNetCore.HttpOverrides;
using Orumi.Core.Capturas;
using Orumi.Core.Ficheros;
using Orumi.Acceso;
using Orumi.Web;

var builder = WebApplication.CreateBuilder(args);
var datos = Environment.GetEnvironmentVariable("ORUMI_DATOS");
if (string.IsNullOrWhiteSpace(datos) || !Directory.Exists(datos))
{
    Console.Error.WriteLine("Falta ORUMI_DATOS o el directorio no existe.");
    return 2;
}

// Identidad: GoTrue de la plataforma. La web habla con él por la red interna y el navegador por su dirección pública.
var authInterno = Environment.GetEnvironmentVariable("ORUMI_WEB_AUTH_URL") ?? "http://platform-gotrue:9999/";
var authPublico = (Environment.GetEnvironmentVariable("ORUMI_WEB_AUTH_PUBLICO") ?? "https://auth.northernarchive.com/auth/v1").TrimEnd('/');
var correos = (Environment.GetEnvironmentVariable("ORUMI_WEB_EMAILS") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
AccesoPorSesion acceso;
try
{
    var http = new HttpClient { BaseAddress = new Uri(authInterno.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(5) };
    acceso = new AccesoPorSesion(new VerificadorGoTrue(http), correos, () => DateTimeOffset.UtcNow);
}
catch (Exception e) when (e is ArgumentException or UriFormatException)
{
    Console.Error.WriteLine("Configuración de acceso no válida: " + e.Message + " (ORUMI_WEB_EMAILS y ORUMI_WEB_AUTH_URL).");
    return 2;
}

var origenAuth = new Uri(authPublico).GetLeftPart(UriPartial.Authority);

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    // Solo se alcanza a través de Traefik (red interna de Docker): se confía en su cabecera X-Forwarded-For (un único salto).
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 64 * 1024);
builder.Logging.SetMinimumLevel(LogLevel.Warning); // nunca se registra el contenido de las notas

var app = builder.Build();
app.UseForwardedHeaders();
app.UsarErroresGenericos();
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
var limites = new PoliticaDeLimites(LimitesDePeticiones.DesdeEntorno(Environment.GetEnvironmentVariable), () => DateTimeOffset.UtcNow);
app.UsarCabecerasDeSeguridad(origenAuth);
app.UsarGuardianDeApi(new GuardianDeApi(acceso, limites, ["/api/config"]));

// Pública: solo dice dónde está el proveedor de identidad (el navegador la necesita para iniciar y renovar la sesión).
app.MapGet("/api/config", () => Results.Json(new { auth = authPublico }));

app.MapGet("/api/comprobar", () => Results.NoContent());

app.MapPost("/api/capturas", (HttpContext c, SolicitudDeCaptura solicitud) =>
{
    try
    {
        var r = servicio.Crear(solicitud);
        Console.WriteLine(Auditoria.Linea(DateTimeOffset.Now, c.Items[GuardianDeApiExtensiones.ClaveDeCorreo] as string, "crear-captura", r.Id, r.Repetida ? "repetida" : "creada"));
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
