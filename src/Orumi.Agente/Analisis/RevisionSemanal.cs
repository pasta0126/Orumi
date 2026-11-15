using System.Globalization;
using Orumi.Agente.Clima;
using Orumi.Core.Aprendizaje;
using Orumi.Core.Ficheros;
using Orumi.Core.Yaml;

namespace Orumi.Agente.Analisis;

public sealed record SemanaDePreguntas(DateOnly Desde, DateOnly Hasta, int Formuladas, int Respondidas, int NoRelevantes, int NoSucedio, int NoRecuerda, int PorLimite, int Pendientes)
{
    public int CerradasPorElUsuario => Respondidas + NoRelevantes + NoSucedio + NoRecuerda;

    public double? TasaDeRespuesta => CerradasPorElUsuario == 0 ? null : (double)Respondidas / CerradasPorElUsuario;

    public double? TasaDeNoRelevante => CerradasPorElUsuario == 0 ? null : (double)NoRelevantes / CerradasPorElUsuario;
}

public sealed record CambioDeAprendizaje(string Id, string Accion, string Descripcion, DateTimeOffset Fecha, string Origen);

public sealed record ResumenDeAprendizaje(IReadOnlyList<CambioDeAprendizaje> Cambios, IReadOnlyList<SemanaDePreguntas> Tendencia, int ReglasActivas);

/// <summary>«Qué ha aprendido Mira»: reglas nuevas o cambiadas esta semana y la evolución semanal de sus preguntas (X64). Solo cuenta las de Mira.</summary>
public static class ResumenDeAprendizajes
{
    public const int SemanasDeTendencia = 4;

    public static ResumenDeAprendizaje Calcular(string datos, DateTimeOffset ahora)
    {
        var hoy = DateOnly.FromDateTime(ahora.DateTime);
        var cambios = MotorDeAprendizaje.Listar(datos, incluirBorradas: true)
            .SelectMany(r => r.Historial.Where(h => h.Fecha > ahora.AddDays(-7)).Select(h => new CambioDeAprendizaje(r.Id, h.Accion, r.Descripcion, h.Fecha, h.Origen)))
            .OrderBy(c => c.Fecha).ToList();
        var preguntas = Almacen.Cargar(datos).DeTipo("pregunta").Where(PreguntasDeContraste.EsDeMira).ToList();
        var semanas = new List<SemanaDePreguntas>();
        for (var k = SemanasDeTendencia - 1; k >= 0; k--)
        {
            var hasta = hoy.AddDays(-7 * k);
            var desde = hasta.AddDays(-6);
            var cohorte = preguntas.Where(p => p.Frontmatter.GetValueOrDefault("creada_el") as string is { Length: >= 10 } t
                && DateOnly.TryParseExact(t[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) && f >= desde && f <= hasta).ToList();
            int N(string estado) => cohorte.Count(p => p.Frontmatter.GetValueOrDefault("estado") as string == estado);
            semanas.Add(new SemanaDePreguntas(desde, hasta, cohorte.Count, N("respondida"), N("no_relevante"), N("no_sucedio"), N("no_recuerda"), N("cerrada_por_limite"), N("pendiente")));
        }

        return new ResumenDeAprendizaje(cambios, semanas, MotorDeAprendizaje.Listar(datos).Count(r => r.EstaActiva));
    }
}

public sealed record ResultadoDeLaRevision(
    string Id, string Ruta, bool EnObservacion, int Contrastes, IReadOnlyList<string> PreguntasCreadas, ResultadoDelAnalisis Analisis);

/// <summary>
/// Revisión semanal (X21, X23): un contraste por hipótesis con su evidencia enlazada, solo frecuencia observada y sin recomendaciones. Durante la
/// etapa de observación solo informa del progreso de datos. Se escribe en <c>analisis/AAAA/semana-AAAA-Www.yaml</c>.
/// </summary>
public static class RevisionSemanal
{
    private const string Formato = "yyyy-MM-dd'T'HH:mm:sszzz";

    public static string IdDeSemana(DateOnly dia) =>
        $"semana-{ISOWeek.GetYear(dia.ToDateTime(TimeOnly.MinValue)):0000}-W{ISOWeek.GetWeekOfYear(dia.ToDateTime(TimeOnly.MinValue)):00}";

