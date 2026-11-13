using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Orumi.Core.Ficheros;

/// <summary>Formato de identificadores y rutas de <c>docs/formato-de-ficheros.md</c> §2 y §5.</summary>
public static partial class Identificadores
{
    private const string Alfabeto = "abcdefghijklmnopqrstuvwxyz0123456789";

    public static readonly Regex Captura = CapturaRx();
    public static readonly Regex Registro = RegistroRx();
    public static readonly Regex Pregunta = PreguntaRx();
    public static readonly Regex Lote = LoteRx();

    [GeneratedRegex(@"^cap-(\d{4})(\d{2})\d{2}-\d{4}-[a-z0-9]{3}$")] private static partial Regex CapturaRx();
    [GeneratedRegex(@"^reg-(\d{4})(\d{2})\d{2}-[a-z0-9]{4}$")] private static partial Regex RegistroRx();
    [GeneratedRegex(@"^preg-(\d{4})(\d{2})\d{2}-[a-z0-9]{4}$")] private static partial Regex PreguntaRx();
    [GeneratedRegex(@"^lote-(\d{4})(\d{2})\d{2}-\d{4}$")] private static partial Regex LoteRx();

    /// <summary><c>cap-AAAAMMDD-HHMM-xxx</c> con la hora local de la comunicación.</summary>
    public static string NuevaCaptura(DateTimeOffset comunicadaEl) =>
        $"cap-{comunicadaEl:yyyyMMdd}-{comunicadaEl:HHmm}-{Sufijo(3)}";

    public static string Sufijo(int longitud)
    {
        Span<char> caracteres = stackalloc char[longitud];
        for (var i = 0; i < longitud; i++)
        {
            caracteres[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        }

        return new string(caracteres);
    }

    /// <summary>Directorio relativo a <c>orumi/</c> que corresponde a un identificador (<c>capturas/AAAA/MM</c>…), o null si el formato no es válido.</summary>
    public static string? DirectorioDe(string id)
    {
        foreach (var (rx, carpeta) in new[] { (Captura, "capturas"), (Registro, "registros"), (Pregunta, "preguntas"), (Lote, "lotes") })
        {
            var m = rx.Match(id);
            if (m.Success)
            {
                return $"{carpeta}/{m.Groups[1].Value}/{m.Groups[2].Value}";
            }
        }

        return null;
    }

    public static string? RutaRelativa(string id) => DirectorioDe(id) is { } d ? $"{d}/{id}.md" : null;
}
