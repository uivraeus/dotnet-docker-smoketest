# Test101 – canonical .NET 10 web app

A deliberately small, "do it the current way" ASP.NET Core app used to smoke-test the
`.Net/Docker` dev container. It checks that each of these works end to end:

- **Build:** the SDK, restore, analyzers, and `TreatWarningsAsErrors`.
- **Unit and integration tests:** xUnit v3 on Microsoft.Testing.Platform, plus code coverage.
- **Local run, hot reload, and debugging:** `dotnet run`, `dotnet watch`, and VS Code `F5`.
- **Container packaging:** a multi-stage Dockerfile that produces a chiseled, non-root image.

## What's inside

| Area | Choice | Why |
|---|---|---|
| Runtime | .NET 10 (LTS), C# latest | Current long-term-support release |
| API style | Minimal APIs, `MapGroup`, `TypedResults`, `Results<…>` unions | Strongly typed responses that the OpenAPI metadata picks up |
| Validation | Built-in `AddValidation()` + DataAnnotations on records | New in .NET 10; invalid input returns RFC 9457 ProblemDetails |
| Errors | `AddProblemDetails()`, `UseExceptionHandler()`, `UseStatusCodePages()` | Consistent `application/problem+json` error bodies |
| API docs | `Microsoft.AspNetCore.OpenApi` (built in) + Scalar UI | Swashbuckle is no longer in the templates |
| Health | `AddHealthChecks()` → `/healthz` | For container and orchestrator probes |
| Time | `TimeProvider` injected | Lets tests control the clock (`FakeTimeProvider`) |
| IDs | `Guid.CreateVersion7()` | Time-ordered UUIDv7 |
| Solution | `.slnx` (XML solution format) | Default from .NET 10 onward |
| Build | `Directory.Build.props`, Central Package Management, `global.json` | One place for settings and package versions |
| Tests | xUnit v3 (4.x), MTP v2, `WebApplicationFactory`, MS Code Coverage | `dotnet test` runs on Microsoft.Testing.Platform (`global.json` → `test.runner`) |
| Image | `aspnet:10.0-noble-chiseled`, non-root, port 8080, BuildKit cache mounts | Distroless image with a small attack surface |

```
test-101/
├── Test101.slnx
├── global.json                  # SDK pin + MTP test runner
├── Directory.Build.props        # shared compiler/analyzer settings
├── Directory.Packages.props     # central package versions
├── Dockerfile / .dockerignore / compose.yaml
├── .vscode/                     # launch, tasks, extension recommendations
├── src/Test101.Api/
│   ├── Program.cs               # composition root
│   ├── Todos/                   # feature folder: model, store, endpoints
│   └── wwwroot/index.html       # tiny vanilla-JS UI for the API
└── tests/Test101.Api.Tests/
    ├── InMemoryTodoStoreTests.cs  # pure unit tests
    └── TodoApiTests.cs            # in-memory HTTP integration tests
```

## Endpoints

| Method | Path | Notes |
|---|---|---|
| GET | `/` | Static landing page (to-do UI) |
| GET | `/api/info` | Framework, environment, and host name |
| GET/POST | `/api/todos` | List / create |
| GET/PUT/DELETE | `/api/todos/{id}` | Read / update / delete |
| GET | `/healthz` | Health check |
| GET | `/openapi/v1.json` | OpenAPI document (Development only) |
| GET | `/scalar` | Interactive API reference (Development only) |

Data is held in memory, so it is lost when the app restarts.

## Develop

```bash
dotnet build                                  # warnings are errors
dotnet run --project src/Test101.Api          # http://localhost:5101
dotnet watch --project src/Test101.Api        # hot reload
```

### Debugging in VS Code

1. Install the recommended **C# Dev Kit** extension (`.vscode/extensions.json`).
   To make it permanent for the dev container, add `ms-dotnettools.csdevkit` to
   `customizations.vscode.extensions` in `.devcontainer/devcontainer.json`.
2. Open **this folder** (`test-101`) as the VS Code workspace, so that `.vscode/` is used.
3. Set a breakpoint, for example in `TodoEndpoints.Create`, then press **F5** and choose
   "Test101.Api (http)". VS Code forwards port 5101 automatically.

## Test

```bash
dotnet test                                   # all tests (MTP runner)
dotnet test --coverage --coverage-output-format cobertura
                                              # -> TestResults/*.cobertura.xml
dotnet test -- --filter-method "*Crud*"       # xUnit v3 filter syntax
```

Tests also appear in the VS Code Test Explorer once C# Dev Kit is installed.

## Container

```bash
docker build -t test101-api:dev .
docker run --rm -p 8080:8080 test101-api:dev
curl localhost:8080/healthz

docker build --target test .                  # run the test suite inside the SDK image
docker compose up --build                     # read-only FS, all caps dropped

# Multi-arch (the build stage runs natively; only the target RID changes)
docker buildx build --platform linux/amd64,linux/arm64 -t test101-api:dev .
```

Key points of the image:

- **Multi-stage build.** The SDK is used only to build. The runtime stage is the
  *chiseled* ASP.NET image, which has no shell and no package manager.
- **Non-root by default.** The image runs as the built-in `app` user (`$APP_UID` = 1654)
  and listens on **8080**.
- **Cache-friendly layers.** Project and props files are copied and restored first.
  NuGet packages live in a BuildKit cache mount, so they never end up in a layer.
- **`--platform=$BUILDPLATFORM` + `-a $TARGETARCH`.** Cross-architecture builds don't
  need to emulate the compiler.
- **No `HEALTHCHECK` instruction.** Chiseled images have no `curl` or shell to run one.
  Point the orchestrator's probe (Kubernetes, ECS, Compose) at `/healthz` instead.

### Troubleshooting: `dotnet restore` hangs during `docker build`

In this dev container, `eth0` has MTU **1400** (WSL2), but the Docker-in-Docker bridge
defaults to **1500**. Small packets get through, but full-size TLS packets are dropped.
As a result, HTTPS from a container or a `RUN` step (NuGet) hangs, while image pulls
(done by `dockerd` itself) still work. Fixes:

- Quick workaround: `docker build --network host …`
- Permanent fix: start `dockerd` with `"mtu": 1400` in `/etc/docker/daemon.json`
  (see the dev container's `postCreateCommand`).

Alternative without a Dockerfile: the SDK can build the image itself with
`dotnet publish src/Test101.Api -t:PublishContainer -p:ContainerFamily=noble-chiseled`.
