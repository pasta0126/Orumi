using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Orumi.Core.Texto;

namespace Orumi.Noa;

public sealed record HipotesisConfirmada(string Id, string Formulacion, string? Comentario);

public sealed record ResultadoDeValidacion(
    IReadOnlyList<Rasgo> Rasgos, IReadOnlyList<Recomendacion> Recomendaciones, IReadOnlyList<string> SinDatos,
    IReadOnlyDictionary<string, int> Descartes, string? Error)
{
    public int TotalDescartes => Descartes.Values.Sum();

    /// <summary>Pares de claves de rasgos que el modelo señaló como contradictorios (ya comprobados contra los rasgos aceptados).</summary>
    public IReadOnlyList<(string A, string B)> Contradicciones { get; init; } = [];
}

/// <summary>
/// Lee y VALIDA la salida del modelo. El perfil es una inferencia sensible: aquí se aplica, en código y no en el prompt, todo lo que no se
/// puede dejar a la buena voluntad del modelo. Un rasgo que no cumple se descarta (y se cuenta, sin reproducir contenido); la salida
/// malformada no escribe nada. El nivel de evidencia lo calcula este código a partir de las fuentes verificadas.
/// </summary>
public static class ValidadorDelPerfil
{
    public const string SinEvidencia = "sin_evidencia";
    public const string CitaNoLiteral = "cita_no_literal";
    public const string Sujeto = "sujeto_no_es_el_usuario";
    public const string TerminoClinico = "termino_clinico";
    public const string TerceroEnElTexto = "tercero_en_el_texto";
    public const string RasgoDeTercero = "rasgo_de_un_tercero";
    public const string CertezaInvalida = "certeza_invalida";
    public const string ApartadoInvalido = "apartado_invalido";
    public const string ClaveInvalida = "clave_invalida";
    public const string TextoInvalido = "texto_invalido";
    public const string DescartadoPorElUsuario = "descartado_por_el_usuario";
    public const string CorregidoPorElUsuario = "corregido_por_el_usuario";
    public const string EnergiaSinHipotesis = "energia_sin_hipotesis_confirmada";
    public const string RecomendacionSinBase = "recomendacion_sin_base";
    public const string Duplicado = "duplicado";

    private static readonly Regex ClaveRx = new(@"^[a-z][a-z0-9_]{2,50}$", RegexOptions.Compiled);

    /// <summary>La familia o los allegados como sujeto de un rasgo («su hijo es…»): el perfil es solo del usuario y menos aún de menores.</summary>
    private static readonly Regex RelacionComoSujeto = new(
        @"\b(hij[oa]s?|madre|padre|pareja|espos[ao]|marido|mujer|amig[oa]s?|companer[oa]s?|jef[ea]|hermano|hermana|abuel[oa]s?|suegr[oa]s?|prim[oa]s?|ti[oa]s?|vecin[oa]s?)(?:\s+suy[oa]s?|\s+de(?:l)?\s+(?:el\s+)?(?:trabajo|clase|casa|la\s+oficina))?\s+(es|son|esta|estan|tiene|tienen|siente|sienten|muestra|muestran|parece|parecen|suele|suelen|sufre|sufren|se\s+siente|se\s+muestra)\b",
        RegexOptions.Compiled);

    private static readonly string[] Sensibles =
    [
        "salud", "enfermedad", "dolor", "medic", "terapia", "psicolog", "dinero", "deuda", "sueldo", "econom", "hipoteca", "ingresos", "religi", "politic", "sexual", "intim", "divorcio", "duelo", "adiccion",
    ];

    public static ResultadoDeValidacion Validar(
        string respuesta, Corpus corpus, Perfil? previo, ConfiguracionDeNoa config, IReadOnlyList<HipotesisConfirmada> confirmadas, int version, DateTimeOffset ahora)
    {
        var descartes = new Dictionary<string, int>(StringComparer.Ordinal);
        void D(string motivo) => descartes[motivo] = descartes.GetValueOrDefault(motivo) + 1;
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(Limpiar(respuesta));
        }
        catch (JsonException e)
        {
            return new ResultadoDeValidacion([], [], [], descartes, $"JSON no válido en {e.Path ?? "?"}");
        }

