using System.Security.Cryptography;
using System.Text;

namespace Orumi.Web;

public enum ResultadoDeAcceso
{
    Permitido,
    Denegado,
    Bloqueado,
}

/// <summary>
/// Acceso de un solo usuario por token. Comparación en tiempo constante y bloqueo temporal de un origen tras varios
/// intentos fallidos seguidos. El motivo del rechazo nunca se detalla al cliente.
/// </summary>
public sealed class AccesoPorToken
{
    public const int LongitudMinima = 32;

    private readonly byte[] _huella;
    private readonly Func<DateTimeOffset> _reloj;
    private readonly int _maxFallos;
    private readonly TimeSpan _bloqueo;
    private readonly Dictionary<string, (int Fallos, DateTimeOffset? BloqueadoHasta)> _estado = [];
    private readonly object _cerrojo = new();

    public AccesoPorToken(string token, Func<DateTimeOffset> reloj, int maxFallos = 5, TimeSpan? bloqueo = null)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < LongitudMinima)
        {
            throw new ArgumentException($"El token de acceso debe tener al menos {LongitudMinima} caracteres.");
        }

        _huella = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        _reloj = reloj;
        _maxFallos = maxFallos;
        _bloqueo = bloqueo ?? TimeSpan.FromMinutes(15);
    }

    public ResultadoDeAcceso Comprobar(string? presentado, string origen)
    {
        lock (_cerrojo)
        {
            var ahora = _reloj();
            var (fallos, hasta) = _estado.GetValueOrDefault(origen);
            if (hasta is { } h && h > ahora)
            {
                return ResultadoDeAcceso.Bloqueado;
            }

            if (hasta is not null)
            {
                fallos = 0; // el bloqueo ya terminó
            }

            var correcto = presentado is not null
                           && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(presentado)), _huella);
            if (correcto)
            {
                _estado.Remove(origen);
                return ResultadoDeAcceso.Permitido;
            }

            fallos++;
            _estado[origen] = fallos >= _maxFallos ? (0, ahora + _bloqueo) : (fallos, null);
            return fallos >= _maxFallos ? ResultadoDeAcceso.Bloqueado : ResultadoDeAcceso.Denegado;
        }
    }
}
