namespace Orumi.Core.Validacion;

public sealed record Incidencia(string Fichero, string Mensaje)
{
    public override string ToString() => $"{Fichero}: {Mensaje}";
}
