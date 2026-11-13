namespace Orumi.Agente;

public enum TipoResultado { Correcto, Error, Cancelado, Expirado }

/// <summary>Resultado de una petición dentro de un lote diferido. El texto se interpreta con la misma validación que el modo inmediato.</summary>
public sealed record ResultadoLote(
    string Id, TipoResultado Tipo, string? Texto = null, UsoDeTokens? Uso = null, string? Detalle = null, CategoriaError Categoria = CategoriaError.Otro);

public sealed record EstadoLote(bool Terminado);

/// <summary>Proveedor que admite procesamiento diferido (spec <c>procesamiento-diferido</c>): enviar, consultar y recoger.</summary>
public interface IProveedorPorLotes
{
    string Nombre { get; }

    string Modelo { get; }

    bool EnviaDatosAFuera { get; }

    /// <summary>Crea el lote en el proveedor y devuelve su identificador.</summary>
    Task<string> EnviarAsync(IReadOnlyList<(string Id, SolicitudExtraccion Solicitud)> peticiones, CancellationToken ct);

    Task<EstadoLote> ConsultarAsync(string loteId, CancellationToken ct);

    Task<IReadOnlyList<ResultadoLote>> RecogerAsync(string loteId, CancellationToken ct);
}
