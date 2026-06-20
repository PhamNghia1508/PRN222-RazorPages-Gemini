namespace PRN222.BLL.Services.Interfaces;

public interface IBackgroundTaskQueue
{
    ValueTask QueueAsync(Func<IServiceProvider, CancellationToken, Task> workItem);
}
