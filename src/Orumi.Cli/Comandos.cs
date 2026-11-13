using System.Text;
using Orumi.Agente;
using Orumi.Agente.Proveedores;
using Orumi.Core.Capturas;
using Orumi.Core.Ficheros;
using Orumi.Core.Validacion;

namespace Orumi.Cli;

/// <summary>Comandos del MVP. Separados de <c>Main</c> para poder probarlos sin proceso.</summary>
public sealed class Comandos(Func<Stream> entrada, TextWriter salida, TextWriter error, Func<DateTimeOffset> reloj, Func<string, string?> entorno)
{
    public const string VariableDatos = "ORUMI_DATOS";
    public const string VariableConfiguracion = "ORUMI_CONFIG";
    public const string VariableExigirVolumen = "ORUMI_EXIGIR_VOLUMEN";

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
                "procesar" => Procesar(opciones),
                "preparar-volumen" => PrepararVolumen(opciones),
                _ => Uso($"Comando desconocido: {args[0]}"),
            };
        }
        catch (ArgumentException e)
        {
            return Uso(e.Message);
        }
        catch (ProcesamientoFallidoException e)
        {
            error.WriteLine($"No se procesó nada: {e.Message}.");
            return 2;
        }
        catch (FileNotFoundException e)
        {
            error.WriteLine($"No se encuentra el fichero: {e.FileName}");
            return 2;
        }
        catch (VolumenNoDisponibleException e)
        {
            error.WriteLine(e.Message);
            return 5;
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
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or FormatException)
        {
            // Error inesperado: mensaje breve (tipo y texto del error, nunca contenido de las notas) y salida limpia; sin trazas ni procesos colgados.
            error.WriteLine($"Error: {e.GetType().Name}: {e.Message}");
            return 70;
        }
    }

    private int Capturar(Opciones o)
    {
        var datos = DirectorioDatos(o);
        ExigirVolumenSiProcede(o, datos, null);
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
        ExigirVolumenSiProcede(o, datos, null);
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

    private int Procesar(Opciones o)
    {
        var rutaConfig = o.Valor("config") ?? entorno(VariableConfiguracion);
        Configuracion? config = rutaConfig is null ? null : Configuracion.Leer(File.ReadAllText(rutaConfig, Encoding.UTF8));
        var datos = o.Valor("datos") ?? (string.IsNullOrWhiteSpace(config?.Datos) ? null : config.Datos) ?? entorno(VariableDatos);
        if (string.IsNullOrWhiteSpace(datos))
        {
            throw new ArgumentException($"Indica el directorio de datos con --datos, en la configuración o con la variable {VariableDatos}.");
        }

        IProveedorIA proveedor;
        if (o.Valor("simulado") is { } respuestas)
        {
            var respuesta = LectorDeRespuesta.Analizar(File.ReadAllText(respuestas, Encoding.UTF8));
            proveedor = new ProveedorSimulado(_ => respuesta);
        }
        else if (config is { Proveedor: "anthropic" })
        {
            proveedor = new ProveedorAnthropic(config.Modelo);
        }
        else
        {
            throw new ArgumentException("Indica el proveedor en la configuración (--config) o usa --simulado FICHERO para una prueba sin enviar nada.");
        }

        var ejecucion = o.Valor("ejecucion") ?? "peticion";
        if (ejecucion is not ("lote" or "peticion"))
        {
            throw new ArgumentException("--ejecucion debe ser lote o peticion.");
        }

        ExigirVolumenSiProcede(o, datos, config);
        if (!Directory.Exists(datos))
        {
            error.WriteLine($"No existe el directorio de datos: {datos}");
            return 2;
        }

        using var bloqueo = BloqueoDeEjecucion.Adquirir(datos);
        if (bloqueo is null)
        {
            error.WriteLine("Ya hay otra ejecución de Mira en curso: esta termina sin procesar nada.");
            return 3;
        }

        var opciones = new OpcionesProcesador(config?.MaximoPendientes ?? 5, config?.VentanaRelacionadosDias ?? 14);
        var reprocesar = o.Valor("reprocesar") is { } id ? new[] { id } : null;
        var resultado = new Procesador(datos, proveedor, opciones, reloj, config, entorno)
            .EjecutarAsync(ejecucion, reprocesar: reprocesar).GetAwaiter().GetResult();

        if (resultado.Detenida)
        {
            error.WriteLine("No se envió nada: las comprobaciones de privacidad no se cumplen.");
            foreach (var p in resultado.Problemas)
            {
                error.WriteLine($"  - {p.Motivo}");
            }

            salida.WriteLine($"Capturas pendientes: {resultado.Pendientes}.");
            return 4;
        }

        if (resultado.Pendientes == 0)
        {
            salida.WriteLine("No hay capturas pendientes.");
            return 0;
        }

        salida.WriteLine($"Procesadas {resultado.Procesadas.Count} de {resultado.Pendientes} captura(s): " +
                         $"{resultado.RegistrosCreados.Count} registro(s) creados, {resultado.RegistrosActualizados.Count} actualizados, " +
                         $"{resultado.PreguntasCreadas.Count} pregunta(s) nuevas.");
        if (resultado.LoteId is not null)
        {
            salida.WriteLine($"Lote: {resultado.LoteId}");
        }

        foreach (var p in resultado.Problemas)
        {
            error.WriteLine($"  Pendiente {p.Captura}: {p.Motivo}");
        }

        return resultado.Problemas.Count == 0 ? 0 : 1;
    }

    private int PrepararVolumen(Opciones o)
    {
        var datos = DirectorioDatos(o);
        Volumen.Preparar(datos);
        salida.WriteLine($"Volumen preparado: {datos}");
        return 0;
    }

    /// <summary>Si se exige el volumen cifrado (opción o configuración), sin él no se escribe ni se procesa nada.</summary>
    private void ExigirVolumenSiProcede(Opciones o, string datos, Configuracion? config)
    {
        var exigir = o.Valor("exigir-volumen") is "si" or "true" || config?.ExigirVolumen == true
                     || entorno(VariableExigirVolumen) is "1" or "si" or "true";
        if (!exigir)
        {
            // La configuración puede exigirlo aunque el comando no la lea (capturar, validar).
            if (config is null && o.Valor("config") is { } r && File.Exists(r)
                && Configuracion.Leer(File.ReadAllText(r, Encoding.UTF8)).ExigirVolumen)
            {
                Volumen.Exigir(datos);
            }

            return;
        }

        Volumen.Exigir(datos);
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
          orumi preparar-volumen [--datos RUTA]   (una vez, con el volumen cifrado ya montado)
          orumi procesar  [--config FICHERO] [--datos RUTA] [--ejecucion lote|peticion] [--reprocesar CAPTURA]
                          [--simulado RESPUESTAS.json]   (prueba sin enviar nada al proveedor)

        El directorio de datos también puede indicarse con la variable ORUMI_DATOS.
        Con --exigir-volumen si (o `volumen: {exigir: true}` en la configuración, o ORUMI_EXIGIR_VOLUMEN=1)
        ninguna herramienta escribe ni procesa si el volumen cifrado no está disponible.
        """;
}

internal sealed class Opciones
{
    private static readonly HashSet<string> Conocidas =
        ["datos", "texto", "archivo", "fecha-acontecimiento", "etiqueta", "dispositivo", "config", "ejecucion", "reprocesar", "simulado", "exigir-volumen"];

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
