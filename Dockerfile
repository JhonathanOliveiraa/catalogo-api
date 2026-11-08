# syntax=docker/dockerfile:1

# Estágio SDK na arquitetura do runner ($BUILDPLATFORM, x64 na CI): compila em modo
# cruzado para $TARGETARCH (arm64) sem emulação QEMU (AD-11).
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# restore em camada própria: só invalida quando o projeto muda
COPY Directory.Build.props ./
COPY src/CatalogoApi/CatalogoApi.csproj src/CatalogoApi/
RUN dotnet restore src/CatalogoApi/CatalogoApi.csproj -a $TARGETARCH

COPY src/CatalogoApi/ src/CatalogoApi/
RUN dotnet publish src/CatalogoApi/CatalogoApi.csproj \
        --configuration Release \
        -a $TARGETARCH \
        --no-restore \
        --output /app \
        -p:UseAppHost=false

# Imagem final: chiseled (sem shell, sem gerenciador de pacotes, sem RUN), usuário
# não-root da própria imagem (UID 1654) e porta 8080.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app ./

USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Sem curl na imagem: o próprio binário faz o GET em /health/live e sai com 0 ou 1.
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD ["dotnet", "CatalogoApi.dll", "--healthcheck"]

ENTRYPOINT ["dotnet", "CatalogoApi.dll"]
