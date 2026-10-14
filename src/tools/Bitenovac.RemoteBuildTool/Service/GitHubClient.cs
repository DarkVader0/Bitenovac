using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents a completed workflow run, as the GitHub REST API lists it.
/// </summary>
/// <param name="Id">The run's id.</param>
/// <param name="Attempt">The run's latest attempt.</param>
/// <param name="Workflow">The workflow's name, such as <c>PR</c>.</param>
/// <param name="Conclusion">How it ended, such as <c>success</c> or <c>failure</c>.</param>
/// <param name="HeadSha">The commit it ran on.</param>
/// <param name="HeadBranch">The branch it ran on.</param>
/// <param name="PullRequest">The pull request it ran for, if any.</param>
/// <param name="UpdatedAt">When it last changed, which for a completed run is when it ended.</param>
internal sealed record WorkflowRun(long Id, int Attempt, string Workflow, string Conclusion, string? HeadSha, string? HeadBranch, int? PullRequest, DateTimeOffset UpdatedAt)
{
    /// <summary>
    /// Gets the build id its stages recorded, the run id and the attempt.
    /// </summary>
    public string BuildId => $"{Id.ToString(CultureInfo.InvariantCulture)}-{Attempt.ToString(CultureInfo.InvariantCulture)}";
}

/// <summary>
/// Provides the GitHub REST API calls the service needs: a branch's head and the completed runs
/// with their jobs.
/// </summary>
/// <param name="http">The client to send requests with.</param>
/// <param name="repository">The repository, as <c>owner/name</c>.</param>
internal sealed class GitHubClient(HttpClient http, string repository)
{
    /// <summary>
    /// Creates a client for the GitHub API at <paramref name="apiUrl"/>, authenticated with <paramref name="token"/>.
    /// </summary>
    /// <param name="apiUrl">The API's address.</param>
    /// <param name="token">The token.</param>
    /// <param name="handler">What sends the requests; the network when omitted.</param>
    /// <returns>The configured client, which the caller disposes.</returns>
    public static HttpClient CreateHttpClient(string apiUrl, string token, HttpMessageHandler? handler = null)
    {
        var client = handler is null ? new HttpClient() : new HttpClient(handler);
        client.BaseAddress = new Uri(apiUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Bitenovac.RemoteBuildTool", "1"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    /// <summary>
    /// Returns the commit a branch points at.
    /// </summary>
    /// <param name="branch">The branch.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>The commit's SHA.</returns>
    /// <exception cref="HttpRequestException">GitHub refused or could not be reached.</exception>
    public async Task<string> BranchHeadAsync(string branch, CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync($"repos/{repository}/branches/{Uri.EscapeDataString(branch)}", cancellationToken);
        return document.RootElement.GetProperty("commit").GetProperty("sha").GetString()
            ?? throw new HttpRequestException($"GitHub gave no commit for branch {branch}.");
    }

    /// <summary>
    /// Returns the runs that completed since <paramref name="since"/>, newest first.
    /// </summary>
    /// <param name="since">The earliest creation time to list.</param>
    /// <param name="cancellationToken">Stops the requests.</param>
    /// <returns>The runs, at most the first 300.</returns>
    /// <exception cref="HttpRequestException">GitHub refused or could not be reached.</exception>
    public async Task<IReadOnlyList<WorkflowRun>> CompletedRunsAsync(DateTimeOffset since, CancellationToken cancellationToken)
    {
        var runs = new List<WorkflowRun>();
        var created = since.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        for (var page = 1; page <= 3; page++)
        {
            using var document = await GetJsonAsync(
                $"repos/{repository}/actions/runs?status=completed&per_page=100&page={page}&created=%3E%3D{created}", cancellationToken);
            var listed = document.RootElement.GetProperty("workflow_runs");
            runs.AddRange(listed.EnumerateArray().Select(Run));
            if (listed.GetArrayLength() < 100)
                break;
        }

        return runs;
    }

    /// <summary>
    /// Returns the jobs of one attempt of a run, as the API's JSON for <see cref="Telemetry.WorkflowJob.Parse"/>.
    /// </summary>
    /// <param name="run">The run and attempt.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>The response body.</returns>
    /// <exception cref="HttpRequestException">GitHub refused or could not be reached.</exception>
    public Task<string> JobsAsync(WorkflowRun run, CancellationToken cancellationToken) =>
        http.GetStringAsync(
            $"repos/{repository}/actions/runs/{run.Id.ToString(CultureInfo.InvariantCulture)}/attempts/{run.Attempt.ToString(CultureInfo.InvariantCulture)}/jobs?per_page=100",
            cancellationToken);

    private async Task<JsonDocument> GetJsonAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
    }

    private static WorkflowRun Run(JsonElement run) => new(
        run.GetProperty("id").GetInt64(),
        run.TryGetProperty("run_attempt", out var attempt) && attempt.ValueKind == JsonValueKind.Number ? attempt.GetInt32() : 1,
        run.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
        run.TryGetProperty("conclusion", out var conclusion) ? conclusion.GetString() ?? "unknown" : "unknown",
        run.TryGetProperty("head_sha", out var sha) ? sha.GetString() : null,
        run.TryGetProperty("head_branch", out var branch) ? branch.GetString() : null,
        run.TryGetProperty("pull_requests", out var pulls) && pulls.ValueKind == JsonValueKind.Array && pulls.GetArrayLength() > 0
            ? pulls[0].GetProperty("number").GetInt32()
            : null,
        run.GetProperty("updated_at").GetDateTimeOffset());
}
