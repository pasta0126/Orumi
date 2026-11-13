using System.Globalization;
using System.Text.RegularExpressions;
using Orumi.Core.Ficheros;

namespace Orumi.Core.Correcciones;

/// <summary>
/// Gestión de registros: deshacer, eliminar, restaurar, fusionar y separar. Eliminar y fusionar dejan una «tumba» (el fichero y su clave
/// se conservan) para que sean reversibles y Mira no recree lo hecho. Las capturas nunca se tocan.
/// </summary>
public sealed partial class Operaciones
{
    private static readonly string[] Estructurales = ["estado", "fuentes", "fusion", "separados", "conflictos", "relaciones"];
    private static readonly string[] CamposQueSeFusionan = ["hora", "duracion", "personas", "entidades", "lugares", "proyectos", "herramientas", "contexto", "acciones", "resultados", "problemas", "ideas"];

    /// <summary>Cambios en memoria sobre varios documentos; se validan juntos y se escriben todos o ninguno.</summary>
    private sealed class Trabajo(Almacen almacen)
    {
        private readonly Dictionary<string, Documento> _cambios = [];

        public List<Documento> Cambios => [.. _cambios.Values];

        public Documento? Buscar(string id, string tipo) =>
            _cambios.Values.FirstOrDefault(d => d.Id == id && d.Tipo == tipo) ?? almacen.DeTipo(tipo).FirstOrDefault(d => d.Id == id);

        public IEnumerable<Documento> Todos(string tipo) =>
            almacen.DeTipo(tipo).Select(d => _cambios.GetValueOrDefault(d.Ruta) ?? d)
                .Concat(_cambios.Values.Where(d => d.Tipo == tipo && !almacen.Documentos.Any(x => x.Ruta == d.Ruta)));

        /// <summary>Modifica un documento (la última versión que haya): <paramref name="f"/> recibe una copia y devuelve el cuerpo nuevo o null.</summary>
        public void Mutar(string id, string tipo, Func<Dictionary<string, object?>, string, string?> f)
        {
            var doc = Buscar(id, tipo) ?? throw new OperacionInvalidaException($"No existe ningún {tipo} con el identificador «{id}».");
            var fm = Clonar(doc.Frontmatter);
            var cuerpo = f(fm, doc.Cuerpo) ?? doc.Cuerpo;
            _cambios[doc.Ruta] = doc with { Frontmatter = fm, Cuerpo = cuerpo };
        }

        public void Mutar(string id, string tipo, Action<Dictionary<string, object?>> f) => Mutar(id, tipo, (fm, _) => { f(fm); return null; });

        public void Nuevo(Documento d) => _cambios[d.Ruta] = d;
    }

    private static void ExigirActivo(Documento registro, string accion)
    {
        if (registro.Frontmatter.GetValueOrDefault("estado") is string estado)
        {
            throw new OperacionInvalidaException($"No se puede {accion} el registro {registro.Id}: está {estado}.");
        }
    }

    private void Anotar(Dictionary<string, object?> fm, string campo, object? anterior, object? nuevo, Action<Dictionary<string, object?>>? extra = null)
    {
        var e = new Dictionary<string, object?> { ["fecha"] = Instante, ["origen"] = "usuario", ["campo"] = campo, ["anterior"] = anterior, ["nuevo"] = nuevo };
        extra?.Invoke(e);
        Lista(fm, "historial").Add(e);
    }

    // ------------------------------------------------------------------ deshacer