        using (doc)
        {
            var raiz = doc.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object || !raiz.TryGetProperty("rasgos", out var lista) || lista.ValueKind != JsonValueKind.Array)
            {
                return new ResultadoDeValidacion([], [], [], descartes, "la respuesta no tiene la lista «rasgos»");
            }

            var anteriores = (previo?.Rasgos ?? []).ToDictionary(r => r.Clave);
            var aceptados = new List<Rasgo>();
            var claves = new HashSet<string>();
            foreach (var e in lista.EnumerateArray())
            {
                var r = ValidarRasgo(e, corpus, anteriores, config, confirmadas, version, ahora, claves, D);
                if (r is not null)
                {
                    aceptados.Add(r);
                    claves.Add(r.Clave);
                }
            }

            // Los rasgos del usuario (corregidos) se conservan tal cual y también pueden fundar una recomendación.
            var baseDeRecomendacion = aceptados.Concat((previo?.Rasgos ?? []).Where(r => r.Origen == Rasgo.DelUsuario && r.Visible && !aceptados.Any(a => a.Clave == r.Clave))).ToDictionary(r => r.Clave);
            var recomendaciones = new List<Recomendacion>();
            if (raiz.TryGetProperty("recomendaciones", out var recs) && recs.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in recs.EnumerateArray())
                {
                    if (ValidarRecomendacion(e, baseDeRecomendacion, corpus, config, D) is { } rec && recomendaciones.All(x => x.Id != rec.Id))
                    {
                        recomendaciones.Add(rec);
                    }
                }
            }

            var sinDatos = raiz.TryGetProperty("sin_datos", out var sd) && sd.ValueKind == JsonValueKind.Array
                ? sd.EnumerateArray().Select(x => x.GetString()).OfType<string>().Where(Apartados.Existe).Distinct().ToList() : [];
            // Contradicciones señaladas por el modelo: solo cuentan si ambas claves son rasgos visibles y distintos (la pregunta la redacta el código).
            var contradicciones = new List<(string, string)>();
            if (raiz.TryGetProperty("contradicciones", out var cs) && cs.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in cs.EnumerateArray().Take(5))
                {
                    var claves2 = c.ValueKind == JsonValueKind.Object && c.TryGetProperty("rasgos", out var rs) && rs.ValueKind == JsonValueKind.Array
                        ? rs.EnumerateArray().Select(x => x.GetString()).OfType<string>().Distinct().Order(StringComparer.Ordinal).ToList() : [];
                    if (claves2.Count == 2 && claves2.All(baseDeRecomendacion.ContainsKey) && !contradicciones.Contains((claves2[0], claves2[1])))
                    {
                        contradicciones.Add((claves2[0], claves2[1]));
                    }
                }
            }

            return new ResultadoDeValidacion(aceptados, recomendaciones, sinDatos, descartes, null) { Contradicciones = contradicciones };
        }
    }

    private static Rasgo? ValidarRasgo(
        JsonElement e, Corpus corpus, IReadOnlyDictionary<string, Rasgo> anteriores, ConfiguracionDeNoa config, IReadOnlyList<HipotesisConfirmada> confirmadas,
        int version, DateTimeOffset ahora, HashSet<string> vistas, Action<string> descartar)
    {
        string? S(string k) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        if (e.ValueKind != JsonValueKind.Object)
        {
            descartar(TextoInvalido);
            return null;
        }

        var clave = S("clave");
        if (clave is null || !ClaveRx.IsMatch(clave))
        {
            descartar(ClaveInvalida);
            return null;
        }

        if (!vistas.Add(clave))
        {
            descartar(Duplicado);
            return null;
        }

        var apartado = S("apartado");
        if (apartado is null || !Apartados.Existe(apartado))
        {
            descartar(ApartadoInvalido);
            return null;
        }

        if (S("sujeto") != "usuario")
        {
            descartar(Sujeto);
            return null;
        }

        var texto = S("texto")?.Trim();
        var contexto = S("contexto")?.Trim();
        if (texto is null || texto.Length is < 10 or > 400 || texto.Any(char.IsControl) || contexto?.Length > 300 || contexto?.Any(char.IsControl) == true)
        {
            descartar(TextoInvalido);
            return null;
        }

        var normal = Normalizacion.SinAcentos(texto).ToLowerInvariant();
        if (HayTerminoClinico(normal + " " + Normalizacion.SinAcentos(contexto ?? string.Empty).ToLowerInvariant(), config))
        {
            descartar(TerminoClinico);
            return null;
        }

        if (corpus.Seudonimos.MencionaTercero(texto))
        {
            descartar(TerceroEnElTexto);
            return null;
        }

        if (RelacionComoSujeto.IsMatch(normal))
        {
            descartar(RasgoDeTercero);
            return null;
        }

        var certeza = S("certeza");
        var esEnergia = apartado == Apartados.EnergiaYAnimo;
        if (certeza is null || certeza is not (Certezas.Dicho or Certezas.Interpretado or Certezas.Hipotesis or Certezas.Confirmado) || certeza == Certezas.Confirmado && !esEnergia)
        {
            descartar(CertezaInvalida); // «confirmado» solo lo pone el usuario (o una hipótesis que ya confirmó) y «recomendación» no es un rasgo
            return null;
        }

        // Lo que el usuario ya corrigió o descartó se respeta.
        anteriores.TryGetValue(clave, out var previo);
        if (previo?.Origen == Rasgo.DelUsuario)
        {
            descartar(CorregidoPorElUsuario);
            return null;
        }

        var sensible = (e.TryGetProperty("sensible", out var sv) && sv.ValueKind == JsonValueKind.True) || Sensibles.Any(normal.Contains);
        List<EvidenciaDeRasgo> evidencia = [];
        var hipotesis = new List<string>();
        int fuentes, semanas;
        string nivel;
        if (esEnergia)
        {
            // Energía y ánimo solo se apoya en lo que el usuario ya confirmó (decisión 12): no hay conjeturas aquí.
            hipotesis = e.TryGetProperty("hipotesis", out var hs) && hs.ValueKind == JsonValueKind.Array
                ? hs.EnumerateArray().Select(x => x.GetString()).OfType<string>().Where(id => confirmadas.Any(c => c.Id == id)).Distinct().ToList() : [];
            if (hipotesis.Count == 0)
            {
                descartar(EnergiaSinHipotesis);
                return null;
            }

            if (certeza != Certezas.Confirmado)
            {
                certeza = Certezas.Confirmado;
            }

            fuentes = 0;
            semanas = 0;
            nivel = Niveles.Patron;
        }
        else
        {
            if (certeza == Certezas.Confirmado)
            {
                descartar(CertezaInvalida);
                return null;
            }

            if (!e.TryGetProperty("evidencia", out var ev) || ev.ValueKind != JsonValueKind.Array)
            {
                descartar(SinEvidencia);
                return null;
            }

            foreach (var item in ev.EnumerateArray())
            {
                var fuente = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("fuente", out var f) ? f.GetString() : null;
                var cita = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("cita", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                if (fuente is null || corpus.Buscar(fuente) is not { } origen || string.IsNullOrWhiteSpace(cita) || cita.Length > 400)
                {
                    descartar(CitaNoLiteral);
                    continue;
                }

                if (!corpus.CitaEsLiteral(fuente, cita))
                {
                    descartar(CitaNoLiteral); // inventada o reformulada: no vale
                    continue;
                }

                if (evidencia.All(x => x.Fuente != fuente))
                {
                    evidencia.Add(new EvidenciaDeRasgo(fuente, origen.Fecha, corpus.Seudonimos.Revelar(cita.Trim())));
                }
            }

            if (evidencia.Count == 0)
            {
                descartar(SinEvidencia);
                return null;
            }

            fuentes = evidencia.Count;
            semanas = evidencia.Select(x => Semana(x.Fecha)).Distinct().Count();
            nivel = fuentes >= config.RegistrosParaPatron && semanas >= config.SemanasParaPatron ? Niveles.Patron
                : fuentes >= config.RegistrosParaIndicio ? Niveles.Indicio : Niveles.Conjetura;
            if (certeza == Certezas.Hipotesis && fuentes < 2)
            {
                descartar(CertezaInvalida); // una hipótesis derivada de varios registros no puede salir de uno
                return null;
            }
        }

        var estado = EstadosDeRasgo.Activo;
        var oculto = false;
        string? comentario = null;
        List<EntradaDeRasgo> historial = [];
        if (previo is not null)
        {
            historial.AddRange(previo.Historial);
            if (previo.Estado is EstadosDeRasgo.Descartado or EstadosDeRasgo.Borrado)
            {
                var @base = previo.EvidenciaAlDescartar ?? 0;
                if (fuentes < Math.Max(2 * @base, @base + 2))
                {
                    descartar(DescartadoPorElUsuario);
                    return null;
                }

                historial.Add(new EntradaDeRasgo(ahora, "reabierto", "noa", "Evidencia nueva sustancial."));
            }
            else
            {
                estado = previo.Estado;
                oculto = previo.Oculto;
                comentario = previo.ComentarioDelUsuario;
            }
        }

        historial.Add(new EntradaDeRasgo(ahora, previo is null ? "propuesto" : "actualizado", "noa", $"{nivel}; {fuentes} fuente(s) en {semanas} semana(s)"));
        return new Rasgo(clave, apartado, corpus.Seudonimos.Revelar(texto), contexto is null ? null : corpus.Seudonimos.Revelar(contexto), certeza, nivel, sensible,
            evidencia, fuentes, semanas, hipotesis, estado, Rasgo.DelAgente, oculto, comentario, null, previo?.VersionCreada ?? version, ahora, historial);
    }

    private static Recomendacion? ValidarRecomendacion(JsonElement e, IReadOnlyDictionary<string, Rasgo> baseDeRasgos, Corpus corpus, ConfiguracionDeNoa config, Action<string> descartar)
    {
        var texto = e.ValueKind == JsonValueKind.Object && e.TryGetProperty("texto", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()?.Trim() : null;
        if (texto is null || texto.Length is < 10 or > 400 || texto.Any(char.IsControl))
        {
            descartar(TextoInvalido);
            return null;
        }

        var normal = Normalizacion.SinAcentos(texto).ToLowerInvariant();
        if (HayTerminoClinico(normal, config))
        {
            descartar(TerminoClinico);
            return null;
        }

        if (corpus.Seudonimos.MencionaTercero(texto))
        {
            descartar(TerceroEnElTexto);
            return null;
        }

        // Solo patrones o indicios fundan una recomendación: una conjetura nunca (decisión 5).
        var refs = e.TryGetProperty("rasgos", out var rs) && rs.ValueKind == JsonValueKind.Array
            ? rs.EnumerateArray().Select(x => x.GetString()).OfType<string>().Where(c => baseDeRasgos.TryGetValue(c, out var r) && r.Nivel is Niveles.Patron or Niveles.Indicio).Distinct().ToList() : [];
        if (refs.Count == 0)
        {
            descartar(RecomendacionSinBase);
            return null;
        }

        var id = "rec-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normal)))[..8].ToLowerInvariant();
        return new Recomendacion(id, corpus.Seudonimos.Revelar(texto), refs, EstadosDeRasgo.Activo, false);
    }

    private static bool HayTerminoClinico(string normalizado, ConfiguracionDeNoa config) =>
        config.Terminos.Any(t => Regex.IsMatch(normalizado, @"\b" + Regex.Escape(Normalizacion.SinAcentos(t).ToLowerInvariant())));

    private static (int, int) SemanaIso(DateTime d) => (ISOWeek.GetYear(d), ISOWeek.GetWeekOfYear(d));

    private static (int, int) Semana(string fecha) =>
        DateTime.TryParseExact(fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? SemanaIso(d) : (0, 0);

    private static string Limpiar(string texto)
    {
        var t = texto.Trim();
        if (t.StartsWith("```", StringComparison.Ordinal))
        {
            var primera = t.IndexOf('\n');
            var ultima = t.LastIndexOf("```", StringComparison.Ordinal);
            if (primera > 0 && ultima > primera)
            {
                t = t[(primera + 1)..ultima].Trim();
            }
        }

        return t;
    }
}
