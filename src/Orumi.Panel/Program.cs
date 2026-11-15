using Microsoft.AspNetCore.HttpOverrides;
using Orumi.Acceso;
using Orumi.Agente;
using Orumi.Core.Ficheros;
using Orumi.Noa;
using Orumi.Core.Hipotesis;
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
// La página vive en otro origen (orumi.northernarchive.com/panel) y la API en este: lista cerrada de orígenes permitidos.
var apiPublica = (Environment.GetEnvironmentVariable("ORUMI_PANEL_API") ?? "https://orumi-api.northernarchive.com").TrimEnd('/');
var origenesPermitidos = (Environment.GetEnvironmentVariable("ORUMI_PANEL_ORIGENES") ?? "https://orumi.northernarchive.com")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(o => o.TrimEnd('/')).ToList();
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
app.UsarCabecerasDeSeguridad(origenAuth, true, apiPublica);
app.UsarCors(origenesPermitidos);
var limites = new PoliticaDeLimites(LimitesDePeticiones.DesdeEntorno(Environment.GetEnvironmentVariable), () => DateTimeOffset.UtcNow);
app.UsarGuardianDeApi(new GuardianDeApi(acceso, limites, []), origenesPermitidos); // ninguna ruta pública bajo /api

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

// Noa (perfil). Se activa con la sección «noa» de la configuración (ORUMI_CONFIG); sin ella solo se puede leer y gestionar lo que ya exista.
Configuracion? configuracionGeneral = null;
ConfiguracionDeNoa? configuracionDeNoa = null;
if (Environment.GetEnvironmentVariable("ORUMI_CONFIG") is { Length: > 0 } rutaConfig && File.Exists(rutaConfig))
{
    var yamlConfig = File.ReadAllText(rutaConfig, System.Text.Encoding.UTF8);
    configuracionGeneral = Configuracion.Leer(yamlConfig);
    configuracionDeNoa = ConfiguracionDeNoa.Leer(yamlConfig);
}

var repaso = new RepasoDelPanel(repositorio, Ahora, Console.WriteLine, configuracionGeneral,
    () => configuracionGeneral is { Proveedor: "anthropic" } ? new Orumi.Agente.Etiquetado.ProveedorAnthropicTexto(configuracionGeneral.Modelo) : null,
    Environment.GetEnvironmentVariable);
var perfil = new PerfilDelPanel(repositorio, Ahora, Console.WriteLine, configuracionDeNoa, configuracionGeneral,
    () => configuracionGeneral is { Proveedor: "anthropic" } ? new Orumi.Agente.Etiquetado.ProveedorAnthropicTexto(configuracionGeneral.Modelo) : null,
    Environment.GetEnvironmentVariable);

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
    r.Error is null ? Results.Json(new { mensaje = r.Mensaje, deshacer = r.Deshacer }) : Results.Json(new { error = r.Error }, statusCode: r.Estado);

IResult? IdInvalido(string id, System.Text.RegularExpressions.Regex rx) =>
    rx.IsMatch(id) ? null : Results.Json(new { error = "Identificador no válido." }, statusCode: 400);

IResult? Largo(params string?[] valores) =>
    valores.Any(v => v is { Length: > 2000 }) ? Results.Json(new { error = "El valor es demasiado largo." }, statusCode: 400) : null;

// ------------------------------------------------------------------ API (todas protegidas por el guardián; ninguna es pública)
// Configuración de la página (fuera de /api): dónde está el proveedor de identidad y la API. No contiene datos.
app.MapGet("/config.json", () => Results.Json(new { auth = authPublico, api = apiPublica }));
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
app.MapGet("/api/preguntas", (HttpContext c) => Seguro(() => lectura.PreguntasPendientes(Q(c, "agente"))));
app.MapGet("/api/papelera", () => Seguro(() => lectura.Papelera()));
app.MapGet("/api/aprendizajes", () => Seguro(() => lectura.Aprendizajes()));
app.MapGet("/api/descubrimientos", () => Seguro(() => lectura.Descubrimientos()));

