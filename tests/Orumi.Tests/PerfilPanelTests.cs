using System.Text.Json;
using Orumi.Agente;
using Orumi.Agente.Etiquetado;
using Orumi.Noa;
using Orumi.Panel;

namespace Orumi.Tests;

/// <summary>El perfil de Noa en el panel (spec <c>perfil-personal</c>, tarea 4.1): lectura, acciones del usuario, bloqueo, generación y descarga.</summary>
public class PerfilPanelTests : IDisposable
{
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "orumi-perfilpanel-" + Guid.NewGuid().ToString("N")[..8]);
    private static readonly DateTimeOffset Ahora = new(2026, 11, 20, 5, 0, 0, TimeSpan.FromHours(1));
    private readonly List<string> _auditoria = [];

    public PerfilPanelTests() => Directory.CreateDirectory(_datos);

    public void Dispose() => Directory.Delete(_datos, true);

    private static string Json(object o) => JsonSerializer.Serialize(o, new JsonSerializerOptions(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static Rasgo R(string clave, string apartado, string texto, string nivel = Niveles.Patron, bool oculto = false, string estado = EstadosDeRasgo.Activo) =>
        new(clave, apartado, texto, null, Certezas.Hipotesis, nivel, false, [new EvidenciaDeRasgo("reg-20261001-n001", "2026-10-01", "mensajes cortos")], 3, 3, [],
            estado, Rasgo.DelAgente, oculto, null, null, 1, Ahora, []);

    private void Sembrar()
    {
        var perfil = new Perfil(1, Ahora, "gen-1", 26,
            [
                R("prefiere_mensajes_cortos", "comunicacion", "Prefiere mensajes cortos y directos."),
                R("rasgo_oculto", "personalidad", "Es muy reservado con lo suyo.", oculto: true),
                R("rasgo_descartado", "habitos_rutinas", "Madruga mucho.", estado: EstadosDeRasgo.Descartado),
            ],
            [new Recomendacion("rec-0a1b2c3d", "Escribirle mensajes breves.", ["prefiere_mensajes_cortos"], EstadosDeRasgo.Activo, false)], [], []);
        AlmacenDePerfil.Guardar(_datos, perfil, copiaDeVersion: true);
    }

    private PerfilDelPanel Panel(ConfiguracionDeNoa? config = null, IProveedorDeTexto? proveedor = null) =>
        new(new RepositorioDeDatos(_datos, () => DateTimeOffset.UtcNow, TimeSpan.Zero), () => Ahora, _auditoria.Add, config, null, () => proveedor, _ => null);

    [Fact]
    public void Sin_perfil_se_informa_de_que_no_hay_nada_y_de_si_Noa_esta_activada()
    {
        var json = Json(Panel().Ver());

        Assert.Contains("\"perfil\":null", json);
        Assert.Contains("\"activada\":false", json);
        Assert.Null(Panel().Descarga());
    }

    [Fact]
    public void El_perfil_muestra_lo_oculto_marcado_y_nunca_lo_descartado()
    {
        Sembrar();

        var json = Json(Panel(new ConfiguracionDeNoa()).Ver());

        Assert.Contains("prefiere_mensajes_cortos", json);
        Assert.Contains("\"clave\":\"rasgo_oculto\"", json);
        Assert.Contains("\"oculto\":true", json);
        Assert.DoesNotContain("rasgo_descartado", json);
        Assert.Contains("rec-0a1b2c3d", json);
        Assert.Contains("\"cadenciaDias\":14", json);
    }

    [Fact]
    public void La_descarga_en_Markdown_no_incluye_lo_oculto_ni_lo_descartado()
    {
        Sembrar();

        var md = Panel().Descarga()!;

        Assert.Contains("Prefiere mensajes cortos y directos.", md);
        Assert.DoesNotContain("reservado", md);
        Assert.DoesNotContain("Madruga", md);
        Assert.Contains("No incluye diagnósticos", md);
    }

    [Fact]
    public void Corregir_desde_el_panel_prevalece_y_la_auditoria_no_lleva_texto_ni_correo()
    {
        Sembrar();

        var r = Panel().Accion("corregir", "prefiere_mensajes_cortos", "yo@ejemplo.com",
            (d, a) => { OperacionesDelPerfil.Corregir(d, "prefiere_mensajes_cortos", "Prefiere hablar por teléfono.", a); return "ok"; });

        Assert.Equal(200, r.Estado);
        Assert.Equal("Prefiere hablar por teléfono.", AlmacenDePerfil.Leer(_datos)!.Rasgos.Single(x => x.Clave == "prefiere_mensajes_cortos").Texto);
        var linea = Assert.Single(_auditoria);
        Assert.Contains("operacion=perfil-corregir", linea);
        Assert.Contains("resultado=ok", linea);
        Assert.DoesNotContain("teléfono", linea);
        Assert.DoesNotContain("yo@ejemplo.com", linea);
    }

    [Fact]
    public void Un_rasgo_inexistente_da_404_y_un_texto_invalido_400_sin_cambios()
    {
        Sembrar();
        var antes = File.ReadAllText(AlmacenDePerfil.RutaDelPerfil(_datos));

        var noExiste = Panel().Accion("descartar", "no_existe", null, (d, a) => { OperacionesDelPerfil.Descartar(d, "no_existe", a); return "ok"; });
        var corto = Panel().Accion("corregir", "prefiere_mensajes_cortos", null, (d, a) => { OperacionesDelPerfil.Corregir(d, "prefiere_mensajes_cortos", "no", a); return "ok"; });

        Assert.Equal(404, noExiste.Estado);
        Assert.Equal(400, corto.Estado);
        Assert.Equal(antes, File.ReadAllText(AlmacenDePerfil.RutaDelPerfil(_datos)));
    }

    [Fact]
    public void Mientras_Noa_trabaja_las_acciones_dan_409_y_no_cambian_nada()
    {
        Sembrar();
        var antes = File.ReadAllText(AlmacenDePerfil.RutaDelPerfil(_datos));
        using var trabajando = BloqueoDeNoa.Adquirir(_datos);

        var r = Panel().Accion("borrar", "prefiere_mensajes_cortos", null, (d, a) => { OperacionesDelPerfil.Borrar(d, "prefiere_mensajes_cortos", a); return "ok"; });

        Assert.Equal(409, r.Estado);
        Assert.Contains("\"generando\":true", Json(Panel().Ver()));
        Assert.Equal(antes, File.ReadAllText(AlmacenDePerfil.RutaDelPerfil(_datos)));
    }

    [Fact]
    public void Generar_sin_Noa_activada_o_sin_proveedor_no_envia_nada()
    {
        var proveedor = new ProveedorDeTextoSimulado((_, _) => "{}");

        var sinConfig = Panel(null, proveedor).Generar(null);
        var sinProveedor = Panel(new ConfiguracionDeNoa(), null).Generar(null);

        Assert.Equal(409, sinConfig.Estado);
        Assert.Equal(409, sinProveedor.Estado);
        Assert.Empty(proveedor.Peticiones);
    }

    [Fact]
    public void Generar_con_Noa_ya_trabajando_da_409_y_no_lanza_otra()
    {
        var proveedor = new ProveedorDeTextoSimulado((_, _) => "{}");
        using var trabajando = BloqueoDeNoa.Adquirir(_datos);

        var r = Panel(new ConfiguracionDeNoa(), proveedor).Generar(null);

        Assert.Equal(409, r.Estado);
        Assert.Empty(proveedor.Peticiones);
    }

    [Fact]
    public async Task Generar_acepta_la_peticion_responde_202_y_libera_el_bloqueo_al_terminar()
    {
        var proveedor = new ProveedorDeTextoSimulado((_, _) => "{}");

        var r = Panel(new ConfiguracionDeNoa(), proveedor).Generar("yo@ejemplo.com");

        Assert.Equal(202, r.Estado);
        for (var i = 0; i < 100 && AlmacenDePerfil.Generaciones(_datos).Count == 0; i++)
        {
            await Task.Delay(50);
        }

        // Sin registros no hay observación posible: se omite y se anota, sin enviar nada.
        Assert.Equal(Generacion.Omitida, Assert.Single(AlmacenDePerfil.Generaciones(_datos)).Resultado);
        Assert.Empty(proveedor.Peticiones);
        using var libre = BloqueoDeNoa.Adquirir(_datos);
        Assert.NotNull(libre);
        Assert.DoesNotContain(_auditoria, l => l.Contains("yo@ejemplo.com"));
    }

    [Theory]
    [InlineData("prefiere_mensajes_cortos", true)]
    [InlineData("apartado:comunicacion", true)]
    [InlineData("rec-0a1b2c3d", true)]
    [InlineData("../etc/passwd", false)]
    [InlineData("Rasgo", false)]
    [InlineData("a", false)]
    [InlineData("rec-XYZ", false)]
    [InlineData("apartado:../x", false)]
    public void Solo_se_aceptan_identificadores_de_rasgo_apartado_o_recomendacion(string objetivo, bool valido) =>
        Assert.Equal(valido, PerfilDelPanel.EsObjetivoValido(objetivo));

    [Fact]
    public void Borrar_todo_desde_el_panel_elimina_el_perfil_y_no_toca_registros_ni_capturas()
    {
        Sembrar();
        Directory.CreateDirectory(Path.Combine(_datos, "registros"));
        File.WriteAllText(Path.Combine(_datos, "registros", "r.md"), "intacto");

        var r = Panel().Accion("borrar-todo", null, null, (d, _) => { OperacionesDelPerfil.BorrarTodo(d); return "ok"; });

        Assert.Equal(200, r.Estado);
        Assert.Null(AlmacenDePerfil.Leer(_datos));
        Assert.Equal("intacto", File.ReadAllText(Path.Combine(_datos, "registros", "r.md")));
    }
}
