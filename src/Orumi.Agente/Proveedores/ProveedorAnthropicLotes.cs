using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Anthropic.Models.Messages.Batches;

namespace Orumi.Agente.Proveedores;

/// <summary>
/// Procesamiento diferido con la API de lotes de Anthropic (Message Batches): el 50 % de las tarifas y la mayoría de lotes
/// termina en menos de una hora. Envía lo mismo que el modo inmediato (<c>docs/agente-mira.md</c> §7.1). El proveedor retiene los
/// datos del lote 29 días y no admite retención cero.
/// </summary>
public sealed class ProveedorAnthropicLotes(string modelo, AnthropicClient? cliente = null) : IProveedorPorLotes
{
    private readonly Lazy<AnthropicClient> _cliente = new(() => cliente ?? new AnthropicClient());

    public string Nombre => "anthropic";

    public string Modelo => modelo;

    public bool EnviaDatosAFuera => true;

    public async Task<string> EnviarAsync(IReadOnlyList<(string Id, SolicitudExtraccion Solicitud)> peticiones, CancellationToken ct)
    {
        var parametros = new BatchCreateParams
        {
            Requests = peticiones.Select(p => new Request
            {
                CustomID = p.Id, // los identificadores de captura (alfanuméricos y guiones, <= 64) valen como custom_id
                Params = new Params
                {
                    Model = modelo,
                    MaxTokens = 8192,
                    System = ProveedorAnthropic.Instrucciones,
                    Messages = [new UserMessageParam { Content = ProveedorAnthropic.Mensaje(p.Solicitud) }],
                },
            }).ToList(),
        };

        try
        {
            var lote = await _cliente.Value.Messages.Batches.Create(parametros, ct);
            return lote.ID;
        }
        catch (AnthropicApiException e) when (ProveedorAnthropic.Traducir(e) is { } traducida)
        {
            throw traducida;
        }
    }

    public async Task<EstadoLote> ConsultarAsync(string loteId, CancellationToken ct)
    {
        try
        {
            var lote = await _cliente.Value.Messages.Batches.Retrieve(new BatchRetrieveParams { MessageBatchID = loteId }, ct);
            return new EstadoLote(lote.ProcessingStatus == ProcessingStatus.Ended);
        }
        catch (AnthropicApiException e) when (ProveedorAnthropic.Traducir(e) is { } traducida)
        {
            throw traducida;
        }
    }

    public async Task<IReadOnlyList<ResultadoLote>> RecogerAsync(string loteId, CancellationToken ct)
    {
        var resultados = new List<ResultadoLote>();
        try
        {
            await foreach (var item in _cliente.Value.Messages.Batches.ResultsStreaming(new BatchResultsParams { MessageBatchID = loteId }, ct))
            {
                resultados.Add(Convertir(item));
            }
        }
        catch (AnthropicApiException e) when (ProveedorAnthropic.Traducir(e) is { } traducida)
        {
            throw traducida;
        }

        return resultados;
    }

    private static ResultadoLote Convertir(MessageBatchIndividualResponse item)
    {
        var id = item.CustomID;
        if (item.Result.TryPickSucceeded(out var ok))
        {
            var texto = string.Concat(ok.Message.Content.Select(b => b.TryPickText(out var t) ? t.Text : string.Empty));
            return new ResultadoLote(id, TipoResultado.Correcto, texto, new UsoDeTokens(ok.Message.Usage.InputTokens, ok.Message.Usage.OutputTokens));
        }

        if (item.Result.TryPickErrored(out var error))
        {
            // Solo tipo, código y mensaje del error de la API (nunca contenido de las notas).
            var (tipo, codigo, mensaje) = LeerErrorDelResultado(error);
            var categoria = ClasificadorDeErrores.Clasificar(null, tipo, codigo, mensaje);
            return new ResultadoLote(id, TipoResultado.Error, Detalle: tipo, Categoria: categoria);
        }

        return new ResultadoLote(id, item.Result.TryPickCanceled(out _) ? TipoResultado.Cancelado : TipoResultado.Expirado);
    }

    private static (string? Tipo, string? Codigo, string? Mensaje) LeerErrorDelResultado(MessageBatchErroredResult error)
    {
        try
        {
            return ProveedorAnthropic.LeerError(JsonSerializer.Serialize(new { error = new { type = error.Error.Error.Type?.ToString(), message = error.Error.Error.Message } }), null);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or NullReferenceException)
        {
            return (null, null, null);
        }
    }
}