    public ResultadoOperacion Deshacer(string registroId, string? campo = null)
    {
        var almacen = Almacen.Cargar(datos);
        var registro = Buscar(almacen, registroId, "registro");
        ExigirActivo(registro, "deshacer cambios en");
        if (campo is not null && !ValoresDeCampo.EsCorregible(campo))
        {
            throw new OperacionInvalidaException($"El campo «{campo}» no admite correcciones, así que no hay nada que deshacer. Campos: {string.Join(", ", ValoresDeCampo.Corregibles)}.");
        }

        var fm = Clonar(registro.Frontmatter);
        var historial = Lista(fm, "historial").OfType<Dictionary<string, object?>>().ToList();
        Dictionary<string, object?>? objetivo = null;
        var indiceObjetivo = -1;
        for (var i = historial.Count - 1; i >= 0 && objetivo is null; i--)
        {
            var h = historial[i];
            var c = h.GetValueOrDefault("campo") as string;
            if (h.GetValueOrDefault("origen") as string != "usuario" || h.ContainsKey("deshace") || c is null || Estructurales.Contains(c) || (campo is not null && c != campo))
            {
                continue;
            }

            var indice = i.ToString(CultureInfo.InvariantCulture);
            var yaDeshecha = historial.Skip(i + 1).Any(x => x.GetValueOrDefault("deshace") as string == indice && x.GetValueOrDefault("campo") as string == c);
            if (!yaDeshecha)
            {
                objetivo = h;
                indiceObjetivo = i;
            }
        }

        if (objetivo is null)
        {
            throw new OperacionInvalidaException($"No hay ninguna corrección o rechazo suyo{(campo is null ? string.Empty : $" sobre «{campo}»")} en {registroId} que deshacer.");
        }

        var nombre = (string)objetivo["campo"]!;
        var anterior = ClonarValor(objetivo.GetValueOrDefault("anterior")) ?? "desconocido";
        var actual = fm.GetValueOrDefault(nombre);
        var certezaActual = CertezaDe(fm, nombre);
        var mapa = Mapa(fm, "certeza");

        // Si era un rechazo (el campo quedó desconocido y el valor está en rechazos), el valor sale de los rechazos.
        if (objetivo.GetValueOrDefault("nuevo") as string == "desconocido")
        {
            var rechazos = Lista(fm, "rechazos");
            var valor = ValoresDeCampo.Resumen(anterior);
            var rechazo = rechazos.OfType<Dictionary<string, object?>>().LastOrDefault(r => r.GetValueOrDefault("campo") as string == nombre && r.GetValueOrDefault("valor") as string == valor);
            if (rechazo is not null)
            {
                rechazos.Remove(rechazo);
            }
        }

        fm[nombre] = anterior;
        string? aviso = null;
        var certezaAnterior = objetivo.GetValueOrDefault("certeza_anterior") as string;
        if (anterior as string == "desconocido" || nombre == "titulo")
        {
            mapa.Remove(nombre);
        }
        else if (nombre == "entidades" && anterior is List<object?> ents && ents.OfType<Dictionary<string, object?>>().All(e => e.ContainsKey("certeza")))
        {
            mapa.Remove(nombre); // cada entidad vuelve con su propia certeza
        }
        else
        {
            if (certezaAnterior is null)
            {
                certezaAnterior = "interpretado";
                aviso = " La corrección es anterior a este registro de certezas: el valor vuelve como «interpretado» (la más débil); corrígelo si no es así.";
            }

            mapa[nombre] = certezaAnterior;
        }

        Anotar(fm, nombre, actual, anterior, e =>
        {
            e["deshace"] = indiceObjetivo.ToString(CultureInfo.InvariantCulture); // posición de la entrada que se deshace
            if (certezaActual is not null)
            {
                e["certeza_anterior"] = certezaActual;
            }
        });
        return Guardar(almacen, [registro with { Frontmatter = fm }], $"Deshecho: «{nombre}» de {registroId} vuelve a «{ValoresDeCampo.Resumen(anterior)}».{aviso} Si el cambio venía de contestar una pregunta, esa pregunta sigue cerrada.");
    }

    // ------------------------------------------------------------------ eliminar y restaurar

    /// <summary>Cierra las preguntas pendientes que afectan a <paramref name="id"/> y resuelve sus conflictos abiertos, también en los otros registros.</summary>
    private void Despejar(Trabajo t, string id, string motivo)
    {
        var yo = t.Buscar(id, "registro")!;
        foreach (var c in Lista(yo.Frontmatter, "conflictos").OfType<Dictionary<string, object?>>().Where(c => c.GetValueOrDefault("estado") as string == "abierto").ToList())
        {
            var otroId = c.GetValueOrDefault("con") as string;
            var pid = c.GetValueOrDefault("pregunta") as string;
            t.Mutar(id, "registro", fm =>
            {
                foreach (var x in Lista(fm, "conflictos").OfType<Dictionary<string, object?>>().Where(x => x.GetValueOrDefault("con") as string == otroId && x.GetValueOrDefault("pregunta") as string == pid))
                {
                    x["estado"] = "resuelto";
                }
            });
            if (otroId is not null && t.Buscar(otroId, "registro") is not null)
            {
                t.Mutar(otroId, "registro", fm =>
                {
                    foreach (var x in Lista(fm, "conflictos").OfType<Dictionary<string, object?>>().Where(x => x.GetValueOrDefault("con") as string == id && x.GetValueOrDefault("pregunta") as string == pid && x.GetValueOrDefault("estado") as string == "abierto"))
                    {
                        x["estado"] = "resuelto";
                        Anotar(fm, "conflictos", "abierto", "resuelto");
                    }
                });
            }
        }

        foreach (var p in t.Todos("pregunta").Where(p => p.Frontmatter.GetValueOrDefault("estado") as string == "pendiente"
                     && (p.Frontmatter.GetValueOrDefault("registro") as string == id
                         || (p.Frontmatter.GetValueOrDefault("otros_registros") as List<object?> ?? []).OfType<string>().Contains(id))).ToList())
        {
            t.Mutar(p.Id!, "pregunta", fm =>
            {
                fm["estado"] = "no_relevante";
                fm["cerrada_el"] = Instante;
                fm["respuesta"] = new Dictionary<string, object?> { ["texto"] = motivo, ["respondida_el"] = Instante };
            });
        }
    }

