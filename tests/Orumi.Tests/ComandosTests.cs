using Orumi.Agente;
using System.Text;
using Orumi.Cli;
using Orumi.Core.Ficheros;

namespace Orumi.Tests;

public class ComandosTests : IDisposable
{
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "orumi-cli-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _salida = new();
    private readonly StringWriter _error = new();
    private static readonly DateTimeOffset Ahora = new(2026, 11, 16, 9, 0, 0, TimeSpan.FromHours(1));

    public void Dispose()
    {
        if (Directory.Exists(_datos))
        {
            Directory.Delete(_datos, true);
        }
    }

    private int Ejecutar(string stdin, params string[] args)
    {
        var c = new Comandos(
            () => new MemoryStream(new UTF8Encoding(false).GetBytes(stdin)),
            _salida,
            _error,
            () => Ahora,
            _ => null);
        return c.Ejecutar(args);
    }

    [Fact]
    public void Capturar_por_entrada_estandar_crea_la_captura_y_la_confirma()
    {
        var codigo = Ejecutar("Nota por stdin\ncon dos líneas\n", "capturar", "--datos", _datos);

        Assert.Equal(0, codigo);
        Assert.Contains("Captura creada: cap-20261116-0900-", _salida.ToString());
        var fichero = Directory.GetFiles(_datos, "*.md", SearchOption.AllDirectories).Single();
        Assert.Equal("Nota por stdin\ncon dos líneas\n", Documento.Analizar(fichero, File.ReadAllText(fichero)).Cuerpo);
    }

    [Fact]
    public void Capturar_con_texto_y_metadatos()
    {
        var codigo = Ejecutar("", "capturar", "--datos", _datos, "--texto", "Hola", "--fecha-acontecimiento", "2026-11-08", "--etiqueta", "casa");

        Assert.Equal(0, codigo);
        var f = Directory.GetFiles(_datos, "*.md", SearchOption.AllDirectories).Single();
        var fm = Documento.Analizar(f, File.ReadAllText(f)).Frontmatter;
        Assert.Equal("2026-11-08", fm["fecha_acontecimiento"]);
        Assert.Equal("casa", fm["etiqueta"]);
    }

    [Fact]
    public void Capturar_texto_vacio_falla_sin_crear_nada()
    {
        var codigo = Ejecutar("   \n", "capturar", "--datos", _datos);

        Assert.Equal(1, codigo);
        Assert.Contains("vacío", _error.ToString());
        Assert.False(Directory.Exists(_datos) && Directory.GetFiles(_datos, "*", SearchOption.AllDirectories).Length > 0);
    }

    [Fact]
    public void Sin_directorio_de_datos_pide_indicarlo()
    {
        var codigo = Ejecutar("x", "capturar");
        Assert.Equal(2, codigo);
        Assert.Contains("ORUMI_DATOS", _error.ToString());
    }

    [Fact]
    public void Validar_los_ejemplos_da_cero_errores()
    {
        var codigo = Ejecutar("", "validar", "--datos", Ayudantes.Ejemplos());
        Assert.Equal(0, codigo);
        Assert.Contains("OK: sin errores (5 capturas, 7 registros, 4 preguntas, 5 lotes)", _salida.ToString());
    }

    [Fact]
    public void Validar_informa_por_fichero_y_devuelve_error()
    {
        var copia = Ayudantes.CopiarEjemplos();
        try
        {
            Ayudantes.Reemplazar(Ayudantes.Buscar(copia, "cap-20261110-2140"), "estantería del salón", "estantería del comedor");
            var codigo = Ejecutar("", "validar", "--datos", copia);

            Assert.Equal(1, codigo);
            Assert.Contains("cap-20261110-2140-x9d.md: la huella no coincide con el cuerpo", _salida.ToString());
        }
        finally
        {
            Directory.Delete(copia, true);
        }
    }

