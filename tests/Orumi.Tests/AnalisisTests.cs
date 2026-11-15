using System.Globalization;
using Orumi.Agente;
using Orumi.Agente.Analisis;
using Orumi.Agente.Clima;
using Orumi.Cli;
using Orumi.Core.Capturas;
using Orumi.Core.Ficheros;
using Orumi.Core.Hipotesis;

namespace Orumi.Tests;

/// <summary>Contraste de hipótesis, revisión semanal y preguntas de contraste (spec <c>contraste-de-hipotesis</c>, X20–X23, X63).</summary>
public class AnalisisTests : IDisposable
{
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "orumi-ana-" + Guid.NewGuid().ToString("N")[..8]);
    private static readonly DateTimeOffset Inicio = new(2026, 10, 1, 4, 0, 0, TimeSpan.FromHours(2));
    private static readonly DateTimeOffset Ahora = new(2026, 12, 1, 4, 0, 0, TimeSpan.FromHours(1));
    private static readonly DateOnly Hoy = new(2026, 12, 1);
    private static readonly UmbralesDeAnalisis U = new();
    private static readonly ConfiguracionDeClima Clima = ConfiguracionDeClima.Leer([])!;
    private int _n;

    public AnalisisTests() => Directory.CreateDirectory(_datos);

    public void Dispose() => Directory.Delete(_datos, true);

    // ---------------------------------------------------------------- datos sintéticos

    private static DateOnly Dia(int i) => new DateOnly(2026, 10, 1).AddDays(i);

    private string Registro(DateOnly dia, string? ciudad = null, params (string Concepto, string Estado, int? Minutos)[] etiquetas)
    {
        var comunicada = new DateTimeOffset(dia.ToDateTime(new TimeOnly(20, 0)), TimeSpan.FromHours(dia.Month >= 11 || dia.Month <= 3 ? 1 : 2));
        var c = CreadorDeCapturas.Crear(_datos, $"Nota del día {dia:yyyy-MM-dd} número {_n}.", comunicada, ciudad: ciudad);
        var id = $"reg-{dia:yyyyMMdd}-x{++_n:000}";
        var ruta = Path.Combine(_datos, "registros", dia.ToString("yyyy"), dia.ToString("MM"), id + ".md");
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllText(ruta, $"---\nid: {id}\ntipo: registro\nclase: actividad\ntitulo: Nota\ncomunicada_el: {comunicada:yyyy-MM-dd'T'HH:mm:sszzz}\n" +
            $"fuentes:\n  - captura: {c.Id}\n    rol: origen\nfecha: {{desde: {dia:yyyy-MM-dd}, hasta: {dia:yyyy-MM-dd}}}\n---\nNota.\n");
        if (etiquetas.Length > 0)
        {
            AlmacenDeEtiquetas.Guardar(_datos, new EtiquetasDeRegistro(id, "h", Ahora, "simulado", "x",
                [.. etiquetas.Select(e => new Etiqueta(e.Concepto, e.Estado, "mira", $"cita de {e.Concepto}", e.Minutos))]));
        }

        return id;
    }

    private void ClimaDelDia(DateOnly dia, double? uv = 3, double? lluvia = 0, double nubes = 40, double sol = 20000, string ciudad = "Barcelona", bool escribir = true)
    {
        if (!escribir)
        {
            return;
        }

        var c = Clima.Buscar(ciudad)!;
        var d = new ClimaDia(dia, c.Nombre, "open-meteo", c.LatitudRedondeada, c.LongitudRedondeada, "Europe/Madrid", Ahora,
            new ResumenDelDia(uv, "07:00", "17:30", 36000, sol, lluvia, 10, 18, 70, nubes),
            Enumerable.Range(0, 24).Select(h => new ClimaHora(h, 14, 70, 0, nubes, h is > 7 and < 18 ? 200 : 0, 0, uv)).ToList());
        var ruta = FormatoDeClima.Ruta(_datos, dia, c);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllText(ruta, FormatoDeClima.Escribir(d));
    }

    private ResultadoDeContraste Analizar(string id, DateOnly? hoy = null) =>
        MotorDeContraste.Analizar(_datos, Clima, U, hoy ?? Hoy).Contrastes.Single(c => c.Id == id);

    /// <summary>24 días de octubre: UV alto en los pares y bajo en los impares.</summary>
    private void DiasConUv(Func<int, bool, string?> efecto)
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        for (var i = 0; i < 24; i++)
        {
            var alto = i % 2 == 0;
            ClimaDelDia(Dia(i), uv: alto ? 7.5 : 3);
            var e = efecto(i, alto);
            Registro(Dia(i), null, e is null ? [] : [("dolor_de_cabeza", e, null)]);
        }
    }

    // ---------------------------------------------------------------- veredictos

    [Fact]
    public void Una_asociacion_clara_con_datos_suficientes_queda_apoyada_y_no_afirma_causa()
    {
        // UV alto: dolor de cabeza en 9 de 12 días y ausencia confirmada en los otros 3. UV bajo: 1 de 12 con dolor, 11 confirmados sin él.
        var vistos = new Dictionary<bool, int> { [true] = 0, [false] = 0 };
        DiasConUv((i, alto) => { var k = vistos[alto]++; return alto ? (k < 9 ? "presente" : "negado") : (k < 1 ? "presente" : "negado"); });

        var r = Analizar("hip-uv-dolor-de-cabeza#c1");

        Assert.Equal(Veredicto.Apoyada, r.Veredicto);
        Assert.Equal((12, 9, 12, 1), (r.DiasConFactor, r.EfectoConFactor, r.DiasSinFactor, r.EfectoSinFactor));
        Assert.Equal(0.75, r.TasaConFactor);
        Assert.True(r.Diferencia > 0.6);
        Assert.Contains("apoyan tu hipótesis", r.Texto);
        Assert.Contains("no indica que sea la causa", r.Texto);
        Assert.DoesNotContain("recomiend", r.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("deberías", r.Texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sin_diferencia_entre_los_dias_con_y_sin_factor_el_resultado_es_sin_apoyo_con_la_misma_claridad()
    {
        var vistos = new Dictionary<bool, int> { [true] = 0, [false] = 0 };
        DiasConUv((i, alto) => vistos[alto]++ < 6 ? "presente" : "negado");

        var r = Analizar("hip-uv-dolor-de-cabeza#c1");

        Assert.Equal(Veredicto.SinApoyo, r.Veredicto);
        Assert.Equal(0.5, r.TasaConFactor);
        Assert.Equal(0.5, r.TasaSinFactor);
        Assert.Contains("no muestran la diferencia que esperabas", r.Texto);
    }

    [Fact]
    public void Con_pocos_datos_el_resultado_es_insuficiente_con_el_recuento_y_lo_que_falta()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        for (var i = 0; i < 6; i++)
        {
            ClimaDelDia(Dia(i), uv: i % 2 == 0 ? 7.5 : 3);
            Registro(Dia(i), null, i == 0 ? [("dolor_de_cabeza", "presente", null)] : []);
        }

        var r = Analizar("hip-uv-dolor-de-cabeza#c1");

        Assert.Equal(Veredicto.Insuficiente, r.Veredicto);
        Assert.Equal((1, 1, 0, 0), (r.DiasConFactor, r.EfectoConFactor, r.DiasSinFactor, r.EfectoSinFactor));
        Assert.Contains(r.Motivos, m => m.Contains("1 de 4 necesarios"));
        Assert.Contains(r.Motivos, m => m.Contains("días con el efecto mencionado: 1 de 3"));
        Assert.Contains("Aún no hay evidencia suficiente", r.Texto);
    }

    [Fact]
    public void Un_dia_sin_dato_del_efecto_no_cuenta_como_que_no_ocurrio_y_se_cuenta_aparte()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        for (var i = 0; i < 10; i++)
        {
            ClimaDelDia(Dia(i), uv: 7.5);
            Registro(Dia(i), null, i < 3 ? [("dolor_de_cabeza", "presente", null)] : []); // 7 días sin mención
        }

        var r = Analizar("hip-uv-dolor-de-cabeza#c1");

        Assert.Equal(3, r.DiasConFactor);
        Assert.Equal(3, r.EfectoConFactor);
        Assert.Equal(7, r.ExcluidosEfectoSinDato);
        Assert.Equal(1.0, r.TasaConFactor); // 3 de 3 conocidos, no 3 de 10
        Assert.Contains("7 día(s) sin dato del efecto (no se cuentan como que no ocurriera)", r.Texto);
    }

    [Fact]
    public void Los_dias_sin_clima_se_excluyen_y_se_cuentan()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        for (var i = 0; i < 8; i++)
        {
            ClimaDelDia(Dia(i), uv: 7.5, escribir: i >= 3);
            Registro(Dia(i), null, [("dolor_de_cabeza", "presente", null)]);
        }

        var r = Analizar("hip-uv-dolor-de-cabeza#c1");

        Assert.Equal(3, r.ExcluidosSinClima);
        Assert.Equal(5, r.DiasConFactor);
        Assert.Contains("3 día(s) sin datos del clima, excluidos", r.Texto);
    }

    [Fact]
    public void Un_uv_desconocido_no_se_estima_el_dia_se_excluye()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        ClimaDelDia(Dia(0), uv: null);
        Registro(Dia(0), null, [("dolor_de_cabeza", "presente", null)]);

        var r = Analizar("hip-uv-dolor-de-cabeza#c1");

        Assert.Equal(1, r.ExcluidosSinClima);
        Assert.Equal(0, r.DiasConFactor + r.DiasSinFactor);
    }

    [Fact]
    public void El_dia_en_curso_y_las_ventanas_que_aun_no_han_terminado_no_cuentan()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        ClimaDelDia(Hoy.AddDays(-1), uv: 7.5);
        Registro(Hoy.AddDays(-1), null);                                      // ayer: sin mención del efecto
        ClimaDelDia(Hoy, uv: 7.5);
        Registro(Hoy, null, [("dolor_de_cabeza", "presente", null)]);          // hoy: no se cuenta

        var r = Analizar("hip-uv-dolor-de-cabeza#c1");

        Assert.Equal(0, r.DiasConFactor);
        Assert.Equal(1, r.ExcluidosEfectoSinDato); // solo ayer, sin dato del efecto
    }

    // ---------------------------------------------------------------- factores del usuario

    [Fact]
    public void Larga_exposicion_al_sol_exige_mas_de_90_minutos_fuera_en_un_dia_soleado_y_sin_duracion_el_dia_queda_sin_dato()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        // 0: 3 h fuera, soleado → factor; 1: 1 h fuera, soleado → no; 2: fuera sin duración, soleado → sin dato; 3: 3 h fuera pero nublado → no; 4: sin salir, soleado → no
        void D(int i, double nubes, double sol, params (string, string, int?)[] et) { ClimaDelDia(Dia(i), nubes: nubes, sol: sol); Registro(Dia(i), null, et); }
        D(0, 10, 30000, ("exterior", "presente", 180), ("cansancio_fisico", "presente", null));
        D(1, 10, 30000, ("exterior", "presente", 60));
        D(2, 10, 30000, ("exterior", "presente", null));
        D(3, 85, 4000, ("exterior", "presente", 180));
        D(4, 10, 30000);

        var r = Analizar("hip-sol-cansancio#c1");

        Assert.Equal(1, r.DiasConFactor);
        Assert.Equal(1, r.EfectoConFactor);
        Assert.Equal(1, r.ExcluidosFactorSinDato);
        Assert.Contains("más de 90 min al aire libre y día soleado o despejado", r.FactorTexto);
    }

    [Fact]
    public void La_duracion_que_responde_el_usuario_resuelve_el_dia_sin_dato()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        ClimaDelDia(Dia(0), nubes: 10, sol: 30000);
        var reg = Registro(Dia(0), null, ("exterior", "presente", null));
        EscribirPregunta(reg, "exterior_duracion", Dia(0), "respondida", "unas dos horas");

        var r = Analizar("hip-sol-cansancio#c1");

        Assert.Equal(1, r.DiasConFactor + r.DiasSinFactor + r.ExcluidosEfectoSinDato);
        Assert.Equal(0, r.ExcluidosFactorSinDato); // 120 min ≥ 90: el factor se resuelve
    }

    [Fact]
    public void Socializar_se_busca_el_agotamiento_el_mismo_dia_y_la_introspeccion_en_la_ventana_de_uno_a_dos_dias()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        ClimaDelDia(Dia(0)); ClimaDelDia(Dia(1)); ClimaDelDia(Dia(2)); ClimaDelDia(Dia(3));
        Registro(Dia(0), null, ("socializacion", "presente", null), ("agotamiento_social", "presente", null)); // quedó con amigos y acabó agotado
        Registro(Dia(1), null);
        Registro(Dia(2), null, ("soledad_introspeccion", "presente", null)); // dos días después
        Registro(Dia(3), null);

        var agotamiento = Analizar("hip-socializacion-agotamiento#c1");
        var introspeccion = Analizar("hip-socializacion-agotamiento#c2");

        Assert.Equal((1, 1), (agotamiento.DiasConFactor, agotamiento.EfectoConFactor));
        Assert.Equal((1, 1), (introspeccion.DiasConFactor, introspeccion.EfectoConFactor)); // el día 2 está dentro de 1-2 días
    }

    [Fact]
    public void La_introspeccion_fuera_de_la_ventana_no_cuenta_para_ese_dia()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        for (var i = 0; i < 5; i++)
        {
            ClimaDelDia(Dia(i));
        }

        Registro(Dia(0), null, ("socializacion", "presente", null));
        Registro(Dia(3), null, ("soledad_introspeccion", "presente", null)); // 3 días después: fuera de 1-2

        var r = Analizar("hip-socializacion-agotamiento#c2");

        Assert.Equal(0, r.EfectoConFactor);
        Assert.Equal(2, r.ExcluidosEfectoSinDato); // los días 0 y 3 no tienen el efecto en su ventana: sin dato, no «no ocurrió»
    }

    [Fact]
    public void La_jornada_laboral_ordinaria_no_cuenta_como_socializacion()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        ClimaDelDia(Dia(0));
        Registro(Dia(0), null); // reunión de trabajo: el modelo no etiquetó socialización

        var r = Analizar("hip-socializacion-agotamiento#c1");

        Assert.Equal(0, r.DiasConFactor); // el día no tiene el factor
        Assert.Equal(1, r.ExcluidosEfectoSinDato);
    }

    [Fact]
    public void La_lluvia_se_mide_con_el_umbral_de_milimetros_del_dia()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        ClimaDelDia(Dia(0), lluvia: 12.4);
        ClimaDelDia(Dia(1), lluvia: 0.2);
        Registro(Dia(0), null, ("relajacion", "presente", null));
        Registro(Dia(1), null, ("relajacion", "negado", null));

        var r = Analizar("hip-lluvia-relajacion#c1");

        Assert.Equal((1, 1, 1, 0), (r.DiasConFactor, r.EfectoConFactor, r.DiasSinFactor, r.EfectoSinFactor));
    }

    [Fact]
    public void Una_etiqueta_quitada_por_el_usuario_no_cuenta()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        ClimaDelDia(Dia(0), lluvia: 12.4);
        var reg = Registro(Dia(0), null, ("relajacion", "presente", null));
        AlmacenDeEtiquetas.Corregir(_datos, reg, "relajacion", "quitar", Ahora);

        var r = Analizar("hip-lluvia-relajacion#c1");

        Assert.Equal(0, r.EfectoConFactor);
        Assert.Equal(1, r.ExcluidosEfectoSinDato);
    }

    // ---------------------------------------------------------------- respuestas a preguntas de contraste

    private string EscribirPregunta(string registro, string concepto, DateOnly dia, string estado, string respuesta)
    {
        var id = $"preg-20261120-p{++_n:000}";
        var ruta = Path.Combine(_datos, "preguntas", "2026", "11", id + ".md");
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllText(ruta, $"---\nid: {id}\ntipo: pregunta\nregistro: {registro}\notros_registros: []\ncampo: contraste\nconcepto: {concepto}\ndia: {dia:yyyy-MM-dd}\n" +
            $"texto: \"¿…?\"\nestado: {estado}\nvalor: baja\ncreada_el: 2026-11-20T04:00:00+01:00\ncerrada_el: 2026-11-21T09:00:00+01:00\n" +
            $"respuesta: {{texto: \"{respuesta}\", respondida_el: 2026-11-21T09:00:00+01:00}}\n---\nP.\n");
        return id;
    }

    [Fact]
    public void No_sucedio_confirma_la_ausencia_y_una_respuesta_confirma_que_ocurrio()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        ClimaDelDia(Dia(0), uv: 7.5); ClimaDelDia(Dia(1), uv: 7.5);
        var a = Registro(Dia(0), null);
        var b = Registro(Dia(1), null);
        EscribirPregunta(a, "dolor_de_cabeza", Dia(0), "no_sucedio", "No sucedió.");
        EscribirPregunta(b, "dolor_de_cabeza", Dia(1), "respondida", "Sí, por la tarde");

        var r = Analizar("hip-uv-dolor-de-cabeza#c1");

        Assert.Equal((2, 1), (r.DiasConFactor, r.EfectoConFactor));
        Assert.Equal(0, r.ExcluidosEfectoSinDato);
    }

    // ---------------------------------------------------------------- ciudad del día

    [Fact]
    public void El_clima_de_cada_registro_es_el_de_la_ciudad_de_su_captura()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        ClimaDelDia(Dia(0), lluvia: 0, ciudad: "Barcelona");
        ClimaDelDia(Dia(0), lluvia: 15, ciudad: "Segur de Calafell");
        Registro(Dia(0), "Segur de Calafell", ("relajacion", "presente", null)); // ese día llovió en Segur, no en Barcelona

        var r = Analizar("hip-lluvia-relajacion#c1");

        Assert.Equal((1, 1), (r.DiasConFactor, r.EfectoConFactor));
        Assert.Equal("Segur de Calafell", r.Evidencia.Single().Ciudad);
    }

    // ---------------------------------------------------------------- etapa de observación y revisión

    [Fact]
    public void Antes_de_cuatro_semanas_la_revision_solo_informa_del_progreso_y_no_pregunta()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        var hoy = new DateTimeOffset(2026, 10, 12, 4, 0, 0, TimeSpan.FromHours(2));
        for (var i = 0; i < 8; i++)
        {
            ClimaDelDia(Dia(i), uv: 7.5);
            Registro(Dia(i), null, [("dolor_de_cabeza", "presente", null)]);
        }

        var r = RevisionSemanal.Generar(_datos, null, Clima, U, hoy);

        Assert.True(r.EnObservacion);
        Assert.Empty(r.PreguntasCreadas);
        var yaml = File.ReadAllText(r.Ruta);
        Assert.Contains("estado: observacion", yaml);
        Assert.Contains("dias_transcurridos: 11", yaml);
        Assert.Contains("dias_necesarios: 28", yaml);
        Assert.DoesNotContain("veredicto", yaml);
        Assert.False(Directory.Exists(Path.Combine(_datos, "preguntas"))); // en observación no se pregunta nada
    }

    [Fact]
    public void La_revision_semanal_lleva_los_contrastes_con_evidencia_enlazada_y_el_apartado_de_aprendizaje()
    {
        var vistos = new Dictionary<bool, int> { [true] = 0, [false] = 0 };
        DiasConUv((i, alto) => { var k = vistos[alto]++; return alto ? (k < 9 ? "presente" : "negado") : (k < 1 ? "presente" : "negado"); });

        var r = RevisionSemanal.Generar(_datos, null, Clima, U, Ahora);

        Assert.False(r.EnObservacion);
        Assert.Equal(Path.Combine(_datos, "analisis", "2026", "semana-2026-W49.yaml"), r.Ruta);
        var yaml = File.ReadAllText(r.Ruta);
        Assert.Contains("veredicto: apoyada", yaml);
        Assert.Contains("un índice UV alto con luz fuerte me provoca dolor de cabeza", yaml); // sus palabras
        Assert.Contains("registros: [reg-", yaml);                                              // trazabilidad a los registros
        Assert.Contains("clima: \"UV máx 7.5", yaml);                                          // y al clima usado
        Assert.Contains("aprendizaje:", yaml);
        Assert.Contains("tendencia:", yaml);
        Assert.Contains("reglas_activas:", yaml);
        foreach (var prohibido in new[] { "recomend", "deberías", "te aconsejo" })
        {
            Assert.DoesNotContain(prohibido, yaml, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Regenerar_la_semana_reemplaza_el_fichero_y_no_duplica()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        ClimaDelDia(Dia(0)); Registro(Dia(0), null);

        RevisionSemanal.Generar(_datos, null, Clima, U, Ahora);
        RevisionSemanal.Generar(_datos, null, Clima, U, Ahora.AddHours(1));

        Assert.Single(Directory.GetFiles(Path.Combine(_datos, "analisis", "2026"), "semana-*.yaml"));
        Assert.True(RevisionSemanal.Existe(_datos, Hoy));
        Assert.False(RevisionSemanal.Existe(_datos, Hoy.AddDays(7)));
    }

    [Fact]
    public void Las_sugerencias_de_umbral_solo_se_proponen_y_no_cambian_la_configuracion()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        for (var i = 0; i < 10; i++)
        {
            ClimaDelDia(Dia(i), uv: i < 2 ? 6.5 : 5.5); // pocos días con UV ≥ 6; muchos con ≥ 5
            Registro(Dia(i), null, [("dolor_de_cabeza", "presente", null)]);
        }

        var r = RevisionSemanal.Generar(_datos, null, Clima, U, Ahora);

        var yaml = File.ReadAllText(r.Ruta);
        Assert.Contains("Hay 2 día(s) con UV ≥ 6; con un umbral de 5 habría 10", yaml);
        Assert.Contains("No se cambia solo", yaml);
        Assert.Equal(6, Clima.Umbrales.UvAlto);
    }

    // ---------------------------------------------------------------- revisión del usuario (X22)

    [Fact]
    public void Lo_matizado_por_el_usuario_se_muestra_con_su_comentario_y_prevalece()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        ClimaDelDia(Dia(0)); Registro(Dia(0), null);

        Assert.Throws<HipotesisInvalidaException>(() => AlmacenDeHipotesis.Revisar(_datos, "hip-uv-dolor-de-cabeza", null, "matizar", null, 0, Ahora)); // sin comentario
        AlmacenDeHipotesis.Revisar(_datos, "hip-uv-dolor-de-cabeza", null, "matizar", "Solo si además estoy sin gafas de sol", 0, Ahora);

        var r = Analizar("hip-uv-dolor-de-cabeza#c1");
        Assert.Equal("matizada", r.Revision!.Estado);
        Assert.Contains("Lo matizaste tú: «Solo si además estoy sin gafas de sol»", r.Texto);
        Assert.Equal("matizada", AlmacenDeHipotesis.Leer(_datos, "hip-uv-dolor-de-cabeza")!.Estado);
    }

    [Fact]
    public void Lo_descartado_no_vuelve_a_proponerse_salvo_evidencia_nueva_sustancial()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        for (var i = 0; i < 3; i++)
        {
            ClimaDelDia(Dia(i), uv: 7.5);
            Registro(Dia(i), null, [("dolor_de_cabeza", "presente", null)]);
        }

        // Se descarta cuando hay 3 días con el efecto: no vuelve con 4 o 5, sí cuando se duplica (≥ 6, y ≥ 3 más).
        AlmacenDeHipotesis.Revisar(_datos, "hip-sol-cansancio", "c1", "confirmar", null, 0, Ahora);
        AlmacenDeHipotesis.Revisar(_datos, "hip-uv-dolor-de-cabeza", null, "descartar", "No me convence", 3, Ahora);

        var sin = MotorDeContraste.Analizar(_datos, Clima, U, Hoy);
        Assert.DoesNotContain(sin.Contrastes, c => c.Hipotesis == "hip-uv-dolor-de-cabeza");

        for (var i = 3; i < 5; i++)
        {
            ClimaDelDia(Dia(i), uv: 7.5);
            Registro(Dia(i), null, [("dolor_de_cabeza", "presente", null)]);
        }

        Assert.DoesNotContain(MotorDeContraste.Analizar(_datos, Clima, U, Hoy).Contrastes, c => c.Hipotesis == "hip-uv-dolor-de-cabeza"); // 5 < 6

        ClimaDelDia(Dia(5), uv: 7.5);
        Registro(Dia(5), null, [("dolor_de_cabeza", "presente", null)]);

        var vuelve = MotorDeContraste.Analizar(_datos, Clima, U, Hoy).Contrastes.Single(c => c.Hipotesis == "hip-uv-dolor-de-cabeza");
        Assert.True(vuelve.Reabierto);
        Assert.Contains("evidencia nueva sustancial", vuelve.Texto);
    }

    [Fact]
    public void Una_hipotesis_con_todos_sus_contrastes_descartados_queda_descartada_y_se_oculta()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);

        var h = AlmacenDeHipotesis.Revisar(_datos, "hip-lluvia-relajacion", null, "descartar", null, 0, Ahora);

        Assert.Equal(Hipotesis.Descartada, h.Estado);
        // Se sigue etiquetando en silencio para poder detectar evidencia nueva sustancial; no se muestra mientras no la haya.
        Assert.Contains("relajacion", AlmacenDeHipotesis.Conceptos(_datos).Keys);
        Assert.DoesNotContain(MotorDeContraste.Analizar(_datos, Clima, U, Hoy).Contrastes, c => c.Hipotesis == "hip-lluvia-relajacion");
        Assert.Throws<HipotesisInvalidaException>(() => AlmacenDeHipotesis.Revisar(_datos, "hip-sol-cansancio", null, "confirmar", null, 0, Ahora)); // dos contrastes: hay que indicar cuál
        Assert.Throws<HipotesisInvalidaException>(() => AlmacenDeHipotesis.Revisar(_datos, "hip-sol-cansancio", "c9", "confirmar", null, 0, Ahora));
        Assert.Throws<HipotesisInvalidaException>(() => AlmacenDeHipotesis.Revisar(_datos, "hip-sol-cansancio", "c1", "inventar", null, 0, Ahora));
    }

    // ---------------------------------------------------------------- preguntas de contraste

    private void DiasParaPreguntar(int dias = 10)
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        for (var i = 0; i < 24; i++)
        {
            ClimaDelDia(Dia(i), uv: i % 2 == 0 ? 7.5 : 3);
            Registro(Dia(i), null);
        }
    }

    [Fact]
    public void Mira_pregunta_por_pocos_dias_sin_dato_con_texto_neutro_y_sin_repetir()
    {
        DiasParaPreguntar();

        var r1 = RevisionSemanal.Generar(_datos, null, Clima, U, Ahora);
        Assert.Equal(U.MaximoPreguntasDeContraste, r1.PreguntasCreadas.Count);
        var docs = Almacen.Cargar(_datos).DeTipo("pregunta").ToList();
        Assert.All(docs, p =>
        {
            Assert.Equal("pendiente", p.Frontmatter["estado"]);
            Assert.Equal("contraste", p.Frontmatter["campo"]);
            Assert.Equal("Mira", ((Dictionary<string, object?>)p.Frontmatter["generada_por"]!)["agente"]);
            var texto = (string)p.Frontmatter["texto"]!;
            Assert.StartsWith("¿", texto);
            Assert.Contains("«No sucedió»", texto);
            foreach (var prohibido in new[] { "seguro que", "deberías", "recomend", "probablemente", "seguramente" })
            {
                Assert.DoesNotContain(prohibido, texto, StringComparison.OrdinalIgnoreCase);
            }
        });
        Assert.Empty(Validador_Errores());

        // Al cerrar o responder esas preguntas, la siguiente pasada no vuelve a preguntar por lo mismo.
        var preguntados = docs.Select(p => ((string)p.Frontmatter["concepto"]!, (string)p.Frontmatter["dia"]!)).ToHashSet();
        foreach (var p in docs)
        {
            new Orumi.Core.Correcciones.Operaciones(_datos, () => Ahora).CerrarSinDato(p.Id!, "no_sucedio");
        }

        var r2 = RevisionSemanal.Generar(_datos, null, Clima, U, Ahora.AddDays(7));
        var nuevos = Almacen.Cargar(_datos).DeTipo("pregunta").Where(p => r2.PreguntasCreadas.Contains(p.Id!))
            .Select(p => ((string)p.Frontmatter["concepto"]!, (string)p.Frontmatter["dia"]!)).ToList();
        Assert.DoesNotContain(nuevos, preguntados.Contains);
    }

    private IReadOnlyList<string> Validador_Errores() =>
        Orumi.Core.Validacion.Validador.Validar(Almacen.Cargar(_datos)).Where(i => i.Fichero.StartsWith("preguntas", StringComparison.Ordinal)).Select(i => i.Mensaje).ToList();

    [Fact]
    public void Las_preguntas_de_contraste_respetan_el_maximo_de_preguntas_pendientes_de_Mira()
    {
        DiasParaPreguntar();
        var config = Configuracion.Leer("preguntas:\n  maximo_pendientes: 1\n");

        var primera = RevisionSemanal.Generar(_datos, config, Clima, U, Ahora);
        var segunda = RevisionSemanal.Generar(_datos, config, Clima, U, Ahora.AddDays(7));

        Assert.Single(primera.PreguntasCreadas);
        Assert.Empty(segunda.PreguntasCreadas); // ya hay una pendiente y el máximo es 1
    }

    [Fact]
    public void Si_el_usuario_ya_ha_contestado_o_el_dia_no_ha_terminado_no_se_pregunta_por_el()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        for (var i = 0; i < 24; i++)
        {
            ClimaDelDia(Dia(i), uv: 7.5);
            Registro(Dia(i), null, [("dolor_de_cabeza", i % 2 == 0 ? "presente" : "negado", null)]);
        }

        var r = RevisionSemanal.Generar(_datos, null, Clima, U, Ahora);

        Assert.DoesNotContain(Almacen.Cargar(_datos).DeTipo("pregunta"), p => p.Frontmatter.GetValueOrDefault("concepto") as string == "dolor_de_cabeza");
        Assert.NotNull(r);
    }

    [Fact]
    public void Si_no_se_sabe_cuanto_estuvo_fuera_Mira_pregunta_la_duracion()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        for (var i = 0; i < 24; i++)
        {
            ClimaDelDia(Dia(i), nubes: 10, sol: 30000);
            Registro(Dia(i), null, i == 23 ? [("exterior", "presente", null)] : []);
        }

        RevisionSemanal.Generar(_datos, null, Clima, U, Ahora);

        var p = Almacen.Cargar(_datos).DeTipo("pregunta").Single(x => x.Frontmatter.GetValueOrDefault("concepto") as string == "exterior_duracion");
        Assert.Contains("¿Cuánto tiempo estuviste al aire libre el 24 de octubre?", (string)p.Frontmatter["texto"]!);
    }

    // ---------------------------------------------------------------- utilidades

    [Theory]
    [InlineData("2 horas", 120)]
    [InlineData("unas 3 horas", 180)]
    [InlineData("1h30", 90)]
    [InlineData("1 hora y 15 min", 75)]
    [InlineData("45 minutos", 45)]
    [InlineData("media hora", 30)]
    [InlineData("hora y media", 90)]
    [InlineData("dos horas", 120)]
    [InlineData("2:30", 150)]
    [InlineData("1,5 horas", 90)]
    public void Las_duraciones_dichas_por_el_usuario_se_entienden(string texto, int minutos)
    {
        Assert.Equal(minutos, Duraciones.Minutos(texto));
    }

    [Theory]
    [InlineData("un rato")]
    [InlineData("toda la tarde")]
    [InlineData("no sé")]
    [InlineData("3")]
    [InlineData("")]
    [InlineData(null)]
    public void Una_duracion_vaga_o_ambigua_es_desconocida_nunca_se_supone(string? texto)
    {
        Assert.Null(Duraciones.Minutos(texto));
    }

    [Fact]
    public void El_intervalo_de_Wilson_separa_grupos_claros_y_no_separa_los_dudosos()
    {
        Assert.True(Wilson.Separados(9, 12, 1, 12));
        Assert.False(Wilson.Separados(3, 4, 1, 4)); // con 4 días por grupo no hay seguridad
        Assert.False(Wilson.Separados(6, 12, 6, 12));
        Assert.Equal((0.0, 1.0), Wilson.Intervalo(0, 0));
    }

    // ---------------------------------------------------------------- línea de comandos

    [Fact]
    public void La_linea_de_comandos_analiza_revisa_y_descarta()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        for (var i = 0; i < 6; i++)
        {
            ClimaDelDia(Dia(i), uv: 7.5);
            Registro(Dia(i), null, [("dolor_de_cabeza", "presente", null)]);
        }

        var salida = new StringWriter();
        var error = new StringWriter();
        var cmd = new Comandos(() => new MemoryStream(), salida, error, () => Ahora, _ => null);

        Assert.Equal(0, cmd.Ejecutar(["analizar", "--datos", _datos, "--etiquetar", "no"]));
        Assert.Contains("hip-uv-dolor-de-cabeza#c1  [insuficiente]", salida.ToString());
        Assert.Contains("Revisión semana-2026-W49 escrita", salida.ToString());

        Assert.Equal(0, cmd.Ejecutar(["hipotesis", "matizar", "hip-uv-dolor-de-cabeza", "--comentario", "Solo si no llevo gafas", "--datos", _datos]));
        Assert.Equal(2, cmd.Ejecutar(["hipotesis", "matizar", "hip-uv-dolor-de-cabeza", "--datos", _datos])); // sin comentario
        Assert.Equal(2, cmd.Ejecutar(["hipotesis", "confirmar", "hip-sol-cansancio", "--datos", _datos])); // hay dos contrastes
        Assert.Equal(0, cmd.Ejecutar(["hipotesis", "descartar", "hip-sol-cansancio#c2", "--datos", _datos]));
        Assert.Equal("descartada", AlmacenDeHipotesis.Leer(_datos, "hip-sol-cansancio")!.Contrastes[1].Revision!.Estado);
    }

    [Fact]
    public void Procesar_con_hipotesis_completa_la_primera_noche_de_la_semana_el_analisis_sin_cambiar_el_resultado_de_Mira()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        ClimaDelDia(Dia(0)); Registro(Dia(0), null);
        var config = Path.Combine(_datos, "orumi.yaml");
        File.WriteAllText(config, "datos: " + _datos + "\nproveedor:\n  nombre: x\n");
        var salida = new StringWriter();
        var cmd = new Comandos(() => new MemoryStream(), salida, new StringWriter(), () => Ahora, k => k == Comandos.VariableConfiguracion ? config : null,
            proveedorDePrueba: () => new Orumi.Agente.Proveedores.ProveedorSimulado(_ => RespuestaExtraccion.SinHechos));

        var codigo = cmd.Ejecutar(["procesar"]);

        Assert.True(RevisionSemanal.Existe(_datos, Hoy));
        Assert.Contains("Revisión semana-2026-W49 escrita", salida.ToString());
        Assert.NotEqual(4, codigo);
    }

    // ---------------------------------------------------------------- panel

    private (Orumi.Panel.Lectura L, Orumi.Panel.EscrituraDelPanel E) Panel()
    {
        var repo = new Orumi.Panel.RepositorioDeDatos(_datos, () => DateTimeOffset.UtcNow, TimeSpan.Zero);
        return (new Orumi.Panel.Lectura(repo), new Orumi.Panel.EscrituraDelPanel(repo, () => Ahora, _ => { }));
    }

    private static string Json(object o) => System.Text.Json.JsonSerializer.Serialize(o);

    [Fact]
    public void El_panel_sin_revision_devuelve_las_hipotesis_y_ninguna_revision()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);

        var json = Json(Panel().L.Descubrimientos());

        Assert.Contains("\"revision\":null", json);
        Assert.Contains("la lluvia me relaja", json);
        Assert.Contains("\"estado\":\"por_verificar\"", json);
    }

    [Fact]
    public void El_panel_muestra_la_ultima_revision_con_sus_contrastes_y_su_evidencia()
    {
        var vistos = new Dictionary<bool, int> { [true] = 0, [false] = 0 };
        DiasConUv((i, alto) => { var k = vistos[alto]++; return alto ? (k < 9 ? "presente" : "negado") : (k < 1 ? "presente" : "negado"); });
        RevisionSemanal.Generar(_datos, null, Clima, U, Ahora);

        var json = Json(Panel().L.Descubrimientos());

        Assert.Contains("semana-2026-W49", json);
        Assert.Contains("hip-uv-dolor-de-cabeza#c1", json);
        Assert.Contains("apoyada", json);
        Assert.Contains("aprendizaje", json);
    }

    [Fact]
    public void Desde_el_panel_se_declara_una_hipotesis_y_se_revisa_un_contraste()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        var (l, e) = Panel();

        var d = e.Ejecutar("hipotesis-declarar", null, "a@b.c", op => op.DeclararHipotesis("Socializar me deja sin ganas de cocinar", "registro:socializacion", "sin_ganas_de_cocinar", "El usuario dice que no le apetece cocinar.", 0, 1));
        Assert.Equal(200, d.Estado);
        Assert.Contains("sin ganas de cocinar", Json(l.Descubrimientos()));
        Assert.Contains("registro:socializacion", Json(l.Descubrimientos()));

        var r = e.Ejecutar("hipotesis-matizar", "hip-uv-dolor-de-cabeza", "a@b.c", op => op.RevisarHipotesis("hip-uv-dolor-de-cabeza", "c1", "matizar", "Solo sin gafas", 0));
        Assert.Equal(200, r.Estado);
        Assert.Equal("matizada", AlmacenDeHipotesis.Leer(_datos, "hip-uv-dolor-de-cabeza")!.Estado);
    }

    [Theory]
    [InlineData("", "clima:uv_alto", "x_y", 400)]
    [InlineData("Hipótesis válida de prueba", "clima:viento", "x_y", 400)]
    [InlineData("Hipótesis válida de prueba", "registro:inventado", "x_y", 400)] // concepto sin descripción
    [InlineData("Hipótesis válida de prueba", "clima:lluvia", "Efecto Con Mayúsculas", 400)]
    public void El_panel_rechaza_hipotesis_mal_formadas_sin_escribir_nada(string formulacion, string factor, string efecto, int estado)
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        var antes = Directory.GetFiles(Path.Combine(_datos, "hipotesis")).Length;
        var (_, e) = Panel();

        var r = e.Ejecutar("hipotesis-declarar", null, null, op => op.DeclararHipotesis(formulacion, factor, efecto, "El usuario lo dice así de claro.", 0, 0));

        Assert.Equal(estado, r.Estado);
        Assert.Equal(antes, Directory.GetFiles(Path.Combine(_datos, "hipotesis")).Length);
    }

    [Theory]
    [InlineData("hip-sol-cansancio", null, "confirmar", 400)]   // tiene dos contrastes: hay que indicar cuál
    [InlineData("hip-uv-dolor-de-cabeza", null, "matizar", 400)] // sin comentario
    [InlineData("hip-no-existe", null, "descartar", 404)]
    [InlineData("hip-uv-dolor-de-cabeza", "c7", "confirmar", 404)]
    public void El_panel_rechaza_revisiones_invalidas_sin_modificar_nada(string id, string? contraste, string accion, int estado)
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        var antes = File.ReadAllText(AlmacenDeHipotesis.Ruta(_datos, "hip-uv-dolor-de-cabeza"));
        var (_, e) = Panel();

        var r = e.Ejecutar("hipotesis", id, null, op => op.RevisarHipotesis(id, contraste, accion, null, 0));

        Assert.Equal(estado, r.Estado);
        Assert.Equal(antes, File.ReadAllText(AlmacenDeHipotesis.Ruta(_datos, "hip-uv-dolor-de-cabeza")));
    }

    [Fact]
    public void Las_escrituras_de_hipotesis_se_rechazan_con_409_mientras_Mira_procesa()
    {
        AlmacenDeHipotesis.CargarIniciales(_datos, Ahora);
        var (_, e) = Panel();
        using var bloqueo = BloqueoDeEjecucion.Adquirir(_datos);

        var r = e.Ejecutar("hipotesis-descartar", "hip-lluvia-relajacion", null, op => op.RevisarHipotesis("hip-lluvia-relajacion", null, "descartar", null, 0));

        Assert.Equal(409, r.Estado);
        Assert.Equal(Hipotesis.PorVerificar, AlmacenDeHipotesis.Leer(_datos, "hip-lluvia-relajacion")!.Estado);
    }

    [Theory]
    [InlineData("hip-uv-dolor-de-cabeza", true)]
    [InlineData("hip-x", true)]
    [InlineData("../etc/passwd", false)]
    [InlineData("HIP-MAYUS", false)]
    [InlineData("hip-con espacio", false)]
    [InlineData("reg-20261118-ab12", false)]
    public void Solo_se_aceptan_identificadores_de_hipotesis_bien_formados(string id, bool valido)
    {
        Assert.Equal(valido, AlmacenDeHipotesis.EsIdValido(id));
    }
}

