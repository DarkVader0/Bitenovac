# RemoteBuildTool

A content-hashed build cache for .NET repositories on CI.

RemoteBuildTool fingerprints every project from its own inputs and from what it depends on. A
project whose fingerprint is already in the shared cache is restored instead of built, and its test
results and coverage are reused instead of run again. Only what changed is built and tested, and
coverage is gated per project. An optional service, in its own package, keeps the cache filled
from merged pull requests and records every build as queryable telemetry.

```
remotebuildtool plan            what changed, and what the cache can serve
remotebuildtool build Debug     restore the hits, build the misses
remotebuildtool test Debug      reuse or run each test project, then gate coverage
remotebuildtool promote         put this run's output into the cache
```

## How it works

**Two stores.** A store is a directory that holds one entry per project and configuration,
content-addressed so each distinct file is kept once.

- **main** is the shared cache. Only promotion and the service's nightly rebuild write it; every
  run reads it.
- **The run's own store** belongs to one CI run. It holds the run's plan and whatever the run
  built, so later jobs of the same run can pick it up.

**Fingerprints.** Each project gets two hashes:

- Its **own hash** covers the project's own inputs: its source files, the packages it resolves
  (including transitive ones), what the repository's own MSBuild files contribute to it
  (properties, items and item definitions as evaluated, and every repository file that declares
  targets or tasks), the `.editorconfig` files above it, the .NET SDK version, a few properties
  that change its output (`TargetFramework`, `LangVersion`, `Nullable`, `TreatWarningsAsErrors`,
  `Optimize`, `DefineConstants`, `Deterministic`, `ContinuousIntegrationBuild`), and the
  RemoteBuildTool version.
- Its **full hash** combines its own hash with the full hashes of the projects it references, so
  a change anywhere below a project changes it too.

Paths inside the repository are normalised, so the same commit has the same fingerprints on every
machine and in every checkout location.

**One pipeline.**

1. `plan` discovers every project, evaluates it in Debug and Release, restores, hashes, and
   decides per project whether main already holds its full hash (a *hit*) or it must be built (a
   *miss*). It writes the plan into the run's store.
2. `build <Config>` restores the hits from main into the workspace and builds the misses in one
   MSBuild run, then stages what it built in the run's store.
3. `test <Config>` puts every project's build output in place, then runs each test project, or
   reuses its cached result when the test project was a hit. On Debug it merges the coverage and
   gates each project whose own inputs changed or that main has never seen.
4. `promote` copies the run's entries into main, after the change is accepted.

A miss's log line says why it missed: which inputs changed, or which dependency did.

## Requirements

- **.NET 11 SDK** to run the tool. The repository you build can pin any SDK in its
  `global.json`; RemoteBuildTool builds with whatever SDK the repository selects.
- **MSBuild projects** (`.csproj` and friends), each with a single `<TargetFramework>`. A project
  with `<TargetFrameworks>` fails the plan.
