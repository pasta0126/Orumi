using Orumi.Agente;

namespace Orumi.Tests;

public class RobustezDeSalidaTests
{
    private static Dictionary<string, object?> Mapa(object? o) => (Dictionary<string, object?>)o!;

    private sealed class ProveedorQueSeCorta : IProveedorIA
    {
        public string Nombre => "simulado";

        public string Modelo => "m";

        public bool EnviaDatosAFuera => false;

        public Task<RespuestaExtraccion> ExtraerAsync(SolicitudExtraccion solicitud, CancellationToken ct) =>
            throw new ProcesamientoFallidoException("la salida del modelo se cortó por el límite de tokens (la nota es muy larga)") { Uso = new UsoDeTokens(1500, 8000) };
    }

    [Fact]
    public async Task Un_dato_sin_certeza_del_modelo_queda_como_interpretado_y_el_registro_valida()
    {
        using var e = new EntornoMira();
        e.Capturar("Hoy a las 9.30 tuve reunión.");
        e.Respuesta = s => Datos.Resp([Datos.Registro(s.Texto, "evento", "Reunión", campos: new()
        {
            ["hora"] = new Dictionary<string, object?> { ["expresion"] = "9.30", ["valor"] = "09:30", ["aproximada"] = "false" },
            ["personas"] = new List<object?> { "Marta" },
        })]); // sin ninguna certeza para hora ni personas

        var r = await e.Procesar();

        Assert.Single(r.Procesadas);
        var certeza = Mapa(e.Registros().Single().Frontmatter["certeza"]);
        Assert.Equal("interpretado", certeza["hora"]);
        Assert.Equal("interpretado", certeza["personas"]);
        Assert.Empty(e.Validar().Where(i => !i.Mensaje.Contains("sin lote")));
    }

    [Fact]
    public async Task Una_salida_cortada_deja_la_captura_pendiente_con_motivo_claro_y_su_consumo_cuenta()
    {
        using var e = new EntornoMira { Opciones = new OpcionesProcesador(Precios: new Precios(2m, 10m)) };
        e.Capturar("Una nota muy larga.");

        var r = await e.Procesar(proveedor: new ProveedorQueSeCorta());

        Assert.Empty(r.Procesadas);
        Assert.Empty(e.Registros());
        Assert.Contains("límite de tokens", Assert.Single(r.Problemas).Motivo);
        Assert.Equal("8000", Mapa(e.Doc(r.LoteId!).Frontmatter["uso"])["tokens_salida"]);
    }
}
