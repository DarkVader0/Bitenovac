# Local RemoteBuildTool setup

Runs the GitHub pull request pipeline on your own PC, inside WSL.

What you end up with:

- 2 GitHub runners in Ubuntu (WSL), each job limited to 6 CPUs and 6 GB RAM
- the pinned RemoteBuildTool release already installed in the runner image
- the build cache already filled from `master`

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

**2. Get a token**

Open https://github.com/DarkVader0/Bitenovac-Decompression/settings/actions/runners/new
and copy the value after `--token`.

- It works for 1 hour.

**3. Run setup** (Ubuntu)

```bash
cd ~/bitenovac
sudo bash runner/setup-runner.sh --token <TOKEN> --instances 2 --cpus 6 --memory 6g
```

Takes a few minutes. It must end with:

```
Promoted 42 entries into main.
  ok    main is warm
==> Ready
```

**4. Check it** (Ubuntu)

```bash
systemctl list-units 'actions.runner.*' --no-pager
```

Both runners must be `active running`.

On GitHub, Settings → Actions → Runners must show `DESKTOP-...-1` and `DESKTOP-...-2` as **Idle**.

**5. Done.** Open a pull request into `master` and it runs on your PC.

---

## Every day

- **The Ubuntu window must be running.** After a reboot, open Ubuntu once and the runners start
  by themselves. While WSL is off, pull requests wait in "Queued".
- **Watch the runners work** (Ubuntu): `journalctl -u 'actions.runner.*' -f`

---

## After releasing RemoteBuildTool, or changing the Dockerfile or `global.json`

Pull requests use the RemoteBuildTool release **installed in the runner image**, not the source in
the pull request. To ship a change to RemoteBuildTool:

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
bash runner/build-image.sh
sudo bash runner/seed-cache.sh
```

`seed-cache.sh` is needed because a new RemoteBuildTool release empties the cache: every project
would build from zero on the next pull request. Set `REMOTEBUILDTOOL_SOURCE` before `build-image.sh`
to install from a feed other than nuget.org.

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

Every hour the **Cache refresh** workflow fills the cache by itself: it builds, tests and
promotes what changed. Run this only when pull requests are slower than they should be (Ubuntu):

```bash
cd ~/bitenovac
git switch master
git pull
sudo bash runner/seed-cache.sh
```

It must end with `Promoted 42 entries into main.`

---

## Official builds

Every day at 01:00 UTC the **Official** workflow builds every project in Release from scratch, with
no tests and no cache, and drops what can be deployed into the `bitenovac-official` volume:

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

Every PR, Cache refresh and Official run keeps its logs in the `bitenovac-logs` volume, one folder
per pull request or build and one folder per run attempt inside it:

```
PR_18/16473829-1/               kept 30 days
CacheRefresh_412/16473901-1/    kept 30 days
Official_2026.10.11.001/...     kept 1 year
Official_failed_57/...          kept 1 year; a release that dropped nothing
```

Each attempt folder holds the full output of every stage (`plan.log`, `build-Debug.log`,
`test-Debug.log`, `release.log`, ...) and an MSBuild binary log per restore, build, publish and
pack (`build-Debug.binlog`, `publish-<app>.binlog`, ...). Every run prunes what is past its
retention when it finishes.

To read them (Ubuntu):

```bash
sudo ls /var/lib/docker/volumes/bitenovac-logs/_data/PR_18
sudo cat /var/lib/docker/volumes/bitenovac-logs/_data/PR_18/16473829-1/test-Debug.log
```

Open a `.binlog` in the [MSBuild Structured Log Viewer](https://msbuildlog.com) to see why a project
built, what every property evaluated to, and how long each target took. Binary logs need
RemoteBuildTool 0.0.4 or later in the runner image; console logs work with any.

---

## Remove everything

**1.** On GitHub, Settings → Actions → Runners: remove both runners.

**2.** Ubuntu:

```bash
cd ~/bitenovac
sudo bash runner/setup-runner.sh --uninstall --purge --token unused
docker volume rm bitenovac-official
docker image rm bitenovac-remotebuildtool-runner:10.0.400
```

`--purge` keeps the Official drops; the `docker volume rm` deletes them. Use the SDK version from
`global.json` in the image name.

**3.** Check that nothing is left:

```bash
systemctl list-units 'actions.runner.*' --no-pager   # 0 loaded units
docker volume ls --filter 'name=bitenovac-'          # empty
docker image ls | grep bitenovac                     # nothing
```

To set it up again, start from [Setup](#setup).

---
