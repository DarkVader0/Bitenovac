namespace Bitenovac.RemoteBuildTool.Testing.Unit.Tests;

public sealed class TrxReaderTests
{
    [Fact]
    public void Read_ShouldReadEveryTestCaseWithItsOutcomeTimeAndError_WhenTheReportHasResults()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var longMessage = new string('x', 2500);
        var report = TestFactory.WriteFile(Path.Combine(directory.Path, "Lib.Tests.trx"), $"""
            <?xml version="1.0" encoding="utf-8"?>
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Results>
                <UnitTestResult testName="Lib.Tests.Adds" outcome="Passed" duration="00:00:00.0123000" />
                <UnitTestResult testName="Lib.Tests.Fails" outcome="Failed" duration="00:00:01.5000000">
                  <Output>
                    <ErrorInfo>
                      <Message>{longMessage}</Message>
                    </ErrorInfo>
                  </Output>
                </UnitTestResult>
                <UnitTestResult testName="Lib.Tests.Short" outcome="Failed">
                  <Output>
                    <ErrorInfo>
                      <Message>Expected 2.</Message>
                    </ErrorInfo>
                  </Output>
                </UnitTestResult>
                <UnitTestResult testName="Lib.Tests.Skipped" outcome="NotExecuted" />
                <UnitTestResult />
              </Results>
            </TestRun>
            """);

        // Act
        var cases = TrxReader.Read(report);

        // Assert
        Assert.Equal(
            [
                new TestCaseResult("Lib.Tests.Adds", "Passed", TimeSpan.FromMilliseconds(12.3), null),
                new TestCaseResult("Lib.Tests.Fails", "Failed", TimeSpan.FromSeconds(1.5), longMessage[..2000]),
                new TestCaseResult("Lib.Tests.Short", "Failed", null, "Expected 2."),
                new TestCaseResult("Lib.Tests.Skipped", "NotExecuted", null, null),
                new TestCaseResult("", "Unknown", null, null),
            ],
            cases);
    }
}
