using System.Text.Json;

namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Represents one job of a workflow run, as the GitHub REST API lists it.
/// </summary>
/// <param name="Name">The job's name.</param>
/// <param name="CreatedAt">When the job was queued.</param>
/// <param name="StartedAt">When a runner picked it up, if one has.</param>
/// <param name="CompletedAt">When it ended, if it has.</param>
internal sealed record WorkflowJob(string Name, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt)
{
    /// <summary>
    /// Reads the jobs of a <c>GET /repos/{owner}/{repo}/actions/runs/{run_id}/attempts/{attempt}/jobs</c> response.
    /// </summary>
    /// <param name="json">The response body.</param>
    /// <returns>The jobs; a job without a creation time is left out.</returns>
    public static IReadOnlyList<WorkflowJob> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
            return [];

        return [.. jobs.EnumerateArray()
            .Select(job => (
                Name: job.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                CreatedAt: Time(job, "created_at"),
                StartedAt: Time(job, "started_at"),
                CompletedAt: Time(job, "completed_at")))
            .Where(job => job.CreatedAt is not null)
            .Select(job => new WorkflowJob(job.Name, job.CreatedAt!.Value, job.StartedAt, job.CompletedAt))];
    }

    private static DateTimeOffset? Time(JsonElement job, string property) =>
        job.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out var time)
            ? time
            : null;
}
