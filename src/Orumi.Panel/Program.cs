using Microsoft.AspNetCore.HttpOverrides;
using Orumi.Acceso;
using Orumi.Core.Ficheros;
using Orumi.Panel;

var datos = Environment.GetEnvironmentVariable("ORUMI_DATOS");
if (string.IsNullOrWhiteSpace(datos) || !Directory.Exists(datos))
{
    Console.Error.WriteLine("Falta ORUMI_DATOS o el directorio no existe.");
    return 2;
}

// Identidad: GoTrue de la plataforma (por la red interna para validar sesiones, dirección pública para el navegador).
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
var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    // Solo se alcanza a través de Traefik (red interna de Docker): se confía en su cabecera X-Forwarded-For (un único salto).
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 64 * 1024);
builder.Logging.SetMinimumLevel(LogLevel.Warning); // nunca se registra contenido de notas ni valores

var app = builder.Build();
app.UseForwardedHeaders();
app.UsarErroresGenericos();
app.UsarCabecerasDeSeguridad(origenAuth);
var limites = new PoliticaDeLimites(LimitesDePeticiones.DesdeEntorno(Environment.GetEnvironmentVariable), () => DateTimeOffset.UtcNow);
app.UsarGuardianDeApi(new GuardianDeApi(acceso, limites, ["/api/config"]));

TimeZoneInfo zona;
try
{
    zona = TimeZoneInfo.FindSystemTimeZoneById(Environment.GetEnvironmentVariable("TZ") ?? "Europe/Madrid");
}
catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
{
    zona = TimeZoneInfo.Local;
}

DateTimeOffset Ahora() => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zona);
var repositorio = new RepositorioDeDatos(datos, () => DateTimeOffset.UtcNow);
var lectura = new Lectura(repositorio);
var escritura = new EscrituraDelPanel(repositorio, Ahora, Console.WriteLine);

IResult Seguro(Func<object> f)
{
    try
    {
        return Results.Json(f());
    }
    catch (SolicitudInvalidaException e)
    {
        return Results.Json(new { error = e.Message }, statusCode: 400);
    }
    catch (NoEncontradoException e)
    {
        return Results.Json(new { error = e.Message }, statusCode: 404);
    }
}

string? Q(HttpContext c, string nombre) => c.Request.Query[nombre].FirstOrDefault();
string? Correo(HttpContext c) => c.Items[GuardianDeApiExtensiones.ClaveDeCorreo] as string;

IResult Respuesta(RespuestaDeEscritura r) =>
    r.Error is null ? Results.Json(new { mensaje = r.Mensaje }) : Results.Json(new { error = r.Error }, statusCode: r.Estado);

IResult? IdInvalido(string id, System.Text.RegularExpressions.Regex rx) =>
    rx.IsMatch(id) ? null : Results.Json(new { error = "Identificador no válido." }, statusCode: 400);

IResult? Largo(params string?[] valores) =>
    valores.Any(v => v is { Length: > 2000 }) ? Results.Json(new { error = "El valor es demasiado largo." }, statusCode: 400) : null;

// ------------------------------------------------------------------ API (todas protegidas por el guardián; solo /api/config es pública)
app.MapGet("/api/config", () => Results.Json(new { auth = authPublico }));
app.MapGet("/api/estado", () => Seguro(() => lectura.Estado()));
app.MapGet("/api/dias", (HttpContext c) => Seguro(() => lectura.Dias(Q(c, "desde"), Q(c, "hasta"))));
app.MapGet("/api/registros", (HttpContext c) => Seguro(() =>
{
    if (!int.TryParse(Q(c, "pagina") ?? "1", out var pagina) || !int.TryParse(Q(c, "limite") ?? "50", out var limite))
    {
        throw new SolicitudInvalidaException("«pagina» y «limite» deben ser números.");
    }

    return lectura.Registros(new FiltroDeRegistros(
        Q(c, "q"), Q(c, "desde"), Q(c, "hasta"), Q(c, "entidad"), Q(c, "proyecto"), Q(c, "clase"), Q(c, "certeza"), Q(c, "campo"),
        pagina, limite, Q(c, "orden") == "asc"));
}));
app.MapGet("/api/registros/{id}", (string id) => Seguro(() => lectura.Ficha(id)));
app.MapGet("/api/capturas/{id}", (string id) => Seguro(() => lectura.Captura(id)));
app.MapGet("/api/preguntas", () => Seguro(() => lectura.PreguntasPendientes()));

app.MapPost("/api/preguntas/{id}/responder", (HttpContext c, string id, RespuestaAPregunta cuerpo) =>
{
    if (IdInvalido(id, Identificadores.Pregunta) is { } mal)
    {
        return mal;
    }

    if (Largo(cuerpo.Valor, cuerpo.Registro) is { } largo)
    {
        return largo;
    }

    if (cuerpo.Registro is { Length: > 0 } && !Identificadores.Registro.IsMatch(cuerpo.Registro))
    {
        return Results.Json(new { error = "Identificador de registro no válido." }, statusCode: 400);
    }

    return Respuesta(escritura.Ejecutar("responder", id, Correo(c), op => op.Responder(id, cuerpo.Valor ?? string.Empty, string.IsNullOrEmpty(cuerpo.Registro) ? null : cuerpo.Registro)));
});

app.MapPost("/api/preguntas/{id}/cerrar", (HttpContext c, string id, CierreDePregunta cuerpo) =>
{
    if (IdInvalido(id, Identificadores.Pregunta) is { } mal)
    {
        return mal;
    }

    if (cuerpo.Estado is not ("no_recuerda" or "no_relevante"))
    {
        return Results.Json(new { error = "«estado» debe ser no_recuerda o no_relevante." }, statusCode: 400);
    }

    return Respuesta(escritura.Ejecutar("cerrar-pregunta", id, Correo(c), op => op.CerrarSinDato(id, cuerpo.Estado)));
});

app.MapPost("/api/registros/{id}/corregir", (HttpContext c, string id, Correccion cuerpo) =>
{
    if (IdInvalido(id, Identificadores.Registro) is { } mal)
    {
        return mal;
    }

    if (Largo(cuerpo.Campo, cuerpo.Valor) is { } largo)
    {
        return largo;
    }

    return Respuesta(escritura.Ejecutar("corregir", id, Correo(c), op => op.Corregir(id, cuerpo.Campo ?? string.Empty, cuerpo.Valor ?? string.Empty)));
});

app.MapPost("/api/registros/{id}/rechazar", (HttpContext c, string id, Rechazo cuerpo) =>
{
    if (IdInvalido(id, Identificadores.Registro) is { } mal)
    {
        return mal;
    }

    if (Largo(cuerpo.Campo) is { } largo)
    {
        return largo;
    }

    return Respuesta(escritura.Ejecutar("rechazar", id, Correo(c), op => op.Rechazar(id, cuerpo.Campo ?? string.Empty)));
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.Run("http://0.0.0.0:8080");
return 0;

internal sealed record RespuestaAPregunta(string? Valor, string? Registro);

internal sealed record CierreDePregunta(string? Estado);

internal sealed record Correccion(string? Campo, string? Valor);

internal sealed record Rechazo(string? Campo);
