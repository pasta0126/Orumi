using System.Globalization;
using System.Text;
using Orumi.Core.Ficheros;
using Orumi.Core.Validacion;

namespace Orumi.Agente;

public sealed record OpcionesProcesador(
    int MaximoPendientes = 5, int VentanaRelacionadosDias = 14, string VersionMira = "0.1.0-mvp", Precios? Precios = null, Presupuesto? Presupuesto = null);

public sealed record ProblemaDeLote(string Captura, string Motivo, string? Tipo = null);

public sealed class ResultadoEjecucion
{
    public List<string> Procesadas { get; } = [];

    public List<string> RegistrosCreados { get; } = [];

    public List<string> RegistrosActualizados { get; } = [];

    public List<string> PreguntasCreadas { get; } = [];

    public List<string> PreguntasCerradasPorLimite { get; } = [];

    public List<ProblemaDeLote> Problemas { get; } = [];

    /// <summary>Si la ejecución se detuvo por saldo agotado, límite de gasto o clave inválida.</summary>
    public CategoriaError? Fatal { get; set; }

    public List<string> Avisos { get; } = [];

    /// <summary>Envíos creados en esta ejecución (modo por lotes).</summary>
    public List<string> Envios { get; } = [];

    /// <summary>Envíos que siguen abiertos (el proveedor aún no ha terminado el lote).</summary>
    public int EnviosAbiertos { get; set; }

    public long TokensEntrada { get; set; }

    public long TokensSalida { get; set; }

    public decimal? CosteEstimadoUsd { get; set; }

    public string? LoteId { get; set; }

    /// <summary>La ejecución se detuvo antes de enviar nada (privacidad) y dejó las capturas pendientes.</summary>
    public bool Detenida { get; set; }

    public int Pendientes { get; set; }
}

