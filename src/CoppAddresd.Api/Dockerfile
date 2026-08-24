# =========================
# BUILD
# =========================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore "src/CoppAddresd.Api/CoppAddresd.Api.csproj"

RUN dotnet publish \
    "src/CoppAddresd.Api/CoppAddresd.Api.csproj" \
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

ENTRYPOINT ["dotnet", "CoppAddresd.Api.dll"]
