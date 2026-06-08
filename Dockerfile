# Imagem de distribuição da CLI do Agente de Otimização de Hardware (Linux).
# Build multi-stage: compila com o SDK e publica self-contained; a imagem final
# roda o binário sem depender do .NET instalado.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/HardwareOptimizer.Cli/HardwareOptimizer.Cli.csproj \
    -c Release -r linux-x64 --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -o /app

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/ ./
# Sensores/coleta exigem acesso a /sys e /proc; monte-os ao executar:
#   docker run --rm -v /sys:/sys:ro hwopt sensores
ENTRYPOINT ["./HardwareOptimizer.Cli"]
CMD ["demo"]
