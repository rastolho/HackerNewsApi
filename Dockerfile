# syntax=docker/dockerfile:1.7

ARG DOTNET_VERSION=9.0

# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Copy csprojs first so `dotnet restore` is cached as long as deps don't change.
COPY global.json Directory.Build.props ./
COPY src/HackerNews.Domain/HackerNews.Domain.csproj            src/HackerNews.Domain/
COPY src/HackerNews.Application/HackerNews.Application.csproj  src/HackerNews.Application/
COPY src/HackerNews.Infrastructure/HackerNews.Infrastructure.csproj src/HackerNews.Infrastructure/
COPY src/HackerNews.Api/HackerNews.Api.csproj                  src/HackerNews.Api/
RUN dotnet restore src/HackerNews.Api/HackerNews.Api.csproj

# Now copy sources and publish.
COPY src/ src/
RUN dotnet publish src/HackerNews.Api/HackerNews.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final

# curl is used by the HEALTHCHECK below; the runtime image is otherwise minimal.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

# The aspnet base image ships a pre-created non-root `app` user (UID 1654).
USER app

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl --fail http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "HackerNews.Api.dll"]
