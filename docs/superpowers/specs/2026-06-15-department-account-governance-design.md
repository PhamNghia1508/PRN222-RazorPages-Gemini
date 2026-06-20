# Department and Staff Governance Design

## Status

Approved for implementation on 2026-06-15.

## Goal

Make account, department, and course assignment management easy for Admin while
enforcing one consistent business rule across every server endpoint:

> A Lecturer or HeadLecturer may access a course only when the staff member and
> course belong to the same department and an explicit course assignment exists.

HeadLecturer receives upload, document-processing, model-operation, and
knowledge-approval capabilities only inside that valid course scope. Lecturer
receives the existing read and chat capabilities only inside that valid course
scope.

## Confirmed Decisions

- Use one Admin workspace named **Nhân sự & Khoa**.
- Keep `AdminController` and `DepartmentController` as separate ownership
  boundaries, but present their pages as tabs in the same workspace.
- Both Lecturer and HeadLecturer must belong to a department.
- Both Lecturer and HeadLecturer must have at least one explicit course
  assignment.
- Staff may only be assigned courses in their own department.
- Moving a staff member or course to another department automatically removes
  invalid course assignments after an explicit confirmation warning.
- Removing a staff member from a department removes all course assignments.
- Admin remains unrestricted by department and assignment.
- Student course behavior remains unchanged in this scope.

## Current Problems

The current implementation stores both `ApplicationUser.DepartmentId` and
`Course.DepartmentId`, but most course authorization checks use only
`ApplicationUserCourse`.

This allows invalid states:

- Admin can assign a course in Department B to staff in Department A.
- A HeadLecturer in Department A can upload to that Department B course when an
  invalid assignment exists.
- Moving staff or courses between departments leaves stale course assignments.
- Removing staff from a department leaves their course assignments active.
- Account creation requires a course but does not require or set a department.
- Updating assignments can leave staff with no course.
- Creating an account with an existing email can silently replace that user's
  roles instead of reporting a conflict.
- Development-seeded Lecturer and HeadLecturer accounts are created without a
  department or course assignment.
- Multiple controllers duplicate course-scope logic, increasing the chance that
  one workflow enforces a different policy from another.

## Authorization Model

### Effective Staff Course Access

For Lecturer and HeadLecturer, a course is accessible only when all conditions
are true:

```text
user.DepartmentId is not null
course.DepartmentId is not null
user.DepartmentId == course.DepartmentId
ApplicationUserCourse(user.Id, course.Id) exists
```

UI filtering is supplementary. Server-side checks are authoritative.

### Role Capabilities

| Role | Course scope | Capabilities |
|---|---|---|
| Admin | All courses | Full management and model operations |
| HeadLecturer | Same department plus explicit assignment | Read, chat, upload, process documents, model operations, knowledge approval |
| Lecturer | Same department plus explicit assignment | Read metadata and permitted document details, chat, propose knowledge correction |
| Student | Existing behavior unchanged | Chat behavior currently implemented by the application |

### Invalid Assignment Handling

- Invalid requested assignments are rejected; they are not silently ignored.
- An empty assignment set is rejected for Lecturer and HeadLecturer.
- Moving a user to a different department removes assignments outside the new
  department in the same transaction.
- Moving a course to a different department removes assignments belonging to
  staff outside the new department in the same transaction.
- Removing a user from a department removes all their course assignments in the
  same transaction.

## Admin UX

### Navigation

Replace the separate Admin sidebar entries `Tài khoản` and `Quản lý Khoa` with
one entry:

```text
Nhân sự & Khoa
```

Both Admin and Department pages show a shared two-tab workspace header:

- **Tài khoản & phân công**
- **Khoa & môn học**

The controllers and routes remain separate so responsibilities stay clear.

### Account Creation Flow

The form uses a clear sequence:

1. Enter email and password.
2. Select Lecturer or HeadLecturer.
3. Select a required department.
4. Select at least one course from that department.
5. Review a short capability summary and create the account.

Changing the selected department clears any selected courses from the previous
department. The course picker shows only courses in the selected department.
The server repeats all validation and does not trust client filtering.

If the email already exists, account creation fails with a clear message. It
must not alter the existing user's roles or assignments.

### Account List

The account list is optimized for scanning and repeated administration:

- Search by email.
- Filter by role, department, and configuration status.
- Show role, department, assigned-course count, and configuration status on
  every row.
- Expand one row at a time to edit department and course assignments.
- Mark invalid or incomplete legacy configurations with a visible warning.
- Disable save until one department and at least one valid course are selected.
- Show a confirmation when changing department will revoke assignments.

Admin accounts remain visible but do not show department/course assignment
controls.

Development-seeded Lecturer and HeadLecturer accounts do not bypass governance.
When they have no department or assignment, the list marks them as incomplete
and they receive no staff course access until Admin completes their
configuration. The system must not automatically assign an arbitrary first
department or course.

### Department and Course Tab

The department page uses a department-focused master-detail layout:

- Department list with staff count, course count, and configuration warnings.
- Selected department detail with separate staff and course sections.
- Assigning staff or courses to the selected department shows the impact before
  save.
