using System.Text;
using Orumi.Core.Capturas;
using Orumi.Core.Ficheros;
using Orumi.Core.Validacion;

namespace Orumi.Cli;

/// <summary>Comandos del MVP. Separados de <c>Main</c> para poder probarlos sin proceso.</summary>
public sealed class Comandos(Func<Stream> entrada, TextWriter salida, TextWriter error, Func<DateTimeOffset> reloj, Func<string, string?> entorno)
{
    public const string VariableDatos = "ORUMI_DATOS";

    public int Ejecutar(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--ayuda" or "help")
        {
            salida.WriteLine(Ayuda);
            return args.Length == 0 ? 2 : 0;
        }

        try
        {
            var opciones = Opciones.Analizar(args.Skip(1).ToArray());
            return args[0] switch
            {
                "capturar" => Capturar(opciones),
                "validar" => Validar(opciones),
                _ => Uso($"Comando desconocido: {args[0]}"),
            };
        }
        catch (ArgumentException e)
        {
            return Uso(e.Message);
        }
        catch (CapturaInvalidaException e)
        {
            error.WriteLine($"No se creó la captura: {e.Message}");
            return 1;
        }
        catch (EscrituraFallidaException e)
        {
            error.WriteLine($"No se creó la captura: {e.Message}");
            return 1;
        }
    }

    private int Capturar(Opciones o)
    {
        var datos = DirectorioDatos(o);
        var texto = LeerTexto(o);
        var creada = CreadorDeCapturas.Crear(
            datos,
            texto,
            reloj(),
            o.Valor("dispositivo") ?? "terminal",
            o.Valor("fecha-acontecimiento"),
            o.Valor("etiqueta"));
        salida.WriteLine($"Captura creada: {creada.Id}");
        salida.WriteLine(creada.Ruta);
        return 0;
    }

    private int Validar(Opciones o)
    {
        var datos = DirectorioDatos(o);
        if (!Directory.Exists(datos))
        {
            error.WriteLine($"No existe el directorio de datos: {datos}");
            return 2;
        }

        var almacen = Almacen.Cargar(datos);
        var incidencias = Validador.Validar(almacen);
        if (incidencias.Count == 0)
        {
            salida.WriteLine($"OK: sin errores ({almacen.DeTipo("captura").Count()} capturas, {almacen.DeTipo("registro").Count()} registros, " +
                             $"{almacen.DeTipo("pregunta").Count()} preguntas, {almacen.DeTipo("lote").Count()} lotes).");
            return 0;
        }

        foreach (var i in incidencias)
        {
            salida.WriteLine(i);
        }

        salida.WriteLine($"{incidencias.Count} problema(s) en {incidencias.Select(i => i.Fichero).Distinct().Count()} fichero(s).");
        return 1;
    }

    private string DirectorioDatos(Opciones o)
    {
        var datos = o.Valor("datos") ?? entorno(VariableDatos);
        if (string.IsNullOrWhiteSpace(datos))
        {
            throw new ArgumentException($"Indica el directorio de datos con --datos o con la variable {VariableDatos}.");
        }

        return datos;
    }

    private string LeerTexto(Opciones o)
    {
        if (o.Valor("texto") is { } texto)
        {
            return texto;
        }

        if (o.Valor("archivo") is { } archivo)
        {
            return new UTF8Encoding(false).GetString(File.ReadAllBytes(archivo));
        }

        using var flujo = entrada();
        using var memoria = new MemoryStream();
        flujo.CopyTo(memoria);
        return new UTF8Encoding(false).GetString(memoria.ToArray());
    }

    private int Uso(string mensaje)
    {
        error.WriteLine(mensaje);
        error.WriteLine("Usa 'orumi --ayuda' para ver los comandos.");
        return 2;
    }

    public const string Ayuda = """
        orumi — memoria personal

        Uso:
          orumi capturar  [--datos RUTA] [--texto "…" | --archivo FICHERO]   (o el texto por entrada estándar)
                          [--fecha-acontecimiento AAAA-MM-DD] [--etiqueta TEXTO] [--dispositivo TEXTO]
          orumi validar   [--datos RUTA]

        El directorio de datos también puede indicarse con la variable ORUMI_DATOS.
        """;
}

internal sealed class Opciones
{
    private static readonly HashSet<string> Conocidas =
        ["datos", "texto", "archivo", "fecha-acontecimiento", "etiqueta", "dispositivo"];

    private readonly Dictionary<string, string> _valores = [];

    public string? Valor(string nombre) => _valores.GetValueOrDefault(nombre);

    public static Opciones Analizar(string[] args)
    {
        var o = new Opciones();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Argumento inesperado: {args[i]}");
            }

            var nombre = args[i][2..];
            if (!Conocidas.Contains(nombre))
            {
                throw new ArgumentException($"Opción desconocida: --{nombre}");
            }

            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"Falta el valor de --{nombre}");
            }

            o._valores[nombre] = args[++i];
        }

        return o;
    }
}
