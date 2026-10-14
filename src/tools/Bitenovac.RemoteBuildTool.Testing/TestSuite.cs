namespace Bitenovac.RemoteBuildTool.Testing;

/// <summary>
/// Represents one test project to run: the name its output is reported under and how to run it.
/// </summary>
internal sealed record TestJob(string Name, Func<TestRunResult> Run);

/// <summary>
/// Provides methods for running test projects in parallel and handing over each one's result whole.
/// </summary>
internal static class TestSuite
{
    /// <summary>
    /// Runs every job, at most <paramref name="maxParallelism"/> at a time.
    /// </summary>
    /// <param name="jobs">The test projects to run.</param>
    /// <param name="resultsDirectory">The results directory a job that cannot start reports.</param>
    /// <param name="maxParallelism">The most jobs to run at once.</param>
    /// <param name="completed">
    /// Called with each job and its result as the job ends, never for two jobs at once, so a
    /// report written from it is not interleaved.
    /// </param>
    /// <returns>One result per job, in the order of <paramref name="jobs"/>.</returns>
    /// <remarks>
    /// A job that throws <see cref="InvalidOperationException"/>, such as a test project that was
    /// never built, fails with its message as output instead of stopping the others.
    /// </remarks>
    public static IReadOnlyList<TestRunResult> RunAll(IReadOnlyList<TestJob> jobs, string resultsDirectory, int maxParallelism, Action<TestJob, TestRunResult> completed)
    {
        var results = new TestRunResult[jobs.Count];
        var gate = new Lock();

        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = maxParallelism }, index =>
        {
            var job = jobs[index];
            try
            {
                results[index] = job.Run();
            }
            catch (InvalidOperationException exception)
            {
                results[index] = new TestRunResult(TestRunOutcome.Failed, -1, resultsDirectory, exception.Message + Environment.NewLine);
            }

            lock (gate)
                completed(job, results[index]);
        });

        return results;
    }
}
