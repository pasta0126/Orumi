namespace Orumi.Noa;

/// <summary>Niveles de evidencia de una afirmación (X20 ampliado, decisión 5): lo calcula el código a partir de las fuentes verificadas, nunca el modelo.</summary>
public static class Niveles
{
    public const string Patron = "patron";
    public const string Indicio = "indicio";
    public const string Conjetura = "conjetura";
}

public static class Certezas
{
    public const string Dicho = "dicho";
    public const string Confirmado = "confirmado";
    public const string Interpretado = "interpretado";
    public const string Hipotesis = "hipotesis";
    public const string Recomendacion = "recomendacion";
}

public static class EstadosDeRasgo
{
    public const string Activo = "activo";
    public const string Matizado = "matizado";
    public const string Descartado = "descartado";
    public const string Borrado = "borrado";
}

public sealed record Apartado(string Slug, string Titulo, string Descripcion);

/// <summary>Los apartados del perfil: lo que un perfilista profesional (o un atacante de ingeniería social) intentaría deducir. Solo del usuario.</summary>
public static class Apartados
{
    public const string EnergiaYAnimo = "energia_animo";
    public const string Superficie = "superficie";

    public static readonly IReadOnlyList<Apartado> Todos =
    [
        new("personalidad", "Personalidad y carácter", "Cómo es el usuario: rasgos de carácter, temperamento, manera de reaccionar."),
        new("comunicacion", "Estilo de comunicación", "Cómo se expresa y cómo conviene comunicarse con él: directo o rodeos, escrito o hablado, ritmo, tono."),
        new("motivaciones_valores", "Motivaciones y valores", "Qué le mueve, qué le importa, qué defiende, qué prioriza (solo lo que él expresa o que se repite en sus actos)."),
        new("miedos_preocupaciones", "Miedos y preocupaciones", "Qué le inquieta o le preocupa, de qué se protege."),
        new("habitos_rutinas", "Hábitos y rutinas", "Cómo organiza sus días, sus horarios, sus costumbres."),
        new(EnergiaYAnimo, "Energía y ánimo", "Cómo varía su energía o su ánimo y con qué se relaciona. Solo con hipótesis que el usuario haya confirmado."),
        new("relaciones", "Relaciones y su dinámica", "Cómo vive el usuario sus relaciones (familia, amigos, trabajo), SIEMPRE desde su punto de vista y sin atribuir rasgos a las otras personas."),
        new("situacion", "Situación personal, familiar, laboral, económica y de salud", "Qué se deduce de su situación vital. Es sensible: nunca como hecho."),
        new("entorno_lugares", "Entorno y lugares", "Dónde se mueve, qué lugares frecuenta, cómo es su entorno."),
        new("ayuda_desgasta", "Qué le ayuda y qué le desgasta", "Qué situaciones, personas o formas de comunicar le dan energía o se la quitan."),
        new(Superficie, "Superficie de ingeniería social", "Qué podría deducir un tercero a partir de lo que se sabe, qué palancas de persuasión funcionarían con el usuario y qué puntos débiles hay, para que pueda protegerse."),
    ];

    public static Apartado? Buscar(string slug) => Todos.FirstOrDefault(a => a.Slug == slug);

    public static bool Existe(string slug) => Buscar(slug) is not null;
}

public sealed record EvidenciaDeRasgo(string Fuente, string Fecha, string Cita);

public sealed record EntradaDeRasgo(DateTimeOffset Fecha, string Accion, string Origen, string? Detalle);

/// <summary>
/// Una afirmación del perfil sobre el usuario. Lleva su certeza (las cinco categorías), su nivel de evidencia (calculado), las fuentes que la sustentan
/// (con cita literal verificada) y el estado que le da el usuario. Lo que el usuario corrige o matiza prevalece sobre cualquier interpretación.
/// </summary>
public sealed record Rasgo(
    string Clave, string Apartado, string Texto, string? Contexto, string Certeza, string Nivel, bool Sensible,
    IReadOnlyList<EvidenciaDeRasgo> Evidencia, int Fuentes, int Semanas, IReadOnlyList<string> Hipotesis,
    string Estado, string Origen, bool Oculto, string? ComentarioDelUsuario, int? EvidenciaAlDescartar,
    int VersionCreada, DateTimeOffset Actualizado, IReadOnlyList<EntradaDeRasgo> Historial)
{
    public const string DelAgente = "noa";
    public const string DelUsuario = "usuario";

    public bool Visible => !Oculto && Estado is EstadosDeRasgo.Activo or EstadosDeRasgo.Matizado;

    public bool EsConjetura => Nivel == Niveles.Conjetura;
}

/// <summary>Una recomendación de comunicación, del sistema, separada de lo observado y apoyada solo en patrones o indicios.</summary>
public sealed record Recomendacion(string Id, string Texto, IReadOnlyList<string> Rasgos, string Estado, bool Oculta)
{
    public bool Visible => !Oculta && Estado == EstadosDeRasgo.Activo;
}

public sealed record Perfil(
    int Version, DateTimeOffset Generada, string Generacion, int RegistrosVistos,
    IReadOnlyList<Rasgo> Rasgos, IReadOnlyList<Recomendacion> Recomendaciones,
    IReadOnlyList<string> ApartadosSinDatos, IReadOnlyList<string> ApartadosOcultos)
{
    public static Perfil Vacio(DateTimeOffset ahora) => new(0, ahora, string.Empty, 0, [], [], [], []);
}

/// <summary>Constancia de una ejecución: nunca contiene texto del usuario.</summary>
public sealed record Generacion(
    string Id, DateTimeOffset Fecha, string Motivo, string Resultado, string? Detalle, string? Proveedor, string? Modelo,
    int RegistrosEnviados, int RegistrosExcluidos, int RasgosAceptados, int RasgosDescartados, IReadOnlyDictionary<string, int> Descartes,
    long TokensEntrada, long TokensSalida, decimal? CosteUsd, int? Version)
{
    public const string Generada = "generada";
    public const string Omitida = "omitida";
    public const string Fallida = "fallida";
}
