using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;

namespace Orumi.Agente.Etiquetado;

public sealed record RespuestaDeTexto(string Texto, UsoDeTokens? Uso);

/// <summary>Proveedor de IA para tareas de texto libre con respuesta JSON (el etiquetado de registros). Mismas garantías que Mira (X38).</summary>
public interface IProveedorDeTexto
{
    string Nombre { get; }

    string Modelo { get; }

    /// <summary>Si los datos salen del equipo (el simulado, no). Determina si se exige la garantía de privacidad.</summary>
    bool EnviaDatosAFuera { get; }

    Task<RespuestaDeTexto> CompletarAsync(string sistema, string mensaje, CancellationToken ct);
}

public sealed class ProveedorDeTextoSimulado(Func<string, string, string> respuesta, string modelo = "simulado-1") : IProveedorDeTexto
{
    public List<(string Sistema, string Mensaje)> Peticiones { get; } = [];

    public string Nombre => "simulado";

    public string Modelo => modelo;

    public bool EnviaDatosAFuera => false;

    public Task<RespuestaDeTexto> CompletarAsync(string sistema, string mensaje, CancellationToken ct)
    {
        Peticiones.Add((sistema, mensaje));
        return Task.FromResult(new RespuestaDeTexto(respuesta(sistema, mensaje), new UsoDeTokens(100, 50)));
    }
}

/// <summary>Cliente real de Anthropic para el etiquetado: solo el texto de los registros y los conceptos, sin ningún dato meteorológico.</summary>
public sealed class ProveedorAnthropicTexto(string modelo, AnthropicClient? cliente = null) : IProveedorDeTexto
{
    private const int MaximoDeTokens = 16000;

    private readonly Lazy<AnthropicClient> _cliente = new(() => cliente ?? new AnthropicClient());

    public string Nombre => "anthropic";

    public string Modelo => modelo;

    public bool EnviaDatosAFuera => true;

    public async Task<RespuestaDeTexto> CompletarAsync(string sistema, string mensaje, CancellationToken ct)
    {
        var parametros = new MessageCreateParams
        {
            Model = modelo,
            MaxTokens = MaximoDeTokens,
            System = sistema,
            Messages = [new UserMessageParam { Content = mensaje }],
        };

        Message respuesta;
        try
        {
            respuesta = await _cliente.Value.Messages.Create(parametros, ct);
        }
        catch (AnthropicApiException e) when (Proveedores.ProveedorAnthropic.Traducir(e) is { } traducida)
        {
            throw traducida;
        }

        var texto = string.Concat(respuesta.Content.Select(b => b.TryPickText(out var t) ? t.Text : string.Empty));
        var uso = new UsoDeTokens(respuesta.Usage.InputTokens, respuesta.Usage.OutputTokens);
        if (respuesta.StopReason == StopReason.MaxTokens)
        {
            throw new ProcesamientoFallidoException("la salida del modelo se cortó por el límite de tokens") { Uso = uso };
        }

        return new RespuestaDeTexto(texto, uso);
    }
}
