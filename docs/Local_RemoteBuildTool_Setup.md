# Local RemoteBuildTool setup

Runs the GitHub pull request pipeline on your own PC, inside WSL.

What you end up with:

- 6 GitHub runners in Ubuntu (WSL), each job limited to 1 CPU and 2 GB RAM
- the pinned RemoteBuildTool release already installed in the runner image
- the **RemoteBuildTool service**, always running beside the runners: it fills the build cache from
  every new commit on `master`, records every build, compacts telemetry, prunes old logs and
  audits the cache. No workflow does any of that.

Ubuntu commands go in the **Ubuntu** terminal. Windows commands go in **PowerShell**.

---

## First time only: prepare Ubuntu

Skip this section if Ubuntu is already installed and set up.

**1. Install Ubuntu** (PowerShell)

```powershell
wsl --install -d Ubuntu
```

**2. Turn on systemd** (Ubuntu)

```bash
printf '[boot]\nsystemd=true\n' | sudo tee /etc/wsl.conf
```

Then restart WSL (PowerShell) and open Ubuntu again:

```powershell
wsl --shutdown
```

**3. If you have Docker Desktop**, go to Docker Desktop → Settings → Resources → WSL Integration
and **untick Ubuntu**. Setup installs its own Docker inside Ubuntu.

---

## Setup

**1. Get the code** (Ubuntu)

First time:

```bash
git clone https://github.com/DarkVader0/Bitenovac-Decompression.git ~/bitenovac
```

Every time after that:

```bash
cd ~/bitenovac
git switch master
git pull
```

**2. Get two tokens**

- **Runner token**: open https://github.com/DarkVader0/Bitenovac-Decompression/settings/actions/runners/new
  and copy the value after `--token`. It works for 1 hour and is needed only for setup.
- **Service token**: on GitHub, your profile Settings → Developer settings → Fine-grained tokens →
  Generate new token. Repository access: only this repository. Permissions: **Contents: Read** and
  **Actions: Read**. Pick a long expiry; when it runs out, run setup again with a new one. The
  service reads `master` and the finished workflow runs with it, for the build records' queue and
  preparation times.

**3. Run setup** (Ubuntu)

```bash
cd ~/bitenovac
sudo systemctl stop 'actions.runner.*' 2> /dev/null
sudo bash runner/setup-runner.sh --token <RUNNER TOKEN> --api-token <SERVICE TOKEN> --instances 6 --cpus 1 --memory 2g
```

Takes a few minutes. It must end with:

```
  ok    remotebuildtool-service is running; follow it with: sudo bash runner/service.sh logs
==> Ready
```

**4. Check it** (Ubuntu)

```bash
systemctl list-units 'actions.runner.*' --no-pager
sudo bash runner/service.sh logs
```

All six runners must be `active running`. On GitHub, Settings → Actions → Runners must show
`DESKTOP-...-1` to `DESKTOP-...-6` as **Idle**.

Within a minute the service log shows `Cache refresh of <commit> started`: it is building `master`
to fill the cache. Pull requests already work meanwhile; they are only faster once it has finished
(`Cache refresh of <commit>: success`).

**5. Done.** Open a pull request into `master` and it runs on your PC.

---

## Every day

- **The Ubuntu window must be running.** After a reboot, open Ubuntu once and the runners and the
  service start by themselves. While WSL is off, pull requests wait in "Queued".
- **Watch the runners work** (Ubuntu): `journalctl -u 'actions.runner.*' -f`
- **Watch the service** (Ubuntu): `sudo bash runner/service.sh logs`

---

## The RemoteBuildTool service

One container, `remotebuildtool-service`, restarted whenever it stops. Every 15 seconds it starts
whatever is due:

| Work | When | What |
|---|---|---|
| Cache refresh | a new commit on `master` | builds and tests Debug and Release, then promotes into main. The only way anything enters the cache, so only merged code ever does |
| Build records | every 2 minutes | a `build` telemetry row for every workflow run GitHub finished, with its queue and preparation time |
| Housekeeping | daily, 03:30 UTC | compacts the telemetry of earlier days and deletes logs past their retention |
| Cache audit | daily, 01:00 UTC | rebuilds `master` in Debug from scratch and compares it with the cache |

