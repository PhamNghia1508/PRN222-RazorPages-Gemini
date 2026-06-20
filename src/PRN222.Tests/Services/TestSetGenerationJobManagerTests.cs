using FluentAssertions;
using PRN222.BLL.Services;

namespace PRN222.Tests.Services;

public class TestSetGenerationJobManagerTests
{
    [Fact]
    public void TryStart_ShouldRejectSecondRunningJobForSameCourse()
    {
        var manager = new TestSetGenerationJobManager();

        var first = manager.TryStart(1, out var token);
        var second = manager.TryStart(1, out _);

        first.Should().BeTrue();
        second.Should().BeFalse();
        token.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void RequestStop_ShouldCancelRunningJob()
    {
        var manager = new TestSetGenerationJobManager();
        manager.TryStart(1, out var token);

        var stopped = manager.RequestStop(1);
        var status = manager.GetStatus(1);

        stopped.Should().BeTrue();
        token.IsCancellationRequested.Should().BeTrue();
        status.Status.Should().Be("Stopping");
    }
}
