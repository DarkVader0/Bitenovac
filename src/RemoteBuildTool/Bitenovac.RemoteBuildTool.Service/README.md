# RemoteBuildTool service

The optional service for [Bitenovac.RemoteBuildTool](https://www.nuget.org/packages/Bitenovac.RemoteBuildTool),
a content-hashed build cache for .NET repositories on CI.

RemoteBuildTool on its own needs something to put accepted work into main, its shared cache. The
service does that and more, without any workflow of its own:

- it promotes what a merged pull request's run already built into main, without building it again;
- it rebuilds main from scratch every night and records how every project compared with the cache;
- it records every GitHub Actions run as telemetry;
- it compacts telemetry into Parquet and keeps a DuckDB database of views and ready-made queries.

The service is a separate package because DuckDB's native libraries make it large; pull request
runs only need Bitenovac.RemoteBuildTool.

## Install

```
dotnet tool install --global Bitenovac.RemoteBuildTool.Service
remotebuildtool-service --version
```

Install the same version as Bitenovac.RemoteBuildTool: every fingerprint includes the version, so a
service on another version would fill main with entries no pull request looks up.

The service includes every RemoteBuildTool command, as `remotebuildtool-service plan`,
`remotebuildtool-service build Debug` and so on; its nightly rebuild runs them that way.

## Commands

| Command | What it does |
|---|---|
| `serve` | Runs the service until it is stopped with SIGTERM or Ctrl+C. |
| `wake` | Asks the running service to promote merged pull requests now. |
| any other | The RemoteBuildTool command of that name; see `remotebuildtool-service --help`. |

## What it does

`remotebuildtool-service serve` runs on a Linux machine beside the self-hosted runners. Each piece
of its work runs on its own schedule:

| Work | When | What |
|---|---|---|
| Promotion | at once when woken, otherwise every 15 minutes | For each pull request merged into the branch, promotes what its successful run staged: the merge queue's run on the merge commit, otherwise the pull request's own run. Deletes staged runs older than a day. |
| Build records | every 2 minutes | Writes a `build` telemetry row for every workflow run GitHub finished, with its queue and preparation time. |
| Housekeeping | daily, 03:30 UTC | Compacts earlier days' telemetry and deletes logs past their retention. |
| Rebuild | daily, 01:00 UTC, and at once when main is empty | Builds and tests the branch from scratch, then switches main to it and records how every project compared with the cache. A step that runs past its limit is stopped. |

For promotion, every pull request run must stage its store at
`<stagingRoot>/<GITHUB_RUN_ID>`, shared by every attempt of the run, that is `REMOTEBUILDTOOL_PR_STORE` under the
staging directory the service reads. To promote within seconds of a merge, run
`remotebuildtool-service wake` on the service's machine from a workflow triggered by
`pull_request: types: [closed]`:

```yaml
on:
  pull_request:
    types: [closed]

jobs:
  wake:
    if: github.event.pull_request.merged
    runs-on: self-hosted
    steps:
      - run: remotebuildtool-service wake
```

## Host settings

In `/etc/remotebuildtool/config.json`, or the file `REMOTEBUILDTOOL_CONFIG` names:

| Setting | Default | Meaning |
|---|---|---|
| `repository` | required | The repository, as `owner/name`. |
| `gitHubToken` | required | A fine-grained token with read access to the repository's Contents, Pull requests and Actions. |
| `gitHubApiUrl` | `https://api.github.com` | The API, for GitHub Enterprise Server. |
| `cloneUrl` | the repository on GitHub | Where the rebuild clones from. |
| `pool` | `service` | The pool the service's builds are recorded under. |
| `machine` | the machine's name | The machine they are recorded under. |
| `workRoot` | `/work` | The service's checkouts, state and wake socket. |
| `mainStore` | `/mnt/main` | main. |
| `stagingRoot` | `/mnt/staging` | Where pull request runs stage their stores. |
| `logsRoot` | `/mnt/logs` | The logs share. |
| `telemetryRoot` | `/mnt/telemetry` | The telemetry share. |
| `maxCpu` | every CPU | `REMOTEBUILDTOOL_MAX_CPU` for the service's builds. |

## Repository settings

In `remotebuildtool.json` at the root of the default branch, all optional and read again as they
change:

```json
{
  "branch": "main",
  "pollSeconds": 900,
  "buildReportSeconds": 120,
  "housekeeping": "03:30:00",
  "rebuild": "01:00:00",
  "rebuildStepMinutes": 20,
  "logRetentionDays": { "PR_": 30, "Rebuild_": 30, "Official_": 365 }
}
```

| Setting | Default | Meaning |
|---|---|---|
| `branch` | the repository's default branch | The branch whose merges fill main and which the rebuild builds. |
| `pollSeconds` | `900` | How often to look for merges besides each wake. At least 10. |
| `buildReportSeconds` | `120` | How often to record finished runs. At least 10. |
| `housekeeping` | `03:30:00` | When housekeeping runs, in UTC. |
| `rebuild` | `01:00:00` | When the rebuild runs, in UTC; `null` for never. An empty main is rebuilt regardless. |
| `rebuildStepMinutes` | `20` | How long each step of the rebuild may run. At least 1. |
| `logRetentionDays` | as above | How many days to keep each kind of log, by the prefix of its folder. |

## Telemetry

Every RemoteBuildTool command writes JSON lines to the telemetry directory; see the
Bitenovac.RemoteBuildTool README for the tables. The service compacts each finished day into
Parquet and keeps `telemetry.duckdb` in the telemetry directory: a view per table over the Parquet
and today's raw lines, and ready-made queries such as `build_percentiles`, `stage_percentiles`,
`cache_hit_rate`, `cache_miss_reasons`, `slowest_targets`, `flaky_tests`, `failing_tests`,
`machine_load` and `cache_mismatches`. Open it with [DuckDB](https://duckdb.org):

```sql
FROM build_percentiles WHERE Queue = 'PR';
FROM build_stages('16473829-1');
```

Every row that names a build carries its `LogsPath`, its folder in the logs share, for what the
tables do not hold: console output and binary logs.

## Limitations

- Linux only, and GitHub only: it reads pull requests and workflow runs from the GitHub API.
- Every machine and job must see the same main, staging, logs and telemetry directories.

## License

MIT. Source and issues:
[github.com/DarkVader0/Bitenovac](https://github.com/DarkVader0/Bitenovac/tree/master/src/RemoteBuildTool/Bitenovac.RemoteBuildTool.Service).