One build, refresh or audit, runs at a time. Its logs land in the logs share like any other's,
under `CacheRefresh_<commit>` or `CacheAudit_<commit>`, and its telemetry under queue
`Cache refresh` or `Cache audit`.

**Its settings come from two places:**

- `/etc/remotebuildtool/config.json` on this machine, written by setup: the repository, the service
  token, where the stores are, the pool. Readable by the runners' account only.
- `remotebuildtool.json` at the root of `master`, all optional: the branch to watch, how often to
  look, the daily times, and how long to keep each kind of log. A change takes effect once the
  service has refreshed the commit that makes it.

```json
{
  "branch": "master",
  "pollSeconds": 60,
  "buildReportSeconds": 120,
  "housekeeping": "03:30:00",
  "cacheAudit": "01:00:00",
  "cacheAuditConfiguration": "Debug",
  "logRetentionDays": { "PR_": 30, "CacheRefresh_": 30, "CacheAudit_": 30, "Official_": 365 }
}
```

`"cacheAudit": null` turns the audit off. Stop, start and follow it (Ubuntu):

```bash
sudo bash runner/service.sh stop
sudo bash runner/service.sh start
sudo bash runner/service.sh logs
```

---

## After releasing RemoteBuildTool, or changing the Dockerfile or `global.json`

Pull requests and the service use the RemoteBuildTool release **installed in the runner image**,
not the source in the pull request. To ship a change to RemoteBuildTool:

1. Bump `<VersionPrefix>` in `src/tools/Bitenovac.RemoteBuildTool/Bitenovac.RemoteBuildTool.csproj`
   and merge it. That is the only place the version lives: `runner/build-image.sh` reads it from
   there.
2. Wait for the next Official build, or run one: Actions → Official → Run workflow. It keeps the
   package's own version rather than the date.
3. Publish it from that drop: Actions → Deploy RemoteBuildTool → Run workflow. Leave the drop empty
   for the newest one. A version that is already on nuget.org fails the run, so a forgotten bump
   cannot overwrite anything.

nuget.org takes a few minutes to index a new package. Once it is listed, and after any Dockerfile or
`global.json` change merged into `master` (Ubuntu):

```bash
cd ~/bitenovac
git switch master
git pull
sudo bash runner/build-image.sh
```

It restarts the service on the new image. A new RemoteBuildTool release keeps the cache: only a
project's inputs decide whether it is rebuilt. When a release changes the fingerprint rules (the
release notes say so, and `FingerprintFormat.Version` in the source goes up), or after a
`global.json` or Dockerfile change, every project misses once; the service's next cache refresh
fills the cache again. Set `REMOTEBUILDTOOL_SOURCE` before `build-image.sh` to install from a feed
other than nuget.org.

### First time only: let the deploy workflow publish

The deploy workflow uses nuget.org Trusted Publishing: each run trades its GitHub identity for an
API key that expires within the hour, so no key is ever stored.

**1.** On nuget.org, your account → Trusted Publishing → add a policy:

| Field | Value |
|---|---|
| Repository owner | `DarkVader0` |
| Repository | `Bitenovac-Decompression` |
| Workflow file | `deploy-remotebuildtool.yml` |
| Environment | `nuget` |

**2.** On GitHub, Settings → Variables → Actions: add the variable `NUGET_USER` with your nuget.org
username (the profile name, not the email).

**3.** Optional: Settings → Environments → `nuget` → Required reviewers: add yourself, so every
publish waits for your approval. The environment is created on the first run if it does not exist.

To publish by hand on the runner host instead, with a key from nuget.org:

```bash
NUGET_API_KEY=<KEY> bash deploy/remotebuildtool.sh [2026.10.11.001]
```

---

## Releasing the libraries

The five libraries under `src/libraries` are published to nuget.org together, under one version:
`<VersionPrefix>` in the library group of `Directory.Build.props`. The Web app restores them from
nuget.org at the version pinned in `Directory.Packages.props`, not from the source.

