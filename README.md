# dotnet-docker-smoketest

A small ASP.NET Core app written with current .NET practices. Use it to confirm that a
development environment has working .NET and Docker tooling. That can be a laptop, a VM,
a dev container, or a CI runner.

If every step in [Verify your environment](#verify-your-environment) passes, the
environment can build, test, run, debug, and containerize a modern .NET web app.

## Prerequisites

| Tool | Version | Needed for |
|---|---|---|
| .NET SDK | 10.0.100 or later 10.0.x (`global.json` rolls forward to the newest feature band) | build, test, run |
| Docker Engine with BuildKit/buildx | Docker 23+ (BuildKit is the default builder) | container steps |
| Docker Compose v2 | any current release | compose step (optional) |
| VS Code + C# Dev Kit extension | current release | debugging and test explorer (optional) |

### Scope

The intended environment is **Linux (x64 or arm64) with VS Code**. All commands and
instructions assume that environment.

The app is plain cross-platform .NET, so other operating systems (macOS, Windows) and IDEs
(Visual Studio, Rider) may work with minor changes or none. They are outside the intended
scope and are not verified.

## Verify your environment

Run these from the repository root. Each step checks one capability.

| # | Capability | Command | Expected result |
|---|---|---|---|
| 1 | SDK resolves | `dotnet --version` | `10.0.x` |
| 2 | Restore + build (analyzers, warnings as errors) | `dotnet build` | `0 Warning(s)`, `0 Error(s)` |
| 3 | Unit + integration tests | `dotnet test` | `succeeded: 12`, `failed: 0` |
| 4 | Code coverage | `dotnet test --coverage` | a `.coverage` file under `TestResults/` |
| 5 | Run locally | `dotnet run --project src/DotnetDockerSmoketest.Api` | <http://localhost:5101> shows the to-do page |
| 6 | Hot reload | `dotnet watch --project src/DotnetDockerSmoketest.Api` | code edits apply without a restart |
| 7 | Debugger | set a breakpoint, press **F5** (see [Debugging](#debugging)) | the breakpoint is hit |
| 8 | Test discovery | open the VS Code **Testing** view | 12 tests are listed and runnable |
| 9 | Image build | `docker build -t dotnet-docker-smoketest-api:dev .` | the build completes |
| 10 | Tests inside the SDK image | `docker build --target test .` | `succeeded: 12` |
| 11 | Run the image | see [Container](#container) | `/healthz` returns `Healthy` |
| 12 | Compose | `docker compose up --build` | <http://localhost:8080> shows the to-do page |

Steps 1–6 need only the .NET SDK, steps 7–8 also need VS Code, and steps 9–12 need Docker.

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
| Tests | xUnit v3 (4.x) on Microsoft.Testing.Platform (MTP) v2, `WebApplicationFactory`, MS Code Coverage | Current .NET test stack (replaces VSTest); see [Test platform](#test-platform) |
| Image | `aspnet:10.0-noble-chiseled`, non-root, port 8080, BuildKit cache mounts | Distroless image with a small attack surface |

```
dotnet-docker-smoketest/
├── DotnetDockerSmoketest.slnx
├── global.json                  # SDK version policy + MTP test runner
├── Directory.Build.props        # shared compiler/analyzer settings
├── Directory.Packages.props     # central package versions
├── Dockerfile / .dockerignore / compose.yaml
├── .vscode/                     # launch, tasks, extension recommendations
├── src/DotnetDockerSmoketest.Api/
│   ├── Program.cs               # composition root
│   ├── Todos/                   # feature folder: model, store, endpoints
│   └── wwwroot/index.html       # tiny vanilla-JS UI for the API
└── tests/DotnetDockerSmoketest.Api.Tests/
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
dotnet run --project src/DotnetDockerSmoketest.Api          # http://localhost:5101
dotnet watch --project src/DotnetDockerSmoketest.Api        # hot reload
```

### Debugging

1. Open the repository root as the VS Code workspace folder, so that `.vscode/` applies.
2. Install the recommended **C# Dev Kit** extension (listed in `.vscode/extensions.json`).
3. Set a breakpoint, for example in `TodoEndpoints.Create`. Then press **F5** and choose
   "DotnetDockerSmoketest.Api (http)". VS Code forwards port 5101 automatically.

## Test

```bash
dotnet test                                   # all tests
dotnet test --filter-method "*Crud*"          # xUnit v3 filter syntax
dotnet test --coverage                        # -> TestResults/*.coverage (binary)
dotnet test --coverage --coverage-output-format cobertura
                                              # -> TestResults/*.cobertura.xml (for CI tools)
dotnet run --project tests/DotnetDockerSmoketest.Api.Tests
                                              # the test project is itself a runnable executable
```

### Test platform

The tests use **xUnit v3 (4.x)** on **Microsoft.Testing.Platform (MTP) v2**. This is the
current .NET test stack, which replaces the older **VSTest** platform. It differs from what
`dotnet new xunit` generates (xUnit v2 on VSTest), and that has consequences:

- **The `global.json` setting is required.** xUnit v3 4.x supports only MTP v2.
  `"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json` switches
  `dotnet test` to MTP mode (.NET 10 SDK or later). Without it, `dotnet test` fails with
  *"Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on
  .NET 10 SDK and later"*.
- **The test project is an executable.** `OutputType` is `Exe`, and the project contains
  its own test runner. `dotnet test` builds and starts it, and so do `dotnet run` and
  running the binary directly.
- **There are no VSTest packages.** `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`
  and `coverlet.collector` are missing on purpose. They only work with VSTest, so adding
  them back fixes nothing.
- **Coverage** comes from `Microsoft.Testing.Extensions.CodeCoverage` (`--coverage`). Its
  default output is Microsoft's binary `.coverage` format. Use
  `--coverage-output-format cobertura` for CI tools and ReportGenerator.
- **VSTest-only tooling won't work.** That includes coverlet, VSTest-based CI tasks, and
  the old `dotnet test -- <runner args>` argument syntax. In MTP mode, options such as
  `--filter-method` are passed directly.

## Container

```bash
docker build -t dotnet-docker-smoketest-api:dev .
docker run --rm -d --name dotnet-docker-smoketest -p 8080:8080 dotnet-docker-smoketest-api:dev
curl --retry 10 --retry-all-errors --retry-delay 1 localhost:8080/healthz   # app needs ~1s to start
docker stop dotnet-docker-smoketest

docker build --target test .                  # run the test suite inside the SDK image
docker compose up --build                     # read-only FS, all caps dropped

# Multi-arch (the build stage runs natively; only the target RID changes)
docker buildx build --platform linux/amd64,linux/arm64 -t dotnet-docker-smoketest-api:dev .
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

Alternative without a Dockerfile: the SDK can build the image itself with
`dotnet publish src/DotnetDockerSmoketest.Api -t:PublishContainer -p:ContainerFamily=noble-chiseled`.

## Troubleshooting

### `dotnet restore` hangs during `docker build`

This usually means a network MTU mismatch. The Docker bridge defaults to MTU 1500. If the
machine's own network link has a smaller MTU (common with VPNs, some WSL2 network modes,
and nested Docker-in-Docker), large packets such as TLS handshakes are dropped. HTTPS
calls from containers and from build steps then hang, while `docker pull` still works
because the daemon makes those requests itself, outside the bridge.

- Confirm it: `docker build --network host …` succeeds.
- Fix it: set the daemon's MTU to match the link in `daemon.json`, then restart the
  daemon. For example, with a 1400 link:

  ```json
  {
    "mtu": 1400,
    "default-network-opts": { "bridge": { "com.docker.network.driver.mtu": "1400" } }
  }
  ```

  `mtu` covers the default bridge. `default-network-opts` covers networks created later,
  such as the one Compose creates.

### `dotnet test` reports an error about VSTest or the test runner

`dotnet test` must run in Microsoft.Testing.Platform mode (see
[Test platform](#test-platform)). Check that:

- `global.json` still contains `"test": { "runner": "Microsoft.Testing.Platform" }`.
- `dotnet --version` reports 10.0.x or later when run from the repo root. Older SDKs
  ignore the setting.
