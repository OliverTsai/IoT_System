# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /source

COPY src/IoTMonitor.DeviceSimulator/IoTMonitor.DeviceSimulator.csproj src/IoTMonitor.DeviceSimulator/
RUN dotnet restore src/IoTMonitor.DeviceSimulator/IoTMonitor.DeviceSimulator.csproj

COPY src/IoTMonitor.DeviceSimulator/ src/IoTMonitor.DeviceSimulator/
RUN dotnet publish src/IoTMonitor.DeviceSimulator/IoTMonitor.DeviceSimulator.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS final
WORKDIR /app

ENV DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY --from=build /app/publish/ .

USER $APP_UID

ENTRYPOINT ["dotnet", "IoTMonitor.DeviceSimulator.dll"]
