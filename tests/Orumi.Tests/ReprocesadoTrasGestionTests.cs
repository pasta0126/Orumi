using Orumi.Agente;
using Orumi.Core.Correcciones;

namespace Orumi.Tests;

public class ReprocesadoTrasGestionTests
{
    private static Dictionary<string, object?> Mapa(object? o) => (Dictionary<string, object?>)o!;

    private static List<object?> Lista(object? o) => (List<object?>)o!;

    private static Operaciones Ops(EntornoMira e) => new(e.Datos, () => e.Ahora.AddHours(1));

    private static IEnumerable<string> Errores(EntornoMira e) => e.Validar().Where(i => !i.Mensaje.Contains("sin lote")).Select(i => i.ToString());

    private static string CapturaDe(EntornoMira e, string registroId) =>
        (string)Mapa(Lista(e.Doc(registroId).Frontmatter["fuentes"])[0])["captura"]!;

    /// <summary>Dos capturas con un registro cada una (Marta y Pau). El modelo devuelve siempre lo mismo para cada nota.</summary>
    private static async Task<(string R1, string R2)> Preparar(EntornoMira e)
    {
        e.Capturar("Hoy llamé a Marta por el presupuesto.", e.Ahora);
        e.Capturar("Hoy comí con Pau.", e.Ahora.AddMinutes(1));
        e.Respuesta = s => s.Texto.Contains("Marta")
            ? Datos.Resp([Datos.Registro(s.Texto, "actividad", "Llamada con Marta", campos: new() { ["personas"] = new List<object?> { "Marta" } }, certeza: new() { ["personas"] = "dicho" })])
            : Datos.Resp([Datos.Registro(s.Texto, "evento", "Comida con Pau", campos: new() { ["personas"] = new List<object?> { "Pau" } }, certeza: new() { ["personas"] = "dicho" })]);
        await e.Procesar();
        return (e.Registros().Single(r => (string)r.Frontmatter["titulo"]! == "Llamada con Marta").Id!, e.Registros().Single(r => (string)r.Frontmatter["titulo"]! == "Comida con Pau").Id!);
    }

    [Fact]
    public async Task Reprocesar_tras_eliminar_no_recrea_ni_reactiva_el_registro()
    {
        using var e = new EntornoMira();
        var (r1, _) = await Preparar(e);
        var captura = CapturaDe(e, r1);
        Ops(e).Eliminar(r1);
        e.Ahora = e.Ahora.AddDays(1);

        var r = await e.Procesar(reprocesar: [captura]);

        Assert.Equal(2, e.Registros().Count); // ni uno nuevo
        Assert.Equal("eliminado", e.Doc(r1).Frontmatter["estado"]);
        Assert.Empty(r.RegistrosCreados);
        Assert.Empty(r.RegistrosActualizados);
        Assert.Contains("omitido", e.Cargar().DeTipo("lote").OrderBy(l => l.Id).Last().Cuerpo + string.Join(' ', Lista(e.Cargar().DeTipo("lote").OrderBy(l => l.Id).Last().Frontmatter["capturas"]).Select(c => Mapa(c).GetValueOrDefault("nota"))));
        Assert.Empty(Errores(e));
    }

    [Fact]
    public async Task Un_registro_eliminado_no_aparece_en_la_solicitud_al_reprocesar_su_captura_ni_en_otras()
    {
        using var e = new EntornoMira();
        var (r1, _) = await Preparar(e);
        var captura = CapturaDe(e, r1);
        Ops(e).Eliminar(r1);
        SolicitudExtraccion? ultima = null;
        e.Respuesta = s => { if (s.CapturaId == captura) { ultima = s; } return Datos.Resp([Datos.Registro(s.Texto, "actividad", "Llamada con Marta")]); };
        e.Ahora = e.Ahora.AddDays(1);

        await e.Procesar(reprocesar: [captura]);

        Assert.NotNull(ultima);
        Assert.Empty(ultima!.ClavesExistentes); // la clave de la tumba no se ofrece
        Assert.DoesNotContain(ultima.Relacionados, x => x.Id == r1);
        Assert.Equal(2, e.Registros().Count);
    }

    [Fact]
    public async Task Reprocesar_tras_fusionar_no_reabre_el_origen_ni_toca_el_destino()
    {
        using var e = new EntornoMira();
        var (r1, r2) = await Preparar(e);
        Ops(e).Fusionar(r1, r2);
        var destinoAntes = File.ReadAllText(Path.Combine(e.Datos, e.Doc(r1).Ruta));
        var captura = CapturaDe(e, r2);
        e.Ahora = e.Ahora.AddDays(1);

        await e.Procesar(reprocesar: [captura]);

        Assert.Equal("fusionado", e.Doc(r2).Frontmatter["estado"]);
        Assert.Equal(2, e.Registros().Count);
        Assert.Equal(destinoAntes, File.ReadAllText(Path.Combine(e.Datos, e.Doc(r1).Ruta)));
        Assert.Empty(Errores(e));
    }

