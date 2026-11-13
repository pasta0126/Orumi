using Orumi.Core.Ficheros;
using Orumi.Core.Validacion;

namespace Orumi.Tests;

public class ValidadorTests : IDisposable
{
    private readonly string _copia = Ayudantes.CopiarEjemplos();

    public void Dispose() => Directory.Delete(_copia, true);

    private IReadOnlyList<Incidencia> Validar() => Validador.Validar(Almacen.Cargar(_copia));

    [Fact]
    public void Los_ejemplos_validan_sin_errores()
    {
        var almacen = Almacen.Cargar(Ayudantes.Ejemplos());
        var errores = Validador.Validar(almacen);

        Assert.Empty(errores);
        Assert.Equal(5, almacen.DeTipo("captura").Count());
        Assert.Equal(7, almacen.DeTipo("registro").Count());
        Assert.Equal(4, almacen.DeTipo("pregunta").Count());
        Assert.Equal(5, almacen.DeTipo("lote").Count());
    }

    [Fact]
    public void Detecta_una_captura_alterada()
    {
        Ayudantes.Reemplazar(Ayudantes.Buscar(_copia, "cap-20261110-2140"), "estantería del salón", "estantería del comedor");
        Assert.Contains(Validar(), i => i.Mensaje.Contains("huella"));
    }

    [Fact]
    public void Detecta_una_cita_que_no_es_el_inicio_del_fragmento()
    {
        Ayudantes.Reemplazar(Ayudantes.Buscar(_copia, "reg-20261111-a3f9"), "cita: \"Estuve toda la tarde", "cita: \"Estuve toda la noche");
        Assert.Contains(Validar(), i => i.Mensaje.Contains("cita"));
    }

    [Fact]
    public void Detecta_un_conflicto_no_simetrico()
    {
        // Se quita la marca del conflicto de un solo lado.
        var f = Ayudantes.Buscar(_copia, "reg-20261113-e4n7");
        var lineas = File.ReadAllLines(f)
            .Where(l => !l.StartsWith("  - {campo: fecha, con:", StringComparison.Ordinal))
            .Select(l => l == "conflictos:" ? "conflictos: []" : l);
        File.WriteAllLines(f, lineas);
        Assert.Contains(Validar(), i => i.Mensaje.Contains("simétrico"));
    }

    [Fact]
    public void Detecta_una_actividad_abierta_con_fin()
    {
        Ayudantes.Reemplazar(Ayudantes.Buscar(_copia, "reg-20261110-q2x1"), "estado: cerrado", "estado: abierto");
        Assert.Contains(Validar(), i => i.Mensaje.Contains("abierto con fin"));
    }

    [Fact]
    public void Detecta_una_hora_aproximada_sin_valor()
    {
        Ayudantes.Reemplazar(Ayudantes.Buscar(_copia, "reg-20261110-q2x1"), "valor: \"07:30\", ", "");
        Assert.Contains(Validar(), i => i.Mensaje.Contains("aproximada sin valor"));
    }

    [Fact]
    public void Detecta_un_tipo_de_entidad_invalido()
    {
        Ayudantes.Reemplazar(Ayudantes.Buscar(_copia, "reg-20261113-e4n7"), "tipo: organizacion", "tipo: cliente");
        Assert.Contains(Validar(), i => i.Mensaje.Contains("tipo inválido"));
    }

    [Fact]
    public void Detecta_una_certeza_invalida()
    {
        Ayudantes.Reemplazar(Ayudantes.Buscar(_copia, "reg-20261110-r7m4"), "  fecha: interpretado", "  fecha: seguro");
        Assert.Contains(Validar(), i => i.Mensaje.Contains("certeza inválida"));
    }

    [Fact]
    public void Detecta_un_campo_con_valor_y_sin_certeza()
    {
        var f = Ayudantes.Buscar(_copia, "reg-20261110-r7m4");
        File.WriteAllLines(f, File.ReadAllLines(f).Where(l => l != "  fecha: interpretado"));
        Assert.Contains(Validar(), i => i.Mensaje.Contains("fecha sin certeza"));
    }

    [Fact]
    public void Detecta_una_referencia_rota()
    {
        Ayudantes.Reemplazar(Ayudantes.Buscar(_copia, "reg-20261110-r7m4"), "destino: reg-20261110-q2x1", "destino: reg-20261110-zzzz");
        Assert.Contains(Validar(), i => i.Mensaje.Contains("inexistente"));
    }

    [Fact]
    public void Detecta_una_clave_de_extraccion_duplicada_y_una_captura_sin_lote()
    {
        Ayudantes.Reemplazar(Ayudantes.Buscar(_copia, "reg-20261110-r7m4"), "#f2:idea:1", "#f1:actividad:1");
        Assert.Contains(Validar(), i => i.Mensaje.Contains("clave_extraccion"));

        File.Delete(Ayudantes.Buscar(_copia, "lote-20261111-0300"));
        Assert.Contains(Validar(), i => i.Mensaje.Contains("captura sin lote"));
    }

    [Fact]
    public void Detecta_un_fichero_ilegible_sin_abortar()
    {
        File.WriteAllText(Path.Combine(_copia, "capturas", "2026", "11", "cap-20261199-0000-xxx.md"), "esto no es un fichero de ORUMI");
        var errores = Validar();
        Assert.Contains(errores, i => i.Mensaje.Contains("No se puede leer"));
        Assert.True(errores.Count >= 1);
    }

    [Fact]
    public void Una_carpeta_sin_permiso_como_lost_and_found_no_tumba_la_validacion()
    {
        // ext4 crea lost+found (de root, modo 700) en la raíz de un volumen nuevo.
        var perdida = Path.Combine(_copia, "lost+found");
        var ilegible = Path.Combine(_copia, "capturas", "carpeta-ilegible");
        Directory.CreateDirectory(perdida);
        Directory.CreateDirectory(ilegible);
        File.SetUnixFileMode(perdida, 0);
        File.SetUnixFileMode(ilegible, 0);
        try
        {
            Assert.Empty(Validar());
        }
        finally
        {
            File.SetUnixFileMode(perdida, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.SetUnixFileMode(ilegible, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void No_modifica_ningun_fichero()
    {
        var antes = Directory.GetFiles(_copia, "*", SearchOption.AllDirectories).Order().Select(f => (f, File.ReadAllText(f))).ToList();
        Validar();
        var despues = Directory.GetFiles(_copia, "*", SearchOption.AllDirectories).Order().Select(f => (f, File.ReadAllText(f))).ToList();
        Assert.Equal(antes, despues);
    }
}
