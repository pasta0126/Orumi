namespace Orumi.Agente;

/// <summary>Presenta un proveedor por lotes con el nombre y el modelo que necesita el procesador (no llama al modo inmediato).</summary>
public sealed class DescriptorDeProveedor(IProveedorPorLotes lotes) : IProveedorIA
{
    public string Nombre => lotes.Nombre;

    public string Modelo => lotes.Modelo;

    public bool EnviaDatosAFuera => lotes.EnviaDatosAFuera;

    public Task<RespuestaExtraccion> ExtraerAsync(SolicitudExtraccion solicitud, CancellationToken ct) =>
        throw new NotSupportedException("Este proveedor solo procesa por lotes diferidos.");
}
