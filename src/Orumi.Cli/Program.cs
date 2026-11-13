using Orumi.Cli;

var comandos = new Comandos(
    entrada: Console.OpenStandardInput,
    salida: Console.Out,
    error: Console.Error,
    reloj: () => DateTimeOffset.Now,
    entorno: Environment.GetEnvironmentVariable);

return comandos.Ejecutar(args);