- **Test projects on Microsoft.Testing.Platform**, built as executables (the default for
  xUnit v3, MSTest and NUnit runners on the platform), with the
  [`coverlet.MTP`](https://www.nuget.org/packages/coverlet.MTP) extension for coverage. A test
  project is any project with `<IsTestProject>true</IsTestProject>`.
- **For the coverage gate:** [`reportgenerator`](https://www.nuget.org/packages/dotnet-reportgenerator-globaltool)
  in the repository's local tool manifest, `.config/dotnet-tools.json`. It merges the coverage of
  every test project.
- **Optional, for per-test telemetry:** a TRX report, from
  `Microsoft.Testing.Extensions.TrxReport` or xUnit v3's built-in one. RemoteBuildTool finds out
  which one a project uses from its packages.
- **For CI:** every job of a run must see the same main and run store, so on hosted runners the
  stores need a shared filesystem, such as self-hosted runners on one machine or an NFS share.

## Install

As a local tool, pinned in the repository:

```
dotnet new tool-manifest
dotnet tool install Bitenovac.RemoteBuildTool
dotnet tool install dotnet-reportgenerator-globaltool
dotnet remotebuildtool --version
```

Or globally:

```
dotnet tool install --global Bitenovac.RemoteBuildTool
remotebuildtool --version
```

## Quick start

From the repository root, with nothing configured, the stores default to `artifacts/ci-main` and
`artifacts/ci-pr`:

```
dotnet remotebuildtool plan
dotnet remotebuildtool build Debug
dotnet remotebuildtool test Debug
dotnet remotebuildtool promote
```

The first run builds and tests everything and fills main. Run the four commands again without a
change and `plan` reports every project as a hit, `build` builds nothing, and `test` reuses every
result. Change one file and only that project and the projects above it are built and tested.

`remotebuildtool graph` prints the project graph with each project's hashes. Set
`REMOTEBUILDTOOL_CACHELESS=true` to ignore main and build everything.

## Project settings

Everything is optional. Set these in a project file or in `Directory.Build.props`:

| Property | Default | Meaning |
|---|---|---|
| `IsTestProject` | `false` | The project is a test project: `test` runs it. |
| `MinimumLineCoverage` | none | The line coverage, from 0 to 100, the project must keep. Without it, coverage is measured and reported but never fails the gate, and `plan` warns; set `0` to measure without a gate on purpose. |
| `MinimumBranchCoverage` | none | The same for branch coverage. |
| `ExcludeFromCoverage` | `false` | Leave the project out of the gate. Usually set on test projects. |
| `CacheTestResults` | `true` | Reuse the test project's result when it is a hit. Set `false` for a test project that must always run, such as one that depends on outside services. |
| `RemoteBuildToolSkip` | `false` | Leave the project out entirely: it is not restored, built, tested or released, and `plan` warns that it is skipped. A project that stays cannot reference it. |

Percentages are written with a period, such as `95.5`, on every machine.

To make something else part of every project's fingerprint, such as a schema version or a tool a
build step calls, declare it as an item:

```xml
<ItemGroup>
  <RemoteBuildToolHashInput Include="schema=3"/>
</ItemGroup>
```

What `release` does with a project depends on the usual MSBuild properties; see Releases
below.

## Commands

| Command | What it does |
|---|---|
| `plan` | Discovers, verifies, restores and hashes every project in Debug and Release, and writes the plan to the run's store. |
| `build <Debug\|Release>` | Restores the plan's hits from main and builds its misses, then stages them in the run's store. |
| `test <Debug\|Release>` | Runs or reuses each test project. On Debug, merges the coverage and gates the projects whose own inputs changed. |
| `promote` | Copies the run's entries into main and prunes what no entry uses any more. |
| `release` | After `plan`, builds every project but the test projects in Release under the next release version and drops what can be deployed. |
| `graph` | Prints the project graph with each project's hashes. |
| `--version`, `--help` | Print the version or the help. |

Every command exits with 0 on success and 1 on failure, and on GitHub Actions writes its
failures as annotations and a summary of the run.

## Environment variables

| Variable | Default | Meaning |
|---|---|---|
| `REMOTEBUILDTOOL_REPO_ROOT` | current directory | The repository root. |
| `REMOTEBUILDTOOL_MAIN_STORE` | `artifacts/ci-main` | main, the shared cache. |
| `REMOTEBUILDTOOL_PR_STORE` | `artifacts/ci-pr` | The run's own store, which holds its plan. |
| `REMOTEBUILDTOOL_DROP_ROOT` | `artifacts/official` | Where `release` drops each version. |
| `REMOTEBUILDTOOL_CACHELESS` | off | `true` or `1` to ignore main and build everything. |
| `REMOTEBUILDTOOL_COVERAGE_HTML` | on | `0` to skip the HTML coverage report. |
| `REMOTEBUILDTOOL_MAX_CPU` | every CPU | How many processes and MSBuild nodes a command runs at once. |
| `REMOTEBUILDTOOL_LOGS_DIR` | none | Where MSBuild binary logs and the build graph are kept. |
| `REMOTEBUILDTOOL_TELEMETRY_DIR` | none | Where every command writes its telemetry. |

These name the build in telemetry; on GitHub Actions they come from the run itself:

| Variable | Default |
|---|---|
| `REMOTEBUILDTOOL_BUILD_ID` | `<GITHUB_RUN_ID>-<GITHUB_RUN_ATTEMPT>`, or `local-<time>` |
| `REMOTEBUILDTOOL_QUEUE` | `GITHUB_WORKFLOW`, or `local` |
| `REMOTEBUILDTOOL_POOL` | `local` |
| `REMOTEBUILDTOOL_MACHINE` | the machine's name |
| `REMOTEBUILDTOOL_LOG_RUN` | none: the run's folder in the logs share, recorded with each build |

## Running it on GitHub Actions

Each configuration builds and tests in its own job, and every job sees the same stores. This
example uses self-hosted runners that share a disk, with main at `/srv/cache/main` and each run's
store under `/srv/cache/runs`:

```yaml
name: PR

on:
  pull_request:

env:
  REMOTEBUILDTOOL_MAIN_STORE: /srv/cache/main
  REMOTEBUILDTOOL_PR_STORE: /srv/cache/runs/${{ github.run_id }}-${{ github.run_attempt }}
  REMOTEBUILDTOOL_COVERAGE_HTML: "0"

jobs:
  plan:
    runs-on: self-hosted
    steps:
      - uses: actions/checkout@v7
      - run: dotnet tool restore
      - run: dotnet remotebuildtool plan

  debug:
    needs: plan
    runs-on: self-hosted
    steps:
      - uses: actions/checkout@v7
      - run: dotnet tool restore
      - run: dotnet remotebuildtool build Debug
      - run: dotnet remotebuildtool test Debug

  release:
    needs: plan
    runs-on: self-hosted
    steps:
      - uses: actions/checkout@v7
      - run: dotnet tool restore
      - run: dotnet remotebuildtool build Release
      - run: dotnet remotebuildtool test Release
```

Something then has to put accepted work into main. Either:

- **promote after the merge**: run the same pipeline on every push to the default branch and
  finish with `dotnet remotebuildtool promote`; or
- **run the service** (see The service below), which promotes what a pull request's own run
  already built as soon as GitHub reports the pull request merged, without building it again.

A pull request should never promote: main must only hold what was merged.

## Releases

After `plan`, `release` builds every project but the test projects in Release, in one MSBuild
run with the cache off, under the next version: the date and a counter, such as `2026.10.15.001`. It drops into
`REMOTEBUILDTOOL_DROP_ROOT/<version>`:

- `apps/<name>/`: every executable that is not a .NET tool and does not set
  `<IsPublishable>false</IsPublishable>`, published.
- `packages/`: every other project with `<IsPackable>true</IsPackable>`, packed, .NET tools
  included.
- `build-graph.json`: the plan the release was built from.

The version is only a default: a project that sets its own `<VersionPrefix>` keeps it. A release
that fails drops nothing and does not use up its number.

## The service

[Bitenovac.RemoteBuildTool.Service](https://www.nuget.org/packages/Bitenovac.RemoteBuildTool.Service)
is an optional service that runs beside the self-hosted runners. It promotes merged pull requests
into main, rebuilds main nightly, records every GitHub Actions run, and compacts telemetry for
DuckDB. It is a separate package because DuckDB makes it large; install it at the same version as
this one.

## Telemetry

With `REMOTEBUILDTOOL_TELEMETRY_DIR` set, every command writes its records as JSON lines, under
`<table>/date=<day>/`, to these tables:

| Table | One row per |
|---|---|
| `build` | build: result, queue, preparation and run time, totals |
| `stage` | command run: time per phase, CPU, memory, cache hits and misses |
| `target` | project built or tested: duration, cache outcome and why it missed, test counts |
| `test` | test case run: outcome, duration, error |
| `machine` | sample of the machine while a command runs |
| `cache_comparison` | project the nightly rebuild compared with the cache |

The service compacts each finished day into Parquet with DuckDB views and ready-made queries
over it; see its README.

Every row that names a build carries its `LogsPath`, its folder in the logs share, for what the
tables do not hold: console output and binary logs.

## Limitations

- .NET projects built with MSBuild only, one target framework each.
- The stores are directories, so the jobs of a run need a shared filesystem.

## License

MIT. Source and issues:
[github.com/DarkVader0/Bitenovac](https://github.com/DarkVader0/Bitenovac/tree/master/src/RemoteBuildTool/Bitenovac.RemoteBuildTool).
