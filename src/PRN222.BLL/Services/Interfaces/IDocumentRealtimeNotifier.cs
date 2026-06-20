namespace PRN222.BLL.Services.Interfaces;

public interface IDocumentRealtimeNotifier
{
    Task NotifyDocumentChangedAsync(
        DocumentRealtimeNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record DocumentRealtimeNotification(
    int DocumentId,
    int CourseId,
    string FileName,
    string Status,
    string Action,
    int ChunkCount,
    DateTimeOffset OccurredAt);
