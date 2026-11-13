using System.Globalization;
using Orumi.Core.Correcciones;
using Orumi.Core.Ficheros;
using Orumi.Core.Texto;

namespace Orumi.Panel;

public sealed record FiltroDeRegistros(
    string? Q = null, string? Desde = null, string? Hasta = null, string? Entidad = null, string? Proyecto = null,
    string? Clase = null, string? Certeza = null, string? Campo = null, int Pagina = 1, int Limite = 50, bool Ascendente = false);

public sealed record RegistroResumen(
    string Id, string Titulo, string? Clase, string? Desde, string? Hasta, string? Hora, string? CertezaFecha, bool EnConflicto);

public sealed record PaginaDeRegistros(IReadOnlyList<RegistroResumen> Registros, int Total, int Pagina, int Limite);

public sealed record DatoDeFicha(string Campo, object? Valor, string Texto, bool Desconocido, string? Certeza);

public sealed class SolicitudInvalidaException(string mensaje) : Exception(mensaje);

public sealed class NoEncontradoException(string mensaje) : Exception(mensaje);

/// <summary>Consultas de solo lectura sobre los ficheros. Valida todos los parámetros antes de usarlos.</summary>
public sealed class Lectura(RepositorioDeDatos repositorio)
{
    public const int LimiteMaximo = 100;
    public const int LongitudMaximaDeBusqueda = 200;
    public const int DiasMaximosDeRango = 366;

    private static readonly string[] CamposDeFicha =
        ["fecha", "hora", "duracion", "personas", "entidades", "lugares", "proyectos", "herramientas", "contexto", "acciones", "resultados", "problemas", "ideas"];

    // ---------------------------------------------------------------- estado

    public object Estado()
    {
        var a = repositorio.Actual();
        var procesadas = a.DeTipo("lote").SelectMany(l => (l.Frontmatter.GetValueOrDefault("capturas") as List<object?> ?? []).OfType<Dictionary<string, object?>>())
            .Select(c => c.GetValueOrDefault("captura") as string).Where(c => c is not null).ToHashSet();
        var capturas = a.DeTipo("captura").ToList();
        return new
        {
            capturas = capturas.Count,
            pendientesDeProcesar = capturas.Count(c => !procesadas.Contains(c.Id)),
            registros = a.DeTipo("registro").Count(),
            preguntasPendientes = a.DeTipo("pregunta").Count(p => p.Frontmatter.GetValueOrDefault("estado") as string == "pendiente"),
            ultimoLote = a.DeTipo("lote").Select(l => l.Id).OrderBy(i => i, StringComparer.Ordinal).LastOrDefault(),
        };
    }

    // ---------------------------------------------------------------- días

    public object Dias(string? desde, string? hasta)
    {
        var d = Fecha(desde, "desde") ?? throw new SolicitudInvalidaException("Falta «desde» (AAAA-MM-DD).");
        var h = Fecha(hasta, "hasta") ?? throw new SolicitudInvalidaException("Falta «hasta» (AAAA-MM-DD).");
        if (h < d || h.DayNumber - d.DayNumber > DiasMaximosDeRango)
        {
            throw new SolicitudInvalidaException($"El rango debe ser válido y de como máximo {DiasMaximosDeRango} días.");
        }

        var cuenta = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var sinFecha = 0;
        foreach (var r in repositorio.Actual().DeTipo("registro"))
        {
            var (rd, rh) = Rango(r);
            if (rd is null)
            {
                sinFecha++;
                continue;
            }

            // Un registro de varios días cuenta en cada día (con tope, para que un rango absurdo no se dispare).
            var fin = DateOnly.FromDayNumber(Math.Min(rh!.Value.DayNumber, rd.Value.DayNumber + 31));
            for (var dia = rd.Value; dia <= fin; dia = dia.AddDays(1))
            {
                if (dia >= d && dia <= h)
                {
                    var k = dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    cuenta[k] = cuenta.GetValueOrDefault(k) + 1;
                }
            }
        }

        return new { dias = cuenta.Select(x => new { fecha = x.Key, total = x.Value }).ToList(), sinFecha };
    }

