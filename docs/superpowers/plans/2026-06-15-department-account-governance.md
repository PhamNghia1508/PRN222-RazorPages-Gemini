# Department and Staff Governance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build one easy-to-use Admin workspace for staff and departments while enforcing that Lecturer and HeadLecturer course access requires both same-department membership and an explicit course assignment.

**Architecture:** Add a BLL `CourseAccessService` as the single source of effective staff course scope, harden assignment replacement and department moves with transactional reconciliation, then migrate every staff-scoped controller to the central service. Keep `AdminController` and `DepartmentController` separate but present them as tabs in one “Nhân sự & Khoa” workspace.

**Tech Stack:** ASP.NET Core MVC 8, ASP.NET Core Identity, Entity Framework Core 8, SQL Server, Razor views, Bootstrap, xUnit, Moq, FluentAssertions.

---

## File Structure

### New Files

- `src/PRN222.BLL/Services/Interfaces/ICourseAccessService.cs`
  - Defines effective staff course access, department-course validation, and stale-assignment reconciliation.
- `src/PRN222.BLL/Services/CourseAccessService.cs`
  - Implements the intersection of department membership and explicit assignment.
- `src/PRN222.Tests/Services/CourseAccessServiceTests.cs`
  - Covers same-department access, cross-department denial, missing-department denial, validation, and reconciliation.
- `src/PRN222.DAL/Migrations/<timestamp>_EnforceDepartmentCourseAssignments.cs`
  - Deletes legacy invalid assignments once during migration.
- `src/PRN222.MVC/Views/Shared/_StaffGovernanceTabs.cshtml`
  - Shared tab header for the two Admin governance routes.
- `src/PRN222.MVC/Models/Department/DepartmentManagementViewModel.cs`
  - Replaces loosely typed Department `ViewBag` data with selected-department,
    staff, course, assignment-impact, and warning state.

### Modified Files

- `src/PRN222.BLL/Services/Interfaces/ICourseAssignmentService.cs`
- `src/PRN222.BLL/Services/CourseAssignmentService.cs`
  - Replace staff assignments only after department validation; reject empty assignments.
- `src/PRN222.BLL/Services/Interfaces/IDepartmentService.cs`
- `src/PRN222.BLL/Services/DepartmentService.cs`
  - Move staff/courses transactionally and revoke stale assignments before commit.
- `src/PRN222.BLL/DTOs/CourseDto.cs`
- `src/PRN222.BLL/Services/CourseService.cs`
  - Expose course department metadata required by the governed Admin picker.
- `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs`
  - Register `ICourseAccessService`.
- `src/PRN222.MVC/Models/Admin/AdminAccountsViewModel.cs`
  - Add departments, selected department, per-account department/configuration status, and filters.
- `src/PRN222.MVC/Controllers/AdminController.cs`
  - Reject existing email, require department and valid courses, and update governance transactionally.
- `src/PRN222.MVC/Controllers/DepartmentController.cs`
  - Show revocation impact and use transactional department operations.
- `src/PRN222.MVC/Controllers/HomeController.cs`
- `src/PRN222.MVC/Controllers/CourseController.cs`
- `src/PRN222.MVC/Controllers/DocumentController.cs`
- `src/PRN222.MVC/Controllers/ChatController.cs`
- `src/PRN222.MVC/Controllers/KnowledgeController.cs`
- `src/PRN222.MVC/Controllers/EvaluationController.cs`
- `src/PRN222.MVC/Controllers/TestSetGeneratorController.cs`
- `src/PRN222.MVC/Controllers/FinetuneController.cs`
  - Replace assignment-only checks with effective staff course access.
- `src/PRN222.MVC/Views/Admin/Accounts.cshtml`
- `src/PRN222.MVC/Views/Department/Index.cshtml`
- `src/PRN222.MVC/Views/Shared/_Layout.cshtml`
- `src/PRN222.MVC/wwwroot/css/site.css`
  - Implement the combined workspace UX.
- `src/PRN222.Tests/BLL/DepartmentAuthorizationTests.cs`
- `src/PRN222.Tests/MVC/AdminControllerTests.cs`
- `src/PRN222.Tests/MVC/DepartmentControllerTests.cs`
- `src/PRN222.Tests/MVC/AuthorizationConfigurationTests.cs`
- `src/PRN222.Tests/MVC/HomeControllerTests.cs`
- `src/PRN222.Tests/MVC/TestSetGeneratorControllerTests.cs`
  - Regression coverage for the changed dependencies and business rules.

## Task 1: Add the Central Effective Course Access Service

**Files:**
- Create: `src/PRN222.BLL/Services/Interfaces/ICourseAccessService.cs`
- Create: `src/PRN222.BLL/Services/CourseAccessService.cs`
- Create: `src/PRN222.Tests/Services/CourseAccessServiceTests.cs`
- Modify: `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Write failing effective-access tests**

Create `CourseAccessServiceTests.cs` with tests using the existing
`AsAsyncQueryable()` helper:

```csharp
[Fact]
public async Task GetAccessibleStaffCourseIdsAsync_ReturnsOnlyAssignedCoursesInUsersDepartment()
{
    var users = new List<ApplicationUser>
    {
        new() { Id = "staff-1", DepartmentId = 10 }
    };
    var courses = new List<Course>
    {
        new() { Id = 1, DepartmentId = 10 },
        new() { Id = 2, DepartmentId = 20 },
        new() { Id = 3, DepartmentId = 10 }
    };
    var assignments = new List<ApplicationUserCourse>
    {
        new() { UserId = "staff-1", CourseId = 1 },
        new() { UserId = "staff-1", CourseId = 2 }
    };

    var service = CreateService(users, courses, assignments);

    var result = await service.GetAccessibleStaffCourseIdsAsync("staff-1");

    result.Should().BeEquivalentTo([1]);
}