    public static string Ruta(string datos, DateOnly dia) =>
        Path.Combine(datos, "analisis", ISOWeek.GetYear(dia.ToDateTime(TimeOnly.MinValue)).ToString("0000", CultureInfo.InvariantCulture), IdDeSemana(dia) + ".yaml");

    public static bool Existe(string datos, DateOnly dia) => File.Exists(Ruta(datos, dia));

    public static ResultadoDeLaRevision Generar(string datos, Configuracion? configuracion, ConfiguracionDeClima clima, UmbralesDeAnalisis u, DateTimeOffset ahora)
    {
        var hoy = DateOnly.FromDateTime(ahora.DateTime);
        var analisis = MotorDeContraste.Analizar(datos, clima, u, hoy);
        var creadas = analisis.EnObservacion
            ? []
            : PreguntasDeContraste.Crear(datos, configuracion, u, MotorDeContraste.Huecos(datos, clima, u, hoy, analisis.Contrastes), analisis.Contrastes, ahora);
        var aprendizaje = ResumenDeAprendizajes.Calcular(datos, ahora);
        var id = IdDeSemana(hoy);
        var lunes = hoy.AddDays(-(((int)hoy.DayOfWeek + 6) % 7));
        var uc = new UmbralesDeClima(clima.Umbrales.UvAlto, clima.Umbrales.LluviaMmDia, clima.Umbrales.NubosidadDespejadoMax, clima.Umbrales.SolDespejadoMinPct);

        string N(double? v) => v is null ? "desconocido" : v.Value.ToString("0.###", CultureInfo.InvariantCulture);
        string I(int v) => v.ToString(CultureInfo.InvariantCulture);
        YNodo Lista(IEnumerable<string> xs) => new YLista([.. xs.Select(x => (YNodo)new YCitado(x))], EnLinea: true);

        var contrastes = analisis.EnObservacion ? [] : analisis.Contrastes.Select(r => (YNodo)YMapa.De(
            ("id", new YLiteral(r.Id)),
            ("formulacion", new YCitado(r.Formulacion)),
            ("factor", new YCitado(r.FactorTexto)),
            ("efecto", new YLiteral(r.Efecto)),
            ("retardo_dias", YMapa.EnLineaDe(("desde", new YLiteral(I(r.RetardoDesde))), ("hasta", new YLiteral(I(r.RetardoHasta))))),
            ("veredicto", new YLiteral(r.Veredicto)),
            ("resumen", YMapa.EnLineaDe(
                ("dias_con_factor", new YLiteral(I(r.DiasConFactor))), ("efecto_con_factor", new YLiteral(I(r.EfectoConFactor))),
                ("dias_sin_factor", new YLiteral(I(r.DiasSinFactor))), ("efecto_sin_factor", new YLiteral(I(r.EfectoSinFactor))),
                ("tasa_con_factor", new YLiteral(N(r.TasaConFactor))), ("tasa_sin_factor", new YLiteral(N(r.TasaSinFactor))), ("diferencia", new YLiteral(N(r.Diferencia))),
                ("dias_con_efecto", new YLiteral(I(r.EfectosTotales))), ("semanas_con_efecto", new YLiteral(I(r.SemanasConEfecto))))),
            ("excluidos", YMapa.EnLineaDe(
                ("sin_clima", new YLiteral(I(r.ExcluidosSinClima))), ("factor_sin_dato", new YLiteral(I(r.ExcluidosFactorSinDato))), ("efecto_sin_dato", new YLiteral(I(r.ExcluidosEfectoSinDato))))),
            ("motivos", Lista(r.Motivos)),
            ("texto", new YCitado(r.Texto)),
            ("reabierto", new YLiteral(r.Reabierto ? "true" : "false")),
            ("revision", r.Revision is null ? new YLiteral("ninguna") : YMapa.EnLineaDe(
                ("estado", new YLiteral(r.Revision.Estado)), ("comentario", r.Revision.Comentario is null ? new YLiteral("ninguno") : new YCitado(r.Revision.Comentario)),
                ("fecha", new YInstante(r.Revision.Fecha.ToString(Formato, CultureInfo.InvariantCulture))))),
            ("evidencia", new YLista([.. r.Evidencia.Select(e => (YNodo)YMapa.EnLineaDe(
                ("fecha", new YLiteral(e.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))), ("ciudad", new YCitado(e.Ciudad)),
                ("factor", new YLiteral(e.Factor == true ? "si" : "no")), ("efecto", new YLiteral(e.Efecto)),
                ("registros", new YLista([.. e.Registros.Select(x => (YNodo)new YLiteral(x))], EnLinea: true)), ("clima", new YCitado(e.Clima)),
                ("citas", Lista(e.Citas))))])))).ToList();