app.MapGet("/api/repaso", () => Seguro(() => repaso.Ver()));
app.MapPost("/api/repaso", (HttpContext c, Vacio _) => Respuesta(repaso.Lanzar(Correo(c))));
app.MapGet("/api/perfil", () => Seguro(() => perfil.Ver()));
app.MapGet("/api/perfil/descarga", (HttpContext c) =>
{
    var md = perfil.Descarga();
    if (md is null)
    {
        return Results.Json(new { error = "Noa aún no ha generado ningún perfil." }, statusCode: 404);
    }

    c.Response.Headers.ContentDisposition = "attachment; filename=\"perfil-noa.md\"";
    return Results.Text(md, "text/markdown; charset=utf-8");
});

app.MapPost("/api/perfil/generar", (HttpContext c, Vacio _) => Respuesta(perfil.Generar(Correo(c))));

app.MapPost("/api/perfil/rasgos/{objetivo}/{accion}", (HttpContext c, string objetivo, string accion, CambioDePerfil cuerpo) =>
{
    if (!PerfilDelPanel.EsObjetivoValido(objetivo))
    {
        return Results.Json(new { error = "Identificador no válido." }, statusCode: 400);
    }

    if (accion is not ("corregir" or "matizar" or "descartar" or "borrar" or "ocultar" or "mostrar"))
    {
        return Results.Json(new { error = "La acción debe ser corregir, matizar, descartar, borrar, ocultar o mostrar." }, statusCode: 400);
    }

    if (Largo(cuerpo.Texto) is { } largo)
    {
        return largo;
    }

    var texto = cuerpo.Texto ?? string.Empty;
    return Respuesta(perfil.Accion(accion, objetivo, Correo(c), (datos, ahora) =>
    {
        switch (accion)
        {
            case "corregir":
                Orumi.Noa.OperacionesDelPerfil.Corregir(datos, objetivo, texto, ahora);
                return "Corregido: ahora es tu versión y Noa no la vuelve a escribir.";
            case "matizar":
                Orumi.Noa.OperacionesDelPerfil.Matizar(datos, objetivo, texto, ahora);
                return "Matizado: tu comentario prevalece sobre la interpretación de Noa.";
            case "descartar":
                Orumi.Noa.OperacionesDelPerfil.Descartar(datos, objetivo, ahora);
                return "Descartado: no volverá a proponerse salvo evidencia nueva sustancial.";
            case "borrar":
                Orumi.Noa.OperacionesDelPerfil.Borrar(datos, objetivo, ahora);
                return "Borrado: no volverá a proponerse salvo evidencia nueva sustancial.";
            default:
                Orumi.Noa.OperacionesDelPerfil.Ocultar(datos, objetivo, accion == "ocultar", ahora);
                return accion == "ocultar" ? "Oculto: sale de la vista y de la descarga, pero no se borra." : "Visible de nuevo.";
        }
    }));
});

app.MapPost("/api/perfil/borrar-todo", (HttpContext c, BorradoTotal cuerpo) =>
{
    if (cuerpo.Confirmar is not true)
    {
        return Results.Json(new { error = "Borrar el perfil elimina todas sus versiones y su historial. Confírmalo." }, statusCode: 400);
    }

    return Respuesta(perfil.Accion("borrar-todo", null, Correo(c), (datos, _) =>
    {
        Orumi.Noa.OperacionesDelPerfil.BorrarTodo(datos);
        return "Perfil borrado con todo su historial. Capturas, registros y preguntas no se han tocado.";
    }));
});

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

    if (cuerpo.Fusionar is true)
    {
        // «Es el mismo»: responde «misma» y fusiona (el más antiguo es el destino). Se puede deshacer durante un minuto.
        return Respuesta(escritura.EjecutarConDeshacer("responder-y-fusionar", id, Correo(c), op =>
        {
            var (destino, origen) = Orumi.Core.Correcciones.RespuestasRapidas.DestinoDeLaFusion(Orumi.Core.Ficheros.Almacen.Cargar(repositorio.Datos), id);
            var respondida = op.Responder(id, "misma");
            return [respondida, op.Fusionar(destino, origen)];
        }));
    }

    return Respuesta(escritura.Ejecutar("responder", id, Correo(c), op => op.Responder(id, cuerpo.Valor ?? string.Empty, string.IsNullOrEmpty(cuerpo.Registro) ? null : cuerpo.Registro)));
});