[Theory]
[InlineData(null, 10)]
[InlineData(10, null)]
[InlineData(10, 20)]
public async Task CanStaffAccessCourseAsync_WhenDepartmentScopeIsInvalid_ReturnsFalse(
    int? userDepartmentId,
    int? courseDepartmentId)
{
    var service = CreateService(
        [new ApplicationUser { Id = "staff-1", DepartmentId = userDepartmentId }],
        [new Course { Id = 1, DepartmentId = courseDepartmentId }],
        [new ApplicationUserCourse { UserId = "staff-1", CourseId = 1 }]);

    var result = await service.CanStaffAccessCourseAsync("staff-1", 1);

    result.Should().BeFalse();
}
```

Also add tests:

```csharp
CanStaffAccessCourseAsync_WhenDepartmentsMatchAndAssignmentExists_ReturnsTrue
CanStaffAccessCourseAsync_WhenDepartmentsMatchButAssignmentIsMissing_ReturnsFalse
ValidateDepartmentCourseIdsAsync_WhenAnyCourseIsOutsideDepartment_ThrowsInvalidOperationException
ReconcileUserAssignmentsAsync_DeletesAssignmentsOutsideUsersDepartment
ReconcileCourseAssignmentsAsync_DeletesAssignmentsForUsersOutsideCoursesDepartment
```

- [ ] **Step 2: Run the new tests and verify RED**

Run:

```powershell
dotnet test src\PRN222.Tests\PRN222.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~CourseAccessServiceTests" --nologo
```

Expected: compilation fails because `ICourseAccessService` and
`CourseAccessService` do not exist.

- [ ] **Step 3: Define the interface**

Create:

```csharp
namespace PRN222.BLL.Services.Interfaces;

public interface ICourseAccessService
{
    Task<IReadOnlySet<int>> GetAccessibleStaffCourseIdsAsync(string userId);
    Task<bool> CanStaffAccessCourseAsync(string userId, int courseId);
    Task<IReadOnlyList<int>> ValidateDepartmentCourseIdsAsync(
        int departmentId,
        IEnumerable<int> courseIds);
    Task<int> ReconcileUserAssignmentsAsync(string userId);
    Task<int> ReconcileCourseAssignmentsAsync(int courseId);
}
```

- [ ] **Step 4: Implement the minimal service**

Implement `CourseAccessService` with repositories for `ApplicationUser`,
`Course`, and `ApplicationUserCourse`.

The effective-access query must use one explicit database join so the policy is
visible in one place and does not depend on navigation-property loading:

```csharp
var accessibleIds = await (
        from assignment in _assignmentRepository.GetQueryable().AsNoTracking()
        join user in _userRepository.GetQueryable().AsNoTracking()
            on assignment.UserId equals user.Id
        join course in _courseRepository.GetQueryable().AsNoTracking()
            on assignment.CourseId equals course.Id
        where user.Id == userId
              && user.DepartmentId != null
              && course.DepartmentId != null
              && user.DepartmentId == course.DepartmentId
        select course.Id)
    .ToListAsync();

return accessibleIds.ToHashSet();
```

Validation must:

```csharp
var requestedIds = courseIds.Where(id => id > 0).Distinct().ToList();
if (requestedIds.Count == 0)
    throw new InvalidOperationException("Vui lòng chọn ít nhất một môn học.");

var validIds = await _courseRepository.GetQueryable()
    .AsNoTracking()
    .Where(course => requestedIds.Contains(course.Id) &&
                     course.DepartmentId == departmentId)
    .Select(course => course.Id)
    .ToListAsync();

if (validIds.Count != requestedIds.Count)
    throw new InvalidOperationException("Tất cả môn được chọn phải thuộc đúng Khoa.");
```

Reconciliation methods delete invalid tracked `ApplicationUserCourse` rows and
return the number deleted. They do not call `SaveChangesAsync`; the orchestrating
service owns the transaction and save boundary.

- [ ] **Step 5: Register the service**

Add:

```csharp
services.AddScoped<ICourseAccessService, CourseAccessService>();
```

immediately after `ICourseAssignmentService`.

- [ ] **Step 6: Run tests and verify GREEN**

Run:

```powershell
dotnet test src\PRN222.Tests\PRN222.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~CourseAccessServiceTests" --nologo
```

Expected: all `CourseAccessServiceTests` pass.

- [ ] **Step 7: Commit**

```powershell
git add src/PRN222.BLL/Services/Interfaces/ICourseAccessService.cs `
  src/PRN222.BLL/Services/CourseAccessService.cs `
  src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs `
  src/PRN222.Tests/Services/CourseAccessServiceTests.cs
git commit -m "feat: centralize staff course access"
```

## Task 2: Harden Staff Assignment Replacement

**Files:**
- Modify: `src/PRN222.BLL/Services/Interfaces/ICourseAssignmentService.cs`
- Modify: `src/PRN222.BLL/Services/CourseAssignmentService.cs`
- Create: `src/PRN222.Tests/Services/CourseAssignmentServiceTests.cs`