    public ResultadoOperacion Eliminar(string registroId)
    {
        var almacen = Almacen.Cargar(datos);
        var registro = Buscar(almacen, registroId, "registro");
        ExigirActivo(registro, "eliminar");
        var t = new Trabajo(almacen);
        Despejar(t, registroId, "Registro eliminado.");
        t.Mutar(registroId, "registro", fm =>
        {
            fm["estado"] = "eliminado";
            fm["eliminado_el"] = Instante;
            Anotar(fm, "estado", "activo", "eliminado");
        });
        return Guardar(almacen, t.Cambios, $"Registro {registroId} eliminado (a la papelera). La nota original no se ha tocado y puedes restaurarlo con «restaurar».");
    }

    public ResultadoOperacion Restaurar(string registroId)
    {
        var almacen = Almacen.Cargar(datos);
        var registro = Buscar(almacen, registroId, "registro");
        var estado = registro.Frontmatter.GetValueOrDefault("estado") as string;
        if (estado != "eliminado")
        {
            throw new OperacionInvalidaException(estado is null
                ? $"El registro {registroId} no está eliminado."
                : $"El registro {registroId} está {estado}, no eliminado: solo se restauran los eliminados.");
        }

        var fm = Clonar(registro.Frontmatter);
        fm.Remove("estado");
        fm.Remove("eliminado_el");
        Anotar(fm, "estado", "eliminado", "activo");
        return Guardar(almacen, [registro with { Frontmatter = fm }], $"Registro {registroId} restaurado. Las preguntas que se cerraron al eliminarlo no se reabren.");
    }

    // ------------------------------------------------------------------ fusionar

    private static bool Desconocido(Dictionary<string, object?> fm, string campo) =>
        !fm.TryGetValue(campo, out var v) || v is null || v as string == "desconocido";

    private static string AnadirEnlace(string cuerpo, string seccion, string id)
    {
        if (cuerpo.Contains($"[[{id}]]", StringComparison.Ordinal))
        {
            return cuerpo;
        }

        var cabecera = $"\n## {seccion}\n";
        var i = cuerpo.IndexOf(cabecera, StringComparison.Ordinal);
        if (i < 0)
        {
            return cuerpo.TrimEnd('\n') + $"\n\n## {seccion}\n- [[{id}]]\n";
        }

        var inicio = i + cabecera.Length;
        var fin = cuerpo.IndexOf("\n## ", inicio, StringComparison.Ordinal);
        var seccionTexto = fin < 0 ? cuerpo[inicio..] : cuerpo[inicio..fin];
        var nuevo = seccionTexto.TrimEnd('\n') + $"\n- [[{id}]]\n";
        return cuerpo[..inicio] + nuevo + (fin < 0 ? string.Empty : "\n" + cuerpo[fin..].TrimStart('\n'));
    }

    private static string QuitarEnlace(string cuerpo, string id) =>
        Regex.Replace(cuerpo, $@"(?m)^- \[\[{Regex.Escape(id)}\]\][ \t]*\r?\n?", string.Empty);

    private static bool MismaRelacion(Dictionary<string, object?> a, Dictionary<string, object?> b) =>
        a.GetValueOrDefault("tipo") as string == b.GetValueOrDefault("tipo") && a.GetValueOrDefault("destino") as string == b.GetValueOrDefault("destino");

