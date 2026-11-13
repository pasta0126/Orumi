namespace Orumi.Indice;

public static class Conexion
{
    public const string Variable = "ORUMI_DATABASE_URL";

    /// <summary>Convierte <c>postgres://usuario:clave@host:puerto/base</c> en una cadena de conexión de Npgsql (red interna de Docker, sin TLS).</summary>
    public static string DesdeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("postgres" or "postgresql"))
        {
            // Ya es una cadena de conexión de Npgsql.
            return url.Contains('=') ? url : throw new ArgumentException("La URL de la base de datos no es válida.");
        }

        var usuario = Uri.UnescapeDataString(uri.UserInfo.Split(':')[0]);
        var clave = uri.UserInfo.Contains(':') ? Uri.UnescapeDataString(uri.UserInfo[(uri.UserInfo.IndexOf(':') + 1)..]) : string.Empty;
        var puerto = uri.Port > 0 ? uri.Port : 5432;
        return $"Host={uri.Host};Port={puerto};Username={usuario};Password={clave};Database={uri.AbsolutePath.TrimStart('/')};SSL Mode=Disable;GSS Encryption Mode=Disable;Timeout=10;Command Timeout=60";
    }
}
