# =========================
# BUILD
# =========================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore "src/Services/CoppAddresd.Telemedicine/CoppAddresd.Telemedicine.csproj"

RUN dotnet publish \
    "src/Services/CoppAddresd.Telemedicine/CoppAddresd.Telemedicine.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# =========================
# RUNTIME
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "CoppAddresd.Telemedicine.dll"]