    public ResultadoOperacion Fusionar(string destinoId, string origenId)
    {
        if (destinoId == origenId)
        {
            throw new OperacionInvalidaException("El origen y el destino son el mismo registro.");
        }

        var almacen = Almacen.Cargar(datos);
        var destino = Buscar(almacen, destinoId, "registro");
        var origen = Buscar(almacen, origenId, "registro");
        ExigirActivo(destino, "fusionar en");
        ExigirActivo(origen, "fusionar");
        var t = new Trabajo(almacen);
        var avisos = new List<string>();
        var origenFm = origen.Frontmatter;

        Despejar(t, origenId, "Registro fusionado.");

        // Registros que apuntaban al origen: relaciones y tumbas pasan a apuntar al destino.
        foreach (var x in t.Todos("registro").Where(x => x.Id != destinoId && x.Id != origenId).ToList())
        {
            var apuntaRelacion = Lista(x.Frontmatter, "relaciones").OfType<Dictionary<string, object?>>().Any(r => r.GetValueOrDefault("destino") as string == origenId);
            var esTumbaDeOrigen = x.Frontmatter.GetValueOrDefault("fusionado_en") as string == origenId;
            if (!apuntaRelacion && !esTumbaDeOrigen)
            {
                continue;
            }

            t.Mutar(x.Id!, "registro", fm =>
            {
                if (esTumbaDeOrigen)
                {
                    fm["fusionado_en"] = destinoId;
                }

                var rels = Lista(fm, "relaciones");
                foreach (var r in rels.OfType<Dictionary<string, object?>>().Where(r => r.GetValueOrDefault("destino") as string == origenId).ToList())
                {
                    r["destino"] = destinoId;
                    if (rels.OfType<Dictionary<string, object?>>().Count(o => MismaRelacion(o, r)) > 1)
                    {
                        rels.Remove(r);
                    }
                }
            });
        }

        t.Mutar(destinoId, "registro", (fm, cuerpo) =>
        {
            var nuevoCuerpo = cuerpo;
            var destinoFuentes = Lista(fm, "fuentes").OfType<Dictionary<string, object?>>().ToList();
            foreach (var f in Lista(origenFm, "fuentes").OfType<Dictionary<string, object?>>())
            {
                var cap = f.GetValueOrDefault("captura") as string;
                if (cap is null || destinoFuentes.Any(d => d.GetValueOrDefault("captura") as string == cap))
                {
                    continue;
                }

                var copia = (Dictionary<string, object?>)ClonarValor(f)!;
                copia["rol"] = "ampliacion";
                Lista(fm, "fuentes").Add(copia);
                nuevoCuerpo = AnadirEnlace(nuevoCuerpo, "Origen", cap);
            }

            // Entradas del origen (sin duplicar) y orden cronológico.
            var entradas = Lista(fm, "entradas");
            foreach (var e in Lista(origenFm, "entradas").OfType<Dictionary<string, object?>>())
            {
                if (!entradas.OfType<Dictionary<string, object?>>().Any(d => d.GetValueOrDefault("fecha") as string == e.GetValueOrDefault("fecha") as string && d.GetValueOrDefault("captura") as string == e.GetValueOrDefault("captura") as string))
                {
                    entradas.Add(ClonarValor(e));
                }
            }

            fm["entradas"] = entradas.OfType<Dictionary<string, object?>>().OrderBy(e => e.GetValueOrDefault("fecha") as string, StringComparer.Ordinal).Cast<object?>().ToList();

            // Relaciones del origen (sin duplicados, sin apuntar a sí mismo ni al origen).
            var rels = Lista(fm, "relaciones");
            foreach (var r in Lista(origenFm, "relaciones").OfType<Dictionary<string, object?>>())
            {
                var d = r.GetValueOrDefault("destino") as string;
                if (d == destinoId || d == origenId || rels.OfType<Dictionary<string, object?>>().Any(o => MismaRelacion(o, r)))
                {
                    continue;
                }

                rels.Add(ClonarValor(r));
                nuevoCuerpo = d is null ? nuevoCuerpo : AnadirEnlace(nuevoCuerpo, "Relacionados", d);
            }

            // Datos: el destino conserva los suyos; recibe los que no tenía. Si difieren, se anota lo que tenía el origen.
            var certeza = Mapa(fm, "certeza");
            var certezaOrigen = origenFm.GetValueOrDefault("certeza") as Dictionary<string, object?> ?? [];
            var unDia = fm.GetValueOrDefault("fecha") is Dictionary<string, object?> fd && fd.GetValueOrDefault("desde") as string == fd.GetValueOrDefault("hasta") as string;
            foreach (var campo in new[] { "fecha" }.Concat(CamposQueSeFusionan).Append("multidia"))
            {
                if (Desconocido(origenFm, campo))
                {
                    continue;
                }

                var valorOrigen = origenFm[campo];
                if (Desconocido(fm, campo))
                {
                    if (campo == "hora" && !unDia)
                    {
                        avisos.Add("la hora del origen no se copió (el destino abarca varios días)");
                        continue;
                    }

                    fm[campo] = ClonarValor(valorOrigen);
                    if (campo == "multidia")
                    {
                        foreach (var (k, v) in certezaOrigen.Where(kv => kv.Key.StartsWith("multidia.", StringComparison.Ordinal)))
                        {
                            certeza[k] = v;
                        }
                    }
                    else if (certezaOrigen.GetValueOrDefault(campo) is string c)
                    {
                        certeza[campo] = c;
                    }
                }
                else if (ValoresDeCampo.Resumen(fm[campo]) != ValoresDeCampo.Resumen(valorOrigen) && campo != "multidia")
                {
                    Anotar(fm, "fusion", "desconocido", $"{campo}: se conserva «{ValoresDeCampo.Resumen(fm[campo])}»; {origenId} tenía «{ValoresDeCampo.Resumen(valorOrigen)}»");
                }
            }

            Anotar(fm, "fusion", "desconocido", $"fusiona {origenId}");
            return nuevoCuerpo;
        });

        t.Mutar(origenId, "registro", fm =>
        {
            fm["estado"] = "fusionado";
            fm["fusionado_en"] = destinoId;
            fm["fusionado_el"] = Instante;
            Anotar(fm, "estado", "activo", $"fusionado en {destinoId}");
        });

        var nota = avisos.Count == 0 ? string.Empty : " Avisos: " + string.Join("; ", avisos) + ".";
        return Guardar(almacen, t.Cambios, $"{origenId} fusionado en {destinoId}. {destinoId} ahora tiene las fuentes, entradas y datos de ambos; {origenId} queda como registro fusionado (sus conflictos y preguntas pendientes se cerraron).{nota}");
    }

