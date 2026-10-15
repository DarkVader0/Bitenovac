namespace Bitenovac.RemoteBuildTool.Testing;

/// <summary>
/// Specifies which TRX report a test project can write, by the switch its test framework offers.
/// </summary>
internal enum TestReport
{
    /// <summary>
    /// The project writes no report.
    /// </summary>
    None,

    /// <summary>
    /// The project references Microsoft.Testing.Extensions.TrxReport: <c>--report-trx</c>.
    /// </summary>
    Trx,

    /// <summary>
    /// The project runs xUnit.net v3, which writes TRX itself: <c>--report-xunit-trx</c>.
    /// </summary>
    XunitTrx,
}
