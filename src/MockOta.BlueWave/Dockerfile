# Build context = solution root (so all csproj files are visible for restore caching).
# Built from deploy/docker-compose.yml as: build: { context: .., dockerfile: src/MockOta.BlueWave/Dockerfile }

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy only project files first to leverage Docker layer cache on dotnet restore.
COPY HotelOtaSync.sln ./
COPY src/MockOta.BlueWave/MockOta.BlueWave.csproj src/MockOta.BlueWave/
RUN dotnet restore src/MockOta.BlueWave/MockOta.BlueWave.csproj

COPY src/MockOta.BlueWave/ src/MockOta.BlueWave/
RUN dotnet publish src/MockOta.BlueWave/MockOta.BlueWave.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Healthcheck hits the diagnostic root endpoint (returns plain text 200).
HEALTHCHECK --interval=10s --timeout=3s --start-period=15s --retries=5 \
  CMD wget -qO- http://localhost:8080/ >/dev/null 2>&1 || exit 1

ENTRYPOINT ["dotnet", "MockOta.BlueWave.dll"]