    // ------------------------------------------------------------------ separar

    public ResultadoOperacion Separar(string registroId, string capturaId, string? titulo = null)
    {
        var almacen = Almacen.Cargar(datos);
        var registro = Buscar(almacen, registroId, "registro");
        ExigirActivo(registro, "separar");
        var captura = almacen.DeTipo("captura").FirstOrDefault(c => c.Id == capturaId)
                      ?? throw new OperacionInvalidaException($"No existe ninguna captura con el identificador «{capturaId}».");
        var fuente = Lista(registro.Frontmatter, "fuentes").OfType<Dictionary<string, object?>>().FirstOrDefault(f => f.GetValueOrDefault("captura") as string == capturaId)
                     ?? throw new OperacionInvalidaException($"La captura {capturaId} no figura entre las fuentes de {registroId}.");
        if (fuente.GetValueOrDefault("rol") as string != "ampliacion")
        {
            throw new OperacionInvalidaException($"La captura {capturaId} es el origen de {registroId}: solo se pueden separar las que se añadieron como ampliación. Si el registro entero sobra, elimínalo.");
        }

        if (titulo is { Length: > 300 })
        {
            throw new OperacionInvalidaException("El título es demasiado largo.");
        }

        var fm0 = registro.Frontmatter;
        var clase = fm0.GetValueOrDefault("clase") as string ?? "otro";
        var nuevoTitulo = string.IsNullOrWhiteSpace(titulo) ? (fm0.GetValueOrDefault("titulo") as string ?? "Registro separado") : titulo.Trim();
        var entradaDeC = Lista(fm0, "entradas").OfType<Dictionary<string, object?>>().FirstOrDefault(e => e.GetValueOrDefault("captura") as string == capturaId);
        var instante = Instante;

        // Identificador y clave nuevos (la clave empieza por la captura y usa el siguiente fragmento libre).
        var existentes = almacen.DeTipo("registro").ToList();
        string nuevoId;
        do
        {
            nuevoId = $"reg-{ahora():yyyyMMdd}-{Identificadores.Sufijo(4)}";
        }
        while (existentes.Any(r => r.Id == nuevoId));
        var maxFragmento = existentes.Select(r => r.Frontmatter.GetValueOrDefault("clave_extraccion") as string)
            .Where(k => k is not null && k.StartsWith(capturaId + "#f", StringComparison.Ordinal))
            .Select(k => int.TryParse(k![(k.IndexOf("#f", StringComparison.Ordinal) + 2)..].Split(':')[0], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        var clave = $"{capturaId}#f{maxFragmento + 1}:{clase}:1";

        var fuenteNueva = new Dictionary<string, object?> { ["captura"] = capturaId };
        if (fuente.GetValueOrDefault("fragmento") is { } fr)
        {
            fuenteNueva["fragmento"] = ClonarValor(fr);
        }

        fuenteNueva["rol"] = "origen";
        var fm = new Dictionary<string, object?>
        {
            ["id"] = nuevoId,
            ["tipo"] = "registro",
            ["clase"] = clase,
            ["titulo"] = nuevoTitulo,
            ["comunicada_el"] = captura.Frontmatter.GetValueOrDefault("comunicada_el"),
            ["fuentes"] = new List<object?> { fuenteNueva },
            ["fecha"] = entradaDeC?.GetValueOrDefault("fecha") is string fe
                ? new Dictionary<string, object?> { ["desde"] = fe, ["hasta"] = fe }
                : "desconocido",
            ["entradas"] = entradaDeC is null ? new List<object?>() : new List<object?> { ClonarValor(entradaDeC) },
            ["relaciones"] = new List<object?> { new Dictionary<string, object?> { ["tipo"] = "relacionado_con", ["destino"] = registroId, ["certeza"] = "confirmado" } },
            ["conflictos"] = new List<object?>(),
            ["rechazos"] = new List<object?>(),
            ["historial"] = new List<object?>(),
            ["certeza"] = entradaDeC?.GetValueOrDefault("fecha") is string ? new Dictionary<string, object?> { ["fecha"] = "interpretado" } : new Dictionary<string, object?>(),
            ["procedencia"] = new Dictionary<string, object?>
            {
                ["generado_por"] = new Dictionary<string, object?> { ["agente"] = "usuario", ["version"] = "manual", ["modelo"] = "manual" },
                ["generado_el"] = instante,
                ["ejecucion"] = "peticion",
            },
            ["clave_extraccion"] = clave,
        };
        Anotar(fm, "estado", "desconocido", $"separado de {registroId}");
        var cuerpo = $"{nuevoTitulo}.\n\n## Origen\n- [[{capturaId}]]\n\n## Relacionados\n- [[{registroId}]]\n";
        var rutaNueva = Identificadores.RutaRelativa(nuevoId)!;

        var t = new Trabajo(almacen);
        t.Nuevo(new Documento(rutaNueva, fm, cuerpo));
        var aportes = new List<string>();
        t.Mutar(registroId, "registro", (fmA, cuerpoA) =>
        {
            foreach (var h in Lista(fmA, "historial").OfType<Dictionary<string, object?>>().Where(h => h.GetValueOrDefault("captura") as string == capturaId))
            {
                aportes.Add($"{h.GetValueOrDefault("campo")} → {ValoresDeCampo.Resumen(h.GetValueOrDefault("nuevo"))}");
            }

            fmA["fuentes"] = Lista(fmA, "fuentes").OfType<Dictionary<string, object?>>().Where(f => f.GetValueOrDefault("captura") as string != capturaId).Cast<object?>().ToList();
            fmA["entradas"] = Lista(fmA, "entradas").OfType<Dictionary<string, object?>>().Where(e => e.GetValueOrDefault("captura") as string != capturaId).Cast<object?>().ToList();
            var separados = Lista(fmA, "separados");
            if (!separados.OfType<string>().Contains(capturaId))
            {
                separados.Add(capturaId);
            }

            Anotar(fmA, "fuentes", $"incluye {capturaId}", $"separada {capturaId} (ahora en {nuevoId})");
            return QuitarEnlace(cuerpoA, capturaId);
        });

        var revisar = aportes.Count == 0 ? string.Empty : " Datos que esa nota aportó a " + registroId + " (no se revierten solos; revísalos con «corregir»): " + string.Join("; ", aportes) + ".";
        return Guardar(almacen, t.Cambios, $"Captura {capturaId} separada de {registroId}: nuevo registro {nuevoId} («{nuevoTitulo}»).{revisar}");
    }
}
