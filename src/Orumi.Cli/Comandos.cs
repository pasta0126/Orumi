using System.Text;
using Orumi.Agente;
using Orumi.Agente.Proveedores;
using Orumi.Core.Capturas;
using Orumi.Core.Correcciones;
using Orumi.Core.Ficheros;
using Orumi.Core.Validacion;
using Orumi.Indice;
using Npgsql;

namespace Orumi.Cli;

/// <summary>Comandos del MVP. Separados de <c>Main</c> para poder probarlos sin proceso.</summary>
public sealed class Comandos(
    Func<Stream> entrada, TextWriter salida, TextWriter error, Func<DateTimeOffset> reloj, Func<string, string?> entorno,
    Func<IProveedorIA?>? proveedorDePrueba = null,
    Func<IProveedorPorLotes?>? proveedorPorLotesDePrueba = null,
    Func<Orumi.Agente.Clima.ConfiguracionDeClima, Orumi.Agente.Clima.IProveedorDeClima>? proveedorDeClimaDePrueba = null,
    Func<Orumi.Agente.Etiquetado.IProveedorDeTexto?>? proveedorDeTextoDePrueba = null,
    Func<Orumi.Agente.Clima.IGeocodificador>? geocodificadorDePrueba = null)
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
            if (args[0] == "consultar")
            {
                return Consultar(args.Skip(1).ToArray());
            }

            if (args[0] == "hipotesis")
            {
                return Hipotesis(args.Skip(1).ToArray());
            }

            if (args[0] == "etiquetas")
            {
                return EtiquetasDeRegistros(args.Skip(1).ToArray());
            }

            if (args[0] == "aprendizajes")
            {
                return Aprendizajes(args.Skip(1).ToArray());
            }

            if (args[0] is "responder" or "corregir" or "rechazar" or "deshacer" or "eliminar" or "restaurar" or "fusionar" or "separar")
            {
                return Modificar(args);
            }

            var opciones = Opciones.Analizar(args.Skip(1).ToArray());
            return args[0] switch
            {
                "capturar" => Capturar(opciones),
                "validar" => Validar(opciones),
                "procesar" => Procesar(opciones),
                "preparar-volumen" => PrepararVolumen(opciones),
                "reindexar" => Reindexar(opciones),
                "estado" => Estado(opciones),
                "clima" => Clima(opciones),
                "etiquetar" => Etiquetar(opciones),
                "analizar" => Analizar(opciones),
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
        catch (NpgsqlException)
        {
            // Sin credenciales ni detalles de conexión en el mensaje.
            error.WriteLine("No se pudo usar la base de datos del índice (¿está en marcha y es correcta ORUMI_DATABASE_URL?). Los ficheros no se han tocado.");
            return 6;
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
            o.Valor("etiqueta"),
            ciudad: o.Valor("ciudad"));
        salida.WriteLine($"Captura creada: {creada.Id}");
        salida.WriteLine(creada.Ruta);
        MostrarAvisos(datos, ConfigOpcional(o));
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
        MostrarAvisos(datos, ConfigOpcional(o)); // el aviso va por la salida de error y no cambia el resultado de la validación
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

    /// <summary>
    /// Procesa con Mira y, si el clima está activado, completa después el clima de los días con registros. El clima nunca
    /// cambia el resultado ni el código de salida de Mira: un fallo solo deja el día pendiente (spec <c>contexto-meteorologico</c>).
    /// </summary>
    private int Procesar(Opciones o)
    {
        var codigo = ProcesarConMira(o);
        if (o.Valor("simulado") is null && ConfigOpcional(o)?.Clima is not null)
        {
            try
            {
                Clima(o);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                error.WriteLine($"Clima pendiente: {e.Message}");
            }
        }

        // Después del clima: etiquetado diario de los registros nuevos y, la primera noche de cada semana, el análisis semanal.
        // Nada de esto cambia el resultado de Mira ni su código de salida (spec «El clima no bloquea…» y X65).
        if (o.Valor("simulado") is null && ConfigOpcional(o) is { } cfg)
        {
            try
            {
                AnalisisNocturno(o, cfg);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                error.WriteLine($"Análisis pendiente: {e.Message}");
            }
        }

        return codigo;
    }

    private void AnalisisNocturno(Opciones o, Configuracion cfg)
    {
        var datos = o.Valor("datos") ?? (string.IsNullOrWhiteSpace(cfg.Datos) ? null : cfg.Datos) ?? entorno(VariableDatos);
        if (string.IsNullOrWhiteSpace(datos) || !Directory.Exists(datos) || Orumi.Core.Hipotesis.AlmacenDeHipotesis.Listar(datos).Count == 0)
        {
            return;
        }

        if (Orumi.Agente.Analisis.RevisionSemanal.Existe(datos, DateOnly.FromDateTime(reloj().DateTime)))
        {
            Etiquetar(o);
        }
        else
        {
            Analizar(o);
        }
    }

    private int ProcesarConMira(Opciones o)
    {
        var rutaConfig = o.Valor("config") ?? entorno(VariableConfiguracion);
        Configuracion? config = rutaConfig is null ? null : Configuracion.Leer(File.ReadAllText(rutaConfig, Encoding.UTF8));
        var datos = o.Valor("datos") ?? (string.IsNullOrWhiteSpace(config?.Datos) ? null : config.Datos) ?? entorno(VariableDatos);
        if (string.IsNullOrWhiteSpace(datos))
        {
            throw new ArgumentException($"Indica el directorio de datos con --datos, en la configuración o con la variable {VariableDatos}.");
        }

        // Modo de procesamiento: por lotes diferidos (con descuento) o inmediato. `--inmediato si` fuerza el inmediato.
        var inmediato = o.Valor("inmediato") is "si" or "true";
        IProveedorPorLotes? porLotes = null;
        if (!inmediato && config?.Procesamiento is { PorLotes: true } && o.Valor("simulado") is null)
        {
            porLotes = proveedorPorLotesDePrueba?.Invoke()
                       ?? (proveedorDePrueba is null && config is { Proveedor: "anthropic" } ? new ProveedorAnthropicLotes(config.Modelo) : null);
        }

        IProveedorIA proveedor;
        if (porLotes is not null)
        {
            proveedor = new DescriptorDeProveedor(porLotes);
        }
        else if (proveedorDePrueba?.Invoke() is { } inyectado)
        {
            proveedor = inyectado;
        }
        else if (o.Valor("simulado") is { } respuestas)
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

        var opciones = new OpcionesProcesador(
            MaximoPendientes: config?.MaximoPendientes ?? 5,
            VentanaRelacionadosDias: config?.VentanaRelacionadosDias ?? 14,
            Precios: config?.Precios,
            Presupuesto: config?.Presupuesto);
        var reprocesar = o.Valor("reprocesar") is { } id ? new[] { id } : null;
        var procesador = new Procesador(datos, proveedor, opciones, reloj, config, entorno);
        ResultadoEjecucion resultado;
        if (porLotes is not null)
        {
            var minutos = int.TryParse(o.Valor("espera-maxima"), out var m) && m >= 0 ? m : config?.Procesamiento?.EsperaMaximaMinutos ?? 90;
            var intervalo = TimeSpan.FromSeconds(config?.Procesamiento?.IntervaloSegundos ?? 60);
            resultado = procesador.EjecutarPorLotesAsync(ejecucion, porLotes, TimeSpan.FromMinutes(minutos), intervalo).GetAwaiter().GetResult();
        }
        else
        {
            resultado = procesador.EjecutarAsync(ejecucion, reprocesar: reprocesar).GetAwaiter().GetResult();
        }


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

        if (resultado.Fatal is { } fatal)
        {
            error.WriteLine(ClasificadorDeErrores.Explicacion(fatal));
            salida.WriteLine($"Capturas pendientes tras detenerse: {resultado.Pendientes - resultado.Procesadas.Count}. Lote: {resultado.LoteId}");
            return fatal == CategoriaError.ClaveInvalida ? 8 : 7;
        }

        if (resultado.Pendientes == 0 && resultado.Envios.Count == 0 && resultado.EnviosAbiertos == 0 && resultado.LoteId is null)
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

        if (porLotes is not null)
        {
            salida.WriteLine($"Modo por lotes (con descuento): {resultado.Envios.Count} envío(s) creado(s); {resultado.EnviosAbiertos} sigue(n) abierto(s) y se recogerá(n) en la siguiente ejecución.");
        }

        if (resultado.TokensEntrada + resultado.TokensSalida > 0)
        {
            salida.WriteLine($"Uso: {resultado.TokensEntrada} tokens de entrada y {resultado.TokensSalida} de salida; coste estimado " +
                             (resultado.CosteEstimadoUsd is { } c ? $"{Formato.Usd2(c)} US$." : "desconocido (sin precios en la configuración)."));
        }

        foreach (var a in resultado.Avisos)
        {
            error.WriteLine($"⚠ Mira: {a}");
        }

        foreach (var p in resultado.Problemas)
        {
            error.WriteLine($"  Pendiente {p.Captura}: {p.Motivo}");
        }

        return resultado.Problemas.Count == 0 ? 0 : 1;
    }

    private string CadenaDeConexion()
    {
        var url = entorno(Conexion.Variable);
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException($"Indica la base de datos del índice con la variable {Conexion.Variable}.");
        }

        return Conexion.DesdeUrl(url);
    }

    private int Reindexar(Opciones o)
    {
        var datos = DirectorioDatos(o);
        ExigirVolumenSiProcede(o, datos, null);
        if (!Directory.Exists(datos))
        {
            error.WriteLine($"No existe el directorio de datos: {datos}");
            return 2;
        }

        var informe = Indexador.ReindexarAsync(datos, CadenaDeConexion(), o.Valor("esquema") ?? Esquema.Vivo, o.Valor("forzar") is "si" or "true")
            .GetAwaiter().GetResult();

        foreach (var i in informe.Omitidos)
        {
            error.WriteLine($"  Omitido (ilegible): {i}");
        }

        foreach (var i in informe.Graves)
        {
            error.WriteLine($"  Grave: {i}");
        }

        if (informe.Referencias.Count > 0)
        {
            error.WriteLine($"  {informe.Referencias.Count} aviso(s) de referencias o formato (usa 'validar' para verlos).");
        }

        if (!informe.Sustituido)
        {
            error.WriteLine(informe.Motivo);
            return 1;
        }

        salida.WriteLine("Índice regenerado: " + string.Join(", ", informe.Filas.Where(f => f.Key is "captura" or "registro" or "pregunta" or "lote").Select(f => $"{f.Value} {f.Key}")) + ".");
        return informe.Omitidos.Count == 0 ? 0 : 1;
    }

    private int Consultar(string[] args)
    {
        if (args.Length == 0)
        {
            return Uso("Indica qué consultar: entidad X | proyecto X | clase X | certeza X [campo] | fecha DESDE HASTA | clima DESDE HASTA [CIUDAD] | relacion ID | pendientes | sin-procesar | conflictos");
        }

        if (args[0] == "papelera")
        {
            return PapeleraDeRegistros(Opciones.Analizar(args.Skip(1).ToArray()));
        }

        if (args[0] == "pendientes")
        {
            return PendientesConContexto(Opciones.Analizar(args.Skip(1).ToArray()));
        }

        var c = new Consultas(CadenaDeConexion());
        void Registros(IEnumerable<FilaRegistro> filas)
        {
            var n = 0;
            foreach (var f in filas)
            {
                n++;
                salida.WriteLine($"{f.Id}  {f.FechaDesde:yyyy-MM-dd}{(f.FechaHasta != f.FechaDesde ? " → " + f.FechaHasta?.ToString("yyyy-MM-dd") : "")}  [{f.Clase}] {f.Titulo}");
            }

            salida.WriteLine(n == 0 ? "Sin resultados." : $"{n} registro(s).");
        }

        var valor = args.Length > 1 ? string.Join(' ', args.Skip(1)) : string.Empty;
        switch (args[0])
        {
            case "entidad" when valor != "": Registros(c.PorEntidad(valor).GetAwaiter().GetResult()); break;
            case "proyecto" when valor != "": Registros(c.PorProyecto(valor).GetAwaiter().GetResult()); break;
            case "clase" when valor != "": Registros(c.PorClase(valor).GetAwaiter().GetResult()); break;
            case "certeza" when args.Length > 1: Registros(c.PorCerteza(args[1], args.Length > 2 ? args[2] : null).GetAwaiter().GetResult()); break;
            case "relacion" when valor != "": Registros(c.PorRelacion(valor).GetAwaiter().GetResult()); break;
            case "fecha" when args.Length == 3 && DateOnly.TryParse(args[1], out var d) && DateOnly.TryParse(args[2], out var h):
                Registros(c.PorFecha(d, h).GetAwaiter().GetResult());
                break;
            case "clima" when args.Length is 3 or 4 && DateOnly.TryParse(args[1], out var cd) && DateOnly.TryParse(args[2], out var ch):
                var dias = c.ClimaPorFecha(cd, ch, args.Length == 4 ? args[3] : null).GetAwaiter().GetResult();
                static string N(double? v, string u = "") => v is null ? "desconocido" : v.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + u;
                foreach (var f in dias)
                {
                    salida.WriteLine($"{f.Fecha:yyyy-MM-dd}  {f.Ciudad}  {N(f.TemperaturaMinima)}–{N(f.TemperaturaMaxima, " °C")}  hum {N(f.HumedadMedia, " %")}  lluvia {N(f.PrecipitacionMm, " mm")}  nubes {N(f.NubosidadMedia, " %")}  UV máx {N(f.UvMaximo)}");
                }

                salida.WriteLine(dias.Count == 0 ? "Sin clima guardado para ese rango." : $"{dias.Count} día(s).");
                break;
            case "pendientes":
                var preguntas = c.PreguntasPendientes().GetAwaiter().GetResult();
                foreach (var p in preguntas)
                {
                    salida.WriteLine($"{p.Id}  [{p.Valor}] {p.Texto}");
                }

                salida.WriteLine(preguntas.Count == 0 ? "Sin preguntas pendientes." : $"{preguntas.Count} pregunta(s) pendiente(s).");
                break;
            case "sin-procesar":
                var sin = c.CapturasSinProcesar().GetAwaiter().GetResult();
                sin.ForEach(salida.WriteLine);
                salida.WriteLine(sin.Count == 0 ? "No hay capturas sin procesar." : $"{sin.Count} captura(s) sin procesar.");
                break;
            case "conflictos":
                var conflictos = c.ConflictosAbiertos().GetAwaiter().GetResult();
                foreach (var f in conflictos)
                {
                    salida.WriteLine($"{f.RegistroId} ↔ {f.ConRegistroId}  ({f.Campo})  pregunta {f.PreguntaId}");
                }

                salida.WriteLine(conflictos.Count == 0 ? "Sin conflictos abiertos." : $"{conflictos.Count} fila(s) de conflicto abierto.");
                break;
            default:
                return Uso($"Consulta no reconocida o incompleta: {args[0]}");
        }

        return 0;
    }

    /// <summary>La configuración, si existe y es legible; nunca hace fallar un comando.</summary>
    private Configuracion? ConfigOpcional(Opciones o)
    {
        try
        {
            var ruta = o.Valor("config") ?? entorno(VariableConfiguracion);
            return ruta is not null && File.Exists(ruta) ? Configuracion.Leer(File.ReadAllText(ruta, Encoding.UTF8)) : null;
        }
        catch (Exception e) when (e is IOException or FormatException or YamlDotNet.Core.YamlException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Aviso breve en la salida de error (saldo agotado, clave inválida, presupuesto cerca): nunca bloquea ni falla.</summary>
    private void MostrarAvisos(string datos, Configuracion? config)
    {
        try
        {
            foreach (var aviso in ServicioDeEstado.Calcular(datos, config).Avisos)
            {
                error.WriteLine($"⚠ Mira: {aviso}");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException)
        {
            // un aviso nunca debe impedir el comando
        }
    }

    /// <summary>responder / corregir / rechazar: argumentos posicionales, opciones <c>--datos</c> y <c>--registro</c>, y los indicadores <c>--no-recuerdo</c> y <c>--no-relevante</c>.</summary>
    private int Modificar(string[] args)
    {
        var posicionales = new List<string>();
        var opciones = new List<string>();
        string? cierre = null;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--no-recuerdo": cierre = "no_recuerda"; break;
                case "--no-relevante": cierre = "no_relevante"; break;
                case "--no-sucedio": cierre = "no_sucedio"; break;
                case var a when a.StartsWith("--", StringComparison.Ordinal):
                    opciones.Add(a);
                    if (i + 1 < args.Length)
                    {
                        opciones.Add(args[++i]);
                    }

                    break;
                default: posicionales.Add(args[i]); break;
            }
        }

        var o = Opciones.Analizar([.. opciones]);
        var datos = DirectorioDatos(o);
        if (!Directory.Exists(datos))
        {
            error.WriteLine($"No existe el directorio de datos: {datos}");
            return 2;
        }

        string Texto(int desde) => string.Join(' ', posicionales.Skip(desde)).Trim();
        Func<Operaciones, ResultadoOperacion> accion;
        switch (args[0])
        {
            case "responder" when cierre is not null && posicionales.Count == 1:
                accion = op => op.CerrarSinDato(posicionales[0], cierre);
                break;
            case "responder" when cierre is null && posicionales.Count >= 2:
                accion = op => op.Responder(posicionales[0], Texto(1), o.Valor("registro"));
                break;
            case "corregir" when posicionales.Count >= 3:
                accion = op => op.Corregir(posicionales[0], posicionales[1], Texto(2));
                break;
            case "rechazar" when posicionales.Count == 2:
                accion = op => op.Rechazar(posicionales[0], posicionales[1]);
                break;
            case "deshacer" when posicionales.Count is 1 or 2:
                accion = op => op.Deshacer(posicionales[0], posicionales.Count == 2 ? posicionales[1] : null);
                break;
            case "eliminar" when posicionales.Count == 1:
                accion = op => op.Eliminar(posicionales[0]);
                break;
            case "restaurar" when posicionales.Count == 1:
                accion = op => op.Restaurar(posicionales[0]);
                break;
            case "fusionar" when posicionales.Count == 2:
                accion = op => op.Fusionar(posicionales[0], posicionales[1]);
                break;
            case "separar" when posicionales.Count == 2:
                accion = op => op.Separar(posicionales[0], posicionales[1], o.Valor("titulo"));
                break;
            default:
                return Uso("Uso: orumi responder PREGUNTA VALOR [--registro ID] | responder PREGUNTA --no-recuerdo | --no-relevante | --no-sucedio\n"
                           + "     orumi corregir REGISTRO CAMPO VALOR\n     orumi rechazar REGISTRO CAMPO\n"
                           + "     orumi deshacer REGISTRO [CAMPO]\n     orumi eliminar REGISTRO | restaurar REGISTRO\n"
                           + "     orumi fusionar DESTINO ORIGEN\n     orumi separar REGISTRO CAPTURA [--titulo TEXTO]");
        }

        using var bloqueo = BloqueoDeEjecucion.Adquirir(datos);
        if (bloqueo is null)
        {
            error.WriteLine("Mira está procesando ahora mismo: no se ha modificado nada. Vuelve a intentarlo dentro de unos minutos.");
            return 3;
        }

        try
        {
            var r = accion(new Operaciones(datos, reloj));
            salida.WriteLine(r.Mensaje);
            salida.WriteLine("El índice se actualizará en el próximo lote (o con «orumi reindexar»).");
            return 0;
        }
        catch (OperacionInvalidaException e)
        {
            error.WriteLine($"No se ha modificado nada: {e.Message}");
            return 2;
        }
    }

    /// <summary>
    /// Hipótesis del usuario (spec <c>contraste-de-hipotesis</c>): se declaran con sus palabras y quedan «por verificar».
    /// Mira las contrasta con evidencia; nunca las convierte en hechos por sí sola.
    /// </summary>
    private int Hipotesis(string[] args)
    {
        var accion = args.Length > 0 ? args[0] : "listar";
        var o = Opciones.Analizar([.. args.Skip(1).SkipWhile(a => !a.StartsWith("--", StringComparison.Ordinal))]);
        var datos = DirectorioDatos(o);
        if (!Directory.Exists(datos))
        {
            error.WriteLine($"No existe el directorio de datos: {datos}");
            return 2;
        }

        switch (accion)
        {
            case "listar":
                var hs = Orumi.Core.Hipotesis.AlmacenDeHipotesis.Listar(datos);
                foreach (var h in hs)
                {
                    salida.WriteLine($"{h.Id}  [{h.Estado}]  «{h.Formulacion}»");
                    foreach (var c in h.Contrastes)
                    {
                        salida.WriteLine($"    {c.Id}: {string.Join(" + ", c.Factor)} → {c.Efecto}  (retardo {c.RetardoDesde}–{c.RetardoHasta} días)");
                    }
                }

                salida.WriteLine(hs.Count == 0 ? "No hay hipótesis declaradas. Usa «orumi hipotesis iniciales» o «añadir»." : $"{hs.Count} hipótesis.");
                return 0;
            case "confirmar" or "matizar" or "descartar":
                return args.Length >= 2 && !args[1].StartsWith("--", StringComparison.Ordinal)
                    ? RevisarHipotesis(o, args[1], accion)
                    : Uso("Uso: orumi hipotesis confirmar|matizar|descartar HIPOTESIS[#CONTRASTE] [--comentario \"…\"]");
            case "iniciales":
            case "añadir" or "anadir":
                using (var bloqueo = BloqueoDeEjecucion.Adquirir(datos))
                {
                    if (bloqueo is null)
                    {
                        error.WriteLine("Mira está procesando ahora mismo: no se ha modificado nada. Vuelve a intentarlo dentro de unos minutos.");
                        return 3;
                    }

                    try
                    {
                        if (accion == "iniciales")
                        {
                            var nuevas = Orumi.Core.Hipotesis.AlmacenDeHipotesis.CargarIniciales(datos, reloj());
                            salida.WriteLine(nuevas.Count == 0 ? "Las hipótesis iniciales ya estaban cargadas." : $"Cargadas {nuevas.Count} hipótesis iniciales: {string.Join(", ", nuevas)}.");
                            return 0;
                        }

                        var retardo = (o.Valor("retardo") ?? "0").Split('-');
                        var desde = int.Parse(retardo[0], System.Globalization.CultureInfo.InvariantCulture);
                        var hasta = retardo.Length > 1 ? int.Parse(retardo[1], System.Globalization.CultureInfo.InvariantCulture) : desde;
                        var conceptos = (o.Valor("descripciones") ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => x.Split('=', 2)).Where(x => x.Length == 2).ToDictionary(x => x[0].Trim(), x => x[1].Trim());
                        var h = Orumi.Core.Hipotesis.AlmacenDeHipotesis.Anadir(
                            datos, o.Valor("formulacion") ?? throw new ArgumentException("Falta --formulacion con tus palabras."),
                            (o.Valor("factor") ?? throw new ArgumentException("Falta --factor (p. ej. clima:uv_alto).")).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                            o.Valor("efecto") ?? throw new ArgumentException("Falta --efecto (p. ej. dolor_de_cabeza)."), desde, hasta, conceptos, reloj());
                        salida.WriteLine($"Hipótesis {h.Id} declarada: queda «por verificar».");
                        return 0;
                    }
                    catch (Orumi.Core.Hipotesis.HipotesisInvalidaException e)
                    {
                        error.WriteLine($"No se ha modificado nada: {e.Message}");
                        return 2;
                    }
                    catch (FormatException)
                    {
                        error.WriteLine("No se ha modificado nada: --retardo debe ser un número de días o un rango (p. ej. 1-2).");
                        return 2;
                    }
                }

            default:
                return Uso("Uso: orumi hipotesis listar | iniciales | confirmar|matizar|descartar HIPOTESIS[#CONTRASTE] [--comentario \"…\"] | añadir --formulacion \"…\" --factor clima:uv_alto[,…] --efecto CONCEPTO [--retardo 0-1] [--descripciones \"concepto=descripción|…\"]");
        }
    }

    /// <summary>
    /// Etiqueta con el modelo qué conceptos de las hipótesis aparecen explícitamente en lo que el usuario dijo (solo su texto; nunca el clima).
    /// Con <c>--simulado FICHERO</c> usa esa respuesta en lugar del proveedor, sin enviar nada.
    /// </summary>
    private int Etiquetar(Opciones o)
    {
        var rutaConfig = o.Valor("config") ?? entorno(VariableConfiguracion);
        Configuracion? config = rutaConfig is null ? null : Configuracion.Leer(File.ReadAllText(rutaConfig, Encoding.UTF8));
        var datos = o.Valor("datos") ?? (string.IsNullOrWhiteSpace(config?.Datos) ? null : config.Datos) ?? entorno(VariableDatos);
        if (string.IsNullOrWhiteSpace(datos) || !Directory.Exists(datos))
        {
            error.WriteLine($"Indica un directorio de datos existente con --datos, en la configuración o con la variable {VariableDatos}.");
            return 2;
        }

        var proveedor = ProveedorDeTexto(o, config)
                        ?? throw new ArgumentException("Indica el proveedor en la configuración (--config) o usa --simulado FICHERO para una prueba sin enviar nada.");

        using var bloqueo = BloqueoDeEjecucion.Adquirir(datos);
        if (bloqueo is null)
        {
            error.WriteLine("Mira está procesando ahora mismo: esta ejecución termina sin etiquetar nada.");
            return 3;
        }

        var r = new Orumi.Agente.Etiquetado.Etiquetador(datos, proveedor, reloj, config, entorno).EjecutarAsync().GetAwaiter().GetResult();
        if (r.Detenida.Count > 0)
        {
            error.WriteLine("No se envió nada: las comprobaciones de privacidad no se cumplen.");
            r.Detenida.ToList().ForEach(m => error.WriteLine($"  - {m}"));
            return 4;
        }

        salida.WriteLine($"Etiquetado: {r.Enviados} registro(s) enviados, {r.Etiquetados} etiqueta(s), {r.Omitidos} sin cambios, {r.EtiquetasDescartadas} descartada(s) por no cumplir las reglas.");
        if (r.TokensEntrada + r.TokensSalida > 0)
        {
            salida.WriteLine($"Uso: {r.TokensEntrada} tokens de entrada y {r.TokensSalida} de salida; coste estimado " + (r.CosteUsd is { } c ? $"{Formato.Usd2(c)} US$." : "desconocido."));
        }

        r.Problemas.ToList().ForEach(p => error.WriteLine($"  {p}"));
        return r.Problemas.Count == 0 ? 0 : 1;
    }

    /// <summary>Proveedor para el etiquetado: el de prueba, una respuesta simulada o el configurado; <c>null</c> si no hay ninguno.</summary>
    private Orumi.Agente.Etiquetado.IProveedorDeTexto? ProveedorDeTexto(Opciones o, Configuracion? config)
    {
        if (proveedorDeTextoDePrueba?.Invoke() is { } inyectado)
        {
            return inyectado;
        }

        if (o.Valor("simulado") is { } fichero)
        {
            var respuesta = File.ReadAllText(fichero, Encoding.UTF8);
            return new Orumi.Agente.Etiquetado.ProveedorDeTextoSimulado((_, _) => respuesta);
        }

        return config is { Proveedor: "anthropic" } ? new Orumi.Agente.Etiquetado.ProveedorAnthropicTexto(config.Modelo) : null;
    }

    /// <summary>
    /// Análisis semanal (X21): etiqueta los registros nuevos, contrasta las hipótesis con los registros y el clima, crea pocas preguntas de contraste
    /// y escribe la revisión de la semana. Con <c>--simulado</c> el etiquetado usa esa respuesta; sin proveedor se salta el etiquetado.
    /// </summary>
    private int Analizar(Opciones o)
    {
        var rutaConfig = o.Valor("config") ?? entorno(VariableConfiguracion);
        Configuracion? config = rutaConfig is null ? null : Configuracion.Leer(File.ReadAllText(rutaConfig, Encoding.UTF8));
        var datos = o.Valor("datos") ?? (string.IsNullOrWhiteSpace(config?.Datos) ? null : config.Datos) ?? entorno(VariableDatos);
        if (string.IsNullOrWhiteSpace(datos) || !Directory.Exists(datos))
        {
            error.WriteLine($"Indica un directorio de datos existente con --datos, en la configuración o con la variable {VariableDatos}.");
            return 2;
        }

        using var bloqueo = BloqueoDeEjecucion.Adquirir(datos);
        if (bloqueo is null)
        {
            error.WriteLine("Mira está procesando ahora mismo: el análisis termina sin hacer nada.");
            return 3;
        }

        var codigo = 0;
        if (Orumi.Core.Hipotesis.AlmacenDeHipotesis.Listar(datos).Count == 0)
        {
            salida.WriteLine("No hay hipótesis declaradas: usa «orumi hipotesis iniciales» o «añadir». No se analiza nada.");
            return 0;
        }

        if (o.Valor("etiquetar") != "no" && ProveedorDeTexto(o, config) is { } proveedor)
        {
            var e = new Orumi.Agente.Etiquetado.Etiquetador(datos, proveedor, reloj, config, entorno).EjecutarAsync().GetAwaiter().GetResult();
            if (e.Detenida.Count > 0)
            {
                error.WriteLine("Etiquetado detenido: las comprobaciones de privacidad no se cumplen (se sigue con las etiquetas que ya hay).");
                e.Detenida.ToList().ForEach(m => error.WriteLine($"  - {m}"));
                codigo = 1;
            }
            else
            {
                salida.WriteLine($"Etiquetado: {e.Enviados} registro(s) enviados, {e.Etiquetados} etiqueta(s), {e.Omitidos} sin cambios.");
                e.Problemas.ToList().ForEach(p => error.WriteLine($"  {p}"));
            }
        }

        var clima = config?.Clima ?? Orumi.Agente.Clima.ConfiguracionDeClima.Leer([])!;
        var r = Orumi.Agente.Analisis.RevisionSemanal.Generar(datos, config, clima, config?.Analisis ?? new Orumi.Agente.Analisis.UmbralesDeAnalisis(), reloj());
        var a = r.Analisis;
        if (r.EnObservacion)
        {
            salida.WriteLine($"Etapa de observación: {a.DiasDeObservacion} de {(config?.Analisis ?? new Orumi.Agente.Analisis.UmbralesDeAnalisis()).SemanasDeObservacion * 7} días desde el primer registro; {a.DiasConRegistros} día(s) con registros. Aún no hay contrastes.");
        }
        else
        {
            foreach (var c in a.Contrastes)
            {
                salida.WriteLine($"{c.Id}  [{c.Veredicto}]  {c.Texto}");
            }

            if (a.Descartados.Count > 0)
            {
                salida.WriteLine($"{a.Descartados.Count} contraste(s) descartado(s) por ti, sin evidencia nueva sustancial.");
            }
        }

        if (r.PreguntasCreadas.Count > 0)
        {
            salida.WriteLine($"Preguntas de contraste creadas: {r.PreguntasCreadas.Count} ({string.Join(", ", r.PreguntasCreadas)}).");
        }

        salida.WriteLine($"Revisión {r.Id} escrita: {r.Ruta}");
        return codigo;
    }

    /// <summary>El usuario confirma, matiza o descarta un contraste de una hipótesis (X22).</summary>
    private int RevisarHipotesis(Opciones o, string objetivo, string accion)
    {
        var datos = DirectorioDatos(o);
        var partes = objetivo.Split('#', 2);
        using var bloqueo = BloqueoDeEjecucion.Adquirir(datos);
        if (bloqueo is null)
        {
            error.WriteLine("Mira está procesando ahora mismo: no se ha modificado nada. Vuelve a intentarlo dentro de unos minutos.");
            return 3;
        }

        try
        {
            var h = Orumi.Core.Hipotesis.AlmacenDeHipotesis.Leer(datos, partes[0])
                    ?? throw new Orumi.Core.Hipotesis.HipotesisInvalidaException($"No existe la hipótesis «{partes[0]}».");
            var contrasteId = partes.Length > 1 ? partes[1] : (h.Contrastes.Count == 1 ? h.Contrastes[0].Id : null);
            var rutaConfig = o.Valor("config") ?? entorno(VariableConfiguracion);
            var config = rutaConfig is not null && File.Exists(rutaConfig) ? Configuracion.Leer(File.ReadAllText(rutaConfig, Encoding.UTF8)) : null;
            var efectos = Orumi.Agente.Analisis.MotorDeContraste
                .Analizar(datos, config?.Clima ?? Orumi.Agente.Clima.ConfiguracionDeClima.Leer([])!, config?.Analisis ?? new Orumi.Agente.Analisis.UmbralesDeAnalisis(), DateOnly.FromDateTime(reloj().DateTime))
                .Contrastes.FirstOrDefault(c => c.Hipotesis == partes[0] && c.Contraste == contrasteId)?.EfectosTotales ?? 0;
            var r = Orumi.Core.Hipotesis.AlmacenDeHipotesis.Revisar(datos, partes[0], contrasteId, accion, o.Valor("comentario"), efectos, reloj());
            salida.WriteLine($"{partes[0]}{(contrasteId is null ? "" : "#" + contrasteId)}: {accion}. La hipótesis queda «{r.Estado}».");
            return 0;
        }
        catch (Orumi.Core.Hipotesis.HipotesisInvalidaException e)
        {
            error.WriteLine($"No se ha modificado nada: {e.Message}");
            return 2;
        }
    }

    /// <summary>El usuario corrige una etiqueta de un registro (presente, negado o quitar): su palabra prevalece al regenerar.</summary>
    private int EtiquetasDeRegistros(string[] args)
    {
        var pos = args.TakeWhile(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
        var o = Opciones.Analizar([.. args.Skip(pos.Count)]);
        var datos = DirectorioDatos(o);
        if (!Directory.Exists(datos))
        {
            error.WriteLine($"No existe el directorio de datos: {datos}");
            return 2;
        }

        if (pos.Count == 2 && pos[0] == "ver")
        {
            var e = Orumi.Core.Hipotesis.AlmacenDeEtiquetas.Leer(datos, pos[1]);
            if (e is null)
            {
                salida.WriteLine("Ese registro aún no tiene etiquetas.");
                return 0;
            }

            e.Etiquetas.ToList().ForEach(t => salida.WriteLine($"{t.Concepto}  [{t.Estado}, {t.Origen}]  {(t.Minutos is { } m ? m + " min  " : "")}{(t.Cita is null ? "" : "«" + t.Cita + "»")}"));
            salida.WriteLine(e.Etiquetas.Count == 0 ? "Sin etiquetas: no se mencionó ningún concepto." : $"{e.Etiquetas.Count} etiqueta(s).");
            return 0;
        }

        if (pos.Count != 4 || pos[0] != "corregir")
        {
            return Uso("Uso: orumi etiquetas ver REGISTRO | corregir REGISTRO CONCEPTO presente|negado|quitar");
        }

        using var bloqueo = BloqueoDeEjecucion.Adquirir(datos);
        if (bloqueo is null)
        {
            error.WriteLine("Mira está procesando ahora mismo: no se ha modificado nada. Vuelve a intentarlo dentro de unos minutos.");
            return 3;
        }

        try
        {
            Orumi.Core.Hipotesis.AlmacenDeEtiquetas.Corregir(datos, pos[1], pos[2], pos[3], reloj());
            salida.WriteLine($"Etiqueta «{pos[2]}» de {pos[1]}: {pos[3]}. Tu corrección prevalece al regenerar.");
            return 0;
        }
        catch (Orumi.Core.Hipotesis.HipotesisInvalidaException e)
        {
            error.WriteLine($"No se ha modificado nada: {e.Message}");
            return 2;
        }
    }

    /// <summary>
    /// Lo que Mira ha aprendido de cómo se cierran sus preguntas (X64): se ve con su evidencia y el usuario lo desactiva, activa o borra.
    /// Nada se aplica en secreto y ninguna clase de pregunta queda prohibida: solo pasa por detrás de las demás.
    /// </summary>
    private int Aprendizajes(string[] args)
    {
        var accion = args.Length > 0 ? args[0] : "listar";
        var resto = args.Skip(1).ToList();
        var opciones = new List<string>();
        var id = (string?)null;
        for (var i = 0; i < resto.Count; i++)
        {
            if (resto[i].StartsWith("--", StringComparison.Ordinal))
            {
                opciones.Add(resto[i]);
                if (i + 1 < resto.Count)
                {
                    opciones.Add(resto[++i]);
                }
            }
            else
            {
                id ??= resto[i];
            }
        }

        var o = Opciones.Analizar([.. opciones]);
        var datos = DirectorioDatos(o);
        if (!Directory.Exists(datos))
        {
            error.WriteLine($"No existe el directorio de datos: {datos}");
            return 2;
        }

        if (accion == "listar" && id is null)
        {
            var reglas = Orumi.Core.Aprendizaje.MotorDeAprendizaje.Listar(datos);
            foreach (var r in reglas)
            {
                salida.WriteLine($"{r.Id}  [{r.Estado}]  {r.Descripcion}");
                salida.WriteLine($"    evidencia: {string.Join(", ", r.Evidencia)}");
            }

            salida.WriteLine(reglas.Count == 0 ? "Mira aún no ha aprendido nada de tus cierres de preguntas." : $"{reglas.Count} aprendizaje(s).");
            return 0;
        }

        if (id is null || accion is not ("desactivar" or "activar" or "borrar"))
        {
            return Uso("Uso: orumi aprendizajes listar | desactivar ID | activar ID | borrar ID");
        }

        using var bloqueo = BloqueoDeEjecucion.Adquirir(datos);
        if (bloqueo is null)
        {
            error.WriteLine("Mira está procesando ahora mismo: no se ha modificado nada. Vuelve a intentarlo dentro de unos minutos.");
            return 3;
        }

        try
        {
            var r = accion switch
            {
                "desactivar" => Orumi.Core.Aprendizaje.MotorDeAprendizaje.Desactivar(datos, id, reloj()),
                "activar" => Orumi.Core.Aprendizaje.MotorDeAprendizaje.Activar(datos, id, reloj()),
                _ => Orumi.Core.Aprendizaje.MotorDeAprendizaje.Borrar(datos, id, reloj()),
            };
            salida.WriteLine($"{r.Id}: {(accion == "borrar" ? "borrado (solo volverá a aprenderse con evidencia nueva)" : r.Estado)}.");
            return 0;
        }
        catch (Orumi.Core.Aprendizaje.AprendizajeNoEncontradoException e)
        {
            error.WriteLine(e.Message);
            return 2;
        }
    }

    /// <summary>Registros eliminados (a la papelera): se restauran con «orumi restaurar».</summary>
    private int PapeleraDeRegistros(Opciones o)
    {
        var eliminados = Almacen.Cargar(DirectorioDatos(o)).DeTipo("registro")
            .Where(r => r.Frontmatter.GetValueOrDefault("estado") as string == "eliminado")
            .OrderByDescending(r => r.Frontmatter.GetValueOrDefault("eliminado_el") as string, StringComparer.Ordinal).ToList();
        foreach (var r in eliminados)
        {
            salida.WriteLine($"{r.Id}  eliminado el {r.Frontmatter.GetValueOrDefault("eliminado_el")}  «{r.Frontmatter.GetValueOrDefault("titulo")}»");
        }

        salida.WriteLine(eliminados.Count == 0 ? "La papelera está vacía." : $"{eliminados.Count} registro(s) en la papelera.");
        return 0;
    }

    /// <summary>Preguntas pendientes con el contexto necesario para contestar, leídas de los ficheros (sin depender del índice).</summary>
    private int PendientesConContexto(Opciones o)
    {
        var preguntas = PreguntasConContexto.Pendientes(Almacen.Cargar(DirectorioDatos(o)));
        string Titulo(RegistroImplicado r) => r.Titulo is null ? r.Id : $"{r.Id} «{r.Titulo}»";
        foreach (var p in preguntas)
        {
            salida.WriteLine($"{p.Id}  [{p.Valor}] {p.Texto}");
            salida.WriteLine($"    registro: {Titulo(p.Registro)} · campo: {p.Campo} · valor actual: {p.Registro.ValorActual}");
            foreach (var otro in p.Otros)
            {
                salida.WriteLine($"    con: {Titulo(otro)} · valor actual: {otro.ValorActual}");
            }
        }

        salida.WriteLine(preguntas.Count == 0 ? "Sin preguntas pendientes." : $"{preguntas.Count} pregunta(s) pendiente(s).");
        return 0;
    }

    /// <summary>
    /// Obtiene el clima de los días con registros (spec <c>contexto-meteorologico</c>). Sin la sección <c>clima</c> en la
    /// configuración no contacta con ningún servicio. Un fallo del servicio no es un error: el día queda pendiente.
    /// </summary>
    private int Clima(Opciones o)
    {
        var config = ConfigOpcional(o);
        if (config?.Clima is not { } clima)
        {
            salida.WriteLine("El clima no está activado: falta la sección «clima» en la configuración. No se ha contactado con ningún servicio.");
            return 0;
        }

        var datos = o.Valor("datos") ?? (string.IsNullOrWhiteSpace(config.Datos) ? null : config.Datos) ?? DirectorioDatos(o);
        ExigirVolumenSiProcede(o, datos, config);
        using var bloqueo = BloqueoDeEjecucion.Adquirir(datos);
        if (bloqueo is null)
        {
            error.WriteLine("Mira está procesando ahora mismo: el clima se pedirá en la siguiente ejecución.");
            return 0;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var proveedor = proveedorDeClimaDePrueba?.Invoke(clima) ?? new Orumi.Agente.Clima.ProveedorOpenMeteo(http, clima);
        var geocodificador = geocodificadorDePrueba?.Invoke() ?? new Orumi.Agente.Clima.GeocodificadorOpenMeteo(http);
        var ahora = reloj();
        var resultado = Orumi.Agente.Clima.ObtencionDeClima
            .EjecutarAsync(datos, Almacen.Cargar(datos), clima, proveedor, DateOnly.FromDateTime(ahora.DateTime), ahora, geocodificador: geocodificador, cupoDePreguntas: config.MaximoPendientes)
            .GetAwaiter().GetResult();

        salida.WriteLine($"Clima: {resultado.Obtenidos} día(s) obtenido(s), {resultado.YaGuardados} ya guardado(s).");
        if (resultado.Ciudades is { } c)
        {
            c.Aprendidas.ToList().ForEach(n => salida.WriteLine($"Ciudad nueva ubicada: «{n}». Ya se usa su clima; Mira te pedirá que la confirmes."));
            if (c.PreguntasCreadas.Count > 0)
            {
                salida.WriteLine($"Preguntas de ciudad creadas: {c.PreguntasCreadas.Count} ({string.Join(", ", c.PreguntasCreadas)}).");
            }

            c.RespuestasAplicadas.ToList().ForEach(n => salida.WriteLine($"Respuesta de ciudad aplicada: {n}."));
            c.Avisos.ToList().ForEach(n => error.WriteLine($"Ciudad pendiente: {n}"));
        }

        foreach (var s in resultado.SinReconocer)
        {
            salida.WriteLine($"Ciudad sin ubicar en {s.Captura}: «{s.Ciudad}». No se consulta su clima hasta confirmarla.");
        }

        foreach (var f in resultado.Fallos)
        {
            error.WriteLine($"Clima pendiente ({f}). Se reintentará en la siguiente ejecución.");
        }

        return 0;
    }

    private int Estado(Opciones o)
    {
        var config = ConfigOpcional(o);
        var datos = o.Valor("datos") ?? (string.IsNullOrWhiteSpace(config?.Datos) ? null : config.Datos) ?? entorno(VariableDatos);
        if (string.IsNullOrWhiteSpace(datos) || !Directory.Exists(datos))
        {
            error.WriteLine($"Indica un directorio de datos existente con --datos, en la configuración o con la variable {VariableDatos}.");
            return 2;
        }

        ExigirVolumenSiProcede(o, datos, config);
        var e = ServicioDeEstado.Calcular(datos, config);
        salida.WriteLine($"Capturas pendientes de procesar: {e.Pendientes}");
        if (e.EnviosAbiertos > 0)
        {
            salida.WriteLine($"En {e.EnviosAbiertos} envío(s) abierto(s) al proveedor: {e.CapturasEnEnvio} captura(s); se recogerán en la siguiente ejecución.");
        }

        if (e.UltimoLote is { } l)
        {
            salida.WriteLine($"Último lote: {l.Id} ({l.EjecutadoEl:yyyy-MM-dd HH:mm}): {l.Procesadas} captura(s) procesada(s), {l.Problemas} problema(s)");
        }
        else
        {
            salida.WriteLine("Todavía no se ha ejecutado ningún lote.");
        }

        if (e.Presupuesto is { } p && e.GastoEstimado is { } g)
        {
            salida.WriteLine($"Gasto estimado desde {p.Desde:yyyy-MM-dd}: {Formato.Usd2(g)} de {Formato.Usd2(p.SaldoUsd)} US$ " +
                             $"({(p.SaldoUsd > 0 ? g / p.SaldoUsd * 100m : 0m):0}%; aviso al {p.AvisoPorcentaje}%). Es una estimación: compárala con la consola del proveedor.");
        }
        else
        {
            salida.WriteLine("Presupuesto: sin configurar (añade `presupuesto` y `precios` a la configuración).");
        }

        foreach (var a in e.Avisos)
        {
            salida.WriteLine($"⚠ {a}");
        }

        if (e.Avisos.Count == 0)
        {
            salida.WriteLine("Sin avisos.");
        }

        return 0;
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
                          [--fecha-acontecimiento AAAA-MM-DD] [--etiqueta TEXTO] [--ciudad TEXTO] [--dispositivo TEXTO]
          orumi validar   [--datos RUTA]
          orumi clima     [--datos RUTA]   (obtiene el clima de los días con registros; requiere la sección «clima» en la configuración)
          orumi reindexar [--datos RUTA] [--forzar si]   (regenera el índice en PostgreSQL; ORUMI_DATABASE_URL)
          orumi consultar entidad X | proyecto X | clase X | certeza X [campo] | fecha D H | relacion ID | pendientes | sin-procesar | conflictos
          orumi responder PREGUNTA VALOR [--registro ID] | PREGUNTA --no-recuerdo | PREGUNTA --no-relevante | PREGUNTA --no-sucedio
          orumi hipotesis listar | iniciales | añadir --formulacion "…" --factor clima:uv_alto --efecto CONCEPTO [--retardo 0-1]   (tus hipótesis, «por verificar»)
          orumi analizar  [--datos RUTA] [--simulado RESPUESTA.json]   (revisión semanal: contrasta tus hipótesis con tus registros y el clima)
          orumi hipotesis confirmar|matizar|descartar HIPOTESIS[#CONTRASTE] [--comentario "…"]
          orumi etiquetar [--datos RUTA] [--simulado RESPUESTA.json]   (el modelo señala qué conceptos dijiste explícitamente en cada registro)
          orumi etiquetas ver REGISTRO | corregir REGISTRO CONCEPTO presente|negado|quitar
          orumi aprendizajes listar | desactivar ID | activar ID | borrar ID   (lo que Mira ha aprendido de tus cierres de preguntas)
          orumi corregir  REGISTRO CAMPO VALOR     orumi rechazar REGISTRO CAMPO     orumi deshacer REGISTRO [CAMPO]
          orumi eliminar REGISTRO   orumi restaurar REGISTRO   orumi fusionar DESTINO ORIGEN   orumi separar REGISTRO CAPTURA [--titulo T]
          orumi consultar papelera
          orumi estado    [--config FICHERO] [--datos RUTA]   (pendientes, último lote, gasto estimado y avisos)
          orumi preparar-volumen [--datos RUTA]   (una vez, con el volumen cifrado ya montado)
          orumi procesar  [--config FICHERO] [--datos RUTA] [--ejecucion lote|peticion] [--reprocesar CAPTURA]
                          [--inmediato si] [--espera-maxima MINUTOS]   (por defecto, por lotes diferidos si la configuración lo indica)
                          [--simulado RESPUESTAS.json]   (prueba sin enviar nada al proveedor)

        El directorio de datos también puede indicarse con la variable ORUMI_DATOS.
        Con --exigir-volumen si (o `volumen: {exigir: true}` en la configuración, o ORUMI_EXIGIR_VOLUMEN=1)
        ninguna herramienta escribe ni procesa si el volumen cifrado no está disponible.
        """;
}

internal sealed class Opciones
{
    private static readonly HashSet<string> Conocidas =
        ["datos", "texto", "archivo", "fecha-acontecimiento", "etiqueta", "ciudad", "dispositivo", "config", "ejecucion", "reprocesar", "simulado", "exigir-volumen", "esquema", "forzar", "inmediato", "espera-maxima", "registro", "titulo", "formulacion", "factor", "efecto", "descripciones", "retardo", "comentario", "etiquetar"];

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
