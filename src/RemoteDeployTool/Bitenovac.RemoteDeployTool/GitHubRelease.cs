using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bitenovac.RemoteDeployTool;

/// <summary>
/// Provides the deployment of a drop's runner bundle as a GitHub release.
/// </summary>
internal static class GitHubRelease
{
    private const string Api = "https://api.github.com";

    /// <summary>
    /// Releases the runner bundle of a drop: a release named for its RemoteBuildTool version, at the
    /// commit the drop was built from, with the bundle and its SHA-256 as assets.
    /// </summary>
    /// <param name="dropDirectory">The drop.</param>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="token">A token that may create releases in it.</param>
    /// <param name="http">The client the requests are sent with.</param>
    /// <param name="output">Where the deployment reports.</param>
    /// <param name="options">The ledger that the deployment is recorded in.</param>
    /// <param name="time">The clock the ledger is kept by.</param>
    /// <returns>Zero when the release was made; one otherwise.</returns>
    /// <exception cref="InvalidOperationException">
    /// The drop has no runner bundle, the bundle does not match the manifest, the version is already released, or GitHub refused a request.
    /// </exception>
    /// <remarks>
    /// The release is not made when its tag already exists: a version is released once, and a bundle is
    /// changed by raising <c>VersionPrefix</c>, not by replacing an asset.
    /// </remarks>
    public static async Task<int> RunAsync(
        string dropDirectory, string repository, string token, HttpClient http, PipelineOutput output, DeployOptions options, TimeProvider time)
    {
        var manifest = DropManifest.Read(dropDirectory);
        var bundle = manifest.Artifacts.FirstOrDefault(artifact => artifact.Kind == DropArtifact.Runner)
            ?? throw new InvalidOperationException($"drop {manifest.Version} has no runner bundle; refresh its manifest after the bundle is made.");

        var file = Path.Combine(dropDirectory, bundle.Path);
        var digest = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(file)));
        if (digest != bundle.Sha256)
            throw new InvalidOperationException($"{bundle.Path} does not match the SHA-256 the manifest of drop {manifest.Version} records.");

        if (manifest.Sha is null)
            throw new InvalidOperationException($"drop {manifest.Version} does not name the commit it was built from.");

        var tag = $"bitenovac-runner-{bundle.Version}";
        var target = $"github:{repository}";
        output.WriteLine($"==> Releasing {tag} from drop {manifest.Version} to {repository}");

        string? error = null;
        try
        {
            var client = Authorized(http, token);
            var existing = await client.GetAsync($"{Api}/repos/{repository}/releases/tags/{tag}");
            if (existing.IsSuccessStatusCode)
                throw new InvalidOperationException($"{tag} is already released. Raise <VersionPrefix> in src/RemoteBuildTool/Directory.Build.props, then run the drop again.");
            if (existing.StatusCode != System.Net.HttpStatusCode.NotFound)
                throw new InvalidOperationException($"could not check whether {tag} is released: HTTP {(int)existing.StatusCode}.");

            var body = JsonSerializer.Serialize(new
            {
                tag_name = tag,
                target_commitish = manifest.Sha,
                name = $"Runner setup {bundle.Version}",
                body = $"Installs the Bitenovac runner setup with RemoteBuildTool {bundle.Version}, built in Official drop {manifest.Version}. Extract the archive, then run runner/setup-runner.sh as described in docs/Local_RemoteBuildTool_Setup.md.",
            });
            var created = await client.PostAsync($"{Api}/repos/{repository}/releases", Json(body));
            created.EnsureSuccessStatusCode();
            var upload = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement
                .GetProperty("upload_url").GetString()!
                .Split('{')[0];

            await Upload(client, upload, Path.GetFileName(file), "application/gzip", await File.ReadAllBytesAsync(file));
            await Upload(client, upload, $"{Path.GetFileName(file)}.sha256", "text/plain", Encoding.UTF8.GetBytes($"{digest}  {Path.GetFileName(file)}\n"));
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            error = exception.Message;
        }

        var recorded = new List<DeploymentInfo> { options.Deployment(target, manifest, bundle, time.GetUtcNow(), error) };
        options.Ledger?.Record(recorded);
        if (error is not null)
        {
            output.WriteFailure(error);
            return 1;
        }

        output.WriteLine($"Released {tag}.");
        return 0;
    }

    private static HttpClient Authorized(HttpClient http, string token)
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.UserAgent.ParseAdd("RemoteDeployTool");
        return http;
    }

    private static async Task Upload(HttpClient client, string uploadUrl, string name, string contentType, byte[] content)
    {
        using var body = new ByteArrayContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var response = await client.PostAsync($"{uploadUrl}?name={Uri.EscapeDataString(name)}", body);
        response.EnsureSuccessStatusCode();
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
}
