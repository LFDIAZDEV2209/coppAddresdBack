# =========================
# BUILD
# =========================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Capa de restore separada: solo se invalida si cambian los csproj, no con
# cada cambio de código (ahorra el restore frío en cada build de CI con caché).
COPY src/CoppAddresd.Domain/CoppAddresd.Domain.csproj src/CoppAddresd.Domain/
COPY src/CoppAddresd.Application/CoppAddresd.Application.csproj src/CoppAddresd.Application/
COPY src/CoppAddresd.Infrastructure/CoppAddresd.Infrastructure.csproj src/CoppAddresd.Infrastructure/
COPY src/Services/CoppAddresd.Community/CoppAddresd.Community.csproj src/Services/CoppAddresd.Community/

RUN dotnet restore "src/Services/CoppAddresd.Community/CoppAddresd.Community.csproj"

COPY . .

RUN dotnet publish \
    "src/Services/CoppAddresd.Community/CoppAddresd.Community.csproj" \
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

ENTRYPOINT ["dotnet", "CoppAddresd.Community.dll"]
