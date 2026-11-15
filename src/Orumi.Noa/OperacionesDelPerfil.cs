using System.Text;

namespace Orumi.Noa;

public sealed class PerfilInvalidoException(string mensaje) : Exception(mensaje);

/// <summary>
/// El usuario manda sobre su perfil (spec <c>perfil-personal</c>, «Control total del usuario»): corregir y matizar (su palabra prevalece), descartar,
/// ocultar y borrar, rasgo a rasgo o el perfil entero. Quien llama toma antes el bloqueo de Noa. No toca nada fuera de <c>perfil/</c>.
/// </summary>
public static class OperacionesDelPerfil
{
    private static Perfil Cargar(string datos) =>
        AlmacenDePerfil.Leer(datos) ?? throw new PerfilInvalidoException("No existe todavía ningún perfil.");

    private static Rasgo Buscar(Perfil p, string clave) =>
        p.Rasgos.FirstOrDefault(r => r.Clave == clave) ?? throw new PerfilInvalidoException($"No existe el rasgo «{clave}».");

    private static Perfil Cambiar(string datos, Perfil p, string clave, Func<Rasgo, Rasgo> cambio)
    {
        var nuevo = p with { Rasgos = [.. p.Rasgos.Select(r => r.Clave == clave ? cambio(r) : r)] };
        AlmacenDePerfil.Guardar(datos, nuevo, copiaDeVersion: false);
        return nuevo;
    }

    private static Rasgo Anotar(Rasgo r, DateTimeOffset ahora, string accion, string? detalle) =>
        r with { Actualizado = ahora, Historial = [.. r.Historial, new EntradaDeRasgo(ahora, accion, Rasgo.DelUsuario, detalle)] };

    /// <summary>Su versión pasa a «dicho por el usuario» y prevalece: Noa no la vuelve a escribir.</summary>
    public static Perfil Corregir(string datos, string clave, string texto, DateTimeOffset ahora)
    {
        texto = (texto ?? string.Empty).Trim();
        if (texto.Length is < 5 or > 400 || texto.Any(char.IsControl))
        {
            throw new PerfilInvalidoException("El texto debe tener entre 5 y 400 caracteres, sin saltos de línea.");
        }

        var p = Cargar(datos);
        var anterior = Buscar(p, clave).Texto;
        return Cambiar(datos, p, clave, r => Anotar(r with { Texto = texto, Certeza = Certezas.Dicho, Origen = Rasgo.DelUsuario, Estado = EstadosDeRasgo.Activo, Oculto = false }, ahora, "corregido", $"antes: {anterior}"));
    }

    /// <summary>Se conserva lo que Noa observó y se añade el comentario del usuario, que prevalece sobre su interpretación.</summary>
    public static Perfil Matizar(string datos, string clave, string comentario, DateTimeOffset ahora)
    {
        comentario = (comentario ?? string.Empty).Trim();
        if (comentario.Length is < 3 or > 500 || comentario.Any(char.IsControl))
        {
            throw new PerfilInvalidoException("El comentario debe tener entre 3 y 500 caracteres, sin saltos de línea.");
        }

        var p = Cargar(datos);
        Buscar(p, clave);
        return Cambiar(datos, p, clave, r => Anotar(r with { ComentarioDelUsuario = comentario, Estado = EstadosDeRasgo.Matizado }, ahora, "matizado", comentario));
    }

    /// <summary>No vuelve a proponerse salvo evidencia nueva sustancial (al menos el doble de fuentes).</summary>
    public static Perfil Descartar(string datos, string clave, DateTimeOffset ahora)
    {
        var p = Cargar(datos);
        Buscar(p, clave);
        return Cambiar(datos, p, clave, r => Anotar(r with { Estado = EstadosDeRasgo.Descartado, EvidenciaAlDescartar = r.Fuentes }, ahora, "descartado", null));
    }

    /// <summary>Lo borra de la vista; deja una marca para que no se vuelva a proponer sin evidencia nueva sustancial.</summary>
    public static Perfil Borrar(string datos, string clave, DateTimeOffset ahora)
    {
        var p = Cargar(datos);
        Buscar(p, clave);
        return Cambiar(datos, p, clave, r => Anotar(r with { Estado = EstadosDeRasgo.Borrado, EvidenciaAlDescartar = r.Fuentes, Oculto = true, ComentarioDelUsuario = null }, ahora, "borrado", null));
    }

