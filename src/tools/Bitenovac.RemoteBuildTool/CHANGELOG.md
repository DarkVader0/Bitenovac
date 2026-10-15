# Changelog

Every RemoteBuildTool release on [nuget.org](https://www.nuget.org/packages/Bitenovac.RemoteBuildTool).
Every fingerprint includes the RemoteBuildTool version, so after any upgrade the first run builds
everything once.

## 1.3.0

Ready for repositories other than the one it was made in.

- A project without `MinimumLineCoverage` or `MinimumBranchCoverage` is measured but never fails
  the coverage gate; before, `plan` failed on it. Minimums are read with a period, such as `95.5`,
  whatever the machine's culture, and a value that is not a percentage from 0 to 100 fails the
  plan with a message naming the project.
- Test results are reused by default; `<CacheTestResults>false</CacheTestResults>` opts a test
  project out. Before, a project had to opt in.
- The service follows the repository's default branch unless `remotebuildtool.json` names one;
  before, it followed `master`.
- `remotebuildtool --version`, and help that lists every environment variable.
- The package carries a README, tags, release notes and symbols.

## 1.2.2

- Each step of the service's nightly rebuild runs under `rebuildStepMinutes` (20 by default). A
  step past it is stopped with every process it started and the rebuild is recorded as
  `timed_out`; main is left as it was.

## 1.2.1

- A merge wakes the service at once through `remotebuildtool wake`; polling for merges is a
  15-minute fallback.
- `build` stages a cache hit that main lost after the plan, and `test` finds it there.
- `release` no longer builds test projects.

## 1.2.0

- `remotebuildtool serve`, a service that promotes what merged pull requests built into main,
  records every build from GitHub, compacts telemetry, prunes logs and rebuilds main nightly,
  recording how each project compared with the cache.
- Telemetry: build, stage, target, test and machine records as JSON lines, compacted into Parquet
  and queried with DuckDB.
- Fingerprints take in what the repository's own MSBuild files contribute to each project,
  instead of whole files, so a change only misses the projects it reaches.
- Logs show failures first, timings for every step, and a summary of the run on GitHub Actions.
- The artifact store keeps each file's execute bit, so an apphost restored from main stays
  runnable.

## 1.1.0

- Every step reports the CPU time and peak memory it used.

## 1.0.0

- The first stable release: `plan`, `build`, `test`, `promote` and `release`, the content-hashed
  artifact store, test-result reuse and the coverage gate.
