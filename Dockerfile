# Grundet API — production image.
#
# Build context: repo root (so NuGet.Config / Directory.Build.props are
# visible to the SDK stage). Build target: src/Grundet.Api only — the
# project has no ProjectReferences, so nothing else needs to be copied in.
#
# Build (from repo root):
#   docker build -f Dockerfile -t ghcr.io/<owner>/grundet-api:<tag> .
#
# Migrations are NOT run by this image. Per
# docs/infra/deployment-and-scaling-considerations.md → "Separate Runtime,
# Migration, and Admin Roles" and "Deploy & Migration Runbook", `dotnet ef
# database update` runs as a separate deploy step against the migrator
# connection string, before traffic is routed to a new container.

# ---- Build stage --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NuGet.Config ./
COPY src/Directory.Build.props src/Directory.Build.props
COPY src/Grundet.Api/Grundet.Api.csproj src/Grundet.Api/Grundet.Api.csproj
RUN dotnet restore src/Grundet.Api/Grundet.Api.csproj

COPY src/Grundet.Api/ src/Grundet.Api/
RUN dotnet publish src/Grundet.Api/Grundet.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---- Runtime stage -------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is required by the HEALTHCHECK below; the aspnet base image does not
# include it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Non-root: run as the "app" user Microsoft's runtime images already ship.
# (uid 64198 in the mcr.microsoft.com/dotnet images.)
RUN chown -R app:app /app
USER app

COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# docs/infra/deployment-and-scaling-considerations.md §7 — exact directive,
# do not reformat: Coolify needs this to gate rolling restarts on real
# readiness (Kestrel listening + startup complete), not just process start.
HEALTHCHECK --interval=10s --timeout=5s --start-period=15s --retries=3 CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Grundet.Api.dll"]
