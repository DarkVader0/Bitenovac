using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Reporting;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class PlanSummaryTests
{
    [Fact]
    public void ToMarkdown_ShouldCountHitsAndListWhyEachMissBuilds_WhenAConfigurationHasMisses()
    {
        // Arrange
        var byConfiguration = new Dictionary<string, List<PlanEntry>>
        {
            ["Debug"] =
            [
                TestFactory.Entry("src/B/B.csproj", whyCacheMiss: "references changed: src/A/A.csproj"),
                TestFactory.Entry("src/A/A.csproj", whyCacheMiss: "own inputs changed source:src/A/A.cs"),
                TestFactory.Entry("src/C/C.csproj", hit: true),
            ],
            ["Release"] = [TestFactory.Entry("src/A/A.csproj", hit: true)],
        };

        // Act
        var markdown = PlanSummary.ToMarkdown(byConfiguration);

        // Assert
        Assert.Equal("""
            ### Plan

            | Configuration | Projects | Cache hits | To build |
            |---|---|---|---|
            | Debug | 3 | 1 | 2 |
            | Release | 1 | 1 | 0 |

            <details><summary>Why 2 Debug project(s) build</summary>

            - `src/A/A.csproj`: own inputs changed source:src/A/A.cs
            - `src/B/B.csproj`: references changed: src/A/A.csproj

            </details>


            """.ReplaceLineEndings(Environment.NewLine), markdown);
    }
}