    // ---------------------------------------------------------------- registros

    public PaginaDeRegistros Registros(FiltroDeRegistros f)
    {
        Validar(f);
        var desde = Fecha(f.Desde, "desde");
        var hasta = Fecha(f.Hasta, "hasta");
        var q = f.Q is { Length: > 0 } ? Normalizacion.SinAcentos(f.Q.Trim().ToLowerInvariant()) : null;
        var entidad = f.Entidad is { Length: > 0 } ? Normalizacion.SinAcentos(f.Entidad.Trim().ToLowerInvariant()) : null;
        var proyecto = f.Proyecto is { Length: > 0 } ? Normalizacion.SinAcentos(f.Proyecto.Trim().ToLowerInvariant()) : null;

        var filtrados = repositorio.Actual().DeTipo("registro").Where(r => r.Id is not null).Where(r =>
        {
            var (rd, rh) = Rango(r);
            if ((desde is not null || hasta is not null) && (rd is null || (desde is not null && rh < desde) || (hasta is not null && rd > hasta)))
            {
                return false;
            }

            var fm = r.Frontmatter;
            if (f.Clase is { Length: > 0 } && fm.GetValueOrDefault("clase") as string != f.Clase)
            {
                return false;
            }

            if (entidad is not null && !Lista(fm, "entidades").OfType<Dictionary<string, object?>>()
                    .Any(e => Normalizar(e.GetValueOrDefault("nombre") as string) == entidad))
            {
                return false;
            }

            if (proyecto is not null && !Lista(fm, "proyectos").OfType<string>().Any(p => Normalizar(p) == proyecto))
            {
                return false;
            }

            if (f.Certeza is { Length: > 0 } && !TieneCerteza(fm, f.Certeza, f.Campo))
            {
                return false;
            }

            return q is null || TextoBuscable(r).Contains(q, StringComparison.Ordinal);
        }).ToList();

        var ordenados = (f.Ascendente
                ? filtrados.OrderBy(r => Rango(r).Desde ?? DateOnly.MaxValue).ThenBy(Hora, StringComparer.Ordinal)
                : filtrados.OrderByDescending(r => Rango(r).Desde ?? DateOnly.MinValue).ThenByDescending(Hora, StringComparer.Ordinal))
            .ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
        var pagina = ordenados.Skip((f.Pagina - 1) * f.Limite).Take(f.Limite).Select(Resumen).ToList();
        return new PaginaDeRegistros(pagina, ordenados.Count, f.Pagina, f.Limite);
    }

    // ---------------------------------------------------------------- ficha

    public object Ficha(string id)
    {
        var almacen = repositorio.Actual();
        var r = Registro(almacen, id);
        var fm = r.Frontmatter;
        var certeza = fm.GetValueOrDefault("certeza") as Dictionary<string, object?> ?? [];

        var datos = new List<DatoDeFicha>();
        foreach (var campo in CamposDeFicha.Where(fm.ContainsKey))
        {
            var valor = fm[campo];
            var desconocido = valor is null || valor as string == "desconocido";
            var c = certeza.GetValueOrDefault(campo) as string;
            if (c is null && campo == "entidades" && valor is List<object?> entidades)
            {
                // Cada entidad lleva su certeza: si todas coinciden, es la del campo.
                var propias = entidades.OfType<Dictionary<string, object?>>().Select(e => e.GetValueOrDefault("certeza") as string).Distinct().ToList();
                c = propias.Count == 1 ? propias[0] : null;
            }

            datos.Add(new DatoDeFicha(campo, valor, desconocido ? "desconocido" : ValoresDeCampo.Resumen(valor), desconocido, c));
        }