- [ ] **Step 1: Write failing assignment validation tests**

Create tests:

```csharp
[Fact]
public async Task ReplaceStaffAssignmentsAsync_WhenCourseIsOutsideDepartment_ThrowsWithoutChangingAssignments()
{
    var service = CreateService(
        users: [new ApplicationUser { Id = "staff-1", DepartmentId = 10 }],
        courses: [new Course { Id = 2, DepartmentId = 20 }],
        assignments: [new ApplicationUserCourse { UserId = "staff-1", CourseId = 1 }]);

    var act = () => service.ReplaceStaffAssignmentsAsync("staff-1", 10, [2]);

    await act.Should().ThrowAsync<InvalidOperationException>()
        .WithMessage("*đúng Khoa*");
    _unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
}

[Fact]
public async Task ReplaceStaffAssignmentsAsync_WhenCourseIdsAreEmpty_Throws()
{
    var act = () => _service.ReplaceStaffAssignmentsAsync("staff-1", 10, []);

    await act.Should().ThrowAsync<InvalidOperationException>()
        .WithMessage("*ít nhất một môn học*");
}
```

Also test that a mismatched `ApplicationUser.DepartmentId` is rejected.

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet test src\PRN222.Tests\PRN222.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~CourseAssignmentServiceTests" --nologo
```

Expected: compilation fails because `ReplaceStaffAssignmentsAsync` is missing.

- [ ] **Step 3: Add the governed assignment API**

Add this method to the interface:

```csharp
Task ReplaceStaffAssignmentsAsync(
    string userId,
    int departmentId,
    IEnumerable<int> courseIds);
```

Keep the existing `AssignCoursesAsync` method unchanged as a temporary
compatibility method so this commit remains buildable. Task 4 migrates both
Admin call sites and then removes the unsafe method.

Inject `IRepository<ApplicationUser>` and `ICourseAccessService` into
`CourseAssignmentService`. Implement:

```csharp
var user = await _userRepository.GetQueryable()
    .AsNoTracking()
    .FirstOrDefaultAsync(item => item.Id == userId)
    ?? throw new InvalidOperationException("Không tìm thấy tài khoản.");

if (user.DepartmentId != departmentId)
    throw new InvalidOperationException("Tài khoản phải thuộc đúng Khoa.");

var validCourseIds = await _courseAccessService
    .ValidateDepartmentCourseIdsAsync(departmentId, courseIds);
```

Then replace existing rows with exactly `validCourseIds` and save once.

- [ ] **Step 4: Run tests and verify GREEN**

Run the focused tests, then:

```powershell
dotnet test src\PRN222.Tests\PRN222.Tests.csproj --no-restore --nologo
```

Expected: focused tests and the full test project pass. The temporary
`AssignCoursesAsync` compatibility method keeps existing Admin call sites
buildable until Task 4.

- [ ] **Step 5: Commit**

```powershell
git add src/PRN222.BLL/Services/Interfaces/ICourseAssignmentService.cs `
  src/PRN222.BLL/Services/CourseAssignmentService.cs `
  src/PRN222.Tests/Services/CourseAssignmentServiceTests.cs
git commit -m "feat: validate staff course assignments"
```

## Task 3: Make Department Changes Revoke Stale Assignments Transactionally

**Files:**
- Modify: `src/PRN222.BLL/Services/Interfaces/IDepartmentService.cs`
- Modify: `src/PRN222.BLL/Services/DepartmentService.cs`
- Modify: `src/PRN222.Tests/BLL/DepartmentAuthorizationTests.cs`

- [ ] **Step 1: Write failing transaction and reconciliation tests**

Add:

```csharp
[Fact]
public async Task AssignUserToDepartmentAsync_WhenDepartmentChanges_RevokesInvalidAssignmentsInTransaction()
{
    _courseAccessService
        .Setup(s => s.ReconcileUserAssignmentsAsync("staff-1"))
        .ReturnsAsync(2);

    var revoked = await _departmentService.AssignUserToDepartmentAsync("staff-1", 20);

    revoked.Should().Be(2);
    _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
    _courseAccessService.Verify(s => s.ReconcileUserAssignmentsAsync("staff-1"), Times.Once);
    _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
}

[Fact]
public async Task AssignCourseToDepartmentAsync_WhenReconciliationFails_RollsBack()
{
    _courseAccessService
        .Setup(s => s.ReconcileCourseAssignmentsAsync(5))
        .ThrowsAsync(new InvalidOperationException("reconcile failed"));

    var act = () => _departmentService.AssignCourseToDepartmentAsync(5, 20);

    await act.Should().ThrowAsync<InvalidOperationException>();
    _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(), Times.Once);
    _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(), Times.Never);
}
```

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet test src\PRN222.Tests\PRN222.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~DepartmentServiceTests" --nologo
```

Expected: tests fail because department assignment methods return `Task` and do
not use transactions or reconciliation.

- [ ] **Step 3: Change department service contracts**

Use:

```csharp
Task<int> AssignUserToDepartmentAsync(string userId, int? departmentId);
Task<int> AssignCourseToDepartmentAsync(int courseId, int? departmentId);
```

Inject `ICourseAccessService`.

- [ ] **Step 4: Implement transaction boundaries**

Each method must:

```csharp
await _unitOfWork.BeginTransactionAsync();
try
{
    entity.DepartmentId = departmentId;
    repository.Update(entity);
    await _unitOfWork.SaveChangesAsync();

    var revoked = await _courseAccessService.ReconcileUserAssignmentsAsync(userId);
    await _unitOfWork.SaveChangesAsync();
    await _unitOfWork.CommitTransactionAsync();
    return revoked;
}
catch
{
    await _unitOfWork.RollbackTransactionAsync();
    throw;
}
```

Use `ReconcileCourseAssignmentsAsync(courseId)` for courses. Validate a non-null
department exists before beginning the transaction.

- [ ] **Step 5: Run tests and verify GREEN**

Run focused tests. Expected: all `DepartmentServiceTests` pass.

- [ ] **Step 6: Commit**

```powershell
git add src/PRN222.BLL/Services/Interfaces/IDepartmentService.cs `
  src/PRN222.BLL/Services/DepartmentService.cs `
  src/PRN222.Tests/BLL/DepartmentAuthorizationTests.cs
git commit -m "feat: reconcile assignments on department changes"
```

## Task 4: Enforce Governance in Admin Account Workflows

**Files:**
- Modify: `src/PRN222.BLL/DTOs/CourseDto.cs`
- Modify: `src/PRN222.BLL/Services/CourseService.cs`
- Modify: `src/PRN222.MVC/Models/Admin/AdminAccountsViewModel.cs`
- Modify: `src/PRN222.MVC/Controllers/AdminController.cs`
- Create: `src/PRN222.Tests/Services/CourseServiceTests.cs`
- Modify: `src/PRN222.Tests/MVC/AdminControllerTests.cs`

- [ ] **Step 1: Write failing Admin workflow tests**

Add tests:

```csharp
[Fact]
public async Task CreateAccount_WhenEmailExists_ReturnsValidationErrorWithoutChangingRoles()
{
    _userManager.Setup(m => m.FindByEmailAsync("existing@example.com"))
        .ReturnsAsync(existingUser);

    var result = await controller.CreateAccount(new CreateStaffAccountViewModel
    {
        Email = "existing@example.com",
        Password = "Password123",
        Role = ApplicationRoles.Lecturer,
        DepartmentId = 10,
        CourseIds = [1]
    });

    result.Should().BeOfType<ViewResult>();
    controller.ModelState[nameof(CreateStaffAccountViewModel.Email)]!
        .Errors.Should().ContainSingle();
    _userManager.Verify(m => m.RemoveFromRolesAsync(
        It.IsAny<ApplicationUser>(),
        It.IsAny<IEnumerable<string>>()), Times.Never);
}

[Fact]
public async Task CreateAccount_WhenDepartmentIsMissing_DoesNotCreateUser()
{
    var result = await controller.CreateAccount(new CreateStaffAccountViewModel
    {
        Email = "lecturer@example.com",
        Password = "Password123",
        Role = ApplicationRoles.Lecturer,
        DepartmentId = null,
        CourseIds = [1]
    });

    result.Should().BeOfType<ViewResult>();
    _userManager.Verify(m => m.CreateAsync(
        It.IsAny<ApplicationUser>(),
        It.IsAny<string>()), Times.Never);
}
```

Also cover:

```text
GetAllCoursesAsync_IncludesDepartmentMetadata
CreateAccount_WhenCourseIsOutsideDepartment_DoesNotCreateUser
CreateAccount_WhenAssignmentFails_RollsBackAndDeletesNewUser
UpdateAssignments_WhenCourseIdsAreEmpty_RejectsRequest
UpdateAssignments_WhenCourseIsOutsideDepartment_RejectsRequest
```

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet test src\PRN222.Tests\PRN222.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~CourseServiceTests|FullyQualifiedName~AdminControllerTests" --nologo
```

Expected: compilation fails because `CourseDto` lacks department metadata, the
Admin model lacks `DepartmentId`, and the controller still mutates existing
users.

- [ ] **Step 3: Expose course department metadata**

Add optional properties to the existing positional record so current call sites
remain source-compatible:

```csharp
public record CourseDto(
    int Id,
    string Name,
    string? Description,
    int DocumentCount,
    DateTime CreatedAt)
{
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
}
```

In `CourseService.GetAllCoursesAsync` and `GetCourseByIdAsync`, include the
department navigation and set both properties:

```csharp
new CourseDto(course.Id, course.Name, course.Description, documentCount, course.CreatedAt)
{
    DepartmentId = course.DepartmentId,
    DepartmentName = course.Department?.Name
}
```

- [ ] **Step 4: Extend Admin view models**

Add:

```csharp
[Required(ErrorMessage = "Vui lòng chọn Khoa.")]
public int? DepartmentId { get; set; }
```

to `CreateStaffAccountViewModel`.

Add to `AdminAccountsViewModel`:

```csharp
public IReadOnlyList<DepartmentDto> Departments { get; init; } = [];
```

Add to `StaffAccountListItemViewModel`:

```csharp
public int? DepartmentId { get; init; }
public string? DepartmentName { get; init; }
public int InvalidAssignmentCount { get; init; }
public bool HasValidConfiguration =>
    !CanAssignCourses ||
    (DepartmentId.HasValue &&
     AssignedCourseIds.Count > 0 &&
     InvalidAssignmentCount == 0);
