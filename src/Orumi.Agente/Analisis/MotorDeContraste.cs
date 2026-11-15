using System.Globalization;
using Orumi.Agente.Clima;
using Orumi.Core.Ficheros;
using Orumi.Core.Hipotesis;

namespace Orumi.Agente.Analisis;

/// <summary>
/// Contraste de las hipótesis del usuario con sus registros y el clima (spec <c>contraste-de-hipotesis</c>, X20–X23): código determinista,
/// sin modelo y sin afirmar causalidad. Un día sin dato del efecto no es un día sin efecto: solo cuenta lo dicho (<c>presente</c>) o lo
/// confirmado como ausente (el usuario lo dijo o respondió «no sucedió»).
/// </summary>
public static class MotorDeContraste
{
    public const string ConceptoDuracionExterior = "exterior_duracion";

    private sealed record RegistroDelDia(string Id, DateOnly Dia, Ciudad? Ciudad);

    private sealed class Contexto
    {
        public required DateOnly Hoy { get; init; }

        public required DateOnly Ayer { get; init; }

        public required IReadOnlyList<DateOnly> Dias { get; init; }

        public required Dictionary<DateOnly, Ciudad?> CiudadDelDia { get; init; }

        public required Dictionary<DateOnly, List<RegistroDelDia>> RegistrosDelDia { get; init; }

        public required Dictionary<(DateOnly, string), List<(Etiqueta Etiqueta, string Registro)>> Etiquetas { get; init; }

        public required Dictionary<(DateOnly, string), Observado> Respuestas { get; init; }

        public required Dictionary<DateOnly, int> MinutosRespondidos { get; init; }

        public required ConfiguracionDeClima Clima { get; init; }

        public required string Datos { get; init; }

        public Dictionary<(DateOnly, string), ClimaDia?> Cache { get; } = [];

        public ClimaDia? ClimaDe(DateOnly dia)
        {
            if (CiudadDelDia.GetValueOrDefault(dia) is not { } ciudad)
            {
                return null;
            }

            if (Cache.TryGetValue((dia, ciudad.Clave), out var c))
            {
                return c;
            }

            ClimaDia? leido = null;
            var ruta = FormatoDeClima.Ruta(Datos, dia, ciudad);
            if (File.Exists(ruta))
            {
                try
                {
                    leido = FormatoDeClima.Leer(File.ReadAllText(ruta, System.Text.Encoding.UTF8));
                }
                catch (Exception e) when (e is FormatException or KeyNotFoundException or InvalidCastException or YamlDotNet.Core.YamlException)
                {
                    leido = null;
                }
            }

            Cache[(dia, ciudad.Clave)] = leido;
            return leido;
        }
    }

    // ------------------------------------------------------------------ análisis

    public static ResultadoDelAnalisis Analizar(string datos, ConfiguracionDeClima clima, UmbralesDeAnalisis u, DateOnly hoy)
    {
        var almacen = Almacen.Cargar(datos);
        clima = clima.ConAprendidas(datos);
        var ctx = Cargar(almacen, datos, clima, hoy);
        var primero = PrimerRegistro(almacen);
        var observacion = primero is null ? 0 : Math.Max(0, hoy.DayNumber - primero.Value.DayNumber);
        var enObservacion = primero is null || observacion < u.SemanasDeObservacion * 7;
        var resultados = new List<ResultadoDeContraste>();
        var descartados = new List<string>();

        foreach (var h in AlmacenDeHipotesis.Listar(datos))
        {
            foreach (var c in h.Contrastes)
            {
                var r = Evaluar(ctx, h, c, u);
                if (c.Revision is { Estado: Hipotesis.Descartada } rv)
                {
                    var base_ = rv.EfectosAlRevisar;
                    if (r.EfectosTotales >= Math.Max(2 * base_, base_ + u.MinimoEfectos))
                    {
                        var reabierto = r with { Reabierto = true };
                        resultados.Add(reabierto with { Texto = RedactorDeResultados.Texto(reabierto) });
                    }
                    else
                    {
                        descartados.Add(r.Id);
                    }

                    continue;
                }

                resultados.Add(r);
            }
        }

        return new ResultadoDelAnalisis(primero, observacion, enObservacion, ctx.Dias.Count, resultados, descartados);
    }

