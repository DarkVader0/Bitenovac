# Local RemoteBuildTool setup

Runs the GitHub pull request pipeline on your own PC, inside WSL.

What you end up with:

- 3 GitHub runners in Ubuntu (WSL), each job limited to 3 CPUs and 2 GB RAM
- the pinned RemoteBuildTool release already installed in the runner image
- the build cache filled from `master`, and kept filled by every merged pull request
- the **RemoteBuildTool service**, always running beside the runners: it promotes what merged pull
  requests built into the cache, rebuilds the cache from scratch every night, records every build,
  compacts telemetry and prunes old logs. No workflow does any of that.

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

**1. Download the installer** (Ubuntu)

The installer is the newest release, `bitenovac-runner-<drop>` on the GitHub Releases page. It holds
everything setup needs, so no repository checkout is required. The download uses `gh`; without it,
download the two files from the release page instead and continue from `sha256sum`.

```bash
mkdir -p ~/bitenovac-runner && cd ~/bitenovac-runner
gh release download --repo DarkVader0/Bitenovac --pattern 'bitenovac-runner-*'
sha256sum -c bitenovac-runner-*.tar.gz.sha256
tar -xzf bitenovac-runner-*.tar.gz
```

Every command below that names `runner/` runs from this folder, `~/bitenovac-runner`.

**2. Get two tokens**

- **Runner token**: open https://github.com/DarkVader0/Bitenovac/settings/actions/runners/new
  and copy the value after `--token`. It works for 1 hour and is needed only for setup.
- **Service token**: on GitHub, your profile Settings → Developer settings → Fine-grained tokens →
  Generate new token. Repository access: only this repository. Permissions: **Contents: Read**,
  **Pull requests: Read** and **Actions: Read**. Pick a long expiry; when it runs out, run setup
  again with a new one. The service reads `master`, the pull requests merged into it, and the
  finished workflow runs with it, for the build records' queue and preparation times.

**3. Run setup** (Ubuntu)

```bash
cd ~/bitenovac-runner
sudo bash runner/setup-runner.sh --token <RUNNER TOKEN> --repo https://github.com/DarkVader0/Bitenovac --api-token <SERVICE TOKEN> --instances 3 --cpus 3 --memory 2g
```

Setup writes `/etc/bitenovac/runner.env` the first time it runs, with the machine's settings: the
install folder, runner account, labels, repository, the number of agents (`INSTANCES`), each job's
CPUs (`CPUS`) and memory (`MEMORY`). Later runs read it, so the flags are only needed to change
something. A flag given on the command line wins for that run and is not saved.

On this PC the file's values are three agents with 3 CPUs and 2 GB each, the values in the command
above.

Takes a few minutes. It must end with:

```
  ok    remotebuildtool-service is running; follow it with: sudo bash runner/service.sh logs
==> Ready
```

The service then finds the cache empty and fills it straight away: it builds and tests `master`
from scratch, which takes as long as a pull request that changes everything. Its log ends that with
`rebuild of <commit>: success`. Pull requests run meanwhile; they just build more.

**4. Check it** (Ubuntu)

```bash
systemctl list-units 'actions.runner.*' --no-pager
sudo bash runner/service.sh logs
```

All three runners must be `active running`. On GitHub, Settings → Actions → Runners must show
`DESKTOP-...-1` to `DESKTOP-...-3` as **Idle**.

**5. Done.** Open a pull request into `master` and it runs on your PC. Once it is merged, the
service log shows `PR #<number> merged; run <id>: Promoted <n> entries into main.`

**Recommended: turn on the merge queue.** On GitHub, Settings → Rules → Rulesets → New branch
ruleset for `master`: tick **Require merge queue** and **Require status checks to pass**, with the
check `PR`. Each queued pull request is then built on top of `master` and everything queued ahead
of it, which is exactly what `master` holds once it merges, so what it built is promoted as is and
`master` is always fully cached. Without the queue it still works: projects that changed on
`master` after a pull request's build simply build in the next pull request that needs them.

---

## Every day

- **The Ubuntu window must be running.** After a reboot, open Ubuntu once and the runners and the
  service start by themselves. While WSL is off, pull requests wait in "Queued".
- **Watch the runners work** (Ubuntu): `journalctl -u 'actions.runner.*' -f`
- **Watch the service** (Ubuntu): `sudo bash runner/service.sh logs`

---

## The RemoteBuildTool service

One container, `remotebuildtool-service`, restarted whenever it stops. Each piece of its work is a
.NET hosted service with its own timer, so a long rebuild never holds up a promotion:

| Work | When | What |
|---|---|---|
| Promotion | on each merge, and every 15 minutes | for each pull request merged into `master`, promotes what its build staged into main: the merge queue's build of the merge commit, otherwise the pull request's own last successful build. Then deletes staged builds older than 24 hours |
| Build records | every 2 minutes | a `build` telemetry row for every workflow run GitHub finished, with its queue and preparation time |
| Housekeeping | daily, 03:30 UTC | compacts the telemetry of earlier days and deletes logs past their retention |
| Rebuild | daily, 01:00 UTC, and at once when main is empty | builds and tests `master` in Debug and Release from scratch and switches main to it; see [Nightly rebuild](#nightly-rebuild) |

A failure is logged and the work runs again on its next turn; it never stops the service.

**How a merge reaches it.** Each pull request merged into `master` starts the
`GitHub webhook on PR merge` workflow on this machine's runner. Its one step runs
`remotebuildtool-service wake` inside the service's container, which connects to the socket
`/work/wake.sock` the service listens on, and promotion takes its turn at once. Nothing on this
machine is open to the internet: GitHub hands the job to the runner over the connection the runner
already keeps. The 15-minute poll only catches a merge whose workflow did not run, such as one made
while the service was stopped.

**How the cache fills.** Every PR and merge queue run keeps its artifact store in the
`bitenovac-staging` volume (on the share at `/srv/bitenovac/staging`), one folder per run attempt.
Nothing in it reaches main until GitHub reports the pull request merged; then promotion copies only
the projects the run built, so it costs as much as the change, not the repository. A pull request
merged more than 24 hours after its build has nothing left to promote; its projects build again in
the next pull request that needs them, and that one fills main once merged. Besides that, only the
nightly rebuild writes main.

**Its settings come from two places:**

- `/etc/remotebuildtool/config.json` on this machine, written by setup: the repository, the service
  token, where the stores are, the pool. Readable by the runners' account only.
- `remotebuildtool.json` at the root of the default branch, all optional: the branch whose merges
  fill main, how often to look besides each merge, the daily times, and how long to keep each kind
  of log. The service reads it at most once per poll.

```json
{
  "branch": "master",
  "pollSeconds": 900,
  "buildReportSeconds": 120,
  "housekeeping": "03:30:00",
  "rebuild": "01:00:00",
  "rebuildStepMinutes": 20,
  "logRetentionDays": { "PR_": 30, "Rebuild_": 30, "Official_": 365 }
}
```

`"rebuild": null` turns the nightly rebuild off; an empty main is still rebuilt. `rebuildStepMinutes`
is how long each step of a rebuild may run; see [Nightly rebuild](#nightly-rebuild). Stop, start and
follow it, or empty main so it is rebuilt now (Ubuntu):

```bash
sudo bash runner/service.sh stop
sudo bash runner/service.sh start
sudo bash runner/service.sh logs
sudo bash runner/service.sh rebuild
```

---

## After releasing RemoteBuildTool, or changing the Dockerfile or `global.json`

Pull requests and the service use the RemoteBuildTool release **installed in the runner image**,
not the source in the pull request. To ship a change to RemoteBuildTool:

1. Bump `<VersionPrefix>` in `src/RemoteBuildTool/Directory.Build.props` and merge it. That is the
   only place the version lives, for both packages: `runner/build-image.sh` reads it from there.
2. Wait for the next Official build, or run one: Actions → Official → Run workflow. It keeps the
   package's own version rather than the date.
3. Publish it from that drop: Actions → Deploy RemoteBuildTool → Run workflow. Leave the drop empty
   for the newest one. A version that is already on nuget.org fails the run, so a forgotten bump
   cannot overwrite anything.

nuget.org takes a few minutes to index a new package. Once it is listed, and after any Dockerfile or
`global.json` change that has been published in a release (Ubuntu):

```bash
cd ~/bitenovac-runner
sudo bash runner/build-image.sh
```

It restarts the service on the new image. Every fingerprint includes the RemoteBuildTool version,
so a new release starts from an empty cache rather than trusting what an older one built; so does
a `global.json` or Dockerfile change. Until the nightly rebuild, pull requests build everything they
touch. To refill the cache now instead (Ubuntu):

```bash
sudo bash runner/service.sh rebuild
```

Set `REMOTEBUILDTOOL_SOURCE` before `build-image.sh` to install from a feed other than nuget.org.

### First time only: let the deploy workflow publish

The deploy workflow uses nuget.org Trusted Publishing: each run trades its GitHub identity for an
API key that expires within the hour, so no key is ever stored.

**1.** On nuget.org, your account → Trusted Publishing → add a policy:

| Field | Value |
|---|---|
| Repository owner | `DarkVader0` |
| Repository | `Bitenovac` |
| Workflow file | `deploy_RemoteBuildTool.yml` |
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

The libraries of `src/DecompressionAlgorithms` and of `src/DecompressionCalculator` are published to
nuget.org one product at a time, each under its own version: `<VersionPrefix>` in the product
folder's `Directory.Build.props`. While the Web app is being developed it references the two
calculator libraries as projects, so a change to them reaches the app without a release; the pinned
versions in `Directory.Packages.props` wait for the switch back to packages.

1. Bump the `<VersionPrefix>` of the product you changed and merge it.
2. Wait for the next Official build, or run one: Actions → Official → Run workflow. The packages
   keep the library version rather than the date.
3. Publish them from that drop: Actions → Deploy DecompressionAlgorithmLibraries, or Deploy
   DecompressionCalculatorLibraries → Run workflow. Leave the drop empty for the newest one.
   Packages nuget.org already has at that version are skipped, so a run that stopped part way can
   be run again; a run in which every package is already published fails, as the version was not
   bumped.
4. To put the Web app back on packages, replace its two project references with package references
   and set the two `PackageVersion` entries in `Directory.Packages.props` to the version nuget.org now
   lists.

The first time only, add a Trusted Publishing policy on nuget.org for each of the two workflows,
like the one above but with the workflow file `deploy_DecompressionAlgorithmLibraries.yml` and
`deploy_DecompressionCalculatorLibraries.yml`.

To publish by hand on the runner host instead, with a key from nuget.org:

```bash
NUGET_API_KEY=<KEY> bash src/DecompressionAlgorithms/deploy_libraries.sh [2026.10.11.001]
```

---

## Publishing the runner bundle

The runner setup is published on GitHub Releases as `bitenovac-runner-<drop>`, where `<drop>` is the
Official drop it was built in, the same way the Web app is versioned. The release holds the bundle
archive and its `.sha256`. Anyone can download it, so it holds only the setup scripts, the image's
build files and the two pins. No source code, tests, workflows or secrets.

Every Official drop carries the bundle in its `runner/` folder, so it is made from the same commit as
the drop.

1. Wait for the Official run that made the drop, or run one: Actions → Official → Run workflow.
   Its run page shows the drop version.
2. Actions → Deploy RemoteBuildTool → Run workflow. Leave the drop empty for the newest one. Besides
   the NuGet packages, the run publishes the bundle as the release `bitenovac-runner-<drop>`. A drop
   that already has a release fails that part of the run.

A new machine gets the installer from the release, the same way as every other machine: see
[Setup](#setup). Nothing on it needs a checkout of the repository.

---

## Publishing the Web app

The Web app is a static site on GitHub Pages, served from the root of its own domain. Every
Official drop holds it, published, under `apps/Bitenovac.DecompressionCalculator.Web`.

1. Wait for the next Official build, or run one: Actions → Official → Run workflow.
2. Publish it from that drop: Actions → Deploy web → Run workflow. Leave the drop empty for the
   newest one. Installed copies offer the new version with a Reload banner on their next visit.

`src/DecompressionCalculator/deploy_web.sh` copies the site out of the drop and adds what Pages needs: `.nojekyll`, so the
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
bash src/DecompressionCalculator/deploy_web.sh /tmp/site [2026.10.11.001]
```

---

## Nightly rebuild

Every night at 01:00 UTC, and as soon as the service starts with main empty, the service builds
`master` from scratch: plan with the cache off, then build and test Debug and Release. When every
step passes, main is switched to it:

1. Each project's rebuilt output replaces main's entry for it.
2. Every other entry main held, for projects that are gone or older fingerprints, is deleted, and
   so is every file no entry uses any more.
3. Pull requests merged while the rebuild ran are promoted again, so the switch never takes their
   output back out.

A rebuild that fails, such as on a failing test on `master`, leaves main as it was. So does one
whose step runs past `rebuildStepMinutes` (20 by default): the step is stopped with every process it
started, the rebuild is recorded as `timed_out`, and `service.log` names the step. The service
carries on, and the next night's rebuild runs as usual. Its logs are in
the logs share under `Rebuild_<commit>` (`tool-restore.log`, `plan.log`, `build-Debug.log`, ...,
`switch.log`), its telemetry under queue `Rebuild`.

Before each entry is replaced, the switch compares it with the rebuild, file for file in `bin/`,
and records a row per project in the `cache_comparison` telemetry table:

| Outcome | Main held | Means |
|---|---|---|
| `match` | the same fingerprint, the same output | the cache was right |
| `mismatch` | the same fingerprint, other output | the fingerprint misses an input that changes the output; `DifferingFiles` names the files |
| `changed` | another fingerprint | the project changed since main got it; nothing to compare |
| `new` | nothing | nothing to compare |

A mismatch does not fail the rebuild: main is switched to the fresh output either way. To find
them (Ubuntu):

```bash
bash runner/telemetry.sh query "FROM cache_mismatches"
bash runner/telemetry.sh query "FROM cache_comparison_outcomes"
```

---

## Official builds

Every 12 hours, at 00:00 and 12:00 UTC, the **Official** workflow builds every project but the test projects in
Release from scratch, with no cache, and drops what can be deployed into the
`bitenovac-official` volume:

```
2026.10.07.001/
    packages/          one .nupkg per library and .NET tool
    apps/              one published folder per app
    build-graph.json   the build graph, as beside every build's logs, with the run's logs path
```

The number is the date (UTC) and a counter for that day, so a second build on the same day is
`2026.10.07.002`. A build that fails drops nothing and does not use up a number. Run one by hand
from GitHub, Actions → Official → Run workflow.

On GitHub, the drop's version is on the run's page: the notice **Official drop** at the top, and the
line **Official drop** in the run's summary. Each drop also gets its runner bundle in `runner/`.

An executable with `<IsPublishable>false</IsPublishable>`, such as the benchmarks, is built but
left out of the drop.

To see the drops (Ubuntu):

```bash
sudo ls /var/lib/docker/volumes/bitenovac-official/_data
```

---

## Logs

Every PR, merge queue run, Official and nightly rebuild keeps its logs in the `bitenovac-logs`
volume, one folder per pull request, run or commit and one folder per run attempt inside it:

```
PR_18/16473829-1/                                    kept 30 days
Rebuild_8f3c2a1b9d0e/rebuild-8f3c2a1b9d0e-...        kept 30 days
Official_212/16480001-1/                             kept 1 year
```

An Official run's folder is named by its run number; the drop version it made is in its
`release.log`, in its `build` and `stage` telemetry (`DropVersion`), and the drop's
`build-graph.json` names the logs folder back.

Each attempt folder holds the full output of every stage (`plan.log`, `build-Debug.log`,
`test-Debug.log`, `release.log`, ...) and an MSBuild binary log per restore, build, publish and
pack (`build-Debug.binlog`, `publish-<app>.binlog`, ...). The service deletes what is past its
retention every night; `logRetentionDays` in `remotebuildtool.json` sets it.

Beside them is `build-graph.json`, the build graph as `plan` saw it: every project of each
configuration with its references, its fingerprints, whether it was a cache hit and why not, and
every input of its fingerprint. Official drops carry one too. It is plain JSON, so DuckDB in the
runner image reads it directly; for example, every project of one build with what it references
(Ubuntu):

```bash
docker run --rm -v /srv/bitenovac/logs:/logs:ro --entrypoint duckdb bitenovac-remotebuildtool-runner:10.0.402 \
    -c "SELECT unnest(Configurations.Debug, recursive := true) FROM read_json('/logs/PR_18/16473829-1/build-graph.json')"
```

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

With RemoteBuildTool 1.2.0 or later in the runner image, every stage of every PR, merge queue run,
Official and nightly rebuild records what it did, as JSON lines on the NFS share setup creates in Ubuntu
(`/srv/bitenovac`, mounted into jobs as the `bitenovac-telemetry` volume). The logs moved onto the
same share, so the logs path in every record opens from anywhere that mounts it.

| Table | One row per | Holds |
|---|---|---|
| `build` | build: one run attempt | queue, pool, result, queue/preparation/end-to-end time, CPU and memory use, cache hits, failed tests, first error |
| `stage` | RemoteBuildTool command in a job | time per phase (evaluation, restore, hashing, cache lookup and restore, build, test, coverage, publish, pack, store), CPU and memory against what the container was given, first error |
| `target` | project a stage built, restored, tested or deployed | cache hit or miss and why, time; for tests also the test kind and counts. A cache hit's time is its restore from main. A built project has no CPU time of its own: one MSBuild process builds them all |
| `test` | test case per run | outcome, time, error message |
| `machine` | sample every 30 s while a stage runs | host CPU and memory, the container's CPU and memory |
| `cache_comparison` | project the nightly rebuild switched main to | the outcome against what main held (`match`, `mismatch`, `changed`, `new`), both fingerprints, the files that differed |

Files land in `<table>/date=<yyyy-MM-dd>/`, one per stage. Records are written when a stage ends,
so a job killed by its timeout records nothing.

### Querying

Every night at 03:30 UTC the service turns each finished day into Parquet under `parquet/<table>/`
and rebuilds `telemetry.duckdb`: a view per table over the Parquet and today's raw lines, so a
query always sees everything. The service also writes each GitHub build's `build` row a couple of
minutes after it finishes, and its own builds' rows as they end.

Query it with the DuckDB CLI in the runner image (Ubuntu):

```bash
cd ~/bitenovac-runner
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
| `cache_mismatches` | projects whose rebuild differed from main under the same fingerprint, newest first |
| `cache_comparison_outcomes` | each rebuild's matches, mismatches, changed and new projects per configuration |

A GitHub build's id is the run id and attempt, as in `16473829-1`; the run's URL ends in the run id.
The service's rebuilds are `rebuild-<commit>-<time>`. To drill
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

**1.** Create a runner token (step 2 of Setup). Ubuntu:

```bash
cd ~/bitenovac-runner
sudo bash runner/setup-runner.sh --uninstall --token <RUNNER TOKEN>
```

This stops the service and every agent, deregisters the agents with GitHub, and deletes the agents'
folders. The cache, the logs, the telemetry, the Official drops and the settings are kept.

**2.** To delete everything this setup made, run the same command with `--purge`, then the rest by
hand (Ubuntu):

```bash
cd ~/bitenovac-runner
sudo bash runner/setup-runner.sh --uninstall --purge --token <RUNNER TOKEN>
docker volume rm bitenovac-official
docker image rm bitenovac-remotebuildtool-runner:<SDK version from global.json>
sudo rm -f /etc/bitenovac/runner.env
sudo rm /etc/exports.d/bitenovac.exports && sudo exportfs -ra
sudo rm -rf /srv/bitenovac
```

`--purge` deletes the cache, main, the logs and the service's settings. The Official drops and the
telemetry are kept by `--purge` itself, so the `docker volume rm` and `rm -rf` lines are what delete
them.

**3.** Check that nothing is left:

```bash
systemctl list-units 'actions.runner.*' --no-pager   # 0 loaded units
docker volume ls --filter 'name=bitenovac-'          # empty
docker image ls | grep bitenovac                     # nothing
```

To set it up again, start from [Setup](#setup).

---

## Updating a set-up PC

Setup can run again over an installed PC. It keeps the cache, the logs, the telemetry, the Official
drops and the NFS share, and it keeps the service's settings unless you pass `--api-token`.

1. In GitHub, Settings → Actions → Runners: every runner must be **Idle**. A running job stops when
   its agent is replaced.
2. Download the release into a new folder, from [Setup step 1](#setup). The newest release is the
   default; to pick one, give its tag, for example
   `gh release download bitenovac-runner-2026.10.28.001 --repo DarkVader0/Bitenovac --pattern 'bitenovac-runner-*'`.
3. Get a new runner token, then run setup from that folder (Ubuntu). The agent count and limits
   come from `/etc/bitenovac/runner.env`:

   ```bash
   cd ~/bitenovac-runner
   sudo bash runner/setup-runner.sh --token <RUNNER TOKEN>
   ```

   Add `--api-token <SERVICE TOKEN>` only when the service's token has changed. Without it, the
   service keeps its current settings and runs on the new image.

Setup replaces the agents, rebuilds the runner image and restarts the service on it. Check the
result:

```bash
systemctl list-units 'actions.runner.*' --no-pager
sudo bash runner/service.sh logs
```

**Going back.** There is no automatic rollback. Download an older release by its tag (step 2) and
run step 3 from its folder.

---

## Test machines

**A test machine** that must not serve the NFS share or take production jobs: before the first run,
write `/etc/bitenovac/runner.env` with

```
MANAGE_SHARE=false
LABELS=self-hosted,linux,x64,bitenovac-test
RUNNER_NAME_PREFIX=<a name for this machine>
REPOSITORY=https://github.com/DarkVader0/Bitenovac
```

Its volumes stay local, and its runners take only jobs labelled `bitenovac-test`.

---
