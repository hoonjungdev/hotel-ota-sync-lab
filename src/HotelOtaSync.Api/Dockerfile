# Build context = solution root (so all csproj files are visible for restore caching).
# Built from deploy/docker-compose.yml as: build: { context: .., dockerfile: src/HotelOtaSync.Api/Dockerfile }

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy only project files first to leverage Docker layer cache on dotnet restore.
COPY HotelOtaSync.sln ./
COPY src/HotelOtaSync.Domain/HotelOtaSync.Domain.csproj         src/HotelOtaSync.Domain/
COPY src/HotelOtaSync.Application/HotelOtaSync.Application.csproj src/HotelOtaSync.Application/
COPY src/HotelOtaSync.Infrastructure/HotelOtaSync.Infrastructure.csproj src/HotelOtaSync.Infrastructure/
COPY src/HotelOtaSync.Api/HotelOtaSync.Api.csproj               src/HotelOtaSync.Api/
RUN dotnet restore src/HotelOtaSync.Api/HotelOtaSync.Api.csproj

COPY src/HotelOtaSync.Domain/         src/HotelOtaSync.Domain/
COPY src/HotelOtaSync.Application/    src/HotelOtaSync.Application/
COPY src/HotelOtaSync.Infrastructure/ src/HotelOtaSync.Infrastructure/
COPY src/HotelOtaSync.Api/            src/HotelOtaSync.Api/
RUN dotnet publish src/HotelOtaSync.Api/HotelOtaSync.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=3s --start-period=15s --retries=5 \
  CMD wget -qO- http://localhost:8080/health >/dev/null 2>&1 || exit 1

ENTRYPOINT ["dotnet", "HotelOtaSync.Api.dll"]
