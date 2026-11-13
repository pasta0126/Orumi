namespace Orumi.Agente.Proveedores;

/// <summary>Proveedor para pruebas: responde con lo que decida una función, sin enviar nada fuera.</summary>
public sealed class ProveedorSimulado(Func<SolicitudExtraccion, RespuestaExtraccion> respuesta, string modelo = "simulado-1") : IProveedorIA
{
    public List<SolicitudExtraccion> Solicitudes { get; } = [];

    public string Nombre => "simulado";

    public string Modelo => modelo;

    public bool EnviaDatosAFuera => false;

    public Task<RespuestaExtraccion> ExtraerAsync(SolicitudExtraccion solicitud, CancellationToken ct)
    {
        Solicitudes.Add(solicitud);
        return Task.FromResult(respuesta(solicitud));
    }
}