    [Fact]
    public void Un_comando_desconocido_da_uso()
    {
        Assert.Equal(2, Ejecutar("", "volar"));
    }
}

public class ProcesarCliTests : IDisposable
{
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "orumi-proc-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _salida = new();
    private readonly StringWriter _error = new();
    private static readonly DateTimeOffset Ahora = new(2026, 11, 16, 9, 0, 0, TimeSpan.FromHours(1));

    public ProcesarCliTests() => Directory.CreateDirectory(_datos);

    public void Dispose() => Directory.Delete(_datos, true);

    private int Ejecutar(string stdin, params string[] args) =>
        new Comandos(() => new MemoryStream(Encoding.UTF8.GetBytes(stdin)), _salida, _error, () => Ahora, _ => null).Ejecutar(args);

    private string Respuestas(string json)
    {
        var f = Path.Combine(_datos, "..", "orumi-resp-" + Guid.NewGuid().ToString("N")[..6] + ".json");
        File.WriteAllText(f, json);
        return f;
    }

    private const string RespuestaSimple = """
        ```json
        {"registros":[{"fragmento":"Hoy he llamado a Marta.","clase":"evento","titulo":"Llamada a Marta","descripcion":"Llamada a Marta.",
          "campos":{"fecha":{"expresion":"hoy","desde":"2026-11-16","hasta":"2026-11-16"},"personas":["Marta"]},
          "certeza":{"fecha":"interpretado","personas":"dicho"}}],
         "preguntas":[{"campo":"duracion","texto":"¿Cuánto duró la llamada a Marta?","valor":"media","registro_indice":0}]}
        ```
        """;

    [Fact]
    public void Procesar_con_respuestas_simuladas_crea_registros_y_el_resultado_valida()
    {
        Assert.Equal(0, Ejecutar("", "capturar", "--datos", _datos, "--texto", "Hoy he llamado a Marta."));
        var resp = Respuestas(RespuestaSimple);
        try
        {
            var codigo = Ejecutar("", "procesar", "--datos", _datos, "--simulado", resp);

            Assert.Equal(0, codigo);
            Assert.Contains("1 registro(s) creados", _salida.ToString());
            Assert.Contains("1 pregunta(s) nuevas", _salida.ToString());
            Assert.Equal(0, Ejecutar("", "validar", "--datos", _datos));
            Assert.Equal(0, Ejecutar("", "procesar", "--datos", _datos, "--simulado", resp)); // ya no hay pendientes
            Assert.Contains("No hay capturas pendientes", _salida.ToString());
        }
        finally
        {
            File.Delete(resp);
        }
    }

    [Fact]
    public void Una_respuesta_con_formato_invalido_deja_la_captura_pendiente()
    {
        Ejecutar("", "capturar", "--datos", _datos, "--texto", "Hoy he llamado a Marta.");
        var resp = Respuestas("esto no es json");
        try
        {
            var codigo = Ejecutar("", "procesar", "--datos", _datos, "--simulado", resp);
            Assert.Equal(2, codigo); // el fichero de respuestas simuladas es inválido: uso incorrecto, no se procesa nada
            Assert.False(Directory.Exists(Path.Combine(_datos, "registros")));
            Assert.Contains("formato esperado", _error.ToString());
        }
        finally
        {
            File.Delete(resp);
        }
    }

    [Fact]
    public void Si_hay_otra_ejecucion_en_curso_termina_sin_procesar_nada()
    {
        Ejecutar("", "capturar", "--datos", _datos, "--texto", "Hoy he llamado a Marta.");
        var resp = Respuestas(RespuestaSimple);
        try
        {
            using var otra = BloqueoDeEjecucion.Adquirir(_datos);
            Assert.NotNull(otra);

            var codigo = Ejecutar("", "procesar", "--datos", _datos, "--simulado", resp);

            Assert.Equal(3, codigo);
            Assert.Contains("otra ejecución", _error.ToString());
            Assert.False(Directory.Exists(Path.Combine(_datos, "registros")));
        }
        finally
        {
            File.Delete(resp);
        }
    }