app.MapPost("/api/deshacer/{ficha}", (HttpContext c, string ficha, Vacio _) =>
    System.Text.RegularExpressions.Regex.IsMatch(ficha, "^[0-9a-f]{16}$")
        ? Respuesta(escritura.Deshacer(ficha, Correo(c)))
        : Results.Json(new { error = "Identificador no válido." }, statusCode: 400));

app.MapPost("/api/preguntas/{id}/cerrar", (HttpContext c, string id, CierreDePregunta cuerpo) =>
{
    if (IdInvalido(id, Identificadores.Pregunta) is { } mal)
    {
        return mal;
    }

    if (cuerpo.Estado is not ("no_recuerda" or "no_relevante" or "no_sucedio"))
    {
        return Results.Json(new { error = "«estado» debe ser no_recuerda, no_relevante o no_sucedio." }, statusCode: 400);
    }

    return Respuesta(escritura.Ejecutar("cerrar-pregunta", id, Correo(c), op => op.CerrarSinDato(id, cuerpo.Estado)));
});

app.MapPost("/api/aprendizajes/{id}/{accion}", (HttpContext c, string id, string accion, Vacio _) =>
{
    if (!Orumi.Core.Correcciones.Operaciones.IdDeAprendizaje().IsMatch(id))
    {
        return Results.Json(new { error = "Identificador no válido." }, statusCode: 400);
    }

    if (accion is not ("desactivar" or "activar" or "borrar"))
    {
        return Results.Json(new { error = "La acción debe ser desactivar, activar o borrar." }, statusCode: 400);
    }

    return Respuesta(escritura.Ejecutar("aprendizaje-" + accion, id, Correo(c), op => op.CambiarAprendizaje(id, accion)));
});

app.MapPost("/api/hipotesis", (HttpContext c, NuevaHipotesis cuerpo) =>
{
    if (Largo(cuerpo.Formulacion, cuerpo.Factor, cuerpo.Efecto, cuerpo.DescripcionEfecto) is { } largo)
    {
        return largo;
    }

    var desde = Math.Clamp(cuerpo.RetardoDesde ?? 0, 0, 14);
    var hasta = Math.Clamp(cuerpo.RetardoHasta ?? desde, desde, 14);
    return Respuesta(escritura.Ejecutar("hipotesis-declarar", null, Correo(c),
        op => op.DeclararHipotesis(cuerpo.Formulacion ?? string.Empty, cuerpo.Factor ?? string.Empty, cuerpo.Efecto ?? string.Empty, cuerpo.DescripcionEfecto ?? string.Empty, desde, hasta)));
});

