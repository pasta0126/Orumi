using System.Globalization;
using Orumi.Core.Ficheros;
using Orumi.Core.Validacion;

namespace Orumi.Core.Correcciones;

public sealed record ResultadoOperacion(string Mensaje, IReadOnlyList<string> Ficheros);

/// <summary>
/// Respuestas, correcciones y rechazos del usuario sobre los registros. Todo se valida antes de escribir; si algo falla,
/// no se modifica ningún fichero. Las capturas nunca se tocan. El bloqueo frente a Mira lo toma quien llama.
/// </summary>
public sealed class Operaciones(string datos, Func<DateTimeOffset> ahora)
{
    private string Instante => ahora().ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ corregir

    public ResultadoOperacion Corregir(string registroId, string campo, string texto)
    {
        var almacen = Almacen.Cargar(datos);
        var registro = Buscar(almacen, registroId, "registro");
        var fm = Clonar(registro.Frontmatter);
        AplicarCorreccion(fm, campo, texto, null);
        return Guardar(almacen, [registro with { Frontmatter = fm }], $"«{campo}» de {registroId} corregido y confirmado.");
    }

    private void AplicarCorreccion(Dictionary<string, object?> fm, string campo, string texto, string? pregunta)
    {
        if (!ValoresDeCampo.EsCorregible(campo))
        {
            throw new OperacionInvalidaException($"El campo «{campo}» no se puede corregir. Campos corregibles: {string.Join(", ", ValoresDeCampo.Corregibles)}.");
        }

        var anterior = fm.GetValueOrDefault(campo) ?? "desconocido";
        var nuevo = ValoresDeCampo.Analizar(campo, texto, anterior);
        if (campo == "resultados" && anterior is List<object?> && nuevo is string s)
        {
            nuevo = new List<object?> { s }; // se conserva la forma que tenía el campo
        }

        fm[campo] = nuevo;
        var certeza = Mapa(fm, "certeza");
        if (campo == "entidades")
        {
            certeza.Remove("entidades");
            foreach (var e in (List<object?>)nuevo)
            {
                ((Dictionary<string, object?>)e!)["certeza"] = "confirmado";
            }
        }
        else
        {
            certeza[campo] = "confirmado";
        }

        Historial(fm, campo, anterior, nuevo, pregunta);
    }

    // ------------------------------------------------------------------ rechazar

    public ResultadoOperacion Rechazar(string registroId, string campo)
    {
        var almacen = Almacen.Cargar(datos);
        var registro = Buscar(almacen, registroId, "registro");
        var fm = Clonar(registro.Frontmatter);
        if (!ValoresDeCampo.EsCorregible(campo) || campo == "entidades" || campo == "titulo")
        {
            throw new OperacionInvalidaException($"El campo «{campo}» no admite rechazo (solo interpretaciones de fecha, hora, duración y listas).");
        }

        var actual = fm.GetValueOrDefault(campo);
        var certeza = Mapa(fm, "certeza");
        var original = certeza.GetValueOrDefault(campo) as string;
        if (actual is null or "desconocido" || original is not ("interpretado" or "hipotesis"))
        {
            throw new OperacionInvalidaException($"«{campo}» de {registroId} no es una interpretación de Mira (certeza: {original ?? "ninguna"}); no hay nada que rechazar. Si quieres cambiarlo, usa «corregir».");
        }

        var rechazos = Lista(fm, "rechazos");
        rechazos.Add(new Dictionary<string, object?>
        {
            ["campo"] = campo,
            ["valor"] = ValoresDeCampo.Resumen(actual),
            ["certeza_original"] = original,
            ["rechazada_el"] = Instante,
        });
        fm[campo] = "desconocido";
        certeza.Remove(campo);
        Historial(fm, campo, actual, "desconocido", null);
        return Guardar(almacen, [registro with { Frontmatter = fm }], $"Interpretación de «{campo}» de {registroId} rechazada; el campo queda desconocido.");
    }

    // ------------------------------------------------------------------ responder

