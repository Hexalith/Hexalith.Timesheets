using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Opt-in persisted magic-link acceptance against the checked-in local Aspire topology.</summary>
public sealed class MagicLinkLiveDaprTests
{
    /// <summary>Requires real stored record, issue, index, use, restart, and race observations.</summary>
    [Fact]
    public async Task Recorded_magic_links_commit_one_use_and_survive_actor_restart()
    {
        if (Environment.GetEnvironmentVariable("TIMESHEETS_LIVE_DAPR") != "1")
        {
            Assert.Skip("Set TIMESHEETS_LIVE_DAPR=1 to run the local EventStore/Dapr acceptance journey.");
        }

        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(8));
        (int exitCode, string output, string error) = await MagicLinkLiveDaprFixture
            .RunAsync(deadline.Token);
        exitCode.ShouldBe(0, $"Local Dapr journey failed. Output: {output} Error: {error}");
        output.ShouldContain("catalog delivered and persisted");
        output.ShouldContain("non-fixture assertion denied without owner event");
        output.ShouldContain("confirm record/issue/index/GET/use/replay passed race");
        output.ShouldContain("adjust record/issue/index/GET/use/replay passed single");
        output.ShouldContain("composed Time Entry rebuild committed and persisted");
        output.ShouldContain("actor restart and replay passed");
        output.ShouldContain("fresh post-restart link passed");
    }
}
