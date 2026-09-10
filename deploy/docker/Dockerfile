# syntax=docker/dockerfile:1
#
# Build context is the repository root, because the image needs global.json,
# Directory.Packages.props and all three projects:
#
#   docker build -f deploy/docker/Dockerfile -t orderflow-api .

ARG DOTNET_VERSION=10.0

# ---------------------------------------------------------------------------- build ---
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine AS build

WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./

# Project files are copied before the source so restore lands in its own layer. Editing a
# .cs file then leaves the restore cached, which is most of the build time.
COPY src/OrderFlow.Domain/*.csproj          src/OrderFlow.Domain/
COPY src/OrderFlow.Infrastructure/*.csproj  src/OrderFlow.Infrastructure/
COPY src/OrderFlow.Api/*.csproj             src/OrderFlow.Api/

RUN dotnet restore src/OrderFlow.Api/OrderFlow.Api.csproj

COPY src/ src/

RUN dotnet publish src/OrderFlow.Api/OrderFlow.Api.csproj \
      --configuration Release \
      --no-restore \
      --output /app/publish

# -------------------------------------------------------------------------- runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS runtime

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

WORKDIR /app
COPY --from=build /app/publish .

# APP_UID is a non-root user supplied by the base image. Running as root inside a container
# buys nothing here and is the first thing a Kubernetes policy will reject.
USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "OrderFlow.Api.dll"]