    /// <summary>Oculta (o vuelve a mostrar) un rasgo, un apartado entero («apartado:SLUG») o una recomendación («rec-…») sin borrarlos: salen de la vista y de la descarga.</summary>
    public static Perfil Ocultar(string datos, string objetivo, bool oculto, DateTimeOffset ahora)
    {
        var p = Cargar(datos);
        if (objetivo.StartsWith("apartado:", StringComparison.Ordinal))
        {
            var slug = objetivo["apartado:".Length..];
            if (!Apartados.Existe(slug))
            {
                throw new PerfilInvalidoException($"No existe el apartado «{slug}».");
            }

            var lista = p.ApartadosOcultos.Where(a => a != slug).ToList();
            if (oculto)
            {
                lista.Add(slug);
            }

            var nuevoA = p with { ApartadosOcultos = lista };
            AlmacenDePerfil.Guardar(datos, nuevoA, copiaDeVersion: false);
            return nuevoA;
        }

        if (objetivo.StartsWith("rec-", StringComparison.Ordinal))
        {
            if (p.Recomendaciones.All(r => r.Id != objetivo))
            {
                throw new PerfilInvalidoException($"No existe la recomendación «{objetivo}».");
            }

            var nuevoR = p with { Recomendaciones = [.. p.Recomendaciones.Select(r => r.Id == objetivo ? r with { Oculta = oculto } : r)] };
            AlmacenDePerfil.Guardar(datos, nuevoR, copiaDeVersion: false);
            return nuevoR;
        }

        Buscar(p, objetivo);
        return Cambiar(datos, p, objetivo, r => Anotar(r with { Oculto = oculto }, ahora, oculto ? "ocultado" : "mostrado", null));
    }

    /// <summary>Borra el perfil entero con su historial y sus generaciones. No afecta a capturas, registros ni preguntas.</summary>
    public static void BorrarTodo(string datos) => AlmacenDePerfil.BorrarTodo(datos);
}

/// <summary>Descarga del perfil en Markdown, generada al vuelo (sin copia persistente): sin lo oculto, descartado ni borrado, y con las recomendaciones aparte.</summary>
public static class PerfilEnMarkdown
{
    private static readonly Dictionary<string, string> NombreDeCerteza = new()
    {
        [Certezas.Dicho] = "dicho por ti", [Certezas.Confirmado] = "confirmado", [Certezas.Interpretado] = "interpretación no verificada",
        [Certezas.Hipotesis] = "hipótesis de varios registros", [Certezas.Recomendacion] = "recomendación del sistema",
    };

    private static readonly Dictionary<string, string> NombreDeNivel = new()
    {
        [Niveles.Patron] = "patrón", [Niveles.Indicio] = "indicio", [Niveles.Conjetura] = "conjetura (baja confianza)",
    };

    public static string Generar(Perfil p)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Tu perfil, según Noa").AppendLine();
        sb.AppendLine($"Versión {p.Version}, generada el {p.Generada:yyyy-MM-dd}. Es una inferencia a partir de lo que has contado: nada de esto es un hecho hasta que tú lo confirmes. No incluye diagnósticos.").AppendLine();
        var visibles = p.Rasgos.Where(r => r.Visible && !p.ApartadosOcultos.Contains(r.Apartado)).ToList();
        foreach (var a in Apartados.Todos)
        {
            var del = visibles.Where(r => r.Apartado == a.Slug && !r.EsConjetura).ToList();
            if (del.Count == 0)
            {
                continue;
            }

            sb.AppendLine($"## {a.Titulo}").AppendLine();
            foreach (var r in del)
            {
                sb.AppendLine($"- {r.Texto} _({NombreDeNivel[r.Nivel]}; {NombreDeCerteza.GetValueOrDefault(r.Certeza, r.Certeza)}{(r.Sensible ? "; sensible" : "")})_");
                if (r.ComentarioDelUsuario is not null)
                {
                    sb.AppendLine($"  - Tu matiz: «{r.ComentarioDelUsuario}»");
                }

                foreach (var e in r.Evidencia.Take(3))
                {
                    sb.AppendLine($"  - {e.Fecha}: «{e.Cita}» ({e.Fuente})");
                }
            }

            sb.AppendLine();
        }

        var conjeturas = visibles.Where(r => r.EsConjetura).ToList();
        if (conjeturas.Count > 0)
        {
            sb.AppendLine("## Conjeturas (baja confianza, un solo registro)").AppendLine();
            foreach (var r in conjeturas)
            {
                sb.AppendLine($"- {r.Texto} _({Apartados.Buscar(r.Apartado)?.Titulo}{(r.Sensible ? "; sensible" : "")})_");
            }

            sb.AppendLine();
        }

        var recs = p.Recomendaciones.Where(r => r.Visible).ToList();
        if (recs.Count > 0)
        {
            sb.AppendLine("## Recomendaciones de comunicación (del sistema, no son hechos)").AppendLine();
            recs.ForEach(r => sb.AppendLine($"- {r.Texto}"));
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + "\n";
    }
}
