# ORUMI — imagen del MVP (linux/arm64 para la Raspberry Pi; también construye en x64).
# Sin puertos publicados: se usa por línea de comandos (capturar, validar, procesar, reindexar).
# Los datos, la configuración y los secretos NO van en la imagen: se montan o se pasan por entorno.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS construccion
WORKDIR /origen
COPY Orumi.slnx ./
COPY src/ src/
RUN dotnet publish src/Orumi.Cli -c Release -o /publicado --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=construccion /publicado ./
# Usuario sin privilegios (la imagen base ya trae el usuario «app»).
USER app
ENV ORUMI_DATOS=/datos \
    ORUMI_CONFIG=/config/orumi.yaml
ENTRYPOINT ["dotnet", "orumi.dll"]
CMD ["--ayuda"]
