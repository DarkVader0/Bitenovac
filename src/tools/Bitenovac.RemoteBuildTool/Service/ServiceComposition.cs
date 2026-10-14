using Microsoft.Extensions.DependencyInjection;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Provides a method for registering the service's parts: its shared state and its hosted services.
/// </summary>
internal static class ServiceComposition
{
    /// <summary>
    /// Registers everything the service runs.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="host">The machine's settings.</param>
    /// <param name="tool">How to start RemoteBuildTool commands.</param>
    /// <param name="time">The clock.</param>
    /// <param name="handler">Sends the GitHub API's requests; the default network stack when omitted.</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddRemoteBuildToolService(
        this IServiceCollection services, HostSettings host, ToolCommand tool, TimeProvider time, HttpMessageHandler? handler = null)
    {
        services.AddSingleton(host);
        services.AddSingleton(tool);
        services.AddSingleton(time);
        services.AddSingleton(_ => GitHubClient.CreateHttpClient(host.GitHubApiUrl, host.GitHubToken, handler));
        services.AddSingleton(provider => new GitHubClient(provider.GetRequiredService<HttpClient>(), host.Repository));
        services.AddSingleton(_ => new ServiceStateStore(Path.Combine(host.WorkRoot, "state", "service.json")));
        services.AddSingleton<RepositorySettingsSource>();
        services.AddSingleton<MainStoreWriter>();
        services.AddSingleton<PullRequestPromoter>();
        services.AddSingleton<BuildReporter>();
        services.AddSingleton<ServiceBuild>();

        services.AddHostedService<PromotionService>();
        services.AddHostedService<BuildReportService>();
        services.AddHostedService<HousekeepingService>();
        services.AddHostedService<RebuildService>();
        return services;
    }
}
