using System.Globalization;
using System.Text.RegularExpressions;
using Orumi.Core.Ficheros;

namespace Orumi.Agente.Clima;

public sealed record ResultadoDeCiudades(
    IReadOnlyList<string> Aprendidas, IReadOnlyList<string> PreguntasCreadas, IReadOnlyList<string> RespuestasAplicadas, IReadOnlyList<string> Avisos)
{
    public static readonly ResultadoDeCiudades Vacio = new([], [], [], []);
}

/// <summary>
/// Ciudades que ORUMI no conoce (X67): si Mira la tiene clara la busca ya, la usa y después pregunta para confirmar; con dudas pregunta y no
/// consulta su clima hasta tener respuesta; si el usuario corrige, se corrige. Solo sale el nombre de la ciudad hacia el geocodificador.
/// También pregunta si una nota sin ciudad menciona otra ciudad conocida, sin cambiarla por sí sola.
/// </summary>
public static class ResolutorDeCiudades
{
    private const int MaximoDePreguntasNuevas = 3;
    private static readonly Regex Afirmativo = new(@"^\s*(s[ií]|correcto|correcta|vale|ok|exacto|exacta|esa|eso|así es|asi es|claro)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Negativo = new(@"^\s*no\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static async Task<(ConfiguracionDeClima Config, ResultadoDeCiudades Resultado)> ResolverAsync(
        string datos, ConfiguracionDeClima config, IGeocodificador geocodificador, int cupoDePreguntas, DateTimeOffset ahora, CancellationToken ct = default)
    {
        config = config.ConAprendidas(datos);
        var almacen = Almacen.Cargar(datos);
        var d = CiudadesAprendidasStore.Leer(datos);
        var ciudades = d.Ciudades.ToList();
        var notas = d.Notas.ToList();
        var aplicadas = d.Aplicadas.ToList();
        var aprendidas = new List<string>();
        var respuestasAplicadas = new List<string>();
        var avisos = new List<string>();
        var preguntasNuevas = new List<string>();
        var reintentos = new List<(string Clave, string Nombre, string? Registro, string Texto)>();

        // 1. Respuestas del usuario a las preguntas de ciudad: se aplican una vez.
        foreach (var p in almacen.DeTipo("pregunta").Where(p => p.Frontmatter.GetValueOrDefault("campo") as string == "ciudad" && p.Id is not null && !aplicadas.Contains(p.Id!)))
        {
            var estado = p.Frontmatter.GetValueOrDefault("estado") as string;
            if (estado is null or "pendiente")
            {
                continue;
            }

            var tipo = p.Frontmatter.GetValueOrDefault("ciudad_tipo") as string;
            var clave = p.Frontmatter.GetValueOrDefault("ciudad_clave") as string ?? string.Empty;
            var nombre = p.Frontmatter.GetValueOrDefault("ciudad_nombre") as string ?? clave;
            var texto = ((p.Frontmatter.GetValueOrDefault("respuesta") as Dictionary<string, object?>)?.GetValueOrDefault("texto") as string ?? string.Empty).Trim();
            var respondida = estado == "respondida";
            try
            {
                if (respondida && tipo is "confirmar" or "elegir")
                {
                    var i = ciudades.FindIndex(c => c.Clave == clave);
                    if (tipo == "confirmar" && Afirmativo.IsMatch(texto) && i >= 0)
                    {
                        ciudades[i] = ciudades[i] with { Estado = CiudadAprendida.Confirmada };
                    }
                    else if (tipo == "confirmar" && Negativo.IsMatch(texto) && texto.Length <= 4)
                    {
                        if (i >= 0)
                        {
                            ciudades[i] = ciudades[i] with { Estado = CiudadAprendida.Rechazada };
                        }

                        reintentos.Add((clave, nombre, p.Frontmatter.GetValueOrDefault("registro") as string, $"Entendido, no es esa. ¿Cuál es «{nombre}»? Respóndeme con el nombre y la región o el país."));
                    }
                    else
                    {
                        var candidato = (await geocodificador.BuscarAsync(texto, ct)).FirstOrDefault();
                        if (candidato is null)
                        {
                            reintentos.Add((clave, nombre, p.Frontmatter.GetValueOrDefault("registro") as string, $"No encuentro «{texto}». ¿Puedes decirme el nombre completo de «{nombre}», con la región o el país?"));
                        }
                        else
                        {
                            var nueva = Nueva(clave, nombre, candidato, CiudadAprendida.Confirmada, "usuario", ahora);
                            ciudades.RemoveAll(c => c.Clave == clave);
                            ciudades.Add(nueva);
                        }
                    }
                }
                else if (respondida && tipo == "mencion" && p.Frontmatter.GetValueOrDefault("ciudad_captura") is string cap)
                {
                    if (Afirmativo.IsMatch(texto))
                    {
                        notas.RemoveAll(n => n.Captura == cap);
                        notas.Add(new NotaConCiudad(cap, nombre, "usuario", ahora));
                    }
                    else if (!(Negativo.IsMatch(texto) && texto.Length <= 4) && texto.Length > 0)
                    {
                        var conocida = config.Buscar(texto);
                        var destino = conocida?.Nombre;
                        if (conocida is null && (await geocodificador.BuscarAsync(texto, ct)).FirstOrDefault() is { } c)
                        {
                            var k = Ciudad.Normalizar(texto);
                            ciudades.RemoveAll(x => x.Clave == k);
                            ciudades.Add(Nueva(k, texto, c, CiudadAprendida.Confirmada, "usuario", ahora));
                            destino = texto;
                        }

                        if (destino is not null)
                        {
                            notas.RemoveAll(n => n.Captura == cap);
                            notas.Add(new NotaConCiudad(cap, destino, "usuario", ahora));
                        }
                        else
                        {
                            avisos.Add($"No se encontró la ciudad que indicaste para una nota ({p.Id}).");
                        }
                    }
                }

                aplicadas.Add(p.Id!);
                respuestasAplicadas.Add(p.Id!);
            }
            catch (ClimaNoDisponibleException e)
            {
                avisos.Add($"La respuesta a {p.Id} se aplicará en la siguiente ejecución: {e.Message}");
            }
        }

        // 2. Ciudades que el usuario indicó y no se conocen.
        config = config with { Ciudades = [.. config.Ciudades.Where(c => ciudades.All(x => x.Clave != c.Clave)), .. ciudades.Where(c => c.Estado != CiudadAprendida.Rechazada).Select(c => c.ACiudad())] };
        var plan = ObtencionDeClima.Planificar(almacen, config.ConNotas(notas));
        var preguntas = almacen.DeTipo("pregunta").ToList();
        bool HayPregunta(string clave, string tipo) => preguntas.Any(q => q.Frontmatter.GetValueOrDefault("ciudad_clave") as string == clave && q.Frontmatter.GetValueOrDefault("ciudad_tipo") as string == tipo);
        var sinReconocer = plan.SinReconocer.GroupBy(s => Ciudad.Normalizar(s.Ciudad)).ToList();
        var porPreguntar = new List<(string Clave, string Nombre, string? Registro, string Tipo, string Texto)>();
        foreach (var grupo in sinReconocer)
        {
            var clave = grupo.Key;
            var nombre = grupo.First().Ciudad;
            var registro = grupo.First().Registro;
            if (ciudades.Any(c => c.Clave == clave))
            {
                continue; // ya aprendida (o rechazada: espera la respuesta del usuario)
            }

            try
            {
                var candidatos = await geocodificador.BuscarAsync(nombre, ct);
                if (ElegirCiudad.Clara(candidatos) is { } clara)
                {
                    ciudades.Add(Nueva(clave, nombre, clara, CiudadAprendida.PorConfirmar, "geocodificada", ahora));
                    aprendidas.Add(nombre);
                }
                else
                {
                    var posibles = candidatos.Count == 0 ? "No la encuentro" : "Podría ser " + string.Join(", o ", candidatos.Take(3).Select(c => c.Descripcion));
                    porPreguntar.Add((clave, nombre, registro, "elegir", $"No tengo clara la ciudad «{nombre}». {posibles}. ¿Cuál es? Respóndeme con el nombre y la región o el país. Hasta entonces no consulto su clima."));
                }
            }
            catch (ClimaNoDisponibleException e)
            {
                avisos.Add($"No se pudo ubicar «{nombre}» por ahora ({e.Message}); se reintentará.");
            }
        }

        // 3. Preguntas: confirmar las ciudades aprendidas que aún no se han confirmado, elegir las dudosas y reintentos tras una corrección.
        foreach (var c in ciudades.Where(c => c.Estado == CiudadAprendida.PorConfirmar && c.Origen == "geocodificada" && !HayPregunta(c.Clave, "confirmar")))
        {
            var registro = RegistroDeLaCiudad(almacen, c.Clave);
            porPreguntar.Add((c.Clave, c.Nombre, registro, "confirmar",
                $"He buscado «{c.Nombre}» y creo que es {c.Descripcion}. Ya estoy usando su clima. ¿Es correcta? Responde «sí» o dime la ciudad correcta (por ejemplo «Lleida, Cataluña»)."));
        }

        foreach (var (clave, nombre, registro, texto) in reintentos)
        {
            if (!porPreguntar.Any(x => x.Clave == clave && x.Tipo == "elegir"))
            {
                porPreguntar.Add((clave, nombre, registro, "elegir", texto));
            }
        }

        // 4. Una nota sin ciudad que menciona otra ciudad conocida: se pregunta, no se cambia sola.
        porPreguntar.AddRange(Menciones(almacen, config, notas, preguntas, ahora));

        var pendientesDeMira = preguntas.Count(q => q.Frontmatter.GetValueOrDefault("estado") as string == "pendiente" && Analisis.PreguntasDeContraste.EsDeMira(q));
        var libres = Math.Min(cupoDePreguntas - pendientesDeMira, MaximoDePreguntasNuevas);
        var ids = preguntas.Select(q => q.Id).OfType<string>().ToHashSet();
        foreach (var (clave, nombre, registro, tipo, texto) in porPreguntar)
        {
            if (preguntasNuevas.Count >= libres || registro is null)
            {
                continue;
            }

            // Una sola pregunta pendiente por ciudad y tipo; nunca se repite una ya cerrada.
            // Si el usuario cerró una pregunta sobre esta ciudad sin contestarla (no relevante, no lo recuerdo…), no se insiste.
            if (preguntas.Any(q => q.Frontmatter.GetValueOrDefault("ciudad_clave") as string == clave && q.Frontmatter.GetValueOrDefault("ciudad_tipo") as string == tipo
                                   && q.Frontmatter.GetValueOrDefault("estado") as string is "no_relevante" or "no_recuerda" or "no_sucedio"))
            {
                continue;
            }

            if (tipo == "elegir"
                ? preguntas.Any(q => q.Frontmatter.GetValueOrDefault("ciudad_clave") as string == clave && q.Frontmatter.GetValueOrDefault("ciudad_tipo") as string == "elegir"
                                     && q.Frontmatter.GetValueOrDefault("estado") as string == "pendiente")
                : HayPregunta(clave, tipo))
            {
                continue;
            }

            preguntasNuevas.Add(Escribir(datos, ids, ahora, registro, tipo, clave, nombre, texto));
        }

        CiudadesAprendidasStore.Guardar(datos, new DatosDeCiudades(ciudades, notas, aplicadas));
        return (config.ConAprendidas(datos), new ResultadoDeCiudades(aprendidas, preguntasNuevas, respuestasAplicadas, avisos));
    }

    private static CiudadAprendida Nueva(string clave, string nombre, CandidatoDeCiudad c, string estado, string origen, DateTimeOffset ahora)
    {
        var ciudad = ElegirCiudad.ACiudad(nombre, c);
        return new CiudadAprendida(clave, nombre.Trim(), c.Descripcion, ciudad.Latitud, ciudad.Longitud, estado, origen, ahora);
    }

    /// <summary>Un registro de una captura que indicó esa ciudad: el que se enlaza desde la pregunta.</summary>
    private static string? RegistroDeLaCiudad(Almacen almacen, string clave) =>
        almacen.DeTipo("registro").Where(r => r.Id is not null)
            .Select(r => (Id: r.Id!, Captura: (r.Frontmatter.GetValueOrDefault("fuentes") as List<object?> ?? []).OfType<Dictionary<string, object?>>().FirstOrDefault()?.GetValueOrDefault("captura") as string))
            .Where(x => x.Captura is not null && almacen.Documentos.FirstOrDefault(d => d.Id == x.Captura)?.Frontmatter.GetValueOrDefault("ciudad") is string ciu && Ciudad.Normalizar(ciu) == clave)
            .Select(x => x.Id).FirstOrDefault();

    private static IEnumerable<(string Clave, string Nombre, string? Registro, string Tipo, string Texto)> Menciones(
        Almacen almacen, ConfiguracionDeClima config, IReadOnlyList<NotaConCiudad> notas, IReadOnlyList<Orumi.Core.Ficheros.Documento> preguntas, DateTimeOffset ahora)
    {
        var porDefecto = config.PorDefecto;
        var hoy = DateOnly.FromDateTime(ahora.DateTime);
        var registrosPorCaptura = almacen.DeTipo("registro").Where(r => r.Id is not null)
            .Select(r => (Id: r.Id!, Captura: (r.Frontmatter.GetValueOrDefault("fuentes") as List<object?> ?? []).OfType<Dictionary<string, object?>>().FirstOrDefault()?.GetValueOrDefault("captura") as string))
            .Where(x => x.Captura is not null).GroupBy(x => x.Captura!).ToDictionary(g => g.Key, g => g.First().Id);
        foreach (var c in almacen.DeTipo("captura").Where(c => c.Id is not null && !c.Frontmatter.ContainsKey("ciudad") && !notas.Any(n => n.Captura == c.Id)))
        {
            if (!DateOnly.TryParseExact((c.Frontmatter.GetValueOrDefault("comunicada_el") as string ?? "")[..Math.Min(10, (c.Frontmatter.GetValueOrDefault("comunicada_el") as string ?? "").Length)], "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha) || hoy.DayNumber - fecha.DayNumber > 45 || !registrosPorCaptura.TryGetValue(c.Id!, out var registro))
            {
                continue;
            }

            var texto = Orumi.Core.Texto.Normalizacion.SinAcentos(c.Cuerpo).ToLowerInvariant();
            foreach (var ciudad in config.Ciudades.Where(x => x.Clave != porDefecto.Clave))
            {
                var nombre = Orumi.Core.Texto.Normalizacion.SinAcentos(ciudad.Nombre).ToLowerInvariant();
                if (!Regex.IsMatch(texto, @"\b" + Regex.Escape(nombre) + @"\b"))
                {
                    continue;
                }

                var clave = ciudad.Clave + "#" + c.Id;
                if (preguntas.Any(q => q.Frontmatter.GetValueOrDefault("ciudad_clave") as string == clave && q.Frontmatter.GetValueOrDefault("ciudad_tipo") as string == "mencion"))
                {
                    continue;
                }

                yield return (clave, ciudad.Nombre, registro, "mencion",
                    $"En tu nota del {fecha.ToString("d 'de' MMMM", CultureInfo.GetCultureInfo("es-ES"))} aparece «{ciudad.Nombre}», pero la nota no indica ciudad y se ha tomado como {porDefecto.Nombre}. ¿Estabas en {ciudad.Nombre}? Responde «sí» o «no».");
            }
        }
    }

    private static string Escribir(string datos, HashSet<string> ids, DateTimeOffset ahora, string registro, string tipo, string clave, string nombre, string texto)
    {
        string id;
        do
        {
            id = $"preg-{ahora:yyyyMMdd}-{Identificadores.Sufijo(4)}";
        }
        while (!ids.Add(id));

        var fm = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["tipo"] = "pregunta",
            ["registro"] = registro,
            ["otros_registros"] = new List<object?>(),
            ["campo"] = "ciudad",
            ["clase_pregunta"] = "ciudad:" + tipo,
            ["ciudad_tipo"] = tipo,
            ["ciudad_clave"] = clave,
            ["ciudad_nombre"] = nombre,
            ["texto"] = texto,
            ["estado"] = "pendiente",
            ["valor"] = "media",
            ["creada_el"] = ahora.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            ["generada_por"] = new Dictionary<string, object?> { ["agente"] = "Mira", ["version"] = "0.1.0-mvp", ["modelo"] = "determinista" },
        };
        if (tipo == "mencion")
        {
            fm["ciudad_captura"] = clave[(clave.IndexOf('#') + 1)..];
        }

        var ruta = Path.Combine(datos, Identificadores.RutaRelativa(id)!);
        EscrituraAtomica.EscribirNuevo(ruta, FrontmatterCanonico.Escribir(fm, $"Pregunta de ciudad de Mira sobre [[{registro}]].\n", "pregunta"));
        return id;
    }
}