```

- [ ] **Step 5: Harden account creation**

Inject `IDepartmentService`, `ICourseAccessService`, and `IUnitOfWork`.

Before creating a user:

```csharp
if (model.DepartmentId is null)
    ModelState.AddModelError(nameof(model.DepartmentId), "Vui lòng chọn Khoa.");

if (await _userManager.FindByEmailAsync(email) is not null)
    ModelState.AddModelError(nameof(model.Email), "Email này đã tồn tại.");

await _courseAccessService.ValidateDepartmentCourseIdsAsync(
    model.DepartmentId.Value,
    model.CourseIds);
```

Create the user with `DepartmentId = model.DepartmentId`. Wrap user creation,
role assignment, and `ReplaceStaffAssignmentsAsync` in one `IUnitOfWork`
transaction. On failure, roll back and add a non-technical ModelState error.

- [ ] **Step 6: Harden assignment updates**

Change action signature:

```csharp
public async Task<IActionResult> UpdateAssignments(
    string userId,
    int? departmentId,
    int[] courseIds)
```

Reject missing department and empty courses. Update the user's department and
replace assignments inside one transaction. Return the revoked-count message
when a department change removed old assignments.

Migrate both Admin call sites to:

```csharp
await _courseAssignmentService.ReplaceStaffAssignmentsAsync(
    user.Id,
    departmentId.Value,
    courseIds);
```

After both call sites use the governed API, remove `AssignCoursesAsync` from
`ICourseAssignmentService` and `CourseAssignmentService`.

- [ ] **Step 7: Build account list data**

Load departments once. Populate department information, raw assigned-course
count, invalid-assignment count, and valid-configuration status for each staff
row. Compute `InvalidAssignmentCount` by comparing every raw assignment's
course department with the staff member's department. Keep incomplete seeded
staff visible.

- [ ] **Step 8: Run tests and verify GREEN**

Run focused Course and Admin tests, then all MVC tests:

```powershell
dotnet test src\PRN222.Tests\PRN222.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~CourseServiceTests|FullyQualifiedName~AdminControllerTests" --nologo
dotnet test src\PRN222.Tests\PRN222.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~PRN222.Tests.MVC" --nologo
dotnet test src\PRN222_Assignment1.sln --no-restore --nologo
rg -n "AssignCoursesAsync" src
```

Expected: all tests pass and the `rg` scan returns no matches.

- [ ] **Step 9: Commit**

```powershell
git add src/PRN222.BLL/DTOs/CourseDto.cs `
  src/PRN222.BLL/Services/CourseService.cs `
  src/PRN222.MVC/Models/Admin/AdminAccountsViewModel.cs `
  src/PRN222.MVC/Controllers/AdminController.cs `
  src/PRN222.Tests/Services/CourseServiceTests.cs `
  src/PRN222.Tests/MVC/AdminControllerTests.cs
git commit -m "feat: enforce governed staff account setup"
```

## Task 5: Adopt Effective Course Scope Across MVC Workflows

**Files:**
- Modify: `src/PRN222.MVC/Controllers/HomeController.cs`
- Modify: `src/PRN222.MVC/Controllers/CourseController.cs`
- Modify: `src/PRN222.MVC/Controllers/DocumentController.cs`
- Modify: `src/PRN222.MVC/Controllers/ChatController.cs`
- Modify: `src/PRN222.MVC/Controllers/KnowledgeController.cs`
- Modify: `src/PRN222.MVC/Controllers/EvaluationController.cs`
- Modify: `src/PRN222.MVC/Controllers/TestSetGeneratorController.cs`
- Modify: `src/PRN222.MVC/Controllers/FinetuneController.cs`
- Modify: `src/PRN222.Tests/MVC/HomeControllerTests.cs`
- Modify: `src/PRN222.Tests/MVC/TestSetGeneratorControllerTests.cs`
- Create: `src/PRN222.Tests/MVC/EffectiveCourseScopeControllerTests.cs`

- [ ] **Step 1: Write failing cross-department controller tests**

Add real controller-level tests for Document upload and Chat:

```csharp
[Fact]
public async Task DocumentUpload_WhenHeadLecturerHasStaleCrossDepartmentAssignment_ReturnsForbid()
{
    courseAccess.Setup(s => s.CanStaffAccessCourseAsync("head-1", 20))
        .ReturnsAsync(false);

    var result = await controller.Upload(file.Object, 20);

    result.Should().BeOfType<ForbidResult>();
    documentService.Verify(s => s.UploadDocumentAsync(
        It.IsAny<DocumentUploadDto>(),
        It.IsAny<Stream>()), Times.Never);
}

[Fact]
public async Task ChatAsk_WhenLecturerHasStaleCrossDepartmentAssignment_ReturnsForbid()
{
    courseAccess.Setup(s => s.CanStaffAccessCourseAsync("lecturer-1", 20))
        .ReturnsAsync(false);

    var result = await controller.Ask(new AskQuestionDto
    {
        CourseId = 20,
        Question = "Question"
    });

    result.Should().BeOfType<ForbidResult>();
}
```

Add these named regressions to the same file so every affected workflow has an
explicit denial check:

```text
HomeIndex_WhenStaffScopeExcludesCourse_DoesNotIncludeCourseInDashboard
CourseIndex_WhenStaffScopeExcludesCourse_DoesNotIncludeCourse
DocumentDetails_WhenStaffScopeExcludesCourse_ReturnsForbid
DocumentProcess_WhenStaffScopeExcludesCourse_ReturnsForbid
ChatCitationImage_WhenStaffScopeExcludesCourse_ReturnsForbid
KnowledgePropose_WhenStaffScopeExcludesCourse_ReturnsForbid
KnowledgeApprove_WhenStaffScopeExcludesCourse_ReturnsForbid
EvaluationCreateRun_WhenStaffScopeExcludesCourse_ReturnsForbid
TestSetGeneratorGenerate_WhenStaffScopeExcludesCourse_ReturnsForbid
FinetuneDataset_WhenStaffScopeExcludesCourse_ReturnsForbid
DocumentUpload_WhenAdminRequestsAnyCourse_DoesNotConsultCourseAccessService
ChatAsk_WhenStudentRequestsCourse_DoesNotConsultCourseAccessService
```

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet test src\PRN222.Tests\PRN222.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~EffectiveCourseScopeControllerTests" --nologo
```

Expected: compilation fails because controllers do not accept
`ICourseAccessService`.

- [ ] **Step 3: Replace assignment dependency in all staff-scoped controllers**

For each listed controller:

```csharp
private readonly ICourseAccessService _courseAccessService;
```

Replace:

```csharp
await _courseAssignmentService.GetAssignedCourseIdsAsync(userId)
```

with:

```csharp
await _courseAccessService.GetAccessibleStaffCourseIdsAsync(userId)
```

Replace staff `CanAccessCourseAsync` bodies with:

```csharp
if (!IsCourseScopedUser())
    return true;

var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
return !string.IsNullOrWhiteSpace(userId) &&
       await _courseAccessService.CanStaffAccessCourseAsync(userId, courseId);
```

Preserve explicit Admin bypass and Student behavior.

- [ ] **Step 4: Update controller construction tests**

Replace `Mock<ICourseAssignmentService>` with `Mock<ICourseAccessService>` in
affected tests. Add one assertion that Home dashboard receives only effective
course IDs.

- [ ] **Step 5: Run focused and full tests**

Run:

```powershell
dotnet test src\PRN222.Tests\PRN222.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~EffectiveCourseScopeControllerTests|FullyQualifiedName~HomeControllerTests|FullyQualifiedName~TestSetGeneratorControllerTests" `
  --nologo
dotnet test src\PRN222_Assignment1.sln --no-restore --nologo
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/PRN222.MVC/Controllers `
  src/PRN222.Tests/MVC/EffectiveCourseScopeControllerTests.cs `
  src/PRN222.Tests/MVC/HomeControllerTests.cs `
  src/PRN222.Tests/MVC/TestSetGeneratorControllerTests.cs
git commit -m "fix: enforce department course scope across workflows"
```

## Task 6: Update Department Controller Workflows

**Files:**
- Modify: `src/PRN222.MVC/Controllers/DepartmentController.cs`
- Create: `src/PRN222.MVC/Models/Department/DepartmentManagementViewModel.cs`
- Modify: `src/PRN222.Tests/MVC/DepartmentControllerTests.cs`
- Modify: `src/PRN222.Tests/MVC/AuthorizationConfigurationTests.cs`

- [ ] **Step 1: Write failing revoked-assignment feedback tests**

Add:

```csharp
[Fact]
public async Task AssignUser_WhenMoveRevokesAssignments_ShowsRevocationCount()
{
    departmentService.Setup(s => s.GetByIdAsync(20))
        .ReturnsAsync(new DepartmentDto { Id = 20, Name = "Khoa B" });
    departmentService.Setup(s => s.AssignUserToDepartmentAsync("staff-1", 20))
        .ReturnsAsync(3);

    await controller.AssignUser(20, "staff-1");

    controller.TempData["Success"].Should().Be(
        "Đã phân công \"staff@example.com\" vào Khoa và thu hồi 3 môn không còn đúng Khoa.");
}
```

Also cover course moves and removal from department.

- [ ] **Step 2: Run tests and verify RED**

Expected: mocks cannot return an integer because the current service methods
return `Task`.

- [ ] **Step 3: Use transactional service results**

Capture the returned revoked count in `AssignUser`, `AssignCourse`, and
`RemoveUser`. Use exact success messages that state the impact. Preserve role
validation, missing-entity validation, antiforgery, and logging.

- [ ] **Step 4: Replace Department ViewBag data with a typed model**

Create:

```csharp
public sealed class DepartmentManagementViewModel
{
    public IReadOnlyList<DepartmentDto> Departments { get; init; } = [];
    public int? SelectedDepartmentId { get; init; }
    public DepartmentDto? SelectedDepartment { get; init; }
    public IReadOnlyList<DepartmentStaffItemViewModel> Staff { get; init; } = [];
    public IReadOnlyList<DepartmentCourseItemViewModel> Courses { get; init; } = [];
}

public sealed class DepartmentStaffItemViewModel
{
    public required string UserId { get; init; }
    public required string Email { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int AssignedCourseCount { get; init; }
    public int InvalidAssignmentCount { get; init; }
}

public sealed class DepartmentCourseItemViewModel
{
    public int CourseId { get; init; }
    public required string Name { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int AssignedStaffCount { get; init; }
    public int InvalidAssignmentCount { get; init; }
}
```

Change `Index` to accept `int? departmentId`, select the requested department
or the first department, and return `DepartmentManagementViewModel`. Load the
staff, course, and assignment rows in bounded queries and remove
`PopulateViewBagAsync`.