        var sugerencias = analisis.EnObservacion ? [] : Sugerencias(datos, clima, u, analisis, uc);
        var pares = new List<(string, YNodo)>
        {
            ("id", new YLiteral(id)),
            ("tipo", new YLiteral("revision_semanal")),
            ("generada_el", new YInstante(ahora.ToString(Formato, CultureInfo.InvariantCulture))),
            ("periodo", YMapa.EnLineaDe(("desde", new YLiteral(lunes.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))), ("hasta", new YLiteral(lunes.AddDays(6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))))),
            ("estado", new YLiteral(analisis.EnObservacion ? "observacion" : "completa")),
            ("observacion", YMapa.EnLineaDe(
                ("primer_registro", analisis.PrimerRegistro is { } p ? new YLiteral(p.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) : new YLiteral("desconocido")),
                ("dias_transcurridos", new YLiteral(I(analisis.DiasDeObservacion))), ("dias_necesarios", new YLiteral(I(u.SemanasDeObservacion * 7))),
                ("dias_con_registros", new YLiteral(I(analisis.DiasConRegistros))))),
            ("umbrales", YMapa.EnLineaDe(
                ("diferencia_apoyo", new YLiteral(N(u.DiferenciaApoyo))), ("diferencia_sin_apoyo", new YLiteral(N(u.DiferenciaSinApoyo))), ("minimo_dias_por_grupo", new YLiteral(I(u.MinimoDiasPorGrupo))),
                ("minimo_efectos", new YLiteral(I(u.MinimoEfectos))), ("minimo_semanas", new YLiteral(I(u.MinimoSemanas))),
                ("uv_alto", new YLiteral(N(uc.UvAlto))), ("lluvia_mm_dia", new YLiteral(N(uc.LluviaMmDia))))),
            ("contrastes", new YLista(contrastes)),
            ("descartados", Lista(analisis.Descartados)),
            ("sugerencias", Lista(sugerencias)),
            ("aprendizaje", YMapa.De(
                ("reglas_activas", new YLiteral(I(aprendizaje.ReglasActivas))),
                ("cambios", new YLista([.. aprendizaje.Cambios.Select(c => (YNodo)YMapa.EnLineaDe(
                    ("fecha", new YInstante(c.Fecha.ToString(Formato, CultureInfo.InvariantCulture))), ("regla", new YLiteral(c.Id)), ("accion", new YLiteral(c.Accion)),
                    ("origen", new YLiteral(c.Origen)), ("descripcion", new YCitado(c.Descripcion))))])),
                ("tendencia", new YLista([.. aprendizaje.Tendencia.Select(s => (YNodo)YMapa.EnLineaDe(
                    ("desde", new YLiteral(s.Desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))), ("hasta", new YLiteral(s.Hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))),
                    ("formuladas", new YLiteral(I(s.Formuladas))), ("respondidas", new YLiteral(I(s.Respondidas))), ("no_relevantes", new YLiteral(I(s.NoRelevantes))),
                    ("no_sucedio", new YLiteral(I(s.NoSucedio))), ("no_recuerda", new YLiteral(I(s.NoRecuerda))), ("por_limite", new YLiteral(I(s.PorLimite))),
                    ("pendientes", new YLiteral(I(s.Pendientes))), ("tasa_respuesta", new YLiteral(N(s.TasaDeRespuesta))), ("tasa_no_relevante", new YLiteral(N(s.TasaDeNoRelevante)))))])))),
            ("preguntas_creadas", new YLista([.. creadas.Select(x => (YNodo)new YLiteral(x))], EnLinea: true)),
            ("etiquetado", UltimoEtiquetado(datos)),
        };
        var ruta = Ruta(datos, hoy);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        EscrituraAtomica.Reemplazar(ruta, YamlEscritor.Escribir(YMapa.De([.. pares])));
        return new ResultadoDeLaRevision(id, ruta, analisis.EnObservacion, analisis.Contrastes.Count, creadas, analisis);
    }

    /// <summary>Constancia del último etiquetado con el modelo (proveedor, modelo, coste): el análisis en sí es código, sin coste de IA.</summary>
    private static YNodo UltimoEtiquetado(string datos)
    {
        var carpeta = Path.Combine(datos, "analisis", "ejecuciones");
        var ultimo = Directory.Exists(carpeta) ? Directory.EnumerateFiles(carpeta, "eti-*.yaml", SearchOption.AllDirectories).Order(StringComparer.Ordinal).LastOrDefault() : null;
        if (ultimo is null)
        {
            return new YLiteral("ninguno");
        }

        try
        {
            var m = YamlLector.LeerMapa(File.ReadAllText(ultimo, System.Text.Encoding.UTF8));
            var uso = m.GetValueOrDefault("uso") as Dictionary<string, object?> ?? [];
            return YMapa.EnLineaDe(
                ("ejecucion", new YLiteral((string)m["id"]!)), ("proveedor", new YLiteral((string)m["proveedor"]!)), ("modelo", new YLiteral((string)m["modelo"]!)),
                ("coste_estimado_usd", uso.GetValueOrDefault("coste_estimado_usd") is string c ? new YCitado(c) : new YLiteral("desconocido")));
        }
        catch (Exception e) when (e is FormatException or KeyNotFoundException or InvalidCastException or YamlDotNet.Core.YamlException)
        {
            return new YLiteral("ilegible");
        }
    }

    /// <summary>
    /// Sugerencias de ajuste de umbrales: solo se proponen, nunca se aplican (spec «Umbrales ajustables»). Para un factor de clima con pocos días,
    /// cuántos días más habría con un umbral algo menor.
    /// </summary>
    private static List<string> Sugerencias(string datos, ConfiguracionDeClima clima, UmbralesDeAnalisis u, ResultadoDelAnalisis analisis, UmbralesDeClima uc)
    {
        var dias = LeerClima(datos);
        var textos = new List<string>();
        foreach (var r in analisis.Contrastes.Where(r => r.Veredicto == Veredicto.Insuficiente && r.DiasConFactor < u.MinimoDiasPorGrupo))
        {
            var h = Orumi.Core.Hipotesis.AlmacenDeHipotesis.Leer(datos, r.Hipotesis);
            var c = h?.Contrastes.FirstOrDefault(x => x.Id == r.Contraste);
            if (c is null)
            {
                continue;
            }

            foreach (var cond in c.Factor.Select(Orumi.Core.Hipotesis.CondicionDeFactor.Analizar).Where(x => x.Tipo == "clima"))
            {
                if (cond.Nombre == "uv_alto" && uc.UvAlto > 2)
                {
                    var con = dias.Count(d => d.Dia.UvMaximo >= uc.UvAlto);
                    var menor = dias.Count(d => d.Dia.UvMaximo >= uc.UvAlto - 1);
                    if (menor > con)
                    {
                        textos.Add(FormattableString.Invariant($"Hay {con} día(s) con UV ≥ {uc.UvAlto}; con un umbral de {uc.UvAlto - 1} habría {menor}. Si te parece mejor, cámbialo en la configuración (clima → umbrales → uv_alto). No se cambia solo."));
                    }
                }
                else if (cond.Nombre == "lluvia" && uc.LluviaMmDia > 0.2)
                {
                    var umbral = uc.LluviaMmDia / 2;
                    var con = dias.Count(d => d.Dia.PrecipitacionMm >= uc.LluviaMmDia);
                    var menor = dias.Count(d => d.Dia.PrecipitacionMm >= umbral);
                    if (menor > con)
                    {
                        textos.Add(FormattableString.Invariant($"Hay {con} día(s) con lluvia ≥ {uc.LluviaMmDia} mm; con un umbral de {umbral} mm habría {menor}. No se cambia solo."));
                    }
                }
            }
        }

        return [.. textos.Distinct()];
    }

    private static List<ClimaDia> LeerClima(string datos)
    {
        var carpeta = Path.Combine(datos, "clima");
        if (!Directory.Exists(carpeta))
        {
            return [];
        }

        var lista = new List<ClimaDia>();
        foreach (var f in Directory.EnumerateFiles(carpeta, "*.yaml", SearchOption.AllDirectories))
        {
            try
            {
                lista.Add(FormatoDeClima.Leer(File.ReadAllText(f, System.Text.Encoding.UTF8)));
            }
            catch (Exception e) when (e is FormatException or KeyNotFoundException or InvalidCastException or YamlDotNet.Core.YamlException)
            {
                // derivado y regenerable
            }
        }

        return lista;
    }
}
