# Build context is the repository root (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until a project file or package version changes.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Wallet.Core/Wallet.Core.csproj src/Wallet.Core/
COPY src/Wallet.Infrastructure/Wallet.Infrastructure.csproj src/Wallet.Infrastructure/
COPY src/Wallet.Api/Wallet.Api.csproj src/Wallet.Api/
RUN dotnet restore src/Wallet.Api/Wallet.Api.csproj

COPY src/ src/
RUN dotnet publish src/Wallet.Api/Wallet.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Wallet.Api.dll"]