    /// <summary>Primer día en que el usuario comunicó algo (inicio de la etapa de observación, X21).</summary>
    public static DateOnly? PrimerRegistro(Almacen almacen) =>
        almacen.DeTipo("captura").Select(c => c.Frontmatter.GetValueOrDefault("comunicada_el") as string)
            .Where(t => t is { Length: >= 10 } && DateOnly.TryParseExact(t[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            .Select(t => DateOnly.ParseExact(t![..10], "yyyy-MM-dd", CultureInfo.InvariantCulture)).Order().Select(d => (DateOnly?)d).FirstOrDefault();

    private static Contexto Cargar(Almacen almacen, string datos, ConfiguracionDeClima clima, DateOnly hoy)
    {
        var capturas = almacen.Documentos.Where(d => d.Tipo == "captura" && d.Id is not null).ToDictionary(d => d.Id!);
        var registros = new List<RegistroDelDia>();
        foreach (var r in almacen.DeTipo("registro").Where(r => r.Id is not null && r.Frontmatter.GetValueOrDefault("estado") is not ("eliminado" or "fusionado")))
        {
            if (r.Frontmatter.GetValueOrDefault("fecha") is not Dictionary<string, object?> f || f.GetValueOrDefault("desde") is not string desde
                || desde != f.GetValueOrDefault("hasta") as string || !DateOnly.TryParseExact(desde, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia))
            {
                continue;
            }

            var cap = (r.Frontmatter.GetValueOrDefault("fuentes") as List<object?> ?? []).OfType<Dictionary<string, object?>>().FirstOrDefault()?.GetValueOrDefault("captura") as string;
            var ciudadDicha = cap is not null && capturas.TryGetValue(cap, out var cd) ? cd.Frontmatter.GetValueOrDefault("ciudad") as string : null;
            registros.Add(new RegistroDelDia(r.Id!, dia, clima.ResolverDeCaptura(cap, ciudadDicha).Ciudad));
        }

        var ayer = hoy.AddDays(-1);
        var porDia = registros.Where(r => r.Dia <= ayer).GroupBy(r => r.Dia).ToDictionary(g => g.Key, g => g.ToList());
        var ciudadDelDia = porDia.ToDictionary(p => p.Key, p => p.Value.Where(r => r.Ciudad is not null).GroupBy(r => r.Ciudad!.Clave)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => g.First().Ciudad).FirstOrDefault());

        var dias = porDia.Keys.Order().ToList();
        var fechaDe = registros.ToDictionary(r => r.Id, r => r.Dia);
        var etiquetas = new Dictionary<(DateOnly, string), List<(Etiqueta, string)>>();
        foreach (var e in AlmacenDeEtiquetas.LeerTodas(datos))
        {
            if (!fechaDe.TryGetValue(e.Registro, out var dia) || dia > ayer)
            {
                continue;
            }

            foreach (var t in e.Etiquetas.Where(t => t.Estado != Etiqueta.Quitado))
            {
                if (!etiquetas.TryGetValue((dia, t.Concepto), out var l))
                {
                    etiquetas[(dia, t.Concepto)] = l = [];
                }

                l.Add((t, e.Registro));
            }
        }

        // Respuestas del usuario a las preguntas de contraste: respondida = ocurrió; «no sucedió» = ausencia confirmada.
        var respuestas = new Dictionary<(DateOnly, string), Observado>();
        var minutos = new Dictionary<DateOnly, int>();
        foreach (var p in almacen.DeTipo("pregunta"))
        {
            if (p.Frontmatter.GetValueOrDefault("concepto") is not string concepto || p.Frontmatter.GetValueOrDefault("dia") is not string d
                || !DateOnly.TryParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia))
            {
                continue;
            }

            var estado = p.Frontmatter.GetValueOrDefault("estado") as string;
            var texto = (p.Frontmatter.GetValueOrDefault("respuesta") as Dictionary<string, object?>)?.GetValueOrDefault("texto") as string;
            if (concepto == ConceptoDuracionExterior)
            {
                if (estado == "respondida" && Duraciones.Minutos(texto) is { } m)
                {
                    minutos[dia] = m;
                }
            }
            else if (estado == "respondida")
            {
                respuestas[(dia, concepto)] = Observado.Presente;
            }
            else if (estado == "no_sucedio" && !respuestas.ContainsKey((dia, concepto)))
            {
                respuestas[(dia, concepto)] = Observado.Ausente;
            }
        }

        return new Contexto
        {
            Hoy = hoy, Ayer = ayer, Dias = dias, CiudadDelDia = ciudadDelDia, RegistrosDelDia = porDia, Etiquetas = etiquetas,
            Respuestas = respuestas, MinutosRespondidos = minutos, Clima = clima, Datos = datos,
        };
    }

    // ------------------------------------------------------------------ factor y efecto

    /// <summary>Observación del efecto un día: lo dicho, lo negado o lo respondido; lo demás es «sin dato», nunca ausencia.</summary>
    private static Observado Efecto(Contexto c, string concepto, DateOnly dia)
    {
        if (dia > c.Ayer)
        {
            return Observado.SinDato; // el día no ha terminado
        }

        var etiquetas = c.Etiquetas.GetValueOrDefault((dia, concepto)) ?? [];
        if (etiquetas.Any(e => e.Etiqueta.Estado == Etiqueta.Presente) || c.Respuestas.GetValueOrDefault((dia, concepto)) == Observado.Presente)
        {
            return Observado.Presente;
        }

        return etiquetas.Any(e => e.Etiqueta.Estado == Etiqueta.Negado) || c.Respuestas.GetValueOrDefault((dia, concepto)) == Observado.Ausente
            ? Observado.Ausente : Observado.SinDato;
    }

    private enum Motivo
    {
        Ninguno,
        SinClima,
        SinDuracion,
    }

    private static bool? Condicion(Contexto c, CondicionDeFactor cond, DateOnly dia, UmbralesDeClima u, out Motivo motivo)
    {
        motivo = Motivo.Ninguno;
        if (cond.Tipo == "registro")
        {
            var presentes = (c.Etiquetas.GetValueOrDefault((dia, cond.Nombre)) ?? []).Where(e => e.Etiqueta.Estado == Etiqueta.Presente).ToList();
            if (presentes.Count == 0)
            {
                return false; // sin mención: el día no tuvo ese factor (solo se miran días con registros)
            }

            if (cond.MinutosMinimos is not { } minimo)
            {
                return true;
            }

            var sabidos = presentes.Where(e => e.Etiqueta.Minutos is not null).Sum(e => e.Etiqueta.Minutos!.Value);
            var desconocidos = presentes.Count(e => e.Etiqueta.Minutos is null);
            if (desconocidos > 0 && c.MinutosRespondidos.TryGetValue(dia, out var resp))
            {
                sabidos = Math.Max(sabidos, resp);
                desconocidos = 0;
            }

            if (sabidos >= minimo)
            {
                return true;
            }

            if (desconocidos == 0)
            {
                return false;
            }

            motivo = Motivo.SinDuracion; // estuvo fuera pero no se sabe cuánto
            return null;
        }

        var clima = c.ClimaDe(dia);
        if (clima is null)
        {
            motivo = Motivo.SinClima;
            return null;
        }

        bool? r = cond.Nombre switch
        {
            "uv_alto" => (clima.Dia.UvMaximo ?? clima.Horas.Max(h => h.Uv)) is { } uv ? uv >= u.UvAlto : null,
            "lluvia" => (clima.Dia.PrecipitacionMm ?? Suma(clima.Horas.Select(h => h.Precipitacion))) is { } mm ? mm >= u.LluviaMmDia : null,
            "soleado" => Soleado(clima, u),
            _ => null,
        };
        if (r is null)
        {
            motivo = Motivo.SinClima;
        }

        return r;
    }

    private static double? Suma(IEnumerable<double?> v)
    {
        var k = v.Where(x => x is not null).Select(x => x!.Value).ToList();
        return k.Count == 0 ? null : k.Sum();
    }

    /// <summary>Soleado o despejado: poca nubosidad en las horas de luz y buena parte de las horas de luz con sol.</summary>
    private static bool? Soleado(ClimaDia clima, UmbralesDeClima u)
    {
        var luz = clima.Horas.Where(h => h.Radiacion is > 0).ToList();
        var nubes = luz.Count > 0 ? Suma(luz.Select(h => h.Nubosidad)) / luz.Count(h => h.Nubosidad is not null) : clima.Dia.NubosidadMedia;
        if (nubes is null || clima.Dia.SolSegundos is null || clima.Dia.LuzSegundos is not > 0)
        {
            return null;
        }

        return nubes <= u.NubosidadDespejadoMax && clima.Dia.SolSegundos / clima.Dia.LuzSegundos * 100 >= u.SolDespejadoMinPct;
    }

    private static bool? Factor(Contexto c, Contraste contraste, DateOnly dia, UmbralesDeClima u, out Motivo motivo)
    {
        motivo = Motivo.Ninguno;
        bool? total = true;
        foreach (var cond in contraste.Factor.Select(CondicionDeFactor.Analizar))
        {
            var v = Condicion(c, cond, dia, u, out var m);
            if (v == false)
            {
                motivo = Motivo.Ninguno;
                return false;
            }

            if (v is null)
            {
                total = null;
                motivo = m;
            }
        }

        return total;
    }

    // ------------------------------------------------------------------ evaluación de un contraste

    private static ResultadoDeContraste Evaluar(Contexto c, Hipotesis h, Contraste contraste, UmbralesDeAnalisis u)
    {
        var uc = new UmbralesDeClima(c.Clima.Umbrales.UvAlto, c.Clima.Umbrales.LluviaMmDia, c.Clima.Umbrales.NubosidadDespejadoMax, c.Clima.Umbrales.SolDespejadoMinPct);
        int conF = 0, efConF = 0, sinF = 0, efSinF = 0, sinClima = 0, sinFactor = 0, sinEfecto = 0;
        var evidencia = new List<EvidenciaDeDia>();

        foreach (var dia in c.Dias)
        {
            var factor = Factor(c, contraste, dia, uc, out var motivo);
            if (factor is null)
            {
                if (motivo == Motivo.SinClima)
                {
                    sinClima++;
                }
                else
                {
                    sinFactor++;
                }

                continue;
            }

            var hayPresente = false;
            var todosAusentes = true;
            for (var d = contraste.RetardoDesde; d <= contraste.RetardoHasta; d++)
            {
                var o = Efecto(c, contraste.Efecto, dia.AddDays(d));
                hayPresente |= o == Observado.Presente;
                todosAusentes &= o == Observado.Ausente;
            }

            var observado = hayPresente ? Observado.Presente : todosAusentes ? Observado.Ausente : Observado.SinDato;
            if (observado == Observado.SinDato)
            {
                sinEfecto++;
                continue;
            }

            if (factor == true)
            {
                conF++;
                efConF += observado == Observado.Presente ? 1 : 0;
            }
            else
            {
                sinF++;
                efSinF += observado == Observado.Presente ? 1 : 0;
            }

            evidencia.Add(Evidencia(c, contraste, dia, factor.Value, observado));
        }

        var diasConEfecto = c.Dias.Where(d => Efecto(c, contraste.Efecto, d) == Observado.Presente).ToList();
        var semanas = diasConEfecto.Select(d => (ISOWeek.GetYear(d.ToDateTime(TimeOnly.MinValue)), ISOWeek.GetWeekOfYear(d.ToDateTime(TimeOnly.MinValue)))).Distinct().Count();
        double? tf = conF > 0 ? (double)efConF / conF : null;
        double? ts = sinF > 0 ? (double)efSinF / sinF : null;
        double? dif = tf is { } a && ts is { } b ? a - b : null;

        var motivos = new List<string>();
        if (conF < u.MinimoDiasPorGrupo)
        {
            motivos.Add($"días con el factor y el efecto conocido: {conF} de {u.MinimoDiasPorGrupo} necesarios");
        }

        if (sinF < u.MinimoDiasPorGrupo)
        {
            motivos.Add($"días sin el factor y con el efecto conocido: {sinF} de {u.MinimoDiasPorGrupo} necesarios");
        }

        if (diasConEfecto.Count < u.MinimoEfectos)
        {
            motivos.Add($"días con el efecto mencionado: {diasConEfecto.Count} de {u.MinimoEfectos} necesarios");
        }

        if (semanas < u.MinimoSemanas)
        {
            motivos.Add($"semanas distintas con el efecto: {semanas} de {u.MinimoSemanas} necesarias");
        }

        string veredicto;
        if (motivos.Count > 0 || dif is null)
        {
            veredicto = Veredicto.Insuficiente;
        }
        else if (dif >= u.DiferenciaApoyo && Wilson.Separados(efConF, conF, efSinF, sinF))
        {
            veredicto = Veredicto.Apoyada;
        }
        else if (dif <= u.DiferenciaSinApoyo)
        {
            veredicto = Veredicto.SinApoyo;
        }
        else
        {
            veredicto = Veredicto.Insuficiente;
            motivos.Add("la diferencia observada es pequeña o no es concluyente con los días que hay");
        }

        var res = new ResultadoDeContraste(
            h.Id, contraste.Id, h.Formulacion, TextoDeFactor(contraste, uc), contraste.Efecto, contraste.RetardoDesde, contraste.RetardoHasta,
            veredicto, conF, efConF, sinF, efSinF, tf, ts, dif, diasConEfecto.Count, semanas, sinClima, sinFactor, sinEfecto, motivos,
            [.. evidencia.OrderByDescending(e => e.Fecha).Take(u.DiasDeEvidencia).OrderBy(e => e.Fecha)], string.Empty, contraste.Revision, false);
        return res with { Texto = RedactorDeResultados.Texto(res) };
    }

    private static EvidenciaDeDia Evidencia(Contexto c, Contraste contraste, DateOnly dia, bool factor, Observado observado)
    {
        var registros = new List<string>();
        var citas = new List<string>();
        for (var d = contraste.RetardoDesde; d <= contraste.RetardoHasta; d++)
        {
            foreach (var (e, r) in c.Etiquetas.GetValueOrDefault((dia.AddDays(d), contraste.Efecto)) ?? [])
            {
                registros.Add(r);
                if (e.Cita is not null)
                {
                    citas.Add(e.Cita);
                }
            }
        }

        foreach (var cond in contraste.Factor.Select(CondicionDeFactor.Analizar).Where(x => x.Tipo == "registro"))
        {
            foreach (var (_, r) in c.Etiquetas.GetValueOrDefault((dia, cond.Nombre)) ?? [])
            {
                registros.Add(r);
            }
        }

        var clima = c.ClimaDe(dia);
        var texto = clima is null ? "sin clima" : FormattableString.Invariant($"UV máx {Num(clima.Dia.UvMaximo)}, lluvia {Num(clima.Dia.PrecipitacionMm)} mm, nubes {Num(clima.Dia.NubosidadMedia)} %");
        return new EvidenciaDeDia(dia, c.CiudadDelDia.GetValueOrDefault(dia)?.Nombre ?? "?", factor, observado == Observado.Presente ? "presente" : "ausente",
            [.. registros.Distinct().Order(StringComparer.Ordinal)], texto, [.. citas.Distinct()]);
    }

    private static string Num(double? v) => v is null ? "desconocido" : v.Value.ToString("0.#", CultureInfo.InvariantCulture);

    internal static string TextoDeFactor(Contraste c, UmbralesDeClima u) => string.Join(" y ", c.Factor.Select(CondicionDeFactor.Analizar).Select(cond => cond switch
    {
        { Tipo: "clima", Nombre: "uv_alto" } => FormattableString.Invariant($"UV alto (≥ {u.UvAlto})"),
        { Tipo: "clima", Nombre: "lluvia" } => FormattableString.Invariant($"lluvia (≥ {u.LluviaMmDia} mm)"),
        { Tipo: "clima", Nombre: "soleado" } => "día soleado o despejado",
        { Tipo: "registro", Nombre: "socializacion" } => "socializar",
        { Tipo: "registro", Nombre: "exterior", MinutosMinimos: { } m } => $"más de {m} min al aire libre",
        { Tipo: "registro" } => cond.Nombre.Replace('_', ' '),
        _ => cond.Nombre,
    }));

    /// <summary>Huecos por los que Mira puede preguntar: días del contraste con el efecto (o la duración de estar fuera) sin dato.</summary>
    public static IReadOnlyList<HuecoDeContraste> Huecos(string datos, ConfiguracionDeClima clima, UmbralesDeAnalisis u, DateOnly hoy, IReadOnlyList<ResultadoDeContraste> resultados)
    {
        clima = clima.ConAprendidas(datos);
        var ctx = Cargar(Almacen.Cargar(datos), datos, clima, hoy);
        var uc = new UmbralesDeClima(clima.Umbrales.UvAlto, clima.Umbrales.LluviaMmDia, clima.Umbrales.NubosidadDespejadoMax, clima.Umbrales.SolDespejadoMinPct);
        var huecos = new List<HuecoDeContraste>();
        var hipotesis = AlmacenDeHipotesis.Listar(datos).ToDictionary(h => h.Id);
        foreach (var r in resultados.Where(r => r.Veredicto == Veredicto.Insuficiente && r.Revision?.Estado is not Hipotesis.Descartada || r.Reabierto))
        {
            var contraste = hipotesis[r.Hipotesis].Contrastes.First(c => c.Id == r.Contraste);
            foreach (var dia in ctx.Dias.OrderByDescending(d => d))
            {
                var factor = Factor(ctx, contraste, dia, uc, out var motivo);
                var referencia = ctx.RegistrosDelDia[dia].First().Id;
                if (factor is null)
                {
                    if (motivo == Motivo.SinDuracion)
                    {
                        huecos.Add(new HuecoDeContraste(r.Hipotesis, r.Contraste, ConceptoDuracionExterior, dia, referencia, "duracion"));
                    }

                    continue;
                }

                for (var d = contraste.RetardoDesde; d <= contraste.RetardoHasta; d++)
                {
                    var e = dia.AddDays(d);
                    if (e <= ctx.Ayer && Efecto(ctx, contraste.Efecto, e) == Observado.SinDato)
                    {
                        huecos.Add(new HuecoDeContraste(r.Hipotesis, r.Contraste, contraste.Efecto, e, referencia, "efecto", factor));
                    }
                }
            }
        }

        return huecos;
    }
}

public sealed record UmbralesDeClima(double UvAlto, double LluviaMmDia, double NubosidadDespejadoMax, double SolDespejadoMinPct);

/// <summary>Intervalo de Wilson al 95 % de una proporción (pequeñas muestras).</summary>
public static class Wilson
{
    public static (double Bajo, double Alto) Intervalo(int exitos, int total)
    {
        if (total <= 0)
        {
            return (0, 1);
        }

        const double z = 1.96;
        var p = (double)exitos / total;
        var z2 = z * z;
        var centro = (p + z2 / (2 * total)) / (1 + z2 / total);
        var margen = z * Math.Sqrt(p * (1 - p) / total + z2 / (4.0 * total * total)) / (1 + z2 / total);
        return (Math.Max(0, centro - margen), Math.Min(1, centro + margen));
    }