app.MapPost("/api/hipotesis/{id}/{accion}", (HttpContext c, string id, string accion, RevisionDeHipotesis cuerpo) =>
{
    if (!AlmacenDeHipotesis.EsIdValido(id))
    {
        return Results.Json(new { error = "Identificador no válido." }, statusCode: 400);
    }

    if (accion is not ("confirmar" or "matizar" or "descartar"))
    {
        return Results.Json(new { error = "La acción debe ser confirmar, matizar o descartar." }, statusCode: 400);
    }

    if (Largo(cuerpo.Contraste, cuerpo.Comentario) is { } largo)
    {
        return largo;
    }

    return Respuesta(escritura.Ejecutar("hipotesis-" + accion, id, Correo(c), op =>
    {
        var efectos = Orumi.Agente.Analisis.MotorDeContraste
            .Analizar(repositorio.Datos, Orumi.Agente.Clima.ConfiguracionDeClima.Leer([])!, new Orumi.Agente.Analisis.UmbralesDeAnalisis(), DateOnly.FromDateTime(DateTime.Now))
            .Contrastes.FirstOrDefault(r => r.Hipotesis == id && (string.IsNullOrEmpty(cuerpo.Contraste) || r.Contraste == cuerpo.Contraste))?.EfectosTotales ?? 0;
        return op.RevisarHipotesis(id, string.IsNullOrEmpty(cuerpo.Contraste) ? null : cuerpo.Contraste, accion, cuerpo.Comentario, efectos);
    }));
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

app.MapPost("/api/registros/{id}/deshacer", (HttpContext c, string id, Deshaciendo cuerpo) =>
{
    if (IdInvalido(id, Identificadores.Registro) is { } mal)
    {
        return mal;
    }

    if (Largo(cuerpo.Campo) is { } largo)
    {
        return largo;
    }

    return Respuesta(escritura.Ejecutar("deshacer", id, Correo(c), op => op.Deshacer(id, string.IsNullOrEmpty(cuerpo.Campo) ? null : cuerpo.Campo)));
});

app.MapPost("/api/registros/{id}/eliminar", (HttpContext c, string id, Vacio _) =>
    IdInvalido(id, Identificadores.Registro) ?? Respuesta(escritura.Ejecutar("eliminar", id, Correo(c), op => op.Eliminar(id))));

app.MapPost("/api/registros/{id}/restaurar", (HttpContext c, string id, Vacio _) =>
    IdInvalido(id, Identificadores.Registro) ?? Respuesta(escritura.Ejecutar("restaurar", id, Correo(c), op => op.Restaurar(id))));

app.MapPost("/api/registros/{id}/fusionar", (HttpContext c, string id, Fusionando cuerpo) =>
{
    if (IdInvalido(id, Identificadores.Registro) is { } mal)
    {
        return mal;
    }

    if (cuerpo.Origen is null || IdInvalido(cuerpo.Origen, Identificadores.Registro) is { } malOrigen)
    {
        return Results.Json(new { error = "Indica el registro que se fusiona en este." }, statusCode: 400);
    }

    return Respuesta(escritura.Ejecutar("fusionar", id, Correo(c), op => op.Fusionar(id, cuerpo.Origen)));
});

app.MapPost("/api/registros/{id}/separar", (HttpContext c, string id, Separando cuerpo) =>
{
    if (IdInvalido(id, Identificadores.Registro) is { } mal)
    {
        return mal;
    }

    if (cuerpo.Captura is null || IdInvalido(cuerpo.Captura, Identificadores.Captura) is { } malCaptura)
    {
        return Results.Json(new { error = "Indica la captura que se separa." }, statusCode: 400);
    }

    if (Largo(cuerpo.Titulo) is { } largo)
    {
        return largo;
    }

    return Respuesta(escritura.Ejecutar("separar", id, Correo(c), op => op.Separar(id, cuerpo.Captura, string.IsNullOrWhiteSpace(cuerpo.Titulo) ? null : cuerpo.Titulo)));
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.Run("http://0.0.0.0:8080");
return 0;

internal sealed record RespuestaAPregunta(string? Valor, string? Registro, bool? Fusionar = null);

internal sealed record CierreDePregunta(string? Estado);

internal sealed record Correccion(string? Campo, string? Valor);

internal sealed record Rechazo(string? Campo);

internal sealed record Deshaciendo(string? Campo);

internal sealed record Vacio;

internal sealed record CambioDePerfil(string? Texto);

internal sealed record BorradoTotal(bool? Confirmar);

internal sealed record NuevaHipotesis(string? Formulacion, string? Factor, string? Efecto, string? DescripcionEfecto, int? RetardoDesde, int? RetardoHasta);

internal sealed record RevisionDeHipotesis(string? Contraste, string? Comentario);

internal sealed record Fusionando(string? Origen);

internal sealed record Separando(string? Captura, string? Titulo);