    [Fact]
    public async Task Reprocesar_tras_separar_no_vuelve_a_ampliar_el_original_y_conserva_el_registro_separado()
    {
        using var e = new EntornoMira();
        e.Ahora = new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.FromHours(1));
        e.Capturar("Hoy he empezado a pintar el pasillo, aún no he terminado.");
        e.Respuesta = _ => Datos.Resp([Datos.Registro("Hoy he empezado a pintar el pasillo, aún no he terminado.", "actividad", "Pintar el pasillo", dia: "2026-11-13",
            multidia: new MultidiaExtraido("abierto", null, null, "dicho", "interpretado"))]);
        await e.Procesar();
        var original = e.Registros().Single().Id!;
        e.Ahora = new DateTimeOffset(2026, 11, 16, 18, 0, 0, TimeSpan.FromHours(1));
        var c2 = e.Capturar("Hoy he terminado de pintar el pasillo.").Id;
        e.Respuesta = s => Datos.Resp([Datos.Registro("Hoy he terminado de pintar el pasillo.", "actividad", "Pintar el pasillo", dia: null, amplia: s.Relacionados.Single().Id,
            multidia: new MultidiaExtraido("cerrado", "2026-11-16", "2026-11-16", "dicho", "interpretado"))]);
        await e.Procesar();
        Ops(e).Separar(original, c2, "Terminar de pintar");
        var separado = e.Registros().Single(r => r.Id != original).Id!;

        // El modelo, al reprocesar la nota separada, vuelve a proponer ampliar el original.
        e.Respuesta = s => Datos.Resp([Datos.Registro("Hoy he terminado de pintar el pasillo.", "actividad", "Pintar el pasillo", dia: null, amplia: original)]);
        e.Ahora = e.Ahora.AddDays(1);
        await e.Procesar(reprocesar: [c2]);

        Assert.Single(Lista(e.Doc(original).Frontmatter["fuentes"]));
        Assert.Equal(2, e.Registros().Count);
        Assert.Equal("Terminar de pintar", e.Doc(separado).Frontmatter["titulo"]);
        Assert.Empty(Errores(e));
    }

    [Fact]
    public async Task Tras_deshacer_una_correccion_mira_puede_volver_a_actualizar_ese_campo_y_antes_no()
    {
        using var e = new EntornoMira();
        var (r1, _) = await Preparar(e);
        var captura = CapturaDe(e, r1);
        Ops(e).Corregir(r1, "personas", "Marta, Pau");
        e.Respuesta = s => Datos.Resp([Datos.Registro(s.Texto, "actividad", "Llamada con Marta", campos: new() { ["personas"] = new List<object?> { "Marta", "Ana" } }, certeza: new() { ["personas"] = "dicho" })]);

        e.Ahora = e.Ahora.AddDays(1);
        await e.Procesar(reprocesar: [captura]);
        Assert.Equal(["Marta", "Pau"], Lista(e.Doc(r1).Frontmatter["personas"]).Cast<string>()); // protegido por la corrección

        Ops(e).Deshacer(r1, "personas");
        e.Ahora = e.Ahora.AddDays(1);
        await e.Procesar(reprocesar: [captura]);
        Assert.Equal(["Marta", "Ana"], Lista(e.Doc(r1).Frontmatter["personas"]).Cast<string>()); // vuelve a ser de Mira
        Assert.Empty(Errores(e));
    }

    [Fact]
    public async Task Una_relacion_hacia_un_registro_omitido_se_descarta_sin_romper_el_reprocesado()
    {
        using var e = new EntornoMira();
        var (r1, _) = await Preparar(e);
        var captura = CapturaDe(e, r1);
        Ops(e).Eliminar(r1);
        e.Respuesta = s => Datos.Resp([
            Datos.Registro(s.Texto, "actividad", "Llamada con Marta"),
            Datos.Registro(s.Texto, "idea", "Otra idea", relaciones: [new RelacionExtraida("relacionado_con", "interpretado", null, 0)])]);
        e.Ahora = e.Ahora.AddDays(1);

        var r = await e.Procesar(reprocesar: [captura]);

        Assert.Empty(r.Problemas);
        Assert.Equal(3, e.Registros().Count); // «Otra idea» sí se crea (no coincide con la tumba); la «Llamada» se omite
        var idea = e.Registros().Single(x => (string)x.Frontmatter["titulo"]! == "Otra idea");
        Assert.Empty(Lista(idea.Frontmatter.GetValueOrDefault("relaciones") ?? new List<object?>()));
        Assert.Empty(Errores(e));
    }
}
