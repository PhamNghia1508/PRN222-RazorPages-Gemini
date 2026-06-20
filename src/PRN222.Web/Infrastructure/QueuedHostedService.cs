namespace PRN222.Web.Infrastructure;

public class QueuedHostedService : BackgroundService
{
    private readonly BackgroundTaskQueue _taskQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QueuedHostedService> _logger;

    public QueuedHostedService(
        BackgroundTaskQueue taskQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<QueuedHostedService> logger)
    {
        _taskQueue = taskQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Func<IServiceProvider, CancellationToken, Task> workItem;

            try
            {
                workItem = await _taskQueue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                await workItem(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Queued background task failed.");
            }
        }
    }
}