        var capturas = new Dictionary<string, object>();
        var fuentes = new List<object>();
        foreach (var fu in Lista(fm, "fuentes").OfType<Dictionary<string, object?>>())
        {
            var cid = fu.GetValueOrDefault("captura") as string;
            var fr = fu.GetValueOrDefault("fragmento") as Dictionary<string, object?>;
            fuentes.Add(new { captura = cid, rol = fu.GetValueOrDefault("rol"), cita = fr?.GetValueOrDefault("cita"), desde = fr?.GetValueOrDefault("desde"), hasta = fr?.GetValueOrDefault("hasta") });
            if (cid is not null && !capturas.ContainsKey(cid) && almacen.DeTipo("captura").FirstOrDefault(c => c.Id == cid) is { } c)
            {
                capturas[cid] = CapturaComoObjeto(c);
            }
        }

        var titulos = almacen.DeTipo("registro").Where(x => x.Id is not null).ToDictionary(x => x.Id!, x => x.Frontmatter.GetValueOrDefault("titulo") as string);
        var relaciones = Lista(fm, "relaciones").OfType<Dictionary<string, object?>>().Select(x =>
            new { tipo = x.GetValueOrDefault("tipo"), destino = x.GetValueOrDefault("destino"), titulo = titulos.GetValueOrDefault(x.GetValueOrDefault("destino") as string ?? string.Empty), certeza = x.GetValueOrDefault("certeza") }).ToList();
        var conflictos = Lista(fm, "conflictos").OfType<Dictionary<string, object?>>().Select(x =>
            new { campo = x.GetValueOrDefault("campo"), con = x.GetValueOrDefault("con"), titulo = titulos.GetValueOrDefault(x.GetValueOrDefault("con") as string ?? string.Empty), pregunta = x.GetValueOrDefault("pregunta"), estado = x.GetValueOrDefault("estado") }).ToList();

        return new
        {
            id = r.Id,
            clase = fm.GetValueOrDefault("clase"),
            titulo = fm.GetValueOrDefault("titulo"),
            comunicadaEl = fm.GetValueOrDefault("comunicada_el"),
            descripcion = r.Cuerpo.Split("\n## ", 2)[0].Trim(),
            datos,
            multidia = fm.GetValueOrDefault("multidia"),
            fuentes,
            capturas = capturas.Values.ToList(),
            entradas = Lista(fm, "entradas"),
            relaciones,
            conflictos,
            rechazos = Lista(fm, "rechazos"),
            historial = Lista(fm, "historial"),
            procedencia = fm.GetValueOrDefault("procedencia"),
        };
    }

    // ---------------------------------------------------------------- capturas (solo lectura)

    public object Captura(string id)
    {
        if (!Identificadores.Captura.IsMatch(id))
        {
            throw new SolicitudInvalidaException("Identificador de captura no válido.");
        }

        var c = repositorio.Actual().DeTipo("captura").FirstOrDefault(x => x.Id == id) ?? throw new NoEncontradoException("No existe esa captura.");
        return CapturaComoObjeto(c);
    }

    private static object CapturaComoObjeto(Documento c) => new
    {
        id = c.Id,
        comunicadaEl = c.Frontmatter.GetValueOrDefault("comunicada_el"),
        origen = c.Frontmatter.GetValueOrDefault("origen"),
        fechaAcontecimiento = c.Frontmatter.GetValueOrDefault("fecha_acontecimiento"),
        etiqueta = c.Frontmatter.GetValueOrDefault("etiqueta"),
        texto = c.Cuerpo,
    };

    // ---------------------------------------------------------------- preguntas

    public IReadOnlyList<PreguntaConContexto> PreguntasPendientes() => PreguntasConContexto.Pendientes(repositorio.Actual());

    // ---------------------------------------------------------------- utilidades

    private Documento Registro(Almacen almacen, string id)
    {
        if (!Identificadores.Registro.IsMatch(id))
        {
            throw new SolicitudInvalidaException("Identificador de registro no válido.");
        }

        return almacen.DeTipo("registro").FirstOrDefault(x => x.Id == id) ?? throw new NoEncontradoException("No existe ese registro.");
    }

