using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.DAL.Repositories.Interfaces;
using PRN222.Web.Hubs;
using PRN222.Web.Infrastructure;
using PRN222.Web.Models.Admin;

namespace PRN222.Web.Pages.Admin;

[Authorize(Roles = ApplicationRoles.Admin)]
public class AccountsModel(
    UserManager<ApplicationUser> userManager,
    ICourseService courseService,
    ICourseAssignmentService courseAssignmentService,
    IDepartmentService departmentService,
    ICourseAccessService courseAccessService,
    IUnitOfWork unitOfWork,
    IHubContext<AdminHub> hubContext,
    ILogger<AccountsModel> logger) : PageModel
{
    public CreateStaffAccountViewModel NewAccount { get; private set; } = new();
    public IReadOnlyList<StaffAccountListItemViewModel> Accounts { get; private set; } = [];
    public IReadOnlyList<PRN222.BLL.DTOs.CourseDto> Courses { get; private set; } = [];
    public IReadOnlyList<PRN222.BLL.DTOs.DepartmentDto> Departments { get; private set; } = [];

    public async Task OnGetAsync()
    {
        await LoadAsync(new CreateStaffAccountViewModel
        {
            Role = ApplicationRoles.Lecturer
        });
    }

    public async Task<IActionResult> OnPostCreateAccountAsync(CreateStaffAccountViewModel model)
    {
        var email = model.Email?.Trim() ?? string.Empty;

        if (!ApplicationRoles.StaffCreatableByAdmin.Contains(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Admin chi duoc tao tai khoan giang vien hoac truong bo mon.");
        }

        if (ApplicationRoles.CanBeAssignedCourses(model.Role))
        {
            if (model.DepartmentId is null)
            {
                ModelState.AddModelError(nameof(model.DepartmentId), "Vui long chon Khoa.");
            }

            if (model.CourseIds.Count == 0)
            {
                ModelState.AddModelError(nameof(model.CourseIds), "Vui long gan it nhat mot mon hoc cho tai khoan nay.");
            }
        }

        if (model.Role == ApplicationRoles.HeadLecturer && model.DepartmentId is int departmentId)
        {
            var conflictingHeadLecturer = await FindHeadLecturerInDepartmentAsync(departmentId);
            if (conflictingHeadLecturer is not null)
            {
                ModelState.AddModelError(
                    nameof(model.DepartmentId),
                    BuildHeadLecturerConflictMessage(conflictingHeadLecturer));
            }
        }

        if (!string.IsNullOrWhiteSpace(email) && await userManager.FindByEmailAsync(email) is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "Email nay da ton tai.");
        }

        if (ApplicationRoles.CanBeAssignedCourses(model.Role) && model.DepartmentId is not null && model.CourseIds.Count > 0)
        {
            try
            {
                await courseAccessService.ValidateDepartmentCourseIdsAsync(model.DepartmentId.Value, model.CourseIds);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.CourseIds), ex.Message);
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(model);
            return Page();
        }

        ApplicationUser? createdUser = null;
        await unitOfWork.BeginTransactionAsync();
        try
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DepartmentId = model.DepartmentId
            };

            var createResult = await userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                await unitOfWork.RollbackTransactionAsync();
                AddIdentityErrors(createResult);
                await LoadAsync(model);
                return Page();
            }

            var roleResult = await userManager.AddToRoleAsync(user, model.Role);
            if (!roleResult.Succeeded)
            {
                await unitOfWork.RollbackTransactionAsync();
                AddIdentityErrors(roleResult);
                await LoadAsync(model);
                return Page();
            }

            if (ApplicationRoles.CanBeAssignedCourses(model.Role))
            {
                await courseAssignmentService.ReplaceStaffAssignmentsAsync(
                    user.Id,
                    model.DepartmentId!.Value,
                    model.CourseIds);
            }

            if (model.Role == ApplicationRoles.HeadLecturer)
            {
                await departmentService.SetHeadLecturerOwnerAsync(model.DepartmentId!.Value, user.Id);
            }

            await unitOfWork.CommitTransactionAsync();
            createdUser = user;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create governed staff account for {Email}.", email);
            await unitOfWork.RollbackTransactionAsync();
            ModelState.AddModelError(string.Empty, "Khong the tao tai khoan. Vui long kiem tra phan cong va thu lai.");
            await LoadAsync(model);
            return Page();
        }

        await hubContext.Clients.Group(AdminHub.AdminsGroup).SendAsync(AdminHub.UserCreated, new
        {
            userId = createdUser!.Id,
            email,
            role = model.Role,
            roleDisplayName = ApplicationRoles.GetDisplayName(model.Role)
        });

        TempData["SuccessMessage"] = $"Da tao tai khoan {ApplicationRoles.GetDisplayName(model.Role).ToLowerInvariant()} cho {email}.";
        return Redirect("/Admin/Accounts");
    }

    public async Task<IActionResult> OnPostUpdateAssignmentsAsync(string userId, int? departmentId, int[] courseIds)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        var roles = await userManager.GetRolesAsync(user);
        if (!roles.Any(ApplicationRoles.CanBeAssignedCourses))
        {
            TempData["ErrorMessage"] = "Chi tai khoan giang vien hoac truong bo mon moi can gan mon hoc.";
            return Redirect("/Admin/Accounts");
        }

        if (departmentId is null)
        {
            TempData["ErrorMessage"] = "Vui long chon Khoa truoc khi gan mon hoc.";
            return Redirect("/Admin/Accounts");
        }

        if (roles.Contains(ApplicationRoles.HeadLecturer) && user.DepartmentId != departmentId)
        {
            var conflictingHeadLecturer = await FindHeadLecturerInDepartmentAsync(departmentId.Value, user.Id);
            if (conflictingHeadLecturer is not null)
            {
                TempData["ErrorMessage"] = BuildHeadLecturerConflictMessage(conflictingHeadLecturer);
                return Redirect("/Admin/Accounts");
            }
        }

        if (courseIds.Length == 0)
        {
            TempData["ErrorMessage"] = "Vui long gan it nhat mot mon hoc.";
            return Redirect("/Admin/Accounts");
        }

        try
        {
            await courseAccessService.ValidateDepartmentCourseIdsAsync(departmentId.Value, courseIds);
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return Redirect("/Admin/Accounts");
        }

        await unitOfWork.BeginTransactionAsync();
        try
        {
            user.DepartmentId = departmentId;
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                await unitOfWork.RollbackTransactionAsync();
                TempData["ErrorMessage"] = string.Join(" ", updateResult.Errors.Select(error => error.Description));
                return Redirect("/Admin/Accounts");
            }

            await courseAssignmentService.ReplaceStaffAssignmentsAsync(user.Id, departmentId.Value, courseIds);
            if (roles.Contains(ApplicationRoles.HeadLecturer))
            {
                await departmentService.SetHeadLecturerOwnerAsync(departmentId.Value, user.Id);
            }

            await unitOfWork.CommitTransactionAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update course assignments for user {UserId}.", userId);
            await unitOfWork.RollbackTransactionAsync();
            TempData["ErrorMessage"] = "Khong the cap nhat phan cong mon hoc. Vui long thu lai.";
            return Redirect("/Admin/Accounts");
        }

        var assignedCourseIds = await courseAssignmentService.GetAssignedCourseIdsAsync(user.Id);
        var assignedCourseIdSet = assignedCourseIds.ToHashSet();
        var assignedCourses = (await courseService.GetAllCoursesAsync())
            .Where(course => assignedCourseIdSet.Contains(course.Id))
            .OrderBy(course => course.Name)
            .Select(course => new
            {
                id = course.Id,
                name = course.Name,
                description = course.Description,
                documentCount = course.DocumentCount,
                createdAt = course.CreatedAt
            })
            .ToList();
        var assignmentPayload = new
        {
            userId = user.Id,
            email = user.Email,
            courseIds = assignedCourses.Select(course => course.id).ToArray(),
            courses = assignedCourses
        };

        await Task.WhenAll(
            hubContext.Clients.Group(AdminHub.AdminsGroup)
                .SendAsync(AdminHub.UserAssignmentsUpdated, assignmentPayload),
            hubContext.Clients.User(user.Id)
                .SendAsync(AdminHub.UserAssignmentsUpdated, assignmentPayload));

        TempData["SuccessMessage"] = $"Da cap nhat mon hoc cho {user.Email}.";
        return Redirect("/Admin/Accounts");
    }

    private async Task LoadAsync(CreateStaffAccountViewModel newAccount)
    {
        var viewModel = await BuildAccountsViewModelAsync(newAccount);
        NewAccount = viewModel.NewAccount;
        Accounts = viewModel.Accounts;
        Courses = viewModel.Courses;
        Departments = viewModel.Departments;
    }

    private async Task<AdminAccountsViewModel> BuildAccountsViewModelAsync(CreateStaffAccountViewModel newAccount)
    {
        var courses = (await courseService.GetAllCoursesAsync())
            .OrderBy(course => course.Name)
            .ToList();
        var departments = (await departmentService.GetAllAsync())
            .OrderBy(department => department.Name)
            .ToList();
        var managementRoles = new HashSet<string>(
            [ApplicationRoles.Admin, ApplicationRoles.HeadLecturer, ApplicationRoles.Lecturer],
            StringComparer.OrdinalIgnoreCase);
        var users = await userManager.Users
            .OrderBy(user => user.Email)
            .ToListAsync();
        var accounts = new List<StaffAccountListItemViewModel>();
        var assignmentLookup = await courseAssignmentService.GetAssignedCourseIdsByUserAsync(users.Select(user => user.Id));
        var courseLookup = courses.ToDictionary(course => course.Id);
        var departmentLookup = departments.ToDictionary(department => department.Id);

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var visibleRoles = roles
                .Where(managementRoles.Contains)
                .OrderBy(role => role == ApplicationRoles.Admin ? 0 : role == ApplicationRoles.HeadLecturer ? 1 : 2)
                .ToList();

            if (visibleRoles.Count == 0)
            {
                continue;
            }

            assignmentLookup.TryGetValue(user.Id, out var assignedCourseIds);
            assignedCourseIds ??= [];
            var invalidAssignmentCount = visibleRoles.Any(ApplicationRoles.CanBeAssignedCourses)
                ? assignedCourseIds.Count(courseId =>
                    !courseLookup.TryGetValue(courseId, out var course) ||
                    user.DepartmentId is null ||
                    course.DepartmentId is null ||
                    course.DepartmentId != user.DepartmentId)
                : 0;

            accounts.Add(new StaffAccountListItemViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? user.UserName ?? string.Empty,
                Roles = visibleRoles,
                AssignedCourseIds = assignedCourseIds,
                AssignedCourseNames = assignedCourseIds
                    .Where(courseLookup.ContainsKey)
                    .Select(courseId => courseLookup[courseId].Name)
                    .ToList(),
                DepartmentId = user.DepartmentId,
                DepartmentName = user.DepartmentId is int departmentId &&
                    departmentLookup.TryGetValue(departmentId, out var department)
                        ? department.Name
                        : null,
                InvalidAssignmentCount = invalidAssignmentCount
            });
        }

        return new AdminAccountsViewModel
        {
            NewAccount = newAccount,
            Accounts = accounts,
            Courses = courses,
            Departments = departments
        };
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
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
}