/// <summary>
/// Procesa las capturas pendientes siguiendo el contrato de <c>docs/agente-mira.md</c>: extracción con el proveedor,
/// reclamo de claves, protegidos, aportaciones, conflictos, preguntas con máximo y valor, validación previa a escribir
/// y lote con bitácora.
/// </summary>
public sealed class Procesador(
    string datos,
    IProveedorIA proveedor,
    OpcionesProcesador opciones,
    Func<DateTimeOffset> reloj,
    Configuracion? configuracion = null,
    Func<string, string?>? entorno = null)
{
    private static readonly string[] CamposPermitidos =
        ["fecha", "hora", "duracion", "herramientas", "personas", "entidades", "lugares", "proyectos", "contexto", "acciones", "resultados", "problemas", "ideas"];

    /// <summary>Campos por los que Mira puede preguntar (solo datos estructurales, X36).</summary>
    private static readonly string[] CamposPreguntables =
        ["fecha", "hora", "duracion", "personas", "lugares", "proyectos", "entidades", "herramientas", "resultados", "coincidencia"];

    private static readonly string[] ClasesConResultadoPreguntable = ["actividad", "problema"];

    private const string Iso = "yyyy-MM-dd'T'HH:mm:sszzz";

    /// <param name="reprocesar">Capturas ya procesadas que se vuelven a procesar por petición expresa.</param>
    public async Task<ResultadoEjecucion> EjecutarAsync(
        string ejecucion, CancellationToken ct = default, Action<string>? hito = null, IReadOnlyCollection<string>? reprocesar = null)
    {
        var resultado = new ResultadoEjecucion();
        var almacen = Almacen.Cargar(datos);
        var pendientes = Pendientes(almacen, reprocesar);
        resultado.Pendientes = pendientes.Count;
        if (pendientes.Count == 0)
        {
            return resultado;
        }

        // Privacidad: antes de enviar nada (fallo cerrado).
        if (configuracion is not null)
        {
            var motivos = ComprobadorDePrivacidad.Comprobar(proveedor, configuracion, DateOnly.FromDateTime(reloj().DateTime), entorno ?? (_ => null));
            if (motivos.Count > 0)
            {
                resultado.Detenida = true;
                resultado.Problemas.AddRange(motivos.Select(m => new ProblemaDeLote("-", m)));
                EscribirLote(almacen, ejecucion, resultado);
                return resultado;
            }
        }

        foreach (var captura in pendientes)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var r = await ProcesarCapturaAsync(almacen, captura, ejecucion, ct);
                resultado.Procesadas.Add(captura.Id!);
                resultado.RegistrosCreados.AddRange(r.Creados);
                resultado.RegistrosActualizados.AddRange(r.Actualizados);
                resultado.PreguntasCreadas.AddRange(r.PreguntasCreadas);
                resultado.PreguntasCerradasPorLimite.AddRange(r.CerradasPorLimite);
                _entradas.Add(r.Entrada(captura.Id!, PreciosEfectivos()));
                almacen = Almacen.Cargar(datos);
            }
            catch (ProveedorException e)
            {
                // Saldo agotado, límite de gasto o clave inválida: seguir fallaría igual. Se corta, todo queda pendiente y se explica qué hacer.
                resultado.Problemas.Add(new ProblemaDeLote(captura.Id!, ClasificadorDeErrores.Explicacion(e.Categoria), ClasificadorDeErrores.Etiqueta(e.Categoria)));
                resultado.Fatal = e.Categoria;
                break;
            }
            catch (ProcesamientoFallidoException e)
            {
                // La captura queda pendiente; el resto de capturas sigue.
                resultado.Problemas.Add(new ProblemaDeLote(captura.Id!, e.Message));
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                resultado.Problemas.Add(new ProblemaDeLote(captura.Id!, $"error del proveedor o de escritura ({e.GetType().Name})"));
            }
        }

        hito?.Invoke("antes_del_lote");
        EscribirLote(almacen, ejecucion, resultado);
        return resultado;
    }

    private readonly List<Dictionary<string, object?>> _entradas = [];
    private long _tokensEntrada;
    private long _tokensSalida;
    private bool _hayUso;
    private bool _modoLotes;
    private readonly List<string> _enviosDelLote = [];

    /// <summary>Precios con el descuento del procesamiento diferido cuando se procesa por lotes.</summary>
    private Precios? PreciosEfectivos() => opciones.Precios is { } p && _modoLotes ? p.ConDescuentoDeLote() : opciones.Precios;

    private static List<Documento> Pendientes(Almacen almacen, IReadOnlyCollection<string>? reprocesar)
    {
        var procesadas = almacen.DeTipo("lote")
            .SelectMany(l => l.Frontmatter.TryGetValue("capturas", out var c) && c is List<object?> lista ? lista : [])
            .OfType<Dictionary<string, object?>>()
            .Select(c => c.GetValueOrDefault("captura") as string)
            .ToHashSet();
        return almacen.DeTipo("captura")
            .Where(c => c.Id is not null && (!procesadas.Contains(c.Id) || reprocesar?.Contains(c.Id) == true))
            .OrderBy(c => DateTimeOffset.Parse((string)c.Frontmatter["comunicada_el"]!, CultureInfo.InvariantCulture))
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToList();
    }

    // ------------------------------------------------------------------ una captura

    private sealed class ResultadoCaptura
    {
        public List<string> Creados { get; } = [];

        public List<string> Actualizados { get; } = [];

        public List<string> PreguntasCreadas { get; } = [];

        public List<string> CerradasPorLimite { get; } = [];

        public int PreguntasDescartadas { get; set; }

        public string? Nota { get; set; }

        public long? TokensEntrada { get; set; }

        public long? TokensSalida { get; set; }

        public Dictionary<string, object?> Entrada(string capturaId, Precios? precios)
        {
            var e = new Dictionary<string, object?>
            {
                ["captura"] = capturaId,
                ["registros_creados"] = Creados.Cast<object?>().ToList(),
                ["registros_actualizados"] = Actualizados.Cast<object?>().ToList(),
                ["preguntas_creadas"] = PreguntasCreadas.Cast<object?>().ToList(),
            };
            if (CerradasPorLimite.Count > 0)
            {
                e["preguntas_cerradas_por_limite"] = CerradasPorLimite.Cast<object?>().ToList();
            }

            if (TokensEntrada is { } te && TokensSalida is { } ts)
            {
                e["tokens_entrada"] = te.ToString(CultureInfo.InvariantCulture);
                e["tokens_salida"] = ts.ToString(CultureInfo.InvariantCulture);
                e["coste_estimado_usd"] = precios is null ? "desconocido" : Formato.Usd(precios.Coste(te, ts));
            }

            if (PreguntasDescartadas > 0)
            {
                e["preguntas_descartadas"] = PreguntasDescartadas.ToString(CultureInfo.InvariantCulture);
            }

            if (Nota is not null)
            {
                e["nota"] = Nota;
            }

            return e;
        }
    }

    private (SolicitudExtraccion Solicitud, List<Documento> Existentes, IReadOnlyList<RegistroRelacionado> Relacionados) Preparar(Almacen almacen, Documento captura)
    {
        var capId = captura.Id!;
        var texto = captura.Cuerpo;
        var comunicada = DateTimeOffset.Parse((string)captura.Frontmatter["comunicada_el"]!, CultureInfo.InvariantCulture);

        var existentes = almacen.DeTipo("registro")
            .Where(r => ((string?)r.Frontmatter.GetValueOrDefault("clave_extraccion"))?.StartsWith(capId + "#f", StringComparison.Ordinal) == true)
            .ToList();
        var relacionados = SelectorDeRelacionados.Seleccionar(almacen, texto, comunicada, opciones.VentanaRelacionadosDias, capId);
        var solicitud = new SolicitudExtraccion(
            capId,
            texto,
            comunicada,
            captura.Frontmatter.GetValueOrDefault("fecha_acontecimiento") as string,
            captura.Frontmatter.GetValueOrDefault("etiqueta") as string,
            existentes.Select(r => (string)r.Frontmatter["clave_extraccion"]!).ToList(),
            relacionados);
        return (solicitud, existentes, relacionados);
    }

    private void ContarUso(UsoDeTokens? uso, ResultadoCaptura res)
    {
        if (uso is null)
        {
            return;
        }

        // El consumo cuenta aunque la captura luego no se pueda guardar: la llamada ya se hizo y se cobra.
        _tokensEntrada += uso.Entrada;
        _tokensSalida += uso.Salida;
        _hayUso = true;
        res.TokensEntrada = uso.Entrada;
        res.TokensSalida = uso.Salida;
    }

    private async Task<ResultadoCaptura> ProcesarCapturaAsync(Almacen almacen, Documento captura, string ejecucion, CancellationToken ct)
    {
        var res = new ResultadoCaptura();
        var (solicitud, existentes, relacionados) = Preparar(almacen, captura);
        RespuestaExtraccion respuesta;
        try
        {
            respuesta = await proveedor.ExtraerAsync(solicitud, ct);
        }
        catch (ProcesamientoFallidoException e) when (e.Uso is not null)
        {
            ContarUso(e.Uso, res); // una salida inservible también se cobra
            throw;
        }

        ContarUso(respuesta.Uso, res);
        return AplicarRespuesta(almacen, captura, ejecucion, respuesta, existentes, relacionados.Select(r => r.Id).ToHashSet(), res);
    }

    /// <summary>Convierte la respuesta del proveedor (inmediata o recogida de un lote) en registros, preguntas y ficheros.</summary>
    private ResultadoCaptura AplicarRespuesta(
        Almacen almacen, Documento captura, string ejecucion, RespuestaExtraccion respuesta,
        List<Documento> existentes, HashSet<string> ofrecidos, ResultadoCaptura res)
    {
        var capId = captura.Id!;
        var comunicada = DateTimeOffset.Parse((string)captura.Frontmatter["comunicada_el"]!, CultureInfo.InvariantCulture);
        var ahora = reloj();

        if (respuesta.Registros.Count == 0)
        {
            res.Nota = "sin hechos registrables";
            return res;
        }

        var ctx = new Contexto(almacen, captura, comunicada, ahora, ejecucion, existentes, ofrecidos);
        var cambios = new Dictionary<string, (Documento Doc, string Texto, bool Nuevo)>();
        var ids = new List<string>(); // id por índice de respuesta
        var acciones = new List<Accion>();

        // Pasada 1: qué hace cada elemento (reclamar, ampliar o crear) e identificadores.
        foreach (var r in respuesta.Registros)
        {
            acciones.Add(Decidir(ctx, r, acciones));
        }

        ids.AddRange(acciones.Select(a => a.Id));

        // Pasada 2: construir o actualizar ficheros.
        var preguntasAuto = new List<(string RegistroId, string[] Otros, string Campo, string Texto)>();
        for (var i = 0; i < respuesta.Registros.Count; i++)
        {
            var r = respuesta.Registros[i];
            var a = acciones[i];
            var rutaDoc = Identificadores.RutaRelativa(a.Id)!;
            var fm = a.Existente is null ? NuevoFrontmatter(ctx, r, a, ids) : Actualizar(ctx, r, a, ids);
            var cuerpo = a.Existente is null ? NuevoCuerpo(ctx, r, fm) : CuerpoActualizado(a.Existente.Cuerpo, r, capId, fm);

            if (r.EnConflictoCon is { } otroId && r.ConflictoCampo is { } campoC)
            {
                ValidarOfrecido(ctx, otroId, "EnConflictoCon");
                var pid = NuevoId("preg", 4, ctx, a.Id);
                AnadirConflicto(fm, campoC, otroId, pid);
                cuerpo = AnadirEnlace(cuerpo, "En conflicto con", otroId);
                var otro = ctx.Almacen.Documentos.First(d => d.Id == otroId);
                var otroFm = Clonar(otro.Frontmatter);
                AnadirConflicto(otroFm, campoC, a.Id, pid);
                AnadirHistorial(otroFm, ctx, "conflictos", "desconocido", $"conflicto en {campoC} con {a.Id}", capId);
                Sellar(otroFm, ctx);
                cambios[otro.Ruta] = Preparar(otro.Ruta, "registro", otroFm, AnadirEnlace(otro.Cuerpo, "En conflicto con", a.Id), nuevo: false);
                res.Actualizados.Add(otroId);
                ctx.PreguntaPrevista[pid] = true;
                preguntasAuto.Add((a.Id, [otroId], campoC, $"Hay dos versiones de {campoC} para «{r.Titulo}» y «{((string?)otro.Frontmatter.GetValueOrDefault("titulo"))}». ¿Son lo mismo? Si lo son, ¿cuál es la correcta?"));
                ctx.IdsDePregunta[(a.Id, campoC)] = pid;
            }

            if (r.Duda is { } duda)
            {
                ValidarOfrecido(ctx, duda, "Duda");
                var titulo = ctx.Almacen.Documentos.First(d => d.Id == duda).Frontmatter.GetValueOrDefault("titulo") as string;
                preguntasAuto.Add((a.Id, [duda], "coincidencia", $"¿«{r.Titulo}» es la misma que «{titulo}»? Si lo es, las uno; si no, quedan separadas."));
            }

            cambios[rutaDoc] = Preparar(rutaDoc, "registro", fm, cuerpo, nuevo: a.Existente is null);
            // Un registro que ningún lote ha dado por creado (huérfano de una ejecución interrumpida) se adopta como creado.
            var huerfano = a.Existente is not null && !ctx.CreadosEnLotes.Contains(a.Id);
            (a.Existente is null || huerfano ? res.Creados : res.Actualizados).Add(a.Id);
        }

        // Preguntas: candidatas del proveedor + automáticas, filtradas, ordenadas y limitadas.
        PlanificarPreguntas(ctx, respuesta, ids, preguntasAuto, cambios, res);

        // Validar antes de escribir: solo lo que se va a tocar.
        var propuesto = ctx.Almacen.Con(cambios.Values.Select(c => c.Doc));
        var tocados = cambios.Keys.ToHashSet();
        var errores = Validador.Validar(propuesto)
            .Where(i => tocados.Contains(i.Fichero))
            .Where(i => i.Mensaje is not ("registro no creado por ningún lote" or "captura sin lote"))
            .ToList();
        if (errores.Count > 0)
        {
            throw new ProcesamientoFallidoException("la salida no cumple el formato: " + string.Join("; ", errores.Take(5).Select(e => $"{Path.GetFileNameWithoutExtension(e.Fichero)}: {e.Mensaje}")));
        }

        foreach (var (ruta, (doc, contenido, nuevo)) in cambios.OrderBy(c => !c.Value.Nuevo))
        {
            var completa = Path.Combine(datos, ruta);
            if (nuevo)
            {
                EscrituraAtomica.EscribirNuevo(completa, contenido);
            }
            else
            {
                EscrituraAtomica.Reemplazar(completa, contenido);
            }
        }

        return res;
    }

    // ------------------------------------------------------------------ decisiones

    private sealed record Accion(string Id, Documento? Existente, string Clave, int Desde, int Hasta, string Cita);

    private sealed class Contexto(
        Almacen almacen, Documento captura, DateTimeOffset comunicada, DateTimeOffset ahora, string ejecucion,
        List<Documento> existentesDeLaCaptura, HashSet<string> ofrecidos)
    {
        public Almacen Almacen { get; } = almacen;

        public Documento Captura { get; } = captura;

        public DateTimeOffset Comunicada { get; } = comunicada;

        public DateTimeOffset Ahora { get; } = ahora;

        public string Ejecucion { get; } = ejecucion;

        public List<Documento> ExistentesDeLaCaptura { get; } = existentesDeLaCaptura;

        public HashSet<string> Ofrecidos { get; } = ofrecidos;

        public HashSet<string> CreadosEnLotes { get; } = almacen.DeTipo("lote")
            .SelectMany(l => l.Frontmatter.GetValueOrDefault("capturas") is List<object?> cs ? cs : [])
            .OfType<Dictionary<string, object?>>()
            .SelectMany(c => c.GetValueOrDefault("registros_creados") is List<object?> rc ? rc.OfType<string>() : [])
            .ToHashSet();

        public HashSet<string> IdsUsados { get; } = [.. almacen.Documentos.Select(d => d.Id!).Where(i => i is not null)];

        public Dictionary<string, bool> PreguntaPrevista { get; } = [];

        public Dictionary<(string, string), string> IdsDePregunta { get; } = [];
    }

    private static void ValidarOfrecido(Contexto ctx, string id, string campo)
    {
        if (!ctx.Ofrecidos.Contains(id))
        {
            throw new ProcesamientoFallidoException($"{campo} se refiere a un registro que no se ofreció al proveedor ({id})");
        }
    }

    private Accion Decidir(Contexto ctx, RegistroExtraido r, List<Accion> anteriores)
    {
        var (desde, hasta, cita) = Localizar(ctx.Captura.Cuerpo, r.Fragmento);
        if (!Validador.EsClaseValida(r.Clase))
        {
            throw new ProcesamientoFallidoException($"clase inválida «{r.Clase}»");
        }

        foreach (var k in r.Campos.Keys.Where(k => !CamposPermitidos.Contains(k)))
        {
            throw new ProcesamientoFallidoException($"campo no permitido «{k}»");
        }

        // Ampliación clara: el registro existente recibe la captura como nueva fuente.
        if (r.Amplia is { } amplia)
        {
            ValidarOfrecido(ctx, amplia, "Amplia");
            var doc = ctx.Almacen.Documentos.First(d => d.Id == amplia);
            return new Accion(amplia, doc, (string)doc.Frontmatter["clave_extraccion"]!, desde, hasta, cita);
        }

        // Reprocesado: reclama una clave existente de esta captura (o la reconoce por su fragmento y clase).
        Documento? existente = null;
        if (r.Reclama is { } clave)
        {
            existente = ctx.ExistentesDeLaCaptura.FirstOrDefault(e => (string)e.Frontmatter["clave_extraccion"]! == clave)
                        ?? throw new ProcesamientoFallidoException($"reclama una clave que no existe ({clave})");
        }
        else if (r.Duda is null && r.EnConflictoCon is null)
        {
            var candidatos = ctx.ExistentesDeLaCaptura.Where(e => EsElMismo(e, r.Clase, desde, hasta)).ToList();
            if (candidatos.Count == 1 && anteriores.All(a => a.Id != candidatos[0].Id))
            {
                existente = candidatos[0];
            }
        }

        if (existente is not null)
        {
            return new Accion(existente.Id!, existente, (string)existente.Frontmatter["clave_extraccion"]!, desde, hasta, cita);
        }

        // Nuevo registro: clave capturaId#f<n>:<clase>:<k>, asignada una sola vez.
        var id = NuevoId("reg", 4, ctx, null);
        ctx.IdsUsados.Add(id);
        var capId = ctx.Captura.Id!;
        var fragmentos = new Dictionary<(int, int), int>();
        var porClase = new Dictionary<(int, string), int>();
        foreach (var (e, d, h, n, c) in ctx.ExistentesDeLaCaptura.Select(Desglosar).Concat(anteriores.Where(a => a.Existente is null).Select(DesglosarNuevo)))
        {
            fragmentos.TryAdd((d, h), n);
            porClase[(n, c)] = porClase.GetValueOrDefault((n, c)) + 1;
        }

        var nFrag = fragmentos.TryGetValue((desde, hasta), out var nExistente) ? nExistente : (fragmentos.Count == 0 ? 1 : fragmentos.Values.Max() + 1);
        var k2 = porClase.GetValueOrDefault((nFrag, r.Clase)) + 1;
        return new Accion(id, null, $"{capId}#f{nFrag}:{r.Clase}:{k2}", desde, hasta, cita);

        (string, int, int, int, string) Desglosar(Documento e)
        {
            var clave = (string)e.Frontmatter["clave_extraccion"]!;
            var partes = clave[(clave.IndexOf("#f", StringComparison.Ordinal) + 2)..].Split(':');
            var fuente = ((List<object?>)e.Frontmatter["fuentes"]!)[0] as Dictionary<string, object?>;
            var fr = fuente?.GetValueOrDefault("fragmento") as Dictionary<string, object?>;
            return (e.Id!, int.Parse((string?)fr?.GetValueOrDefault("desde") ?? "-1"), int.Parse((string?)fr?.GetValueOrDefault("hasta") ?? "-1"), int.Parse(partes[0]), partes[1]);
        }

        (string, int, int, int, string) DesglosarNuevo(Accion a)
        {
            var partes = a.Clave[(a.Clave.IndexOf("#f", StringComparison.Ordinal) + 2)..].Split(':');
            return (a.Id, a.Desde, a.Hasta, int.Parse(partes[0]), partes[1]);
        }
    }

    private static bool EsElMismo(Documento e, string clase, int desde, int hasta)
    {
        if ((string?)e.Frontmatter.GetValueOrDefault("clase") != clase)
        {
            return false;
        }

        var fuente = (e.Frontmatter["fuentes"] as List<object?>)?.FirstOrDefault() as Dictionary<string, object?>;
        return fuente?.GetValueOrDefault("fragmento") is Dictionary<string, object?> fr
               && (string?)fr.GetValueOrDefault("desde") == desde.ToString(CultureInfo.InvariantCulture)
               && (string?)fr.GetValueOrDefault("hasta") == hasta.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Posición del fragmento en el cuerpo, en caracteres Unicode (puntos de código), y su cita.</summary>
    private static (int Desde, int Hasta, string Cita) Localizar(string cuerpo, string fragmento)
    {
        if (string.IsNullOrEmpty(fragmento))
        {
            throw new ProcesamientoFallidoException("un registro llegó sin fragmento de origen");
        }

        var indice = cuerpo.IndexOf(fragmento, StringComparison.Ordinal);
        if (indice < 0)
        {
            throw new ProcesamientoFallidoException("el fragmento citado no está en la captura");
        }

        var desde = cuerpo[..indice].EnumerateRunes().Count();
        var longitud = fragmento.EnumerateRunes().Count();
        var cita = string.Concat(fragmento.EnumerateRunes().Take(80).Select(r => r.ToString()));
        return (desde, desde + longitud, cita);
    }

    // ------------------------------------------------------------------ construcción de registros

    private static readonly string[] CamposConCertezaObligatoria =
        ["fecha", "hora", "duracion", "contexto", "personas", "lugares", "proyectos", "herramientas", "acciones", "resultados", "problemas", "ideas"];

    /// <summary>Si el modelo da un valor sin indicar su certeza, se anota la más débil («interpretado»): nunca se supone «dicho».</summary>
    private static void CompletarCertezaOmitida(Dictionary<string, object?> fm, Dictionary<string, object?> certeza)
    {
        foreach (var campo in CamposConCertezaObligatoria)
        {
            if (fm.TryGetValue(campo, out var v) && v is not null && v as string != "desconocido" && !certeza.ContainsKey(campo))
            {
                certeza[campo] = "interpretado";
            }
        }
    }

    private Dictionary<string, object?> NuevoFrontmatter(Contexto ctx, RegistroExtraido r, Accion a, List<string> ids)
    {
        var fm = new Dictionary<string, object?>
        {
            ["id"] = a.Id,
            ["tipo"] = "registro",
            ["clase"] = r.Clase,
            ["titulo"] = r.Titulo,
            ["comunicada_el"] = (string)ctx.Captura.Frontmatter["comunicada_el"]!,
            ["fuentes"] = new List<object?> { Fuente(ctx.Captura.Id!, a, "origen") },
        };
        var certeza = new Dictionary<string, object?>();
        fm["fecha"] = r.Campos.TryGetValue("fecha", out var f) && f is not null ? f : "desconocido";
        foreach (var (campo, valor) in r.Campos.Where(c => c.Key != "fecha"))
        {
            fm[campo] = valor;
        }

        foreach (var (campo, c) in r.Certeza)
        {
            // Un campo desconocido o ausente no lleva certeza (los modelos la ponen a veces): se descarta sin inventar nada.
            if (campo.Contains('.') || (fm.TryGetValue(campo, out var valorCampo) && valorCampo as string != "desconocido"))
            {
                certeza[campo] = c;
            }
        }

        CompletarCertezaOmitida(fm, certeza);

        if (r.Multidia is { } m)
        {
            fm["multidia"] = MultidiaComoMapa(m);
            fm["entradas"] = new List<object?> { Entrada(ctx, r, fm) };
            certeza["multidia.estado"] = m.CertezaEstado;
            if (m.FinDesde is not null)
            {
                certeza["multidia.fin"] = m.CertezaFin;
            }
        }

        var relaciones = Relaciones(r, ids, ctx);
        if (relaciones.Count > 0)
        {
            fm["relaciones"] = relaciones;
        }

        fm["conflictos"] = new List<object?>();
        fm["rechazos"] = new List<object?>();
        fm["historial"] = new List<object?>();
        fm["certeza"] = certeza.ToDictionary(p => p.Key, p => (object?)p.Value);
        fm["procedencia"] = new Dictionary<string, object?>
        {
            ["generado_por"] = Agente(),
            ["generado_el"] = ctx.Ahora.ToString(Iso, CultureInfo.InvariantCulture),
            ["ejecucion"] = ctx.Ejecucion,
        };
        fm["clave_extraccion"] = a.Clave;
        return fm;
    }

    private Dictionary<string, object?> Agente() => new()
    {
        ["agente"] = "Mira",
        ["version"] = opciones.VersionMira,
        ["modelo"] = proveedor.Modelo,
    };

    private static Dictionary<string, object?> Fuente(string capturaId, Accion a, string rol) => new()
    {
        ["captura"] = capturaId,
        ["fragmento"] = new Dictionary<string, object?>
        {
            ["desde"] = a.Desde.ToString(CultureInfo.InvariantCulture),
            ["hasta"] = a.Hasta.ToString(CultureInfo.InvariantCulture),
            ["cita"] = a.Cita,
        },
        ["rol"] = rol,
    };

    private static Dictionary<string, object?> MultidiaComoMapa(MultidiaExtraido m) => new()
    {
        ["estado"] = m.Estado,
        ["fin"] = m.FinDesde is null
            ? "desconocido"
            : new Dictionary<string, object?> { ["desde"] = m.FinDesde, ["hasta"] = m.FinHasta ?? m.FinDesde },
    };

    private static Dictionary<string, object?> Entrada(Contexto ctx, RegistroExtraido r, Dictionary<string, object?> fm)
    {
        var fecha = fm["fecha"] is Dictionary<string, object?> f && f.GetValueOrDefault("desde") is string d
            ? d
            : ctx.Comunicada.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new Dictionary<string, object?> { ["fecha"] = fecha, ["captura"] = ctx.Captura.Id, ["resumen"] = r.ResumenEntrada ?? r.Titulo };
    }

    private static List<object?> Relaciones(RegistroExtraido r, List<string> ids, Contexto ctx)
    {
        var lista = new List<object?>();
        foreach (var rel in r.Relaciones)
        {
            string? destino = rel.DestinoId;
            if (destino is not null)
            {
                ValidarOfrecido(ctx, destino, "Relación");
            }
            else if (rel.DestinoIndice is { } idx && idx >= 0 && idx < ids.Count)
            {
                destino = ids[idx];
            }

            if (destino is null)
            {
                throw new ProcesamientoFallidoException("una relación no indica su destino");
            }

            lista.Add(new Dictionary<string, object?> { ["tipo"] = rel.Tipo, ["destino"] = destino, ["certeza"] = rel.Certeza });
        }

        return lista;
    }

    private static string NuevoCuerpo(Contexto ctx, RegistroExtraido r, Dictionary<string, object?> fm)
    {
        var sb = new StringBuilder(r.Descripcion.Trim()).Append("\n\n## Origen\n- [[").Append(ctx.Captura.Id).Append("]]\n");
        if (fm.TryGetValue("relaciones", out var rel) && rel is List<object?> lista && lista.Count > 0)
        {
            sb.Append("\n## Relacionados\n");
            foreach (var d in lista.OfType<Dictionary<string, object?>>().Select(x => (string)x["destino"]!).Distinct())
            {
                sb.Append("- [[").Append(d).Append("]]\n");
            }
        }

        return sb.ToString();
    }

    // ------------------------------------------------------------------ actualización (reprocesado y aportaciones)

    private static bool EsProtegido(Dictionary<string, object?> fm, string campo)
    {
        if (fm.TryGetValue("certeza", out var c) && c is Dictionary<string, object?> cz && cz.GetValueOrDefault(campo) as string == "confirmado")
        {
            return true;
        }

        // Cuenta la última entrada del usuario sobre el campo: si es una reversión (deshacer), el campo vuelve a ser de Mira.
        var ultima = fm.TryGetValue("historial", out var h) && h is List<object?> lista
            ? lista.OfType<Dictionary<string, object?>>().LastOrDefault(x => x.GetValueOrDefault("origen") as string == "usuario" && x.GetValueOrDefault("campo") as string == campo)
            : null;
        return ultima is not null && ultima.GetValueOrDefault("deshace") is null;
    }

    private Dictionary<string, object?> Actualizar(Contexto ctx, RegistroExtraido r, Accion a, List<string> ids)
    {
        var fm = Clonar(a.Existente!.Frontmatter);
        var capId = ctx.Captura.Id!;
        var certeza = (Dictionary<string, object?>)fm["certeza"]!;
        var esAmpliacion = r.Amplia is not null;

        if (esAmpliacion)
        {
            ((List<object?>)fm["fuentes"]!).Add(Fuente(capId, a, "ampliacion"));
            AnadirHistorial(fm, ctx, "fuentes", "desconocido", $"añade {capId} como ampliación", capId);
        }

        // titulo
        if (!EsProtegido(fm, "titulo") && (string?)fm["titulo"] != r.Titulo && !esAmpliacion)
        {
            AnadirHistorial(fm, ctx, "titulo", (string)fm["titulo"]!, r.Titulo, capId);
            fm["titulo"] = r.Titulo;
        }

        var camposNuevos = esAmpliacion ? r.Campos.Where(c => c.Key is not "fecha").ToList() : r.Campos.ToList();
        foreach (var (campo, nuevo) in camposNuevos)
        {
            if (EsProtegido(fm, campo))
            {
                continue;
            }

            var existe = fm.TryGetValue(campo, out var actual);
            var actualEsDesconocido = !existe || actual as string == "desconocido";
            if (nuevo as string == "desconocido" || nuevo is null)
            {
                continue; // nunca se pierde información conocida por reprocesar
            }

            if (existe && !actualEsDesconocido && Igual(actual, nuevo) && certeza.GetValueOrDefault(campo) as string == r.Certeza.GetValueOrDefault(campo))
            {
                continue;
            }

            AnadirHistorial(fm, ctx, campo, existe ? actual : "desconocido", nuevo, capId);
            fm[campo] = nuevo;
            if (r.Certeza.TryGetValue(campo, out var c))
            {
                certeza[campo] = c;
            }
            else if (CamposConCertezaObligatoria.Contains(campo))
            {
                certeza[campo] = "interpretado"; // el modelo la omitió: se usa la más débil
            }
        }

        if (r.Multidia is { } m && !EsProtegido(fm, "multidia"))
        {
            var anterior = fm.GetValueOrDefault("multidia");
            var nuevoMd = MultidiaComoMapa(m);
            if (!Igual(anterior, nuevoMd))
            {
                AnadirHistorial(fm, ctx, "multidia", anterior ?? "desconocido", nuevoMd, capId);
                fm["multidia"] = nuevoMd;
                certeza["multidia.estado"] = m.CertezaEstado;
                if (m.FinDesde is not null)
                {
                    certeza["multidia.fin"] = m.CertezaFin;
                }
                else
                {
                    certeza.Remove("multidia.fin");
                }
            }

            if (esAmpliacion)
            {
                if (!fm.TryGetValue("entradas", out var en) || en is not List<object?>)
                {
                    fm["entradas"] = new List<object?>();
                }

                ((List<object?>)fm["entradas"]!).Add(Entrada(ctx, r, new Dictionary<string, object?>
                {
                    ["fecha"] = new Dictionary<string, object?> { ["desde"] = ctx.Comunicada.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                }));
            }
        }

        foreach (var rel in Relaciones(r, ids, ctx).OfType<Dictionary<string, object?>>())
        {
            var lista = fm.TryGetValue("relaciones", out var rr) && rr is List<object?> l ? l : [];
            if (!lista.OfType<Dictionary<string, object?>>().Any(x => (string?)x["destino"] == (string?)rel["destino"] && (string?)x["tipo"] == (string?)rel["tipo"]))
            {
                lista.Add(rel);
                fm["relaciones"] = lista;
            }
        }

        Sellar(fm, ctx);
        return fm;
    }

    private static string CuerpoActualizado(string cuerpo, RegistroExtraido r, string capId, Dictionary<string, object?> fm)
    {
        if (r.Amplia is not null && !cuerpo.Contains($"[[{capId}]]", StringComparison.Ordinal))
        {
            cuerpo = cuerpo.Replace("\n## Origen\n", $"\n## Origen\n- [[{capId}]]\n", StringComparison.Ordinal);
        }

        if (fm.TryGetValue("relaciones", out var rel) && rel is List<object?> lista)
        {
            foreach (var d in lista.OfType<Dictionary<string, object?>>().Select(x => (string)x["destino"]!))
            {
                if (!cuerpo.Contains($"[[{d}]]", StringComparison.Ordinal))
                {
                    cuerpo = AnadirEnlace(cuerpo, "Relacionados", d);
                }
            }
        }

        return cuerpo;
    }

    private void Sellar(Dictionary<string, object?> fm, Contexto ctx)
    {
        var proc = (Dictionary<string, object?>)fm["procedencia"]!;
        proc["actualizado_por"] = Agente();
        proc["actualizado_el"] = ctx.Ahora.ToString(Iso, CultureInfo.InvariantCulture);
    }

    private static void AnadirHistorial(Dictionary<string, object?> fm, Contexto ctx, string campo, object? anterior, object? nuevo, string captura)
    {
        if (!fm.TryGetValue("historial", out var h) || h is not List<object?>)
        {
            fm["historial"] = new List<object?>();
        }

        ((List<object?>)fm["historial"]!).Add(new Dictionary<string, object?>
        {
            ["fecha"] = ctx.Ahora.ToString(Iso, CultureInfo.InvariantCulture),
            ["origen"] = "mira",
            ["campo"] = campo,
            ["anterior"] = anterior,
            ["nuevo"] = nuevo,
            ["captura"] = captura,
        });
    }

    private static void AnadirConflicto(Dictionary<string, object?> fm, string campo, string con, string pregunta)
    {
        if (!fm.TryGetValue("conflictos", out var c) || c is not List<object?>)
        {
            fm["conflictos"] = new List<object?>();
        }

        ((List<object?>)fm["conflictos"]!).Add(new Dictionary<string, object?>
        {
            ["campo"] = campo,
            ["con"] = con,
            ["pregunta"] = pregunta,
            ["estado"] = "abierto",
        });
    }

    private static string AnadirEnlace(string cuerpo, string seccion, string id)
    {
        var enlace = $"- [[{id}]]\n";
        var cabecera = $"\n## {seccion}\n";
        if (cuerpo.Contains(cabecera, StringComparison.Ordinal))
        {
            var i = cuerpo.IndexOf(cabecera, StringComparison.Ordinal) + cabecera.Length;
            var fin = cuerpo.IndexOf("\n## ", i, StringComparison.Ordinal);
            fin = fin < 0 ? cuerpo.Length : fin + 1;
            return cuerpo.Insert(fin, enlace);
        }

        return (cuerpo.EndsWith('\n') ? cuerpo : cuerpo + "\n") + $"\n## {seccion}\n" + enlace;
    }

    // ------------------------------------------------------------------ preguntas

    private sealed record Candidata(string RegistroId, string[] Otros, string Campo, string Texto, string Valor, bool Automatica);

    private static int Orden(string valor) => valor switch { "alta" => 0, "media" => 1, _ => 2 };

    private void PlanificarPreguntas(
        Contexto ctx, RespuestaExtraccion respuesta, List<string> ids,
        List<(string RegistroId, string[] Otros, string Campo, string Texto)> auto,
        Dictionary<string, (Documento Doc, string Texto, bool Nuevo)> cambios, ResultadoCaptura res)
    {
        var fmDe = (string id) => cambios.Values.FirstOrDefault(c => c.Doc.Id == id).Doc?.Frontmatter
                                  ?? ctx.Almacen.Documentos.FirstOrDefault(d => d.Id == id)?.Frontmatter;
        var candidatas = new List<Candidata>();

        foreach (var p in respuesta.Preguntas)
        {
            var registroId = p.RegistroId ?? (p.RegistroIndice is { } i && i >= 0 && i < ids.Count ? ids[i] : null);
            if (registroId is null || fmDe(registroId) is not { } fm || !PuedePreguntarse(ctx, registroId, fm, p.Campo, res))
            {
                res.PreguntasDescartadas++;
                continue;
            }

            candidatas.Add(new Candidata(registroId, [], p.Campo, p.Texto, p.Valor, false));
        }

        foreach (var (registroId, otros, campo, texto) in auto)
        {
            var id = ctx.IdsDePregunta.TryGetValue((registroId, campo), out var pid) ? pid : null;
            candidatas.Add(new Candidata(registroId, otros, campo, texto, "alta", true));
            if (id is not null)
            {
                _idsAutomaticos[(registroId, campo)] = id;
            }
        }

        // Evita duplicar una pregunta pendiente ya existente sobre el mismo registro y campo.
        var existentes = ctx.Almacen.DeTipo("pregunta").ToList();
        candidatas = candidatas
            .Where(c => c.Automatica || !existentes.Any(e => (string?)e.Frontmatter.GetValueOrDefault("registro") == c.RegistroId
                                                              && (string?)e.Frontmatter.GetValueOrDefault("campo") == c.Campo
                                                              && (string?)e.Frontmatter.GetValueOrDefault("estado") is "pendiente"))
            .GroupBy(c => (c.RegistroId, c.Campo)).Select(g => g.First())
            .ToList();

        var pendientes = existentes.Where(e => (string?)e.Frontmatter.GetValueOrDefault("estado") == "pendiente")
            .OrderBy(e => Orden((string)e.Frontmatter["valor"]!)).ThenBy(e => (string)e.Frontmatter["creada_el"]!, StringComparer.Ordinal)
            .ToList();

        // Cupo: las N de mayor valor entre las pendientes y las nuevas; ante igualdad, las existentes.
        var todas = pendientes.Select(p => (Existente: p, Nueva: (Candidata?)null, Valor: (string)p.Frontmatter["valor"]!))
            .Concat(candidatas.Select(c => (Existente: (Documento?)null, Nueva: (Candidata?)c, c.Valor)))
            .Select((x, indice) => (x, indice))
            .OrderBy(t => Orden(t.x.Valor)).ThenBy(t => t.indice)
            .Select(t => t.x)
            .ToList();
        var conservadas = todas.Take(opciones.MaximoPendientes).ToList();

        foreach (var x in todas.Skip(opciones.MaximoPendientes))
        {
            if (x.Existente is { } desplazada)
            {
                var fm = Clonar(desplazada.Frontmatter);
                fm["estado"] = "cerrada_por_limite";
                fm["cerrada_el"] = ctx.Ahora.ToString(Iso, CultureInfo.InvariantCulture);
                cambios[desplazada.Ruta] = Preparar(desplazada.Ruta, "pregunta", fm, desplazada.Cuerpo, nuevo: false);
                res.CerradasPorLimite.Add(desplazada.Id!);
            }
            else
            {
                res.PreguntasDescartadas++;
            }
        }

        foreach (var c in conservadas.Where(x => x.Nueva is not null).Select(x => x.Nueva!))
        {
            var id = _idsAutomaticos.TryGetValue((c.RegistroId, c.Campo), out var previo) ? previo : NuevoId("preg", 4, ctx, null);
            ctx.IdsUsados.Add(id);
            var fm = new Dictionary<string, object?>
            {
                ["id"] = id,
                ["tipo"] = "pregunta",
                ["registro"] = c.RegistroId,
                ["otros_registros"] = c.Otros.Cast<object?>().ToList(),
                ["campo"] = c.Campo,
                ["texto"] = c.Texto,
                ["estado"] = "pendiente",
                ["valor"] = c.Valor,
                ["creada_el"] = ctx.Ahora.ToString(Iso, CultureInfo.InvariantCulture),
                ["generada_por"] = Agente(),
            };
            var ruta = Identificadores.RutaRelativa(id)!;
            cambios[ruta] = Preparar(ruta, "pregunta", fm, $"Pregunta de Mira sobre [[{c.RegistroId}]].\n", nuevo: true);
            res.PreguntasCreadas.Add(id);
        }

        // Una pregunta automática que no llegó a crearse (cupo) deja un conflicto sin pregunta: se garantiza su existencia.
        foreach (var (clave, pid) in _idsAutomaticos)
        {
            if (!res.PreguntasCreadas.Contains(pid) && !ctx.Almacen.Documentos.Any(d => d.Id == pid))
            {
                throw new ProcesamientoFallidoException("el cupo de preguntas dejó un conflicto sin pregunta; se aumenta el máximo o se resuelven pendientes");
            }
        }

        _idsAutomaticos.Clear();
    }

    private readonly Dictionary<(string, string), string> _idsAutomaticos = [];

    /// <summary>Solo datos estructurales; nada que ya esté cubierto o que el usuario haya cerrado.</summary>
    private bool PuedePreguntarse(Contexto ctx, string registroId, Dictionary<string, object?> fm, string campo, ResultadoCaptura res)
    {
        if (!CamposPreguntables.Contains(campo))
        {
            return false;
        }

        var clase = (string?)fm.GetValueOrDefault("clase");
        if (campo == "resultados" && !ClasesConResultadoPreguntable.Contains(clase ?? string.Empty))
        {
            return false; // el resultado de una reunión o de una decisión es contenido, no estado
        }

        // Nada que completar si el dato ya es conocido por el usuario.
        if (campo != "coincidencia" && fm.TryGetValue(campo, out var valor) && valor as string != "desconocido"
            && fm.TryGetValue("certeza", out var cz) && cz is Dictionary<string, object?> certeza
            && certeza.GetValueOrDefault(campo) as string is "dicho" or "confirmado")
        {
            return false;
        }

        foreach (var q in ctx.Almacen.DeTipo("pregunta"))
        {
            var estado = (string?)q.Frontmatter.GetValueOrDefault("estado");
            var mismoCampo = (string?)q.Frontmatter.GetValueOrDefault("campo") == campo;
            if (!mismoCampo)
            {
                continue;
            }

            var mismoRegistro = (string?)q.Frontmatter.GetValueOrDefault("registro") == registroId;
            if (mismoRegistro && estado is "no_recuerda" or "no_relevante" or "respondida")
            {
                return false;
            }

            // «No relevante» desalienta preguntas equivalentes: mismo campo y misma clase de registro.
            if (estado == "no_relevante"
                && ctx.Almacen.Documentos.FirstOrDefault(d => d.Id == (string?)q.Frontmatter.GetValueOrDefault("registro")) is { } origen
                && (string?)origen.Frontmatter.GetValueOrDefault("clase") == clase)
            {
                return false;
            }
        }

        return true;
    }

    // ------------------------------------------------------------------ procesamiento diferido (lotes del proveedor)

    private sealed record EntradaDeEnvio(string Captura, List<string> Claves, HashSet<string> Ofrecidos);

    private static List<Documento> EnviosAbiertos(Almacen almacen) =>
        almacen.DeTipo("envio").Where(e => e.Id is not null && e.Frontmatter.GetValueOrDefault("estado") as string == "abierto")
            .OrderBy(e => e.Id, StringComparer.Ordinal).ToList();

    private static List<EntradaDeEnvio> EntradasDe(Documento envio) =>
        (envio.Frontmatter.GetValueOrDefault("capturas") as List<object?> ?? [])
            .OfType<Dictionary<string, object?>>()
            .Select(c => new EntradaDeEnvio(
                (string)c["captura"]!,
                (c.GetValueOrDefault("claves_existentes") as List<object?> ?? []).OfType<string>().ToList(),
                (c.GetValueOrDefault("ofrecidos") as List<object?> ?? []).OfType<string>().ToHashSet()))
            .ToList();

    private static HashSet<string> CapturasEnLotes(Almacen almacen) =>
        almacen.DeTipo("lote")
            .SelectMany(l => l.Frontmatter.GetValueOrDefault("capturas") as List<object?> ?? [])
            .OfType<Dictionary<string, object?>>()
            .Select(c => c.GetValueOrDefault("captura") as string)
            .OfType<string>()
            .ToHashSet();

    /// <summary>Capturas pendientes que no están ya en un envío abierto (una captura no se envía dos veces).</summary>
    private static List<Documento> PendientesSinEnvio(Almacen almacen)
    {
        var enEnvio = EnviosAbiertos(almacen).SelectMany(e => EntradasDe(e).Select(x => x.Captura)).ToHashSet();
        return Pendientes(almacen, null).Where(c => !enEnvio.Contains(c.Id!)).ToList();
    }

    /// <summary>
    /// Procesa por lotes diferidos (con descuento): recoge los envíos terminados, envía las pendientes como un único lote, espera hasta
    /// el máximo indicado y deja abierto lo que no termine. Cada resultado pasa por la misma validación que el modo inmediato.
    /// </summary>
    public async Task<ResultadoEjecucion> EjecutarPorLotesAsync(
        string ejecucion, IProveedorPorLotes lotes, TimeSpan esperaMaxima, TimeSpan intervalo,
        CancellationToken ct = default, Action<string>? hito = null)
    {
        _modoLotes = true;
        var resultado = new ResultadoEjecucion();
        var almacen = Almacen.Cargar(datos);
        var abiertos = EnviosAbiertos(almacen);
        resultado.Pendientes = PendientesSinEnvio(almacen).Count;
        if (abiertos.Count == 0 && resultado.Pendientes == 0)
        {
            return resultado;
        }

        // Privacidad: antes de enviar nada (fallo cerrado), igual que en el modo inmediato.
        if (configuracion is not null)
        {
            var motivos = ComprobadorDePrivacidad.Comprobar(lotes.Nombre, lotes.EnviaDatosAFuera, configuracion, DateOnly.FromDateTime(reloj().DateTime), entorno ?? (_ => null));
            if (motivos.Count > 0)
            {
                resultado.Detenida = true;
                resultado.Problemas.AddRange(motivos.Select(m => new ProblemaDeLote("-", m)));
                EscribirLote(almacen, ejecucion, resultado);
                return resultado;
            }
        }

        var recogidos = new List<Documento>();
        try
        {
            // 1. Recoger los envíos abiertos que ya hayan terminado.
            foreach (var envio in abiertos)
            {
                ct.ThrowIfCancellationRequested();
                var loteProveedor = (string)envio.Frontmatter["lote_proveedor"]!;
                if (!(await lotes.ConsultarAsync(loteProveedor, ct)).Terminado)
                {
                    resultado.EnviosAbiertos++;
                    continue;
                }

                await RecogerEnvioAsync(envio, lotes, ejecucion, resultado, ct);
                recogidos.Add(envio);
            }

            // 2. Enviar las pendientes que no estén ya en un envío (ni acaben de recogerse).
            almacen = Almacen.Cargar(datos);
            var gestionadas = recogidos.SelectMany(e => EntradasDe(e).Select(x => x.Captura)).ToHashSet();
            var aEnviar = PendientesSinEnvio(almacen).Where(c => !gestionadas.Contains(c.Id!)).ToList();
            if (aEnviar.Count > 0)
            {
                var preparadas = aEnviar.Select(c => (Captura: c, Datos: Preparar(almacen, c))).ToList();
                var loteProveedor = await lotes.EnviarAsync(preparadas.Select(x => (x.Captura.Id!, x.Datos.Solicitud)).ToList(), ct);

                // El envío se registra inmediatamente después de crear el lote en el proveedor.
                var envio = EscribirEnvio(lotes, loteProveedor, preparadas.Select(x => new EntradaDeEnvio(
                    x.Captura.Id!, x.Datos.Solicitud.ClavesExistentes.ToList(), x.Datos.Relacionados.Select(r => r.Id).ToHashSet())).ToList());
                resultado.Envios.Add(envio.Id!);
                hito?.Invoke("enviado");

                // 3. Esperar (acotado) a que termine; si no, queda abierto para la siguiente ejecución.
                var iteraciones = Math.Max(1, (int)Math.Ceiling(esperaMaxima.TotalMilliseconds / Math.Max(intervalo.TotalMilliseconds, 1)));
                var terminado = false;
                for (var i = 0; i < iteraciones && !terminado; i++)
                {
                    terminado = (await lotes.ConsultarAsync(loteProveedor, ct)).Terminado;
                    if (!terminado && i < iteraciones - 1)
                    {
                        await Task.Delay(intervalo, ct);
                    }
                }

                if (terminado)
                {
                    await RecogerEnvioAsync(envio, lotes, ejecucion, resultado, ct);
                    recogidos.Add(envio);
                }
                else
                {
                    resultado.EnviosAbiertos++;
                }
            }
        }
        catch (ProveedorException e)
        {
            // Saldo agotado, límite de gasto o clave inválida: se corta; lo no recogido queda abierto o pendiente.
            resultado.Problemas.Add(new ProblemaDeLote("-", ClasificadorDeErrores.Explicacion(e.Categoria), ClasificadorDeErrores.Etiqueta(e.Categoria)));
            resultado.Fatal = e.Categoria;
        }

        hito?.Invoke("antes_del_lote");
        _enviosDelLote.AddRange(recogidos.Select(e => e.Id!).Concat(resultado.Envios).Distinct());
        EscribirLote(Almacen.Cargar(datos), ejecucion, resultado);

        // 4. Marcar como recogidos los envíos cuyo resultado ya está en un lote de Mira.
        var tras = Almacen.Cargar(datos);
        foreach (var envio in recogidos)
        {
            var primera = EntradasDe(envio).FirstOrDefault()?.Captura;
            var lote = resultado.LoteId ?? tras.DeTipo("lote").FirstOrDefault(l =>
                (l.Frontmatter.GetValueOrDefault("capturas") as List<object?> ?? []).OfType<Dictionary<string, object?>>().Any(c => c.GetValueOrDefault("captura") as string == primera))?.Id;
            if (lote is null)
            {
                continue; // sin lote al que enlazar (no debería ocurrir): queda abierto y se reintenta
            }

            var fm = Clonar(envio.Frontmatter);
            fm["estado"] = "recogido";
            fm["recogido_el"] = reloj().ToString(Iso, CultureInfo.InvariantCulture);
            fm["lote"] = lote;
            EscrituraAtomica.Reemplazar(Path.Combine(datos, envio.Ruta), FrontmatterCanonico.Escribir(fm, envio.Cuerpo, "envio"));
        }

        return resultado;
    }

    private Documento EscribirEnvio(IProveedorPorLotes lotes, string loteProveedor, List<EntradaDeEnvio> capturas)
    {
        var momento = reloj();
        string id;
        do
        {
            id = $"envio-{momento:yyyyMMdd}-{momento:HHmm}";
            momento = momento.AddMinutes(1);
        }
        while (File.Exists(Path.Combine(datos, Identificadores.RutaRelativa(id)!)));

        var fm = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["tipo"] = "envio",
            ["creado_el"] = reloj().ToString(Iso, CultureInfo.InvariantCulture),
            ["estado"] = "abierto",
            ["modo"] = "lotes",
            ["proveedor"] = lotes.Nombre,
            ["modelo"] = lotes.Modelo,
            ["lote_proveedor"] = loteProveedor,
            ["capturas"] = capturas.Select(c => (object?)new Dictionary<string, object?>
            {
                ["captura"] = c.Captura,
                ["claves_existentes"] = c.Claves.Cast<object?>().ToList(),
                ["ofrecidos"] = c.Ofrecidos.Order(StringComparer.Ordinal).Cast<object?>().ToList(),
            }).ToList(),
        };
        var ruta = Identificadores.RutaRelativa(id)!;
        var cuerpo = $"Envío diferido de {capturas.Count} captura(s) al proveedor. Los resultados se recogen cuando el proveedor termina el lote.\n";
        var texto = FrontmatterCanonico.Escribir(fm, cuerpo, "envio");
        EscrituraAtomica.EscribirNuevo(Path.Combine(datos, ruta), texto);
        return Documento.Analizar(ruta, texto);
    }

    private async Task RecogerEnvioAsync(
        Documento envio, IProveedorPorLotes lotes, string ejecucion, ResultadoEjecucion resultado, CancellationToken ct)
    {
        var porId = (await lotes.RecogerAsync((string)envio.Frontmatter["lote_proveedor"]!, ct)).ToDictionary(r => r.Id);
        foreach (var entrada in EntradasDe(envio))
        {
            var almacen = Almacen.Cargar(datos);
            var captura = almacen.DeTipo("captura").FirstOrDefault(c => c.Id == entrada.Captura);
            if (captura is null)
            {
                resultado.Problemas.Add(new ProblemaDeLote(entrada.Captura, "la captura del envío ya no existe"));
                continue;
            }

            if (CapturasEnLotes(almacen).Contains(entrada.Captura))
            {
                continue; // ya recogida en una ejecución anterior que se interrumpió antes de marcar el envío
            }

            var res = new ResultadoCaptura();
            if (!porId.TryGetValue(entrada.Captura, out var r) || r.Tipo != TipoResultado.Correcto)
            {
                if (r is { Tipo: TipoResultado.Error, Categoria: not CategoriaError.Otro })
                {
                    throw new ProveedorException(r.Categoria, ClasificadorDeErrores.Explicacion(r.Categoria));
                }

                var motivo = r?.Tipo switch
                {
                    TipoResultado.Error => "el proveedor devolvió un error para esta captura",
                    TipoResultado.Cancelado => "el proveedor canceló esta captura",
                    TipoResultado.Expirado => "el lote expiró antes de procesar esta captura",
                    _ => "el proveedor no devolvió resultado para esta captura",
                };
                resultado.Problemas.Add(new ProblemaDeLote(entrada.Captura, $"{motivo}; queda pendiente"));
                continue;
            }

            ContarUso(r.Uso, res);
            try
            {
                var respuesta = Proveedores.LectorDeRespuesta.Analizar(r.Texto ?? string.Empty);
                var existentes = almacen.DeTipo("registro")
                    .Where(x => ((string?)x.Frontmatter.GetValueOrDefault("clave_extraccion"))?.StartsWith(entrada.Captura + "#f", StringComparison.Ordinal) == true)
                    .ToList();
                var aplicada = AplicarRespuesta(almacen, captura, ejecucion, respuesta, existentes, entrada.Ofrecidos, res);
                resultado.Procesadas.Add(entrada.Captura);
                resultado.RegistrosCreados.AddRange(aplicada.Creados);
                resultado.RegistrosActualizados.AddRange(aplicada.Actualizados);
                resultado.PreguntasCreadas.AddRange(aplicada.PreguntasCreadas);
                resultado.PreguntasCerradasPorLimite.AddRange(aplicada.CerradasPorLimite);
                _entradas.Add(aplicada.Entrada(entrada.Captura, PreciosEfectivos()));
            }
            catch (ProcesamientoFallidoException e)
            {
                resultado.Problemas.Add(new ProblemaDeLote(entrada.Captura, e.Message));
            }
        }
    }

    // ------------------------------------------------------------------ lote

    private void EscribirLote(Almacen almacen, string ejecucion, ResultadoEjecucion res)
    {
        if (_entradas.Count == 0 && res.Problemas.Count == 0)
        {
            return;
        }

        var momento = reloj();
        string id;
        do
        {
            id = $"lote-{momento:yyyyMMdd}-{momento:HHmm}";
            momento = momento.AddMinutes(1);
        }
        while (File.Exists(Path.Combine(datos, Identificadores.RutaRelativa(id)!)));

        var fm = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["tipo"] = "lote",
            ["ejecutado_el"] = reloj().ToString(Iso, CultureInfo.InvariantCulture),
            ["ejecucion"] = ejecucion,
            ["modo"] = _modoLotes ? "lotes" : "inmediato",
            ["agente"] = new Dictionary<string, object?> { ["agente"] = "Mira", ["version"] = opciones.VersionMira, ["modelo"] = proveedor.Modelo },
            ["proveedor"] = proveedor.Nombre,
            ["capturas"] = _entradas.Cast<object?>().ToList(),
        };
        if (res.Problemas.Count > 0)
        {
            fm["problemas"] = res.Problemas
                .Select(p =>
                {
                    var d = new Dictionary<string, object?> { ["captura"] = p.Captura, ["motivo"] = p.Motivo };
                    if (p.Tipo is not null)
                    {
                        d["tipo"] = p.Tipo;
                    }

                    return (object?)d;
                }).ToList();
        }

        if (_enviosDelLote.Count > 0)
        {
            fm["envios"] = _enviosDelLote.Cast<object?>().ToList();
        }

        decimal? coste = null;
        if (_hayUso)
        {
            coste = PreciosEfectivos()?.Coste(_tokensEntrada, _tokensSalida);
            res.TokensEntrada = _tokensEntrada;
            res.TokensSalida = _tokensSalida;
            res.CosteEstimadoUsd = coste;
            fm["uso"] = new Dictionary<string, object?>
            {
                ["tokens_entrada"] = _tokensEntrada.ToString(CultureInfo.InvariantCulture),
                ["tokens_salida"] = _tokensSalida.ToString(CultureInfo.InvariantCulture),
                ["coste_estimado_usd"] = coste is null ? "desconocido" : Formato.Usd(coste.Value),
            };
        }

        // Aviso previo por presupuesto: solo avisa, nunca detiene (la estimación puede diferir del saldo real).
        if (opciones.Presupuesto is { } presupuesto)
        {
            var previo = ControlDeGasto.GastoEstimado(almacen.DeTipo("lote"), presupuesto.Desde);
            if (ControlDeGasto.AvisoDePresupuesto(previo + (coste ?? 0m), presupuesto) is { } aviso)
            {
                fm["avisos"] = new List<object?> { aviso };
                res.Avisos.Add(aviso);
            }
        }

        var cuerpo = $"Ejecución de Mira ({ejecucion}): {res.Procesadas.Count} captura(s) procesada(s)"
                     + (res.Problemas.Count > 0 ? $", {res.Problemas.Count} problema(s).\n" : ".\n");
        var texto = FrontmatterCanonico.Escribir(fm, cuerpo, "lote");
        EscrituraAtomica.EscribirNuevo(Path.Combine(datos, Identificadores.RutaRelativa(id)!), texto);
        res.LoteId = id;
        _entradas.Clear();
    }

    // ------------------------------------------------------------------ utilidades

    private static (Documento Doc, string Texto, bool Nuevo) Preparar(string ruta, string tipo, Dictionary<string, object?> fm, string cuerpo, bool nuevo)
    {
        var texto = FrontmatterCanonico.Escribir(fm, cuerpo, tipo);
        return (Documento.Analizar(ruta, texto), texto, nuevo);
    }

    private static string NuevoId(string prefijo, int sufijo, Contexto ctx, string? _)
    {
        string id;
        do
        {
            id = $"{prefijo}-{ctx.Ahora:yyyyMMdd}-{Identificadores.Sufijo(sufijo)}";
        }
        while (ctx.IdsUsados.Contains(id));
        ctx.IdsUsados.Add(id);
        return id;
    }

    internal static Dictionary<string, object?> Clonar(Dictionary<string, object?> origen) =>
        (Dictionary<string, object?>)ClonarValor(origen)!;

    private static object? ClonarValor(object? v) => v switch
    {
        Dictionary<string, object?> m => m.ToDictionary(p => p.Key, p => ClonarValor(p.Value)),
        List<object?> l => l.Select(ClonarValor).ToList(),
        _ => v,
    };

    private static bool Igual(object? a, object? b) => Normalizar(a) == Normalizar(b);

    private static string Normalizar(object? v) => v switch
    {
        Dictionary<string, object?> m => "{" + string.Join(",", m.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + Normalizar(p.Value))) + "}",
        List<object?> l => "[" + string.Join(",", l.Select(Normalizar)) + "]",
        System.Collections.IEnumerable e when v is not string => "[" + string.Join(",", e.Cast<object?>().Select(Normalizar)) + "]",
        _ => v?.ToString() ?? "null",
    };
}

