using Orumi.Core.Capturas;
using Orumi.Core.Ficheros;

namespace Orumi.Web;

public sealed record SolicitudDeCaptura(string? Texto, bool Voz, string? FechaAcontecimiento, string? Etiqueta, string? Dispositivo, string? IdEnvio);

public sealed record RespuestaDeCaptura(string Id, bool Repetida);

/// <summary>
/// Crea capturas con el mismo código que la línea de comandos. Idempotente: un identificador de envío ya atendido devuelve
/// la misma captura sin crear otra. Solo escribe capturas nuevas y la marca mínima de envío (<c>.envios-web/</c>).
/// </summary>
public sealed class ServicioDeCaptura(string datos, Func<DateTimeOffset> reloj)
{
    public const int LongitudMaxima = 100_000;

    private static readonly object Cerrojo = new();
    private static readonly string[] Dispositivos = ["web", "web-movil", "web-escritorio"];

    public RespuestaDeCaptura Crear(SolicitudDeCaptura s)
    {
        if (!Guid.TryParseExact(s.IdEnvio, "D", out var envio))
        {
            throw new CapturaInvalidaException("Falta el identificador de envío.");
        }

        if (s.Texto is { Length: > LongitudMaxima })
        {
            throw new CapturaInvalidaException($"La nota es demasiado larga (máximo {LongitudMaxima} caracteres).");
        }

        var marca = Path.Combine(datos, ".envios-web", envio.ToString("D"));
        lock (Cerrojo)
        {
            if (File.Exists(marca))
            {
                var anterior = File.ReadAllText(marca).Trim();
                if (Identificadores.Captura.IsMatch(anterior))
                {
                    return new RespuestaDeCaptura(anterior, true);
                }
            }

            var dispositivo = s.Dispositivo is not null && Dispositivos.Contains(s.Dispositivo) ? s.Dispositivo : "web";
            var etiqueta = string.IsNullOrWhiteSpace(s.Etiqueta) ? null : s.Etiqueta.Trim();
            var fecha = string.IsNullOrWhiteSpace(s.FechaAcontecimiento) ? null : s.FechaAcontecimiento.Trim();
            var creada = CreadorDeCapturas.Crear(datos, s.Texto ?? string.Empty, reloj(), dispositivo, fecha, etiqueta, origen: s.Voz ? "voz" : "texto");
            EscrituraAtomica.EscribirNuevo(marca, creada.Id + "\n");
            return new RespuestaDeCaptura(creada.Id, false);
        }
    }
}
