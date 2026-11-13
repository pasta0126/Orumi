namespace Orumi.Agente;

/// <summary>Datos mínimos de un registro existente que se ofrecen al proveedor (<c>docs/agente-mira.md</c> §7.1).</summary>
public sealed record RegistroRelacionado(
    string Id,
    string Clase,
    string Titulo,
    string? FechaDesde,
    string? FechaHasta,
    IReadOnlyList<string> Personas,
    IReadOnlyList<string> Entidades,
    IReadOnlyList<string> Proyectos,
    string? MultidiaEstado);

/// <summary>Lo único que sale hacia el proveedor para procesar una captura.</summary>
public sealed record SolicitudExtraccion(
    string CapturaId,
    string Texto,
    DateTimeOffset ComunicadaEl,
    string? FechaAcontecimiento,
    string? Etiqueta,
    IReadOnlyList<string> ClavesExistentes,
    IReadOnlyList<RegistroRelacionado> Relacionados);

public sealed record RelacionExtraida(string Tipo, string Certeza, string? DestinoId = null, int? DestinoIndice = null);

public sealed record MultidiaExtraido(string Estado, string? FinDesde, string? FinHasta, string CertezaEstado, string CertezaFin);

/// <summary>Un registro propuesto por el proveedor. Los campos usan los nombres y la forma del formato de ficheros.</summary>
public sealed class RegistroExtraido
{
    public required string Fragmento { get; init; }

    public required string Clase { get; init; }

    public required string Titulo { get; init; }

    public required string Descripcion { get; init; }

    /// <summary>Clave de extracción existente que este elemento reclama (reprocesado).</summary>
    public string? Reclama { get; init; }

    /// <summary>Registro existente al que esta captura aporta (ampliación clara).</summary>
    public string? Amplia { get; init; }

    /// <summary>Registro existente con el que podría coincidir (coincidencia dudosa: no se fusiona).</summary>
    public string? Duda { get; init; }

    /// <summary>Registro existente al que contradice, y campo contradicho.</summary>
    public string? EnConflictoCon { get; init; }

    public string? ConflictoCampo { get; init; }

    /// <summary>fecha, hora, duracion, herramientas, personas, entidades, lugares, proyectos, contexto, acciones, resultados, problemas, ideas.</summary>
    public Dictionary<string, object?> Campos { get; init; } = [];

    /// <summary>Certeza por campo (<c>dicho</c>, <c>confirmado</c>, <c>interpretado</c>…).</summary>
    public Dictionary<string, string> Certeza { get; init; } = [];

    public List<RelacionExtraida> Relaciones { get; init; } = [];

    public MultidiaExtraido? Multidia { get; init; }

    public string? ResumenEntrada { get; init; }
}

public sealed record PreguntaCandidata(string Campo, string Texto, string Valor, string? RegistroId = null, int? RegistroIndice = null);

public sealed record RespuestaExtraccion(
    IReadOnlyList<RegistroExtraido> Registros,
    IReadOnlyList<PreguntaCandidata> Preguntas,
    UsoDeTokens? Uso = null)
{
    public static RespuestaExtraccion SinHechos { get; } = new([], []);
}

/// <summary>Proveedor de IA intercambiable (spec <c>configuracion-del-proveedor-ia</c>).</summary>
public interface IProveedorIA
{
    string Nombre { get; }

    string Modelo { get; }

    /// <summary>Si los datos salen del equipo (el simulado, no). Determina si se exige la garantía de privacidad.</summary>
    bool EnviaDatosAFuera { get; }

    Task<RespuestaExtraccion> ExtraerAsync(SolicitudExtraccion solicitud, CancellationToken ct);
}