public sealed class ProcesamientoFallidoException(string mensaje) : Exception(mensaje)
{
    /// <summary>Consumo de la llamada que falló (ya cobrada), si el proveedor lo informó.</summary>
    public UsoDeTokens? Uso { get; init; }
}

/// <summary>Registros existentes que se ofrecen al proveedor: recientes y que comparten algo con el texto, o actividades abiertas.</summary>
public static class SelectorDeRelacionados
{
    public static IReadOnlyList<RegistroRelacionado> Seleccionar(
        Almacen almacen, string texto, DateTimeOffset comunicada, int ventanaDias, string capturaActualId)
    {
        var textoNormal = Normalizar(texto);
        var desde = comunicada.AddDays(-ventanaDias);
        var resultado = new List<RegistroRelacionado>();
        foreach (var r in almacen.DeTipo("registro"))
        {
            var fm = r.Frontmatter;
            var clave = fm.GetValueOrDefault("clave_extraccion") as string;
            if (clave is not null && clave.StartsWith(capturaActualId + "#f", StringComparison.Ordinal))
            {
                continue; // los de esta misma captura se tratan por sus claves
            }

            if (!DateTimeOffset.TryParse(fm.GetValueOrDefault("comunicada_el") as string, CultureInfo.InvariantCulture, out var cuando) || cuando < desde)
            {
                continue;
            }

            var personas = Textos(fm, "personas");
            var entidades = ((fm.GetValueOrDefault("entidades") as List<object?>) ?? [])
                .OfType<Dictionary<string, object?>>().Select(e => e.GetValueOrDefault("nombre") as string).OfType<string>().ToList();
            var proyectos = Textos(fm, "proyectos");
            var abierta = (fm.GetValueOrDefault("multidia") as Dictionary<string, object?>)?.GetValueOrDefault("estado") as string == "abierto";
            var comparte = personas.Concat(entidades).Concat(proyectos).Any(n => n.Length > 1 && textoNormal.Contains(Normalizar(n), StringComparison.Ordinal));
            if (!abierta && !comparte)
            {
                continue;
            }

            var f = fm.GetValueOrDefault("fecha") as Dictionary<string, object?>;
            resultado.Add(new RegistroRelacionado(
                r.Id!,
                (string)fm["clase"]!,
                (string)fm["titulo"]!,
                f?.GetValueOrDefault("desde") as string,
                f?.GetValueOrDefault("hasta") as string,
                personas,
                entidades,
                proyectos,
                (fm.GetValueOrDefault("multidia") as Dictionary<string, object?>)?.GetValueOrDefault("estado") as string));
        }

        return resultado;
    }

    private static List<string> Textos(Dictionary<string, object?> fm, string clave) =>
        fm.GetValueOrDefault(clave) is List<object?> l ? l.OfType<string>().ToList() : [];

    /// <summary>Minúsculas y sin acentos, para comparar nombres.</summary>
    public static string Normalizar(string s) => Orumi.Core.Texto.Normalizacion.SinAcentos(s);
}
