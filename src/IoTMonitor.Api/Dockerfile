# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /source

COPY src/IoTMonitor.Api/IoTMonitor.Api.csproj src/IoTMonitor.Api/
RUN dotnet restore src/IoTMonitor.Api/IoTMonitor.Api.csproj

COPY src/IoTMonitor.Api/ src/IoTMonitor.Api/
RUN dotnet publish src/IoTMonitor.Api/IoTMonitor.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

USER root
RUN apk add --no-cache krb5-libs \
    && mkdir -p /var/lib/iotmonitor/keys \
    && chown -R "$APP_UID":0 /var/lib/iotmonitor \
    && chmod 0700 /var/lib/iotmonitor/keys

COPY --from=build /app/publish/ .

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "IoTMonitor.Api.dll"]