    [Fact]
    public void Con_un_proveedor_real_sin_garantia_no_se_envia_nada_y_se_informa()
    {
        Ejecutar("", "capturar", "--datos", _datos, "--texto", "Nota privada.");
        var config = Path.Combine(_datos, "..", "orumi-cfg-" + Guid.NewGuid().ToString("N")[..6] + ".yaml");
        File.WriteAllText(config, "datos: " + _datos + "\nproveedor:\n  nombre: anthropic\n  modelo: claude-haiku-5-5\n");
        try
        {
            var codigo = Ejecutar("", "procesar", "--config", config);

            Assert.Equal(4, codigo);
            Assert.Contains("garantía", _error.ToString());
            Assert.Contains("ANTHROPIC_API_KEY", _error.ToString());
            Assert.Contains("Capturas pendientes: 1", _salida.ToString());
            Assert.False(Directory.Exists(Path.Combine(_datos, "registros")));
        }
        finally
        {
            File.Delete(config);
        }
    }

    [Fact]
    public void Sin_proveedor_pide_indicarlo()
    {
        Assert.Equal(2, Ejecutar("", "procesar", "--datos", _datos));
        Assert.Contains("proveedor", _error.ToString());
    }
}

public class LectorDeRespuestaTests
{
    [Fact]
    public void Lee_la_respuesta_con_valla_de_codigo_y_convierte_numeros_a_texto()
    {
        var r = Orumi.Agente.Proveedores.LectorDeRespuesta.Analizar("""
            ```json
            {"registros":[{"fragmento":"x","clase":"actividad","titulo":"T","descripcion":"D",
              "campos":{"duracion":{"expresion":"dos horas","minutos":120}},"certeza":{"duracion":"dicho"},
              "multidia":{"estado":"abierto","certeza_estado":"dicho"}}],"preguntas":[]}
            ```
            """);
        var reg = Assert.Single(r.Registros);
        Assert.Equal("120", ((Dictionary<string, object?>)reg.Campos["duracion"]!)["minutos"]);
        Assert.Equal("abierto", reg.Multidia!.Estado);
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("[]")]
    [InlineData("""{"registros":[{"clase":"actividad"}]}""")]
    public void Una_respuesta_mal_formada_falla_sin_adivinar(string texto)
    {
        Assert.Throws<Orumi.Agente.ProcesamientoFallidoException>(() => Orumi.Agente.Proveedores.LectorDeRespuesta.Analizar(texto));
    }

    [Fact]
    public void El_mensaje_al_proveedor_contiene_solo_lo_minimo()
    {
        var s = new Orumi.Agente.SolicitudExtraccion("cap-1", "texto", new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.FromHours(1)), null, null, [],
            [new Orumi.Agente.RegistroRelacionado("reg-1", "evento", "T", "2026-11-15", "2026-11-15", ["Marta"], [], [], null)]);
        var json = Orumi.Agente.Proveedores.ProveedorAnthropic.Mensaje(s);

        Assert.Contains("\"texto\":\"texto\"", json);
        Assert.DoesNotContain("historial", json);
        Assert.DoesNotContain("certeza", json);
        Assert.DoesNotContain("procedencia", json);
        Assert.DoesNotContain("huella", json);
    }
}

public class VolumenTests : IDisposable
{
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "orumi-vol-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _salida = new();
    private readonly StringWriter _error = new();
    private static readonly DateTimeOffset Ahora = new(2026, 11, 16, 9, 0, 0, TimeSpan.FromHours(1));

    public VolumenTests() => Directory.CreateDirectory(_datos); // punto de montaje «vacío»: volumen sin desbloquear

    public void Dispose() => Directory.Delete(_datos, true);

    private int Ejecutar(params string[] args) =>
        new Comandos(() => new MemoryStream(), _salida, _error, () => Ahora, n => n == Comandos.VariableExigirVolumen ? "1" : null).Ejecutar(args);

