# Phase 26: container image for the JobQueue API.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# The projects reference each other across folders, so the whole source tree is the
# build context; the .dockerignore at the repo root keeps bin/obj/.git out of it.
COPY . .
RUN dotnet publish src/JobQueue.Api/JobQueue.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# ASP.NET Core listens on 8080 by default (ASPNETCORE_HTTP_PORTS).
EXPOSE 8080
ENTRYPOINT ["dotnet", "JobQueue.Api.dll"]