1. Bump the shared `<VersionPrefix>` in `Directory.Build.props` and merge it.
2. Wait for the next Official build, or run one: Actions → Official → Run workflow. The packages
   keep the library version rather than the date.
3. Publish them from that drop: Actions → Deploy libraries → Run workflow. Leave the drop empty for
   the newest one. Packages nuget.org already has at that version are skipped, so a run that
   stopped part way can be run again; a run in which every package is already published fails, as
   the version was not bumped.
4. Once nuget.org lists the new version, move the Web app onto it by changing its two
   `PackageVersion` entries in `Directory.Packages.props`.

The first time only, add a second Trusted Publishing policy on nuget.org, like the one above but
with the workflow file `deploy-libraries.yml`.

To publish by hand on the runner host instead, with a key from nuget.org:

```bash
NUGET_API_KEY=<KEY> bash deploy/libraries.sh [2026.10.11.001]
```

---

## Publishing the Web app

The Web app is a static site on GitHub Pages, served from the root of its own domain. Every
Official drop holds it, published, under `apps/Bitenovac.DecompressionCalculator.Web`.

1. Wait for the next Official build, or run one: Actions → Official → Run workflow.
2. Publish it from that drop: Actions → Deploy web → Run workflow. Leave the drop empty for the
   newest one. Installed copies offer the new version with a Reload banner on their next visit.

`deploy/web.sh` copies the site out of the drop and adds what Pages needs: `.nojekyll`, so the
`_framework` folder is served, and a `404.html` copy of `index.html`, so a reloaded `/plan/...`
still opens the app.

### First time only: point Pages at the domain

**1.** At the domain's DNS provider, add a `CNAME` record from the app's subdomain to
`darkvader0.github.io`.

**2.** On GitHub, your profile Settings → Pages → Add a domain, and add the `TXT` record it gives
you. A verified domain cannot be taken over by another account's Pages site.

**3.** In this repository, Settings → Pages: set Source to **GitHub Actions**, enter the custom
domain, and once the DNS check passes, tick **Enforce HTTPS**. The app installs and works offline
only over HTTPS.

To prepare the site by hand on the runner host:

```bash
bash deploy/web.sh /tmp/site [2026.10.11.001]
```

---

## Fill the cache again

The service refills the cache by itself from every new commit on `master`. To fill it from this
checkout by hand instead, such as on a machine without the service (Ubuntu):

```bash
cd ~/bitenovac
git switch master
git pull
sudo bash runner/seed-cache.sh
```

It must end with `Promoted 42 entries into main.`

---

## Cache audit

Every night at 01:00 UTC the service checks that the cache can be trusted. It plans `master` with
the cache off, rebuilds every project in Debug from scratch, and compares each rebuild with main's
entry under the same fingerprint, file for file in `bin/`. They must be identical. A mismatch fails
the audit and names the project and files: its fingerprint misses an input, so a pull request could
have been handed stale output as a cache hit. The service log and the audit's build record show it:

```bash
sudo bash runner/service.sh logs
bash runner/telemetry.sh query "FROM build WHERE Queue = 'Cache audit' ORDER BY EndTime DESC LIMIT 5"
```

The audit's `audit-Debug.log` in its logs folder lists every project as `match`, `MISMATCH` or
`skip`. A project main holds under another fingerprint, as when `master` moved since the last cache
refresh, is skipped rather than compared.

---

## Official builds

Every 12 hours, at 00:00 and 12:00 UTC, the **Official** workflow builds every project in Release
from scratch, with no tests and no cache, and drops what can be deployed into the
`bitenovac-official` volume:

```
2026.10.07.001/
    packages/   one .nupkg per library and .NET tool
    apps/       one published folder per app
```

The number is the date (UTC) and a counter for that day, so a second build on the same day is
`2026.10.07.002`. A build that fails drops nothing and does not use up a number. Run one by hand
from GitHub, Actions → Official → Run workflow.

