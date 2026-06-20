using Microsoft.AspNetCore.SignalR;
using PRN222.BLL.Services.Interfaces;
using PRN222.Web.Hubs;

namespace PRN222.Web.Infrastructure;

public sealed class SignalRDocumentRealtimeNotifier(
    IHubContext<AdminHub> hubContext,
    ICourseAssignmentService courseAssignmentService,
    ILogger<SignalRDocumentRealtimeNotifier> logger) : IDocumentRealtimeNotifier
{
    public async Task NotifyDocumentChangedAsync(
        DocumentRealtimeNotification notification,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assignedUserIds = await courseAssignmentService
                .GetAssignedUserIdsForCourseAsync(notification.CourseId);

            var notifications = new List<Task>
            {
                hubContext.Clients
                    .Group(AdminHub.AdminsGroup)
                    .SendAsync(AdminHub.DocumentChanged, notification, cancellationToken)
            };

            if (assignedUserIds.Count > 0)
            {
                notifications.Add(hubContext.Clients
                    .Users(assignedUserIds)
                    .SendAsync(AdminHub.DocumentChanged, notification, cancellationToken));
            }

            await Task.WhenAll(notifications);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Could not broadcast document realtime event {Action} for document {DocumentId}.",
                notification.Action,
                notification.DocumentId);
        }
    }
}
