namespace Orumi.Acceso;

public readonly record struct ResultadoDeLimite(bool Permitido, TimeSpan Espera)
{
    public static readonly ResultadoDeLimite Si = new(true, TimeSpan.Zero);
}

/// <summary>
/// Limitador de ventana deslizante por clave (IP, usuario…): como mucho <c>maximo</c> peticiones en cualquier ventana de
/// <c>ventana</c>. Reloj inyectable (se prueba con tiempo simulado). Estado en memoria acotado: si hay demasiadas claves
/// distintas se rechazan las nuevas en vez de crecer sin límite.
/// </summary>
public sealed class LimitadorDePeticiones(int maximo, TimeSpan ventana, Func<DateTimeOffset> reloj, int maximoDeClaves = 50_000)
{
    private readonly Dictionary<string, Queue<DateTimeOffset>> _marcas = [];
    private readonly object _cerrojo = new();

    public ResultadoDeLimite Intentar(string clave)
    {
        lock (_cerrojo)
        {
            var ahora = reloj();
            if (_marcas.Count >= maximoDeClaves / 2)
            {
                Purgar(ahora);
            }

            if (!_marcas.TryGetValue(clave, out var cola))
            {
                if (_marcas.Count >= maximoDeClaves)
                {
                    return new ResultadoDeLimite(false, ventana);
                }

                cola = new Queue<DateTimeOffset>();
                _marcas[clave] = cola;
            }

            while (cola.Count > 0 && cola.Peek() <= ahora - ventana)
            {
                cola.Dequeue();
            }

            if (cola.Count >= maximo)
            {
                var espera = cola.Peek() + ventana - ahora;
                return new ResultadoDeLimite(false, espera > TimeSpan.Zero ? espera : TimeSpan.FromSeconds(1));
            }

            cola.Enqueue(ahora);
            return ResultadoDeLimite.Si;
        }
    }

    private void Purgar(DateTimeOffset ahora)
    {
        foreach (var k in _marcas.Where(x => x.Value.Count == 0 || x.Value.Peek() <= ahora - ventana).Select(x => x.Key).ToList())
        {
            if (_marcas[k].Count == 0 || _marcas[k].All(t => t <= ahora - ventana))
            {
                _marcas.Remove(k);
            }
        }
    }
}

/// <summary>Límites por defecto, holgados para una sola persona; configurables por entorno (<c>ORUMI_LIMITE_*</c>).</summary>
public sealed record LimitesDePeticiones(int PorIp = 120, int PorIpEscritura = 20, int Global = 600, int PorUsuario = 240, int PorUsuarioEscritura = 30)
{
    public static LimitesDePeticiones DesdeEntorno(Func<string, string?> entorno)
    {
        int N(string nombre, int defecto) => int.TryParse(entorno(nombre), out var v) && v > 0 ? v : defecto;
        var d = new LimitesDePeticiones();
        return new LimitesDePeticiones(
            N("ORUMI_LIMITE_IP", d.PorIp), N("ORUMI_LIMITE_IP_ESCRITURA", d.PorIpEscritura), N("ORUMI_LIMITE_GLOBAL", d.Global),
            N("ORUMI_LIMITE_USUARIO", d.PorUsuario), N("ORUMI_LIMITE_USUARIO_ESCRITURA", d.PorUsuarioEscritura));
    }
}

/// <summary>
/// Conjunto de limitadores: antes de autenticar se limita por IP y globalmente (para que el tráfico rechazado no llegue al
/// proveedor de identidad); tras autenticar, por usuario. Las escrituras tienen un límite propio más estricto.
/// </summary>
public sealed class PoliticaDeLimites(LimitesDePeticiones limites, Func<DateTimeOffset> reloj)
{
    private static readonly TimeSpan Minuto = TimeSpan.FromMinutes(1);

    private readonly LimitadorDePeticiones _global = new(limites.Global, Minuto, reloj);
    private readonly LimitadorDePeticiones _ip = new(limites.PorIp, Minuto, reloj);
    private readonly LimitadorDePeticiones _ipEscritura = new(limites.PorIpEscritura, Minuto, reloj);
    private readonly LimitadorDePeticiones _usuario = new(limites.PorUsuario, Minuto, reloj);
    private readonly LimitadorDePeticiones _usuarioEscritura = new(limites.PorUsuarioEscritura, Minuto, reloj);

    /// <summary>Antes de autenticar. El tope global se consulta primero para que ninguna IP lo agote en silencio.</summary>
    public ResultadoDeLimite PorOrigen(string ip, bool escritura)
    {
        var g = _global.Intentar("global");
        if (!g.Permitido)
        {
            return g;
        }

        var i = _ip.Intentar(ip);
        if (!i.Permitido)
        {
            return i;
        }

        return escritura ? _ipEscritura.Intentar(ip) : ResultadoDeLimite.Si;
    }

    public ResultadoDeLimite PorUsuario(string correo, bool escritura)
    {
        var u = _usuario.Intentar(correo);
        if (!u.Permitido)
        {
            return u;
        }

        return escritura ? _usuarioEscritura.Intentar(correo) : ResultadoDeLimite.Si;
    }
}