    public ResultadoOperacion CerrarSinDato(string preguntaId, string estado)
    {
        var almacen = Almacen.Cargar(datos);
        var pregunta = Buscar(almacen, preguntaId, "pregunta");
        var fm = Clonar(pregunta.Frontmatter);
        ExigirPendiente(fm, preguntaId);
        Cerrar(fm, estado, estado == "no_recuerda" ? "No lo recuerdo." : "No es relevante.");
        return Guardar(almacen, [pregunta with { Frontmatter = fm }], $"Pregunta {preguntaId} cerrada como «{estado}»; el registro no cambia y Mira no volverá a preguntarlo.");
    }

    public ResultadoOperacion Responder(string preguntaId, string texto, string? registroDestino = null)
    {
        var almacen = Almacen.Cargar(datos);
        var pregunta = Buscar(almacen, preguntaId, "pregunta");
        var pfm = Clonar(pregunta.Frontmatter);
        ExigirPendiente(pfm, preguntaId);

        var campo = pfm.GetValueOrDefault("campo") as string ?? string.Empty;
        var principal = pfm.GetValueOrDefault("registro") as string ?? string.Empty;
        var otros = (pfm.GetValueOrDefault("otros_registros") as List<object?> ?? []).OfType<string>().ToList();
        var cambios = new List<Documento>();
        string mensaje;

        if (campo == "coincidencia")
        {
            var t = texto.Trim().ToLowerInvariant();
            if (t is not ("misma" or "distinta"))
            {
                throw new OperacionInvalidaException("Esta pregunta es una coincidencia dudosa: responde «misma» o «distinta».");
            }

            mensaje = $"Pregunta {preguntaId} respondida: los registros son {t}s.";
            if (t == "misma")
            {
                var origen = Buscar(almacen, principal, "registro");
                var destino = otros.FirstOrDefault() ?? throw new OperacionInvalidaException("La pregunta no indica el otro registro.");
                Buscar(almacen, destino, "registro");
                var fm = Clonar(origen.Frontmatter);
                var relaciones = Lista(fm, "relaciones");
                if (!relaciones.OfType<Dictionary<string, object?>>().Any(r => r.GetValueOrDefault("destino") as string == destino))
                {
                    relaciones.Add(new Dictionary<string, object?> { ["tipo"] = "relacionado_con", ["destino"] = destino, ["certeza"] = "confirmado" });
                }

                cambios.Add(origen with { Frontmatter = fm });
                mensaje += " Quedan relacionados (no se fusionan).";
            }
        }
        else
        {
            var destino = registroDestino ?? principal;
            var implicados = new[] { principal }.Concat(otros).ToList();
            if (!implicados.Contains(destino))
            {
                throw new OperacionInvalidaException($"El registro {destino} no es de esta pregunta. Indica uno de: {string.Join(", ", implicados)}.");
            }

            var reg = Buscar(almacen, destino, "registro");
            var fm = Clonar(reg.Frontmatter);
            AplicarCorreccion(fm, campo, texto, preguntaId);
            var cambiosPorRuta = new Dictionary<string, Documento> { [reg.Ruta] = reg with { Frontmatter = fm } };

            // conflicto: se marca como resuelto en todos los registros que lo llevan
            foreach (var doc in almacen.DeTipo("registro"))
            {
                var actual = cambiosPorRuta.GetValueOrDefault(doc.Ruta) ?? doc;
                if (actual.Frontmatter.GetValueOrDefault("conflictos") is not List<object?> conflictos
                    || !conflictos.OfType<Dictionary<string, object?>>().Any(c => c.GetValueOrDefault("pregunta") as string == preguntaId))
                {
                    continue;
                }

                var copia = Clonar(actual.Frontmatter);
                foreach (var c in ((List<object?>)copia["conflictos"]!).OfType<Dictionary<string, object?>>()
                             .Where(c => c.GetValueOrDefault("pregunta") as string == preguntaId))
                {
                    c["estado"] = "resuelto";
                }

                cambiosPorRuta[doc.Ruta] = actual with { Frontmatter = copia };
            }

            cambios.AddRange(cambiosPorRuta.Values);
            mensaje = $"«{campo}» de {destino} confirmado y pregunta {preguntaId} respondida.";
            if (cambiosPorRuta.Count > 1)
            {
                mensaje += " El conflicto queda resuelto; la otra versión no se ha cambiado (puedes corregirla con «corregir»).";
            }
        }

        Cerrar(pfm, "respondida", texto.Trim());
        cambios.Add(pregunta with { Frontmatter = pfm });
        return Guardar(almacen, cambios, mensaje);
    }