An executable with `<IsPublishable>false</IsPublishable>`, such as the benchmarks, is built but
left out of the drop.

To see the drops (Ubuntu):

```bash
sudo ls /var/lib/docker/volumes/bitenovac-official/_data
```

---

## Logs

Every PR, Official, cache refresh and cache audit keeps its logs in the `bitenovac-logs` volume, one
folder per pull request or commit and one folder per run attempt inside it:

```
PR_18/16473829-1/                                        kept 30 days
CacheRefresh_8f3c2a1b9d0e/cacherefresh-8f3c2a1b9d0e-...  kept 30 days
Official_2026.10.11.001/...                              kept 1 year
Official_failed_57/...                                   kept 1 year; a release that dropped nothing
```

Each attempt folder holds the full output of every stage (`plan.log`, `build-Debug.log`,
`test-Debug.log`, `release.log`, ...) and an MSBuild binary log per restore, build, publish and
pack (`build-Debug.binlog`, `publish-<app>.binlog`, ...). The service deletes what is past its
retention every night; `logRetentionDays` in `remotebuildtool.json` sets it.

To read them (Ubuntu):

```bash
ls /srv/bitenovac/logs/PR_18
cat /srv/bitenovac/logs/PR_18/16473829-1/test-Debug.log
```

On GitHub, with RemoteBuildTool 1.2.0 or later in the runner image:

- **Failures come first.** Compiler errors and failed stages are error annotations: they head the
  run's page and show inline on the pull request's changed lines. MSBuild's own output and every
  passing test project are folded into groups; a failing test project stays open.
- **Every stage is timed.** Each part ends with a line such as
  `<== Restoring 22 project(s) (1.9 s)`.
- **The run summary** holds the plan's cache hits per configuration, why each miss builds, each
  build's time, and a table of test projects with failures on top.

