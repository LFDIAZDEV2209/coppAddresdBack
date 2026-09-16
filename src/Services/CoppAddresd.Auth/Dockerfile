# =========================
# BUILD
# =========================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Capa de restore separada: solo se invalida si cambian los csproj, no con
# cada cambio de código (ahorra el restore frío en cada build de CI con caché).
COPY src/Services/CoppAddresd.Auth/CoppAddresd.Auth.csproj src/Services/CoppAddresd.Auth/

RUN dotnet restore "src/Services/CoppAddresd.Auth/CoppAddresd.Auth.csproj"

COPY . .

RUN dotnet publish \
    "src/Services/CoppAddresd.Auth/CoppAddresd.Auth.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# =========================
# RUNTIME
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "CoppAddresd.Auth.dll"]
