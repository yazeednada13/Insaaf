# Insaaf API container (Phase 10 preparation; not built or deployed yet).
# Build from src/backend:  docker build -t insaaf-api .
# Configuration comes only from environment variables (see docs/runbook.md); no secrets are baked in.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Insaaf.sln ./
COPY Insaaf.Domain/Insaaf.Domain.csproj Insaaf.Domain/
COPY Insaaf.Application/Insaaf.Application.csproj Insaaf.Application/
COPY Insaaf.Infrastructure/Insaaf.Infrastructure.csproj Insaaf.Infrastructure/
COPY Insaaf.API/Insaaf.API.csproj Insaaf.API/
RUN dotnet restore Insaaf.API/Insaaf.API.csproj
COPY Insaaf.Domain/ Insaaf.Domain/
COPY Insaaf.Application/ Insaaf.Application/
COPY Insaaf.Infrastructure/ Insaaf.Infrastructure/
COPY Insaaf.API/ Insaaf.API/
RUN dotnet publish Insaaf.API/Insaaf.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
# Photos live outside the image, on a mounted volume.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    Storage__ReviewPhotos__RootPath=/data/review-photos
RUN mkdir -p /data/review-photos && chown -R app:app /data
USER app
COPY --from=build /app ./
EXPOSE 8080
# Health probes: configure the host to call GET /health/live and /health/ready (the base image has no curl/wget).
ENTRYPOINT ["dotnet", "Insaaf.API.dll"]
