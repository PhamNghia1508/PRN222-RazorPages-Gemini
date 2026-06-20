using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Data;
using PRN222.DAL.Entities;
using PRN222.Web.Hubs;
using PRN222.Web.Infrastructure;
using PRN222.Web.Models.Department;

namespace PRN222.Web.Pages.Department;

[Authorize(Roles = ApplicationRoles.Admin)]
public class IndexModel(
    IDepartmentService departmentService,
    UserManager<ApplicationUser> userManager,
    ChatbotDbContext db,
    IHubContext<AdminHub> hubContext,
    ILogger<IndexModel> logger) : PageModel
{
    public IReadOnlyList<DepartmentDto> Departments { get; private set; } = [];
    public int? SelectedDepartmentId { get; private set; }
    public DepartmentDto? SelectedDepartment { get; private set; }
    public IReadOnlyList<DepartmentStaffItemViewModel> Staff { get; private set; } = [];
    public IReadOnlyList<DepartmentCourseItemViewModel> Courses { get; private set; } = [];

    public async Task OnGetAsync(int? departmentId)
    {
        await LoadAsync(departmentId);
    }

    public async Task<IActionResult> OnPostCreateAsync(CreateDepartmentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Code))
        {
            TempData["Error"] = "Ten va Ma khoa la bat buoc.";
            return Redirect("/Department");
        }

        try
        {
            await departmentService.CreateAsync(dto);
            TempData["Success"] = $"Da tao Khoa \"{dto.Name}\" thanh cong.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating department");
            TempData["Error"] = "Khong the tao Khoa. Vui long thu lai.";
        }

        return Redirect("/Department");
    }

    public async Task<IActionResult> OnPostAssignUserAsync(int departmentId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            TempData["Error"] = "Vui long chon giang vien can phan cong.";
            return RedirectToDepartment(departmentId);
        }

        try
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "Khong tim thay tai khoan can phan cong.";
                return RedirectToDepartment(departmentId);
            }

            var roles = await userManager.GetRolesAsync(user);
            if (!roles.Any(role =>
                    role == ApplicationRoles.HeadLecturer ||
                    role == ApplicationRoles.Lecturer))
            {
                TempData["Error"] = "Chi duoc phan cong tai khoan giang vien hoac truong bo mon vao Khoa.";
                return RedirectToDepartment(departmentId);
            }

            if (await departmentService.GetByIdAsync(departmentId) == null)
            {
                TempData["Error"] = "Khong tim thay Khoa can phan cong.";
                return RedirectToDepartment(departmentId);
            }

            if (roles.Contains(ApplicationRoles.HeadLecturer))
            {
                var conflictingHeadLecturer = await FindHeadLecturerInDepartmentAsync(departmentId, user.Id);
                if (conflictingHeadLecturer is not null)
                {
                    TempData["Error"] = BuildHeadLecturerConflictMessage(conflictingHeadLecturer);
                    return RedirectToDepartment(departmentId);
                }
            }

            var revoked = await departmentService.AssignUserToDepartmentAsync(userId, departmentId);
            if (roles.Contains(ApplicationRoles.HeadLecturer))
            {
                await departmentService.SetHeadLecturerOwnerAsync(departmentId, user.Id);
            }

            await NotifyUserAssignmentsUpdatedAsync(user);
            TempData["Success"] = BuildAssignmentMessage($"Da phan cong \"{user.Email}\" vao Khoa.", revoked);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error assigning user {UserId} to department {DeptId}", userId, departmentId);
            TempData["Error"] = "Khong the phan cong giang vien. Vui long thu lai.";
        }

        return RedirectToDepartment(departmentId);
    }

    public async Task<IActionResult> OnPostAssignCourseAsync(int departmentId, int courseId)
    {
        if (courseId <= 0)
        {
            TempData["Error"] = "Vui long chon mon hoc can phan cong.";
            return RedirectToDepartment(departmentId);
        }

        try
        {
            if (await departmentService.GetByIdAsync(departmentId) == null)
            {
                TempData["Error"] = "Khong tim thay Khoa can phan cong.";
                return RedirectToDepartment(departmentId);
            }

            var previouslyAssignedUsers = await GetAssignedUsersForCourseAsync(courseId);
            var revoked = await departmentService.AssignCourseToDepartmentAsync(courseId, departmentId);
            var course = await db.Courses.FindAsync(courseId);
            if (course is not null)
            {
                await NotifySubjectUpdatedAsync(course);
            }

            await NotifyRemovedCourseAssignmentUsersAsync(courseId, previouslyAssignedUsers);

            TempData["Success"] = BuildAssignmentMessage($"Da phan cong mon hoc \"{course?.Name}\" vao Khoa.", revoked);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error assigning course {CourseId} to department {DeptId}", courseId, departmentId);
            TempData["Error"] = "Khong the phan cong mon hoc. Vui long thu lai.";
        }

        return RedirectToDepartment(departmentId);
    }

    public async Task<IActionResult> OnPostRemoveUserAsync(string userId, int? departmentId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            TempData["Error"] = "Du lieu khong hop le.";
            return RedirectToDepartment(departmentId);
        }

        try
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "Khong tim thay tai khoan can cap nhat.";
                return RedirectToDepartment(departmentId);
            }

            var roles = await userManager.GetRolesAsync(user);
            if (!roles.Any(role =>
                    role == ApplicationRoles.HeadLecturer ||
                    role == ApplicationRoles.Lecturer))
            {
                TempData["Error"] = "Chi duoc cap nhat Khoa cho tai khoan giang vien hoac truong bo mon.";
                return RedirectToDepartment(departmentId);
            }

            var revoked = await departmentService.AssignUserToDepartmentAsync(userId, null);
            if (roles.Contains(ApplicationRoles.HeadLecturer))
            {
                await departmentService.ClearHeadLecturerOwnerAsync(user.Id);
            }

            await NotifyUserAssignmentsUpdatedAsync(user);
            TempData["Success"] = BuildAssignmentMessage($"Da xoa \"{user.Email}\" khoi Khoa.", revoked);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing user {UserId} from department", userId);
            TempData["Error"] = "Khong the xoa giang vien khoi Khoa. Vui long thu lai.";
        }

        return RedirectToDepartment(departmentId);
    }

    private async Task LoadAsync(int? departmentId)
    {
        var viewModel = await BuildManagementViewModelAsync(departmentId);
        Departments = viewModel.Departments;
        SelectedDepartmentId = viewModel.SelectedDepartmentId;
        SelectedDepartment = viewModel.SelectedDepartment;
        Staff = viewModel.Staff;
        Courses = viewModel.Courses;
    }

    private IActionResult RedirectToDepartment(int? departmentId) =>
        RedirectToPage("/Department/Index", new { departmentId });

    private static string BuildAssignmentMessage(string baseMessage, int revoked)
    {
        return revoked > 0
            ? $"{baseMessage.TrimEnd('.')} va thu hoi {revoked} mon khong con dung Khoa."
            : baseMessage;
    }

    private async Task NotifySubjectUpdatedAsync(PRN222.DAL.Entities.Course course)
    {
        await hubContext.Clients.Group(AdminHub.AdminsGroup).SendAsync(AdminHub.SubjectUpdated, new
        {
            id = course.Id,
            courseId = course.Id,
            name = course.Name,
            description = course.Description,
            documentCount = course.Documents?.Count ?? 0,
            createdAt = course.CreatedAt,
            departmentId = course.DepartmentId
        });
    }

    private async Task NotifyUserAssignmentsUpdatedAsync(ApplicationUser user)
    {
        var assignedCourses = await db.ApplicationUserCourses
            .AsNoTracking()
            .Where(assignment => assignment.UserId == user.Id)
            .Join(
                db.Courses.AsNoTracking(),
                assignment => assignment.CourseId,
                course => course.Id,
                (_, course) => new
                {
                    id = course.Id,
                    name = course.Name,
                    description = course.Description,
                    documentCount = course.Documents.Count,
                    createdAt = course.CreatedAt
                })
            .OrderBy(course => course.name)
            .ToListAsync();

        var payload = new
        {
            userId = user.Id,
            email = user.Email,
            courseIds = assignedCourses.Select(course => course.id).ToArray(),
            courses = assignedCourses
        };

        await Task.WhenAll(
            hubContext.Clients.Group(AdminHub.AdminsGroup)
                .SendAsync(AdminHub.UserAssignmentsUpdated, payload),
            hubContext.Clients.User(user.Id)
                .SendAsync(AdminHub.UserAssignmentsUpdated, payload));
    }

    private async Task<List<ApplicationUser>> GetAssignedUsersForCourseAsync(int courseId)
    {
        return await db.ApplicationUserCourses
            .AsNoTracking()
            .Where(assignment => assignment.CourseId == courseId)
            .Join(
                db.Users.AsNoTracking(),
                assignment => assignment.UserId,
                user => user.Id,
                (_, user) => user)
            .ToListAsync();
    }

    private async Task NotifyRemovedCourseAssignmentUsersAsync(
        int courseId,
        IEnumerable<ApplicationUser> previouslyAssignedUsers)
    {
        var previousUsers = (previouslyAssignedUsers ?? [])
            .Where(user => !string.IsNullOrWhiteSpace(user.Id))
            .GroupBy(user => user.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        if (previousUsers.Count == 0)
        {
            return;
        }

        var remainingUserIdSet = (await db.ApplicationUserCourses
            .AsNoTracking()
            .Where(assignment => assignment.CourseId == courseId)
            .Select(assignment => assignment.UserId)
            .ToListAsync())
            .ToHashSet(StringComparer.Ordinal);

        var removedUsers = previousUsers
            .Where(user => !remainingUserIdSet.Contains(user.Id))
            .ToList();

        if (removedUsers.Count == 0)
        {
            return;
        }

        await Task.WhenAll(removedUsers.Select(NotifyUserAssignmentsUpdatedAsync));
    }

    private async Task<ApplicationUser?> FindHeadLecturerInDepartmentAsync(int departmentId, string? excludedUserId = null)
    {
        var department = await departmentService.GetByIdAsync(departmentId);
        if (!string.IsNullOrWhiteSpace(department?.HeadLecturerUserId) &&
            !string.Equals(department.HeadLecturerUserId, excludedUserId, StringComparison.Ordinal))
        {
            return await userManager.FindByIdAsync(department.HeadLecturerUserId);
        }

        var headLecturers = await userManager.GetUsersInRoleAsync(ApplicationRoles.HeadLecturer);
        return headLecturers.FirstOrDefault(user =>
            user.DepartmentId == departmentId &&
            !string.Equals(user.Id, excludedUserId, StringComparison.Ordinal));
    }

    private static string BuildHeadLecturerConflictMessage(ApplicationUser conflictingHeadLecturer)
    {
        var identity = conflictingHeadLecturer.Email ?? conflictingHeadLecturer.UserName ?? "tai khoan khac";
        return $"Khoa nay da co truong bo mon ({identity}). Moi Khoa chi duoc co mot truong bo mon.";
    }

    private async Task<DepartmentManagementViewModel> BuildManagementViewModelAsync(int? departmentId)
    {
        var departments = (await departmentService.GetAllAsync())
            .OrderBy(department => department.Name)
            .ToList();
        var selectedDepartment = departmentId.HasValue
            ? departments.FirstOrDefault(department => department.Id == departmentId.Value)
            : departments.FirstOrDefault();

        var staffUserIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var staffRoleLookup = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in new[] { ApplicationRoles.HeadLecturer, ApplicationRoles.Lecturer })
        {
            var usersInRole = await userManager.GetUsersInRoleAsync(role);
            foreach (var user in usersInRole)
            {
                staffUserIds.Add(user.Id);
                if (!staffRoleLookup.TryGetValue(user.Id, out var roles))
                {
                    roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    staffRoleLookup[user.Id] = roles;
                }

                roles.Add(role);
            }
        }

        var staffUsers = await db.Users
            .Where(user => staffUserIds.Contains(user.Id))
            .Include(user => user.Department)
            .OrderBy(user => user.Email)
            .ToListAsync();
        var courses = await db.Courses
            .Include(course => course.Department)
            .OrderBy(course => course.Name)
            .ToListAsync();
        var assignments = await db.ApplicationUserCourses
            .AsNoTracking()
            .ToListAsync();

        var staffItems = staffUsers.Select(user =>
        {
            var userAssignments = assignments
                .Where(assignment => assignment.UserId == user.Id)
                .ToList();
            var invalidAssignmentCount = userAssignments.Count(assignment =>
                user.DepartmentId is null ||
                !courses.Any(course =>
                    course.Id == assignment.CourseId &&
                    course.DepartmentId == user.DepartmentId));

            return new DepartmentStaffItemViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? user.UserName ?? string.Empty,
                DepartmentId = user.DepartmentId,
                DepartmentName = user.Department?.Name,
                AssignedCourseCount = userAssignments.Count,
                InvalidAssignmentCount = invalidAssignmentCount,
                IsHeadLecturer = staffRoleLookup.TryGetValue(user.Id, out var roles) &&
                    roles.Contains(ApplicationRoles.HeadLecturer)
            };
        }).ToList();

        var courseItems = courses.Select(course =>
        {
            var courseAssignments = assignments
                .Where(assignment => assignment.CourseId == course.Id)
                .ToList();
            var invalidAssignmentCount = courseAssignments.Count(assignment =>
                course.DepartmentId is null ||
                !staffUsers.Any(user =>
                    user.Id == assignment.UserId &&
                    user.DepartmentId == course.DepartmentId));

            return new DepartmentCourseItemViewModel
            {
                CourseId = course.Id,
                Name = course.Name,
                DepartmentId = course.DepartmentId,
                DepartmentName = course.Department?.Name,
                AssignedStaffCount = courseAssignments.Count,
                InvalidAssignmentCount = invalidAssignmentCount
            };
        }).ToList();

        return new DepartmentManagementViewModel
        {
            Departments = departments,
            SelectedDepartmentId = selectedDepartment?.Id,
            SelectedDepartment = selectedDepartment,
            Staff = staffItems,
            Courses = courseItems
        };
    }
}
