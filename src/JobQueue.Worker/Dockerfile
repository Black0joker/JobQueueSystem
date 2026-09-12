# Phase 26: container image for the JobQueue worker.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# The projects reference each other across folders, so the whole source tree is the
# build context; the .dockerignore at the repo root keeps bin/obj/.git out of it.
COPY . .
RUN dotnet publish src/JobQueue.Worker/JobQueue.Worker.csproj -c Release -o /app/publish

# The worker's diagnostics endpoints (/metrics, /health) run a minimal ASP.NET Core
# server, so the aspnet runtime image is required.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Prometheus metrics + health endpoints (Worker:MetricsPort, default 5209).
EXPOSE 5209
ENTRYPOINT ["dotnet", "JobQueue.Worker.dll"]
