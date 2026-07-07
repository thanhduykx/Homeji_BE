# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json Homeji.sln ./
COPY src/Homeji.Domain/Homeji.Domain.csproj src/Homeji.Domain/
COPY src/Homeji.Application/Homeji.Application.csproj src/Homeji.Application/
COPY src/Homeji.Infrastructure/Homeji.Infrastructure.csproj src/Homeji.Infrastructure/
COPY src/Homeji.Api/Homeji.Api.csproj src/Homeji.Api/
COPY tests/Homeji.Application.UnitTests/Homeji.Application.UnitTests.csproj tests/Homeji.Application.UnitTests/
COPY tests/Homeji.Api.IntegrationTests/Homeji.Api.IntegrationTests.csproj tests/Homeji.Api.IntegrationTests/

RUN dotnet restore Homeji.sln

COPY . .
RUN dotnet publish src/Homeji.Api/Homeji.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Homeji.Api.dll", "--urls", "http://0.0.0.0:8080"]
