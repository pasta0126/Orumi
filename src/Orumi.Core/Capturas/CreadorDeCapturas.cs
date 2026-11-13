using System.Globalization;
using Orumi.Core.Ficheros;
using Orumi.Core.Yaml;

namespace Orumi.Core.Capturas;

public sealed class CapturaInvalidaException(string mensaje) : Exception(mensaje);

public sealed record CapturaCreada(string Id, string Ruta);

/// <summary>
/// Crea una captura inmutable a partir de texto: cuerpo literal, identificador, instante de comunicación,
/// origen texto, dispositivo y huella (spec <c>captura-de-texto</c>).
/// </summary>
public static class CreadorDeCapturas
{
    private const int IntentosDeIdentificador = 20;

    public static CapturaCreada Crear(
        string raizDatos,
        string texto,
        DateTimeOffset comunicadaEl,
        string dispositivo = "terminal",
        string? fechaAcontecimiento = null,
        string? etiqueta = null,
        Func<DateTimeOffset, string>? generadorId = null,
        string origen = "texto")
    {
        if (origen is not ("texto" or "voz"))
        {
            throw new CapturaInvalidaException("El origen debe ser texto o voz.");
        }

        if (string.IsNullOrWhiteSpace(texto))
        {
            throw new CapturaInvalidaException("El texto está vacío: no se crea ninguna captura.");
        }

        if (fechaAcontecimiento is not null
            && !DateOnly.TryParseExact(fechaAcontecimiento, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            throw new CapturaInvalidaException("La fecha del acontecimiento debe tener la forma AAAA-MM-DD.");
        }

        if (string.IsNullOrWhiteSpace(dispositivo))
        {
            throw new CapturaInvalidaException("El dispositivo no puede estar vacío.");
        }

        var generar = generadorId ?? Identificadores.NuevaCaptura;
        Exception? ultimo = null;
        for (var intento = 0; intento < IntentosDeIdentificador; intento++)
        {
            var id = generar(comunicadaEl);
            var ruta = Path.Combine(raizDatos, Identificadores.RutaRelativa(id)
                ?? throw new InvalidOperationException($"Identificador generado no válido: {id}"));

            // El identificador ya existe: se genera otro, la captura existente no se toca.
            if (File.Exists(ruta))
            {
                continue;
            }

            var contenido = Contenido(id, texto, comunicadaEl, dispositivo, fechaAcontecimiento, etiqueta, origen);
            try
            {
                EscrituraAtomica.EscribirNuevo(ruta, contenido);
                Verificar(ruta, id, texto);
                return new CapturaCreada(id, ruta);
            }
            catch (EscrituraFallidaException e) when (e.Message.Contains("Ya existe"))
            {
                ultimo = e; // carrera con otra captura del mismo id: reintento con otro sufijo
            }
        }

        throw new EscrituraFallidaException("No se pudo generar un identificador libre.", ultimo);
    }

    internal static string Contenido(
        string id, string texto, DateTimeOffset comunicadaEl, string dispositivo, string? fechaAcontecimiento, string? etiqueta, string origen = "texto")
    {
        var pares = new List<(string, YNodo)>
        {
            ("id", new YLiteral(id)),
            ("tipo", new YLiteral("captura")),
            ("comunicada_el", new YInstante(comunicadaEl.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture))),
            ("origen", new YLiteral(origen)),
            ("huella", new YLiteral(Huella.De(texto))),
            ("contexto_automatico", YMapa.De(("dispositivo", new YTexto(dispositivo)))),
        };
        if (fechaAcontecimiento is not null)
        {
            pares.Add(("fecha_acontecimiento", new YLiteral(fechaAcontecimiento)));
        }

        if (etiqueta is not null)
        {
            pares.Add(("etiqueta", new YTexto(etiqueta)));
        }

        return "---\n" + YamlEscritor.Escribir(YMapa.De([.. pares])) + "---\n" + texto;
    }

    /// <summary>Relee la captura escrita: debe poder analizarse y su cuerpo y su huella deben coincidir con el original.</summary>
    private static void Verificar(string ruta, string id, string texto)
    {
        try
        {
            var doc = Documento.Analizar(ruta, File.ReadAllText(ruta, System.Text.Encoding.UTF8));
            if (doc.Id != id || doc.Cuerpo != texto || doc.Frontmatter["huella"] as string != Huella.De(texto))
            {
                throw new EscrituraFallidaException($"La verificación de {ruta} falló: no coincide con el texto recibido.");
            }
        }
        catch (Exception e) when (e is not EscrituraFallidaException)
        {
            File.Delete(ruta);
            throw new EscrituraFallidaException($"La verificación de {ruta} falló: {e.Message}", e);
        }
        catch (EscrituraFallidaException)
        {
            File.Delete(ruta);
            throw;
        }
    }
}
