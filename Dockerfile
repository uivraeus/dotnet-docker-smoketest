# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build stage: full SDK, runs on the *build* platform so cross-arch builds are
# fast (no QEMU for the compiler) and targets the requested platform via -a.
# ---------------------------------------------------------------------------
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
ARG CONFIGURATION=Release
WORKDIR /src

# Copy only what restore needs first, so the restore layer is cached until
# a project/package file actually changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Test101.Api/Test101.Api.csproj src/Test101.Api/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore src/Test101.Api/Test101.Api.csproj -a $TARGETARCH

COPY src/ src/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/Test101.Api/Test101.Api.csproj \
        -c $CONFIGURATION -a $TARGETARCH --no-restore \
        -o /app

# ---------------------------------------------------------------------------
# Test stage (optional): `docker build --target test .`
# ---------------------------------------------------------------------------
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS test
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props Test101.slnx ./
COPY src/Test101.Api/Test101.Api.csproj src/Test101.Api/
COPY tests/Test101.Api.Tests/Test101.Api.Tests.csproj tests/Test101.Api.Tests/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore
COPY src/ src/
COPY tests/ tests/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet test --no-restore

# ---------------------------------------------------------------------------
# Runtime stage: Ubuntu "chiseled" (distroless) ASP.NET image.
#  - no shell / package manager -> minimal attack surface
#  - runs as non-root ($APP_UID = 1654) by default
#  - listens on 8080 (ASPNETCORE_HTTP_PORTS) by default
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build --link /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Test101.Api.dll"]
