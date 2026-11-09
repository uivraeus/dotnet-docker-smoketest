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
| VS Code + **C#** extension (`ms-dotnettools.csharp`) | current release | code intelligence, debugging of the app and the tests |

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
| 4 | Code coverage | see [Coverage reports](#coverage-reports) | `coverage-report/Summary.txt` lists coverage per class |
| 5 | Run locally | `dotnet run --project src/DotnetDockerSmoketest.Api` | <http://localhost:5101> shows the to-do page |
| 6 | Hot reload | `dotnet watch --project src/DotnetDockerSmoketest.Api` | code edits apply without a restart |
| 7 | Debugger | set a breakpoint, press **F5** (see [VS Code](#vs-code)) | the breakpoint is hit |
| 8 | Test debugging | set a breakpoint in a test, press **F5** and choose "Tests (xUnit runner)" | the breakpoint is hit; afterwards the terminal shows `Total: 12`, `Failed: 0` |
| 9 | Image build | `docker build -t dotnet-docker-smoketest-api:dev .` | the build completes |
| 10 | Tests inside the SDK image | `docker build --target test .` | `succeeded: 12` |
| 11 | Run the image | see [Container](#container) | `/healthz` returns `Healthy` |
| 12 | Compose | `docker compose up --build` | <http://localhost:8080> shows the to-do page |

Steps 1–6 need only the .NET SDK (step 4 also restores the repo-local ReportGenerator
tool from NuGet). Steps 7–8 also need VS Code with the C# extension. Steps 9–12 need
Docker.

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
| Coverage reports | ReportGenerator as a repo-local .NET tool (`dotnet-tools.json`) | Turns CLI coverage output into a summary and HTML; see [Coverage reports](#coverage-reports) |
| Image | `aspnet:10.0-noble-chiseled`, non-root, port 8080, BuildKit cache mounts | Distroless image with a small attack surface |

```
dotnet-docker-smoketest/
├── DotnetDockerSmoketest.slnx
├── global.json                  # SDK version policy + MTP test runner
├── Directory.Build.props        # shared compiler/analyzer settings
├── Directory.Packages.props     # central package versions
├── dotnet-tools.json            # repo-local tools (ReportGenerator)
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

### VS Code

Open the repository root as the VS Code workspace folder, so that `.vscode/` applies
(launch configuration, tasks and extension recommendations).

The only C# extension needed is **C#** (`ms-dotnettools.csharp`). It is open source and
provides code intelligence and the **debugger**. Everything in this repo works without a
Microsoft account.

**C# Dev Kit** (`ms-dotnettools.csdevkit`) is deliberately not used, and
`.vscode/extensions.json` marks it as unwanted. It is closed source under Visual Studio
license terms, and its features (the **Testing** view, in-editor coverage, the
**C# Project Details** view) require signing in with a Microsoft account. The repo covers
the same needs with plain tools instead:

| Need | Without C# Dev Kit |
|---|---|
| Run tests | `dotnet test`, or the **test** task (*Tasks: Run Test Task*) |
| Debug tests | the "Tests (xUnit runner)" launch configuration (see below) |
| Coverage | the **coverage report** task, or the commands in [Coverage reports](#coverage-reports) |

**Debugging the app:** set a breakpoint, for example in `TodoEndpoints.Create`. Then
press **F5** and choose "DotnetDockerSmoketest.Api (http)". VS Code forwards port 5101 and
opens the page.

**Debugging tests:** set a breakpoint in a test, press **F5** and choose
"Tests (xUnit runner)". VS Code asks for a test method filter: keep `*` for all tests,
or enter a pattern such as `*Crud*`. This starts the test executable with xUnit's own
console runner (see [Test platform](#test-platform)), so the same tests run as with
`dotnet test`.

## Test

```bash
dotnet test                                   # all tests
dotnet test --filter-method "*Crud*"          # xUnit v3 filter syntax
dotnet test --coverage                        # -> TestResults/*.coverage (binary)
dotnet test --coverage --coverage-output-format cobertura
                                              # -> TestResults/*.cobertura.xml (for CI tools)
dotnet run --project tests/DotnetDockerSmoketest.Api.Tests
                                              # same tests via xUnit's own console runner (see below)
```

### Coverage reports

`dotnet test --coverage` only writes a coverage file. To turn it into something readable,
the repo pins **ReportGenerator** as a local .NET tool in `dotnet-tools.json`. In VS Code,
the **coverage report** task (*Tasks: Run Task*) runs all of the following, starting from
a clean `TestResults/`:

```bash
dotnet tool restore                           # once per machine: installs the pinned tools
dotnet test --coverage --coverage-output-format cobertura
dotnet reportgenerator -reports:"TestResults/**/*.cobertura.xml" -targetdir:coverage-report \
  -reporttypes:"TextSummary;Html" -filefilters:"-*/obj/*"
cat coverage-report/Summary.txt               # totals per class
```

- `-filefilters:"-*/obj/*"` leaves out the compiler-generated files, so the figures cover
  only the hand-written app code.
- The HTML report is `coverage-report/index.html`. Open it in a local browser. In a dev
  container or other remote session without a browser, serve it with
  `python3 -m http.server -d coverage-report 8000` and open the port that VS Code forwards.
- `TestResults/` and `coverage-report/` are git-ignored. Delete them before a new run if
  you don't want results from earlier runs merged into the report.

### Test platform

The tests use **xUnit v3 (4.x)** on **Microsoft.Testing.Platform (MTP) v2**. This is the
current .NET test stack, which replaces the older **VSTest** platform. It differs from what
`dotnet new xunit` generates (xUnit v2 on VSTest), and that has consequences:

- **The `global.json` setting is required.** xUnit v3 4.x supports only MTP v2.
  `"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json` switches
  `dotnet test` to MTP mode (.NET 10 SDK or later). Without it, `dotnet test` fails with
  *"Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on
  .NET 10 SDK and later"*.
- **The test project is an executable with two modes.** `OutputType` is `Exe`, and xUnit
  generates its entry point (`obj/…/XunitAutoGeneratedEntryPoint.cs`). That entry point
  picks the runner from the command line:
  - **MTP mode** (started with `--server`): `dotnet test` starts the executable this way.
    IDE test explorers that support MTP do the same. This mode has the registered MTP
    extensions, such as code coverage.
  - **xUnit's own console runner** (any other start): this is what you get from
    `dotnet run --project tests/…`, by running the binary directly, or from the
    "Tests (xUnit runner)" launch configuration. The tests are the same, but MTP options
    such as `--coverage` or `--list-tests` are rejected. Use xUnit's own syntax instead
    (`-method`, `-trait`, `-result-xml`; see `-?`).
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
