# =========================
# BUILD
# =========================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore "src/Services/CoppAddresd.Gateway/CoppAddresd.Gateway.csproj"

RUN dotnet publish \
    "src/Services/CoppAddresd.Gateway/CoppAddresd.Gateway.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# =========================
# RUNTIME
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl es requerido por el healthcheck del docker-compose; la imagen
# mcr.microsoft.com/dotnet/aspnet NO lo incluye por defecto.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish .

# Entorno "Docker": carga appsettings.Docker.json (clusters por nombre de servicio
# dentro de la red de compose) y la configuración base appsettings.json.
ENV ASPNETCORE_ENVIRONMENT=Docker
ENV ASPNETCORE_URLS=http://+:5080

EXPOSE 5080

ENTRYPOINT ["dotnet", "CoppAddresd.Gateway.dll"]