- [ ] **Step 5: Strengthen authorization configuration tests**

Ensure `AssignUser`, `AssignCourse`, and `RemoveUser` remain POST plus
`ValidateAntiForgeryToken`.

- [ ] **Step 6: Run tests and commit**

```powershell
dotnet test src\PRN222.Tests\PRN222.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~DepartmentControllerTests|FullyQualifiedName~AuthorizationConfigurationTests" `
  --nologo
git add src/PRN222.MVC/Controllers/DepartmentController.cs `
  src/PRN222.MVC/Models/Department/DepartmentManagementViewModel.cs `
  src/PRN222.Tests/MVC/DepartmentControllerTests.cs `
  src/PRN222.Tests/MVC/AuthorizationConfigurationTests.cs
git commit -m "feat: report revoked assignments on department moves"
```

## Task 7: Build the Combined “Nhân sự & Khoa” Workspace

**Files:**
- Create: `src/PRN222.MVC/Views/Shared/_StaffGovernanceTabs.cshtml`
- Modify: `src/PRN222.MVC/Views/Shared/_Layout.cshtml`
- Modify: `src/PRN222.MVC/Views/Admin/Accounts.cshtml`
- Modify: `src/PRN222.MVC/Views/Department/Index.cshtml`
- Modify: `src/PRN222.MVC/wwwroot/css/site.css`
- Modify: `src/PRN222.Tests/MVC/AuthorizationConfigurationTests.cs`

- [ ] **Step 1: Add a failing static Razor regression test**

Add:

```csharp
[Fact]
public void Layout_ShouldExposeSingleStaffGovernanceNavigationEntry()
{
    var layout = File.ReadAllText(FindRepositoryFile(
        "src", "PRN222.MVC", "Views", "Shared", "_Layout.cshtml"));

    layout.Should().Contain("<span>Nhân sự &amp; Khoa</span>");
    layout.Should().NotContain("<span>Tài khoản</span>");
    layout.Should().NotContain("<span>Quản lý Khoa</span>");
}
```

- [ ] **Step 2: Run test and verify RED**

Run the single test. Expected: fails because both old navigation entries exist.

- [ ] **Step 3: Create the shared tab partial**

Create a two-link tab strip:

```cshtml
@{
    var activeController = ViewContext.RouteData.Values["controller"]?.ToString();
}
<nav class="governance-tabs" aria-label="Nhân sự và Khoa">
    <a asp-controller="Admin" asp-action="Accounts"
       class="@(activeController == "Admin" ? "active" : null)">
        <i class="bi bi-people"></i>
        <span>Tài khoản &amp; phân công</span>
    </a>
    <a asp-controller="Department" asp-action="Index"
       class="@(activeController == "Department" ? "active" : null)">
        <i class="bi bi-buildings"></i>
        <span>Khoa &amp; môn học</span>
    </a>
</nav>
```

- [ ] **Step 4: Replace sidebar navigation**

Use one Admin-only link to `Admin/Accounts`, active for either `Admin` or
`Department`, labelled `Nhân sự & Khoa`.

- [ ] **Step 5: Redesign account creation and list**

In `Accounts.cshtml`:

- Render the shared tabs under a compact page header.
- Use a three-step visual form section: role, department, courses.
- Add `DepartmentId` select.
- Render course checkboxes with `data-department-id`.
- Disable and hide courses outside the selected department.
- Clear checked courses when department changes.
- Prevent submit when no visible checked course exists.
- Render search/filter toolbar for role, department, and configuration state.
- Show department, assigned-course count, and an incomplete warning on each
  row.
- Expand one account editor at a time using accessible Bootstrap collapse.
- Confirm department changes before submitting.

Use this client filter:

```javascript
function syncCourses(container, departmentId) {
    container.querySelectorAll('[data-department-id]').forEach(option => {
        const matches = option.dataset.departmentId === departmentId;
        option.hidden = !matches;
        const input = option.querySelector('input[type="checkbox"]');
        if (!matches) input.checked = false;
        input.disabled = !matches;
    });
}
```

- [ ] **Step 6: Redesign department page**

In `Department/Index.cshtml`:

- Render the shared tabs.
- Replace repeated full-width department panels with a master-detail layout.
- Left column: searchable department list with counts.
- Right column: selected department staff and course sections.
- Use clear move buttons and confirmation text stating stale assignments will
  be revoked.
- Remove inline styles and inline `onsubmit`; attach confirmations through
  `data-confirm-message` in one script block.

- [ ] **Step 7: Add scoped CSS**

Add classes:

```text
governance-tabs
governance-toolbar
governance-form-steps
governance-account-list
governance-account-row
governance-status
governance-master-detail
governance-department-list
governance-detail
```

Use existing color tokens and radii. Provide responsive layouts at 1024px and
768px. Do not add new gradients or nested cards.

- [ ] **Step 8: Run Razor/build verification**

Run:

```powershell
dotnet test src\PRN222.Tests\PRN222.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~AuthorizationConfigurationTests" --nologo
dotnet build src\PRN222_Assignment1.sln --no-restore --nologo
```

Expected: static regression passes and Razor views compile.

- [ ] **Step 9: Commit**