    private static void Validar(FiltroDeRegistros f)
    {
        foreach (var (nombre, valor) in new[] { ("q", f.Q), ("entidad", f.Entidad), ("proyecto", f.Proyecto), ("clase", f.Clase), ("certeza", f.Certeza), ("campo", f.Campo) })
        {
            if (valor is { Length: > LongitudMaximaDeBusqueda })
            {
                throw new SolicitudInvalidaException($"«{nombre}» es demasiado largo (máximo {LongitudMaximaDeBusqueda} caracteres).");
            }
        }

        if (f.Pagina < 1 || f.Limite < 1 || f.Limite > LimiteMaximo)
        {
            throw new SolicitudInvalidaException($"«pagina» debe ser ≥ 1 y «limite» estar entre 1 y {LimiteMaximo}.");
        }

        if (f.Certeza is { Length: > 0 } && f.Certeza is not ("dicho" or "confirmado" or "interpretado" or "hipotesis" or "recomendacion"))
        {
            throw new SolicitudInvalidaException("«certeza» debe ser dicho, confirmado, interpretado, hipotesis o recomendacion.");
        }
    }

    private static DateOnly? Fecha(string? texto, string nombre)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        return DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : throw new SolicitudInvalidaException($"«{nombre}» debe tener la forma AAAA-MM-DD.");
    }

    private static (DateOnly? Desde, DateOnly? Hasta) Rango(Documento r)
    {
        if (r.Frontmatter.GetValueOrDefault("fecha") is Dictionary<string, object?> f
            && DateOnly.TryParseExact(f.GetValueOrDefault("desde") as string, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            && DateOnly.TryParseExact(f.GetValueOrDefault("hasta") as string, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var h))
        {
            return (d, h);
        }

        return (null, null);
    }

    private static string Hora(Documento r) =>
        r.Frontmatter.GetValueOrDefault("hora") is Dictionary<string, object?> h && h.GetValueOrDefault("valor") is string v ? v : "99:99"; // sin hora conocida, al final

    private static List<object?> Lista(Dictionary<string, object?> fm, string clave) => fm.GetValueOrDefault(clave) as List<object?> ?? [];

    private static string Normalizar(string? s) => Normalizacion.SinAcentos((s ?? string.Empty).Trim().ToLowerInvariant());

    private static bool TieneCerteza(Dictionary<string, object?> fm, string certeza, string? campo)
    {
        var mapa = fm.GetValueOrDefault("certeza") as Dictionary<string, object?> ?? [];
        if (campo is { Length: > 0 })
        {
            return mapa.GetValueOrDefault(campo) as string == certeza
                   || campo == "entidades" && Lista(fm, "entidades").OfType<Dictionary<string, object?>>().Any(e => e.GetValueOrDefault("certeza") as string == certeza);
        }

        return mapa.Values.OfType<string>().Any(v => v == certeza)
               || Lista(fm, "entidades").OfType<Dictionary<string, object?>>().Any(e => e.GetValueOrDefault("certeza") as string == certeza);
    }

    private static string TextoBuscable(Documento r)
    {
        var fm = r.Frontmatter;
        var partes = new List<string> { fm.GetValueOrDefault("titulo") as string ?? string.Empty, r.Cuerpo.Split("\n## ", 2)[0] };
        foreach (var campo in CamposDeFicha.Where(c => c != "fecha" && c != "hora" && c != "duracion"))
        {
            if (fm.TryGetValue(campo, out var v))
            {
                partes.Add(ValoresDeCampo.Resumen(v));
            }
        }

        return Normalizar(string.Join(' ', partes));
    }

    private static RegistroResumen Resumen(Documento r)
    {
        var fm = r.Frontmatter;
        var (d, h) = Rango(r);
        var hora = fm.GetValueOrDefault("hora") is Dictionary<string, object?> hh ? hh.GetValueOrDefault("valor") as string : null;
        var certeza = fm.GetValueOrDefault("certeza") as Dictionary<string, object?>;
        return new RegistroResumen(
            r.Id!, fm.GetValueOrDefault("titulo") as string ?? string.Empty, fm.GetValueOrDefault("clase") as string,
            d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), h?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), hora,
            certeza?.GetValueOrDefault("fecha") as string,
            Lista(fm, "conflictos").OfType<Dictionary<string, object?>>().Any(c => c.GetValueOrDefault("estado") as string == "abierto"));
    }
}
