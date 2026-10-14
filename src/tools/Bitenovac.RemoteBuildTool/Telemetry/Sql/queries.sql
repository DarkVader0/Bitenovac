-- Ready-made queries over the telemetry views build, stage, target, test, machine and
-- cache_comparison. Each is a view, or a table macro where it takes an argument:
--
--   FROM build_percentiles WHERE Queue = 'PR';
--   FROM build_stages('16473829-1');
--
-- Every row that names a build carries its LogsPath, the build's folder in the logs share, for
-- what the tables do not hold: console output and binary logs.

-- Builds per queue and day, with the percentiles of their end-to-end and queue times.
CREATE VIEW build_percentiles AS
SELECT
    Queue,
    CAST(EndTime AS DATE) AS Day,
    count(*) AS Builds,
    count(*) FILTER (WHERE Result <> 'success') AS NotSucceeded,
    quantile_cont(E2ETimeMS, 0.5) AS E2EP50MS,
    quantile_cont(E2ETimeMS, 0.8) AS E2EP80MS,
    quantile_cont(E2ETimeMS, 0.95) AS E2EP95MS,
    quantile_cont(QueueTimeMS, 0.8) AS QueueP80MS,
    quantile_cont(PreparationTimeMS, 0.8) AS PreparationP80MS
FROM build
GROUP BY ALL
ORDER BY Day DESC, Queue;

-- Stages per queue, stage and day, with the 80th percentile of the stage and of each phase.
CREATE VIEW stage_percentiles AS
SELECT
    Queue,
    Stage,
    CAST(StartTime AS DATE) AS Day,
    count(*) AS Stages,
    quantile_cont(DurationMS, 0.8) AS DurationP80MS,
    quantile_cont(EvaluationMS, 0.8) AS EvaluationP80MS,
    quantile_cont(RestoreMS, 0.8) AS RestoreP80MS,
    quantile_cont(HashingMS, 0.8) AS HashingP80MS,
    quantile_cont(CacheLookupMS, 0.8) AS CacheLookupP80MS,
    quantile_cont(CacheRestoreMS, 0.8) AS CacheRestoreP80MS,
    quantile_cont(BuildMS, 0.8) AS BuildP80MS,
    quantile_cont(TestMS, 0.8) AS TestP80MS,
    quantile_cont(CoverageMS, 0.8) AS CoverageP80MS,
    quantile_cont(StoreMS, 0.8) AS StoreP80MS,
    avg(CpuUtilization) AS AverageCpuUtilization,
    max(MemoryUtilization) AS PeakMemoryUtilization
FROM stage
GROUP BY ALL
ORDER BY Day DESC, Queue, Stage;

-- The share of projects each day's build stages restored from main instead of building.
CREATE VIEW cache_hit_rate AS
SELECT
    Queue,
    CAST(StartTime AS DATE) AS Day,
    sum(CacheHits) AS CacheHits,
    sum(CacheMisses) AS CacheMisses,
    sum(CacheHits) / nullif(sum(CacheHits) + sum(CacheMisses), 0) AS HitRate
FROM stage
WHERE Stage LIKE 'build-%'
GROUP BY ALL
ORDER BY Day DESC, Queue;

-- Why projects missed the cache over the last 7 days, most frequent first.
CREATE VIEW cache_miss_reasons AS
SELECT
    Project,
    WhyCacheMiss,
    count(*) AS Misses
FROM target
WHERE Kind = 'build' AND CacheOutcome = 'miss' AND BuildId IN (
    SELECT BuildId FROM stage WHERE StartTime >= now() - INTERVAL 7 DAY)
GROUP BY ALL
ORDER BY Misses DESC;

-- The projects and test projects that took longest over the last 7 days, by their 80th percentile.
CREATE VIEW slowest_targets AS
SELECT
    Project,
    Kind,
    CacheOutcome,
    count(*) AS Runs,
    quantile_cont(DurationMS, 0.8) AS DurationP80MS,
    max(DurationMS) AS DurationMaxMS
