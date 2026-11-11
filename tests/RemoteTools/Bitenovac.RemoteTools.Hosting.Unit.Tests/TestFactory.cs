using System.Net.Http.Headers;

namespace Bitenovac.RemoteTools.Hosting.Unit.Tests;

/// <summary>
/// Provides the inputs shared by the RemoteTools.Hosting tests.
/// </summary>
internal static class TestFactory
{
    /// <summary>
    /// Gets the tool the tests name to GitHub.
    /// </summary>
    public static ProductInfoHeaderValue UserAgent { get; } = new("Bitenovac.Tests", "1.0");
}