```powershell
git add src/PRN222.MVC/Views/Shared/_StaffGovernanceTabs.cshtml `
  src/PRN222.MVC/Views/Shared/_Layout.cshtml `
  src/PRN222.MVC/Views/Admin/Accounts.cshtml `
  src/PRN222.MVC/Views/Department/Index.cshtml `
  src/PRN222.MVC/wwwroot/css/site.css `
  src/PRN222.Tests/MVC/AuthorizationConfigurationTests.cs
git commit -m "feat: unify staff and department administration"
```

## Task 8: Add One-Time Legacy Assignment Cleanup

**Files:**
- Create: `src/PRN222.DAL/Migrations/<timestamp>_EnforceDepartmentCourseAssignments.cs`
- Create: `src/PRN222.DAL/Migrations/<timestamp>_EnforceDepartmentCourseAssignments.Designer.cs`
- Verify: `src/PRN222.DAL/Migrations/ChatbotDbContextModelSnapshot.cs`

- [ ] **Step 1: Generate an empty migration**

Run:

```powershell
dotnet ef migrations add EnforceDepartmentCourseAssignments `
  --project src\PRN222.DAL\PRN222.DAL.csproj `
  --startup-project src\PRN222.MVC\PRN222.MVC.csproj
```

Expected: migration and designer files are created; snapshot has no semantic
model changes.

- [ ] **Step 2: Add deterministic cleanup SQL**

In `Up`:

```csharp
migrationBuilder.Sql("""
    DELETE assignment
    FROM ApplicationUserCourses AS assignment
    INNER JOIN AspNetUsers AS staff ON staff.Id = assignment.UserId
    INNER JOIN Courses AS course ON course.Id = assignment.CourseId
    WHERE staff.DepartmentId IS NULL
       OR course.DepartmentId IS NULL
       OR staff.DepartmentId <> course.DepartmentId;
    """);
```

Leave `Down` empty because deleted invalid authorization rows must not be
recreated.

- [ ] **Step 3: Verify migration SQL and model**

Run:

```powershell
dotnet ef migrations script `
  --project src\PRN222.DAL\PRN222.DAL.csproj `
  --startup-project src\PRN222.MVC\PRN222.MVC.csproj `
  --idempotent
dotnet build src\PRN222_Assignment1.sln --no-restore --nologo
```

Expected: generated script contains the cleanup `DELETE`; build passes.

- [ ] **Step 4: Commit**

```powershell
git add src/PRN222.DAL/Migrations
git commit -m "db: remove invalid cross-department assignments"
```

## Task 9: Full Verification and Business Logic Audit

**Files:**
- Verify: all files changed by Tasks 1-8
- Update if implementation evidence changes: `docs/srs/source-evidence.md`
- Update if implementation requirements change: `docs/srs/software-requirements-specification.md`

- [ ] **Step 1: Run full automated verification**

Run:

```powershell
dotnet test src\PRN222_Assignment1.sln --configuration Debug --nologo
git diff --check
```

Expected: all tests pass; no whitespace errors.

- [ ] **Step 2: Run static business-logic scans**

Run:

```powershell
rg -n "GetAssignedCourseIdsAsync" src/PRN222.MVC/Controllers
rg -n "AssignCoursesAsync" src
rg -n "Tài khoản</span>|Quản lý Khoa</span>" src/PRN222.MVC/Views/Shared/_Layout.cshtml
```

Expected:

- No staff-scoped controller calls `GetAssignedCourseIdsAsync`.
- No references to removed `AssignCoursesAsync`.
- No separate old Admin navigation entries.

- [ ] **Step 3: Run application and browser checks**

Run:

```powershell
dotnet run --project src\PRN222.MVC\PRN222.MVC.csproj
```

Verify as Admin:

```text
Open Nhân sự & Khoa.
Create form requires department before courses.
Only courses in selected department are selectable.
Existing email shows an error and changes nothing.
Moving staff/course shows confirmation and revoked count.
Incomplete seeded staff is clearly marked.
```

Verify as HeadLecturer:

```text
Assigned same-department course is visible.
Assigned cross-department legacy course is not visible.
Upload/process/model operations return Forbid outside effective scope.
```

Verify as Lecturer:

```text
Only same-department assigned courses are visible.
Chat and document metadata work inside effective scope.
Outside-scope course and citation image return Forbid.
```

- [ ] **Step 4: Check responsive and accessibility behavior**

Use browser screenshots at 1440x900, 1024x768, 768x1024, and 390x844.

Verify:

- No overlapping tabs, forms, filters, or account rows.
- All controls are reachable by keyboard.
- Collapse triggers expose correct `aria-expanded`.
- Warning and success states use text plus icons, not color alone.
- Long emails and course names wrap without resizing controls.

- [ ] **Step 5: Update SRS evidence if needed**

Update the code-based SRS source only for confirmed implemented behavior:

```text
effective staff scope = same department plus explicit assignment
transactional stale-assignment revocation
combined Admin governance workspace
```

Rebuild and check the DOCX:

```powershell
& "C:\Users\ADMIN\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" `
  tools\srs\validate_srs.py docs\srs\requirements.json
& "C:\Users\ADMIN\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" `
  tools\srs\build_srs.py --check-only
```

- [ ] **Step 6: Final review and commit**

Review:

```powershell
git status --short
git diff --stat
git diff --check
```

Do not stage the existing screenshot files, ad hoc Python scripts, or
`src/PRN222.MVC/wwwroot/mockup.html`.

Commit only final verified corrections:

```powershell
git add src docs/srs tools/srs
git commit -m "docs: align SRS with department governance"
```