    private void Cerrar(Dictionary<string, object?> pfm, string estado, string respuesta)
    {
        pfm["estado"] = estado;
        pfm["cerrada_el"] = Instante;
        pfm["respuesta"] = new Dictionary<string, object?> { ["texto"] = respuesta, ["respondida_el"] = Instante };
    }

    private static void ExigirPendiente(Dictionary<string, object?> pfm, string id)
    {
        var estado = pfm.GetValueOrDefault("estado") as string;
        if (estado != "pendiente")
        {
            throw new OperacionInvalidaException($"La pregunta {id} ya está cerrada ({estado}).");
        }
    }

    // ------------------------------------------------------------------ utilidades

    private void Historial(Dictionary<string, object?> fm, string campo, object? anterior, object? nuevo, string? pregunta)
    {
        var entrada = new Dictionary<string, object?>
        {
            ["fecha"] = Instante,
            ["origen"] = "usuario",
            ["campo"] = campo,
            ["anterior"] = anterior,
            ["nuevo"] = nuevo,
        };
        if (pregunta is not null)
        {
            entrada["pregunta"] = pregunta;
        }

        Lista(fm, "historial").Add(entrada);
    }

    private static Documento Buscar(Almacen almacen, string id, string tipo) =>
        almacen.DeTipo(tipo).FirstOrDefault(d => d.Id == id)
        ?? throw new OperacionInvalidaException($"No existe ningún{(tipo == "pregunta" ? "a" : string.Empty)} {tipo} con el identificador «{id}».");

    private static Dictionary<string, object?> Mapa(Dictionary<string, object?> fm, string clave)
    {
        if (fm.GetValueOrDefault(clave) is not Dictionary<string, object?> m)
        {
            m = new Dictionary<string, object?>();
            fm[clave] = m;
        }

        return m;
    }

    private static List<object?> Lista(Dictionary<string, object?> fm, string clave)
    {
        if (fm.GetValueOrDefault(clave) is not List<object?> l)
        {
            l = [];
            fm[clave] = l;
        }

        return l;
    }

    private static Dictionary<string, object?> Clonar(Dictionary<string, object?> origen) =>
        origen.ToDictionary(p => p.Key, p => ClonarValor(p.Value));

    private static object? ClonarValor(object? v) => v switch
    {
        Dictionary<string, object?> m => Clonar(m),
        List<object?> l => l.Select(ClonarValor).ToList(),
        _ => v,
    };

    /// <summary>Valida el almacén resultante: si aparece algún error nuevo, no se escribe nada.</summary>
    private ResultadoOperacion Guardar(Almacen almacen, List<Documento> cambios, string mensaje)
    {
        var antes = Validador.Validar(almacen).Select(i => i.ToString()).ToHashSet();
        var propuesto = almacen.Con(cambios);
        var nuevas = Validador.Validar(propuesto).Select(i => i.ToString()).Where(i => !antes.Contains(i)).ToList();
        if (nuevas.Count > 0)
        {
            throw new OperacionInvalidaException("El cambio dejaría los ficheros inválidos y no se ha aplicado:\n  " + string.Join("\n  ", nuevas));
        }

        var rutas = new List<string>();
        foreach (var d in cambios)
        {
            var contenido = FrontmatterCanonico.Escribir(d.Frontmatter, d.Cuerpo, d.Tipo!);
            EscrituraAtomica.Reemplazar(Path.Combine(almacen.Raiz, d.Ruta), contenido);
            rutas.Add(d.Ruta);
        }

        return new ResultadoOperacion(mensaje, rutas);
    }
}
