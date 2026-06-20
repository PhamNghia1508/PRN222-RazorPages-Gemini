using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PRN222.Web.Infrastructure;

namespace PRN222.Web.Hubs;

[Authorize(Roles = ApplicationRoles.Management)]
public class AdminHub : Hub
{
    public const string AdminsGroup = "Administrators";
    public const string ManagementGroup = "Management";

    public const string SubjectCreated = "SubjectCreated";
    public const string SubjectUpdated = "SubjectUpdated";
    public const string SubjectDeleted = "SubjectDeleted";

    public const string UserCreated = "UserCreated";
    public const string UserAssignmentsUpdated = "UserAssignmentsUpdated";

    public const string DocumentChanged = "DocumentChanged";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole(ApplicationRoles.Admin) == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminsGroup);
        }

        if (Context.User?.IsInRole(ApplicationRoles.Admin) == true ||
            Context.User?.IsInRole(ApplicationRoles.HeadLecturer) == true ||
            Context.User?.IsInRole(ApplicationRoles.Lecturer) == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, ManagementGroup);
        }

        await base.OnConnectedAsync();
    }
}