Open a `.binlog` in the [MSBuild Structured Log Viewer](https://msbuildlog.com) to see why a project
built, what every property evaluated to, and how long each target took. Binary logs need
RemoteBuildTool 0.0.4 or later in the runner image; console logs work with any.

### Resource usage

Every stage reports what it used, in its GitHub log and in its `.log` file. RemoteBuildTool writes
one line per MSBuild call and per test project:

```
resources: build-Debug used 63.2 s, CPU 58.1 s (0.92 cores), peak memory 1.30 GiB
resources: test Bitenovac.DecompressionAlgorithms.Core.Unit.Tests used 8.4 s, CPU 7.9 s (0.94 cores), peak memory 412 MiB
```

The container adds one line for the whole stage when it ends:

```
resources: container used 95.10 s, CPU 88.20 s (0.92 cores of 1.00), throttled 41.30 s, peak memory 1.62 GiB of 2.00 GiB (81 %), 0 out-of-memory kill(s)
```

- **peak memory** counts file cache, as the `--memory` limit does, so a peak near 100 % is not a
  failure by itself. An **out-of-memory kill** above 0 is: raise `--memory`.
- **throttled** is time the stage wanted more CPU than `--cpus` allows. A large share means it
  would run faster with more.
- The RemoteBuildTool lines need RemoteBuildTool 1.1.0 or later in the runner image. The container
  line comes from `runner/in-container.sh` and works with any.

Beside each `.log` is a `.resources.csv` with the container's memory and CPU every 5 seconds:

```bash
cat /srv/bitenovac/logs/PR_18/16473829-1/build-Debug.resources.csv
grep -h "resources:" /srv/bitenovac/logs/PR_18/16473829-1/*.log
```

---

## Telemetry

With RemoteBuildTool 1.2.0 or later in the runner image, every stage of every PR, Official, cache
refresh and cache audit records what it did, as JSON lines on the NFS share setup creates in Ubuntu
(`/srv/bitenovac`, mounted into jobs as the `bitenovac-telemetry` volume). The logs moved onto the
same share, so the logs path in every record opens from anywhere that mounts it.

| Table | One row per | Holds |
|---|---|---|
| `build` | build: one run attempt | queue, pool, result, queue/preparation/end-to-end time, CPU and memory use, cache hits, failed tests, first error |
| `stage` | RemoteBuildTool command in a job | time per phase (evaluation, restore, hashing, cache lookup and restore, build, test, coverage, publish, pack, store), CPU and memory against what the container was given, first error |
| `target` | project a stage built, restored, tested or deployed | cache hit or miss and why, time; for tests also the test kind and counts. A cache hit's time is its restore from main. A built project has no CPU time of its own: one MSBuild process builds them all |
| `test` | test case per run | outcome, time, error message |
| `machine` | sample every 30 s while a stage runs | host CPU and memory, the container's CPU and memory |

Files land in `<table>/date=<yyyy-MM-dd>/`, one per stage. Records are written when a stage ends,
so a job killed by its timeout records nothing.

### Querying

Every night at 03:30 UTC the service turns each finished day into Parquet under `parquet/<table>/`
and rebuilds `telemetry.duckdb`: a view per table over the Parquet and today's raw lines, so a
query always sees everything. The service also writes each GitHub build's `build` row a couple of
minutes after it finishes, and its own builds' rows as they end.

Query it with the DuckDB CLI in the runner image (Ubuntu):

```bash
cd ~/bitenovac
bash runner/telemetry.sh shell
bash runner/telemetry.sh query "FROM build_percentiles WHERE Queue = 'PR'"
```

Ready-made queries, from `queries.sql` in RemoteBuildTool:

| Query | Answers |
|---|---|
| `build_percentiles` | builds per queue and day: P50, P80 and P95 end-to-end time, P80 queue and preparation time |
| `stage_percentiles` | P80 of each stage and each of its phases, CPU and memory use |
| `cache_hit_rate` | the share of projects build stages restored instead of building, per day |
| `cache_miss_reasons` | why projects missed the cache in the last 7 days, most frequent first |
| `slowest_targets` | the slowest projects and test projects of the last 7 days |
| `flaky_tests` | test cases that both passed and failed on the same commit in the last 30 days |
| `failing_tests` | test cases that failed, per day |
| `machine_load` | host CPU and memory per machine and hour |
| `build_stages('<build id>')` | one build's stages in order, with their logs path |
| `build_targets('<build id>')` | what each stage of one build did with each project |

A GitHub build's id is the run id and attempt, as in `16473829-1`; the run's URL ends in the run id.
The service's own builds are `cacherefresh-<commit>-<time>` and `cacheaudit-<commit>-<time>`. To drill
from a slow day into its slowest PR build:

```sql
FROM build WHERE Queue = 'PR' AND CAST(EndTime AS DATE) = DATE '2026-10-14' ORDER BY E2ETimeMS DESC LIMIT 1;
FROM build_stages('16473829-1');
```

Its `LogsPath` is the build's folder under `/srv/bitenovac/logs`.

`schema.sql` in RemoteBuildTool types every column. A field the records gain later is left out of
the views until a release adds it there.

Setup moves the logs of an earlier setup onto the share. Stop the runners first if setup says the
logs volume is in use: `sudo systemctl stop 'actions.runner.*'`.

---

## Remove everything

**1.** On GitHub, Settings → Actions → Runners: remove every runner.

**2.** Ubuntu:

```bash
cd ~/bitenovac
sudo bash runner/setup-runner.sh --uninstall --purge --token unused
docker volume rm bitenovac-official
docker image rm bitenovac-remotebuildtool-runner:10.0.400
sudo rm /etc/exports.d/bitenovac.exports && sudo exportfs -ra
sudo rm -rf /srv/bitenovac
```

`--uninstall` stops the service as well. `--purge` keeps the Official drops and the telemetry; the
`docker volume rm` deletes the drops and the last line the telemetry. Use the SDK version from
`global.json` in the image name.

**3.** Check that nothing is left:

```bash
systemctl list-units 'actions.runner.*' --no-pager   # 0 loaded units
docker volume ls --filter 'name=bitenovac-'          # empty
docker image ls | grep bitenovac                     # nothing
```

To set it up again, start from [Setup](#setup).

---