FROM target
WHERE BuildId IN (SELECT BuildId FROM stage WHERE StartTime >= now() - INTERVAL 7 DAY)
GROUP BY ALL
ORDER BY DurationP80MS DESC NULLS LAST;

-- Test cases that both passed and failed on the same commit in the last 30 days.
CREATE VIEW flaky_tests AS
WITH per_commit AS (
    SELECT
        Project,
        TestName,
        Sha,
        count(*) FILTER (WHERE Outcome = 'Failed') AS Failures,
        count(*) FILTER (WHERE Outcome = 'Passed') AS Passes,
        max(ErrorMessage) AS ErrorMessage
    FROM test
    WHERE Sha IS NOT NULL
        AND BuildId IN (SELECT BuildId FROM stage WHERE StartTime >= now() - INTERVAL 30 DAY)
    GROUP BY ALL
)
SELECT
    Project,
    TestName,
    count(*) AS FlakyCommits,
    sum(Failures) AS Failures,
    sum(Passes) AS Passes,
    max(ErrorMessage) AS LastErrorMessage
FROM per_commit
WHERE Failures > 0 AND Passes > 0
GROUP BY ALL
ORDER BY FlakyCommits DESC, Failures DESC;

-- Test cases that failed, per day.
CREATE VIEW failing_tests AS
SELECT
    CAST(stage.StartTime AS DATE) AS Day,
    test.Project,
    test.TestName,
    count(*) AS Failures,
    max(test.ErrorMessage) AS LastErrorMessage
FROM test
JOIN stage USING (BuildId, Stage, Machine)
WHERE test.Outcome = 'Failed'
GROUP BY ALL
ORDER BY Day DESC, Failures DESC;

-- How busy each machine was, per hour.
CREATE VIEW machine_load AS
SELECT
    Machine,
    date_trunc('hour', Time) AS Hour,
    avg(HostCpuUtilization) AS AverageHostCpuUtilization,
    max(HostCpuUtilization) AS PeakHostCpuUtilization,
    max(HostMemoryUtilization) AS PeakHostMemoryUtilization
FROM machine
GROUP BY ALL
ORDER BY Hour DESC, Machine;

-- One build's stages, in order, with where their logs are.
CREATE MACRO build_stages(id) AS TABLE
SELECT Stage, Machine, Job, StartTime, DurationMS, Succeeded, FirstError, CacheHits, CacheMisses,
    EvaluationMS, RestoreMS, HashingMS, CacheLookupMS, CacheRestoreMS, BuildMS, TestMS, CoverageMS, StoreMS,
    CpuUtilization, MemoryUtilization, LogsPath
FROM stage
WHERE BuildId = id
ORDER BY StartTime;

-- One build's projects: what each stage did with each and how long it took.
CREATE MACRO build_targets(id) AS TABLE
SELECT Stage, Project, Kind, TestKind, CacheOutcome, WhyCacheMiss, DurationMS, CpuTimeMS, Succeeded,
    TestsTotal, TestsFailed
FROM target
WHERE BuildId = id
ORDER BY Stage, DurationMS DESC NULLS LAST;

-- Projects whose clean rebuild differed from what main held under the same fingerprint: each one's
-- fingerprint misses an input that changes its output. Newest first.
CREATE VIEW cache_mismatches AS
SELECT Time, BuildId, Sha, Configuration, Project, FullHash, DifferingFiles, LogsPath
FROM cache_comparison
WHERE Outcome = 'mismatch'
ORDER BY Time DESC;

-- How each daily rebuild compared with main, per configuration.
CREATE VIEW cache_comparison_outcomes AS
SELECT
    BuildId,
    min(Time) AS Time,
    Configuration,
    count(*) FILTER (WHERE Outcome = 'match') AS Matches,
    count(*) FILTER (WHERE Outcome = 'mismatch') AS Mismatches,
    count(*) FILTER (WHERE Outcome = 'changed') AS Changed,
    count(*) FILTER (WHERE Outcome = 'new') AS New
FROM cache_comparison
GROUP BY BuildId, Configuration
ORDER BY Time DESC, Configuration;