    private string[] Ficheros() => Directory.GetFiles(_datos, "*", SearchOption.AllDirectories);

    [Fact]
    public void Con_el_volumen_bloqueado_capturar_se_rechaza_y_no_crea_ficheros()
    {
        var codigo = Ejecutar("capturar", "--datos", _datos, "--texto", "nota");

        Assert.Equal(5, codigo);
        Assert.Contains("volumen cifrado", _error.ToString());
        Assert.Empty(Ficheros());
    }

    [Fact]
    public void Con_el_volumen_bloqueado_procesar_no_escribe_ni_procesa_nada()
    {
        var resp = Path.Combine(Path.GetTempPath(), "orumi-resp-vol-" + Guid.NewGuid().ToString("N")[..6] + ".json");
        File.WriteAllText(resp, """{"registros":[],"preguntas":[]}""");
        try
        {
            var codigo = Ejecutar("procesar", "--datos", _datos, "--simulado", resp);

            Assert.Equal(5, codigo);
            Assert.Empty(Ficheros()); // ni lote, ni bloqueo
        }
        finally
        {
            File.Delete(resp);
        }
    }

    [Fact]
    public void Tras_desbloquear_las_capturas_pendientes_se_procesan()
    {
        Assert.Equal(0, Ejecutar("preparar-volumen", "--datos", _datos));
        Assert.Equal(0, Ejecutar("capturar", "--datos", _datos, "--texto", "Hoy he llamado a Marta."));
        var captura = Ficheros().Single(f => f.Contains("cap-"));
        File.Move(Path.Combine(_datos, Volumen.Marcador), Path.Combine(_datos, "oculto")); // «se reinicia el servidor»: vuelve a bloquearse

        var resp = Path.Combine(Path.GetTempPath(), "orumi-resp-vol-" + Guid.NewGuid().ToString("N")[..6] + ".json");
        File.WriteAllText(resp, """{"registros":[],"preguntas":[]}""");
        try
        {
            Assert.Equal(5, Ejecutar("procesar", "--datos", _datos, "--simulado", resp)); // sigue pendiente
            Assert.DoesNotContain(Ficheros(), f => f.Contains("lote-"));

            File.Move(Path.Combine(_datos, "oculto"), Path.Combine(_datos, Volumen.Marcador)); // el usuario desbloquea
            Assert.Equal(0, Ejecutar("procesar", "--datos", _datos, "--simulado", resp));
            Assert.Contains(Ficheros(), f => f.Contains("lote-"));
            Assert.True(File.Exists(captura));
        }
        finally
        {
            File.Delete(resp);
        }
    }

    [Fact]
    public void Sin_exigir_el_volumen_las_herramientas_funcionan_como_antes()
    {
        var codigo = new Comandos(() => new MemoryStream(), _salida, _error, () => Ahora, _ => null)
            .Ejecutar(["capturar", "--datos", _datos, "--texto", "nota"]);
        Assert.Equal(0, codigo);
    }
}

public class ErroresInesperadosTests
{
    [Fact]
    public void Un_error_de_ficheros_da_un_mensaje_breve_y_codigo_70_sin_traza()
    {
        var fichero = Path.GetTempFileName(); // una ruta de datos que es un fichero, no un directorio
        var salida = new StringWriter();
        var error = new StringWriter();
        try
        {
            var codigo = new Comandos(() => new MemoryStream(), salida, error, () => DateTimeOffset.Now, _ => null)
                .Ejecutar(["capturar", "--datos", fichero, "--texto", "contenido secreto de la nota"]);

            Assert.Equal(70, codigo);
            Assert.StartsWith("Error:", error.ToString());
            Assert.DoesNotContain("   at ", error.ToString());
            Assert.DoesNotContain("contenido secreto", error.ToString());
        }
        finally
        {
            File.Delete(fichero);
        }
    }
}
