namespace Orumi.Agente.Proveedores;

/// <summary>Proveedor por lotes para pruebas: responde lo que decida una función y tarda un número fijo de consultas en terminar.</summary>
public sealed class ProveedorPorLotesSimulado(
    Func<SolicitudExtraccion, ResultadoLote> resultado, int consultasHastaTerminar = 0, string modelo = "simulado-lotes-1") : IProveedorPorLotes
{
    private readonly Dictionary<string, (List<(string Id, SolicitudExtraccion Solicitud)> Peticiones, int Consultas)> _lotes = [];
    private int _siguiente;

    public List<IReadOnlyList<(string Id, SolicitudExtraccion Solicitud)>> Envios { get; } = [];

    public int Consultas { get; private set; }

    /// <summary>Si se establece, <see cref="EnviarAsync"/> lanza esta excepción (saldo agotado al enviar, etc.).</summary>
    public Exception? ErrorAlEnviar { get; set; }

    public string Nombre => "simulado";

    public string Modelo => modelo;

    public bool EnviaDatosAFuera => false;

    public Task<string> EnviarAsync(IReadOnlyList<(string Id, SolicitudExtraccion Solicitud)> peticiones, CancellationToken ct)
    {
        if (ErrorAlEnviar is not null)
        {
            throw ErrorAlEnviar;
        }

        Envios.Add(peticiones);
        var id = $"msgbatch_sim_{++_siguiente}";
        _lotes[id] = ([.. peticiones], 0);
        return Task.FromResult(id);
    }

    public Task<EstadoLote> ConsultarAsync(string loteId, CancellationToken ct)
    {
        Consultas++;
        var (peticiones, consultas) = _lotes.TryGetValue(loteId, out var l) ? l : ([], 0);
        _lotes[loteId] = (peticiones, consultas + 1);
        return Task.FromResult(new EstadoLote(consultas + 1 > consultasHastaTerminar));
    }

    public Task<IReadOnlyList<ResultadoLote>> RecogerAsync(string loteId, CancellationToken ct)
    {
        var peticiones = _lotes.TryGetValue(loteId, out var l) ? l.Peticiones : [];
        return Task.FromResult<IReadOnlyList<ResultadoLote>>(peticiones.Select(p => resultado(p.Solicitud)).ToList());
    }
}
