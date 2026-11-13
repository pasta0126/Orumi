using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Orumi.Web;

public enum ResultadoDeAcceso
{
    Permitido,
    Denegado,
    NoAutorizado,
    Bloqueado,
}

/// <summary>Quién es el dueño de una sesión, según el proveedor de identidad.</summary>
public sealed record IdentidadDeSesion(string Correo, bool Verificado);

/// <summary>Consulta al proveedor de identidad. Devuelve null si la sesión no es válida; lanza si el proveedor no responde.</summary>
public interface IVerificadorDeSesion
{
    Task<IdentidadDeSesion?> VerificarAsync(string token, CancellationToken ct);
}

/// <summary>
/// Verifica la sesión preguntando a GoTrue (<c>GET /user</c>) en vez de comprobar la firma: así el servicio no posee el
/// secreto JWT compartido, que es una credencial raíz de todos los proyectos de la plataforma.
/// </summary>
public sealed class VerificadorGoTrue(HttpClient http) : IVerificadorDeSesion
{
    public async Task<IdentidadDeSesion?> VerificarAsync(string token, CancellationToken ct)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Get, "user");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var respuesta = await http.SendAsync(peticion, ct);
        if (respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            return null;
        }

        respuesta.EnsureSuccessStatusCode(); // 5xx u otros: el proveedor no está bien (no es culpa del cliente)
        using var json = await JsonDocument.ParseAsync(await respuesta.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var raiz = json.RootElement;
        var correo = raiz.TryGetProperty("email", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
        var verificado = raiz.TryGetProperty("email_confirmed_at", out var c) && c.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(c.GetString());
        return string.IsNullOrWhiteSpace(correo) ? null : new IdentidadDeSesion(correo, verificado);
    }
}

/// <summary>
/// Acceso de un solo usuario por sesión de Google (GoTrue) con lista de correos permitidos, caché breve de las sesiones
/// aceptadas y bloqueo temporal de un origen tras varios intentos fallidos. El motivo del rechazo no se detalla al cliente.
/// </summary>
public sealed class AccesoPorSesion
{
    private static readonly TimeSpan VidaDeCache = TimeSpan.FromSeconds(60);

    private readonly IVerificadorDeSesion _verificador;
    private readonly HashSet<string> _permitidos;
    private readonly Func<DateTimeOffset> _reloj;
    private readonly int _maxFallos;
    private readonly TimeSpan _bloqueo;
    private readonly Dictionary<string, (int Fallos, DateTimeOffset? BloqueadoHasta)> _estado = [];
    private readonly Dictionary<string, DateTimeOffset> _aceptadas = [];
    private readonly object _cerrojo = new();

    public AccesoPorSesion(IVerificadorDeSesion verificador, IEnumerable<string> correosPermitidos, Func<DateTimeOffset> reloj, int maxFallos = 5, TimeSpan? bloqueo = null)
    {
        _permitidos = correosPermitidos.Select(c => c.Trim().ToLowerInvariant()).Where(c => c.Length > 0).ToHashSet();
        if (_permitidos.Count == 0)
        {
            throw new ArgumentException("Indica al menos un correo permitido.");
        }

        _verificador = verificador;
        _reloj = reloj;
        _maxFallos = maxFallos;
        _bloqueo = bloqueo ?? TimeSpan.FromMinutes(15);
    }

    /// <summary>Lanza la excepción del verificador si el proveedor no responde (no cuenta como intento fallido).</summary>
    public async Task<ResultadoDeAcceso> ComprobarAsync(string? token, string origen, CancellationToken ct = default)
    {
        lock (_cerrojo)
        {
            if (_estado.GetValueOrDefault(origen).BloqueadoHasta is { } h && h > _reloj())
            {
                return ResultadoDeAcceso.Bloqueado;
            }

            if (token is not null && _aceptadas.TryGetValue(Huella(token), out var hasta) && hasta > _reloj())
            {
                _estado.Remove(origen);
                return ResultadoDeAcceso.Permitido;
            }
        }

        IdentidadDeSesion? identidad = null;
        if (!string.IsNullOrWhiteSpace(token))
        {
            identidad = await _verificador.VerificarAsync(token, ct);
        }

        lock (_cerrojo)
        {
            var ahora = _reloj();
            if (identidad is { Verificado: true } i && _permitidos.Contains(i.Correo.Trim().ToLowerInvariant()))
            {
                _estado.Remove(origen);
                foreach (var vencida in _aceptadas.Where(x => x.Value <= ahora).Select(x => x.Key).ToList())
                {
                    _aceptadas.Remove(vencida);
                }

                _aceptadas[Huella(token!)] = ahora + VidaDeCache;
                return ResultadoDeAcceso.Permitido;
            }

            var (fallos, bloqueadoHasta) = _estado.GetValueOrDefault(origen);
            if (bloqueadoHasta is not null)
            {
                fallos = 0; // el bloqueo anterior ya terminó
            }

            fallos++;
            var bloquear = fallos >= _maxFallos;
            _estado[origen] = bloquear ? (0, ahora + _bloqueo) : (fallos, null);
            return bloquear ? ResultadoDeAcceso.Bloqueado : identidad is null ? ResultadoDeAcceso.Denegado : ResultadoDeAcceso.NoAutorizado;
        }
    }

    private static string Huella(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