    /// <summary>Con factor claramente por encima: el límite inferior de su intervalo supera el superior del otro grupo.</summary>
    public static bool Separados(int exitosA, int totalA, int exitosB, int totalB) => Intervalo(exitosA, totalA).Bajo > Intervalo(exitosB, totalB).Alto;
}

/// <summary>Duraciones dichas por el usuario («2 horas», «hora y media», «90 min», «1h30»). Lo ambiguo es desconocido: nunca se supone.</summary>
public static class Duraciones
{
    public static int? Minutos(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var t = texto.Trim().ToLowerInvariant().Replace(',', '.');
        var m = System.Text.RegularExpressions.Regex.Match(t, @"(\d{1,2})\s*h(?:oras?)?\s*(?:y\s*)?(\d{1,2})\s*(?:min\w*|m)\b|(\d{1,2})\s*h\s*(\d{2})\b|(\d{1,2}):(\d{2})\b");
        if (m.Success)
        {
            var (h, mi) = m.Groups[1].Success ? (m.Groups[1].Value, m.Groups[2].Value)
                : m.Groups[3].Success ? (m.Groups[3].Value, m.Groups[4].Value) : (m.Groups[5].Value, m.Groups[6].Value);
            return int.Parse(h, CultureInfo.InvariantCulture) * 60 + int.Parse(mi, CultureInfo.InvariantCulture);
        }

        m = System.Text.RegularExpressions.Regex.Match(t, @"(\d{1,2}(?:\.\d)?)\s*(?:h\b|hora)");
        if (m.Success)
        {
            return (int)Math.Round(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 60);
        }

        m = System.Text.RegularExpressions.Regex.Match(t, @"(\d{1,4})\s*(?:min)");
        if (m.Success)
        {
            return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        if (t.Contains("hora y media", StringComparison.Ordinal))
        {
            return 90;
        }

        if (t.Contains("media hora", StringComparison.Ordinal))
        {
            return 30;
        }

        var palabras = new Dictionary<string, int> { ["una hora"] = 60, ["dos horas"] = 120, ["tres horas"] = 180, ["cuatro horas"] = 240, ["cinco horas"] = 300, ["seis horas"] = 360 };
        foreach (var (k, v) in palabras)
        {
            if (t.Contains(k, StringComparison.Ordinal))
            {
                return v;
            }
        }

        return null;
    }
}
