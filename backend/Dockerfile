# syntax=docker/dockerfile:1
# ---------------------------------------------------------------------------
# TallyG Tax API — multi-stage build
#   build stage : .NET 9 SDK, restore + publish the Api project
#   final stage : ASP.NET 9 runtime, runs the published app on :5080
# Build context is the backend/ folder (see infra/docker-compose.yml).
# ---------------------------------------------------------------------------

# ---- build -----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the solution + project files first so `restore` is cached independently
# of source changes (Docker layer caching: edits to .cs files don't re-restore).
COPY TallyG.Tax.sln Directory.Build.props ./
COPY src/TallyG.Tax.Domain/TallyG.Tax.Domain.csproj           src/TallyG.Tax.Domain/
COPY src/TallyG.Tax.Infrastructure/TallyG.Tax.Infrastructure.csproj src/TallyG.Tax.Infrastructure/
COPY src/TallyG.Tax.Api/TallyG.Tax.Api.csproj                 src/TallyG.Tax.Api/
COPY tests/TallyG.Tax.Tests/TallyG.Tax.Tests.csproj           tests/TallyG.Tax.Tests/
RUN dotnet restore src/TallyG.Tax.Api/TallyG.Tax.Api.csproj

# Copy the rest of the source and publish a self-contained framework-dependent build.
COPY . .
RUN dotnet publish src/TallyG.Tax.Api/TallyG.Tax.Api.csproj \
        -c Release \
        -o /app/publish \
        /p:UseAppHost=false

# ---- runtime ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# curl is used by the compose healthcheck to hit /health (not in the base image).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Kestrel binds here; Database__Provider / ConnectionStrings__Default come from compose.
ENV ASPNETCORE_URLS=http://+:5080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0

# Local document store used by the demo IFileStorage (overridden via Storage__LocalRoot).
RUN mkdir -p /app/.localstore

COPY --from=build /app/publish ./
EXPOSE 5080

ENTRYPOINT ["dotnet", "TallyG.Tax.Api.dll"]