- Moving a staff member or course from another department requires
  confirmation and states how many assignments will be revoked.
- Empty states explain the next action instead of showing blank tables.

## Backend Architecture

### Central Course Access Service

Add `ICourseAccessService` in the BLL as the single source of effective
course-scope decisions.

Responsibilities:

- Return effective accessible course IDs for staff by intersecting department
  membership and explicit assignments.
- Check access to one course.
- Validate that requested assignments belong to a department.
- Reconcile stale assignments after department changes.

The role decision remains explicit at the MVC boundary:

- Admin bypasses staff scope.
- Student keeps existing behavior.
- Lecturer and HeadLecturer use `ICourseAccessService`.

This avoids making the BLL depend directly on ASP.NET Identity claims while
still removing duplicated department-plus-assignment queries from controllers.

Proposed interface:

```csharp
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

Validation methods throw a business exception when any requested course is
missing or belongs to another department. Reconciliation methods return the
number of revoked assignments for UI feedback and audit logging.

### Service Ownership

- `AdminController` orchestrates account creation and assignment updates.
- `DepartmentController` orchestrates department membership changes.
- `CourseAssignmentService` performs explicit assignment replacement.
- `CourseAccessService` evaluates effective access and removes invalid
  assignments.
- `DepartmentService` transactionally changes department relationships and
  invokes assignment reconciliation before commit.

Multi-step changes use the existing Unit of Work transaction support so
department changes and assignment revocation either both succeed or both roll
back.

Account creation also runs as one transaction across Identity user creation,
role assignment, department assignment, and course assignment. A failure after
Identity user creation rolls back the new account instead of leaving an
incomplete staff user.

### Controllers Using Central Access

Replace staff assignment-only checks with central effective access in:

- `HomeController`
- `CourseController`
- `DocumentController`
- `ChatController`
- `EvaluationController`
- `TestSetGeneratorController`
- `FinetuneController`
- `KnowledgeController`

Admin and Student exceptions remain explicit and test-covered.

## Request Validation and Errors

- Missing department: show a validation message beside the department field.
- No selected courses: show a validation message beside the course picker.
- Cross-department course IDs: reject the request and preserve submitted form
  values.
- Existing email: reject account creation without changing the user.
- Missing user/course/department: return the established NotFound or form-error
  behavior depending on the workflow.
- Concurrent department change: revalidate immediately before persistence.
- Transaction failure: roll back both the relationship change and revoked
  assignments, log the exception, and show a non-technical error.

## Data and Migration Impact

No new table is required. Existing fields and relationships are sufficient:

- `ApplicationUser.DepartmentId`
- `Course.DepartmentId`
- `ApplicationUserCourse`

The implementation includes a one-time migration cleanup that deletes existing
assignments when the user or course has no department or their departments do
not match. The cleanup must not assign missing departments automatically.
Runtime department changes continue to reconcile invalid assignments
transactionally.

## Testing Strategy

### Unit and Service Tests

- Effective access requires both same department and assignment.
- Same department without assignment is denied.
- Assignment across different departments is denied.
- Missing user or course department is denied.
- Admin bypass remains unaffected at controller level.
- Student behavior remains unchanged.
- Assignment validation rejects any cross-department course ID.
- Empty assignment replacement for staff is rejected.
- Moving a user revokes stale assignments.
- Moving a course revokes stale assignments.
- Removing a user from a department revokes all assignments.
- Reconciliation and department change roll back together on failure.

### Controller Tests

- Account creation requires department and valid course IDs.
- Existing email does not change roles or assignments.
- Development-seeded incomplete staff have no course access and are visibly
  marked for Admin configuration.
- Account update rejects invalid and empty assignments.
- Upload is forbidden for a HeadLecturer with an assignment from another
  department.
- Staff cannot access course, document, chat, benchmark, test-set, fine-tune,
  or knowledge operations outside effective scope.
- Department change actions require antiforgery tokens.

### UI and Browser Checks

- Tab navigation clearly shows the active area.
- Course options change when department changes.
- Cross-department courses cannot be selected.
- Confirmation appears before assignment revocation.
- Invalid legacy rows show warnings.
- Forms and expanded rows work by keyboard.
- Layout remains usable at mobile, tablet, and desktop widths.

## Out of Scope

- Multiple departments per staff member.
- Department-level subroles beyond Lecturer and HeadLecturer.
- Student enrollment and student-specific course assignment.
- Department deletion workflow.
- Full audit-history UI for all Admin assignment changes.
- Redesign of unrelated Chat, Benchmark, or document screens.

## Acceptance Criteria

- Admin can create staff only with one department and at least one course in
  that department.
- Admin cannot create or update a cross-department assignment through UI or a
  forged request.
- HeadLecturer cannot upload or process documents outside effective scope.
- Lecturer cannot view or chat with courses outside effective scope.
- Changing department membership revokes stale assignments transactionally.
- Admin can understand each staff member's role, department, assigned courses,
  and configuration status without switching between unrelated screens.
- Existing automated tests remain green and new regression tests cover every
  confirmed invalid-state scenario.
