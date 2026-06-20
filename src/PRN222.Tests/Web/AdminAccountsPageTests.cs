using FluentAssertions;

namespace PRN222.Tests.Web;

public class AdminAccountsPageTests
{
    [Fact]
    public void AccountsPage_ShouldRejectDuplicateEmailAndInvalidStaffConfiguration()
    {
        var source = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Admin", "Accounts.cshtml.cs");

        source.Should().Contain("FindByEmailAsync(email)");
        source.Should().Contain("Email nay da ton tai.");
        source.Should().Contain("Admin chi duoc tao tai khoan giang vien hoac truong bo mon.");
        source.Should().Contain("Vui long chon Khoa.");
        source.Should().Contain("Vui long gan it nhat mot mon hoc cho tai khoan nay.");
    }

    [Fact]
    public void AccountsPage_ShouldEnforceDepartmentScopedCourseAssignmentsAndSingleHeadLecturer()
    {
        var source = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Admin", "Accounts.cshtml.cs");

        source.Should().Contain("ValidateDepartmentCourseIdsAsync(model.DepartmentId.Value, model.CourseIds)");
        source.Should().Contain("ValidateDepartmentCourseIdsAsync(departmentId.Value, courseIds)");
        source.Should().Contain("FindHeadLecturerInDepartmentAsync(departmentId)");
        source.Should().Contain("FindHeadLecturerInDepartmentAsync(departmentId.Value, user.Id)");
        source.Should().Contain("Moi Khoa chi duoc co mot truong bo mon.");
    }

    [Fact]
    public void AccountsPage_ShouldUseTransactionRollbackForCreateAndAssignmentUpdates()
    {
        var source = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Admin", "Accounts.cshtml.cs");

        source.Should().Contain("await unitOfWork.BeginTransactionAsync();");
        source.Should().Contain("await unitOfWork.RollbackTransactionAsync();");
        source.Should().Contain("await unitOfWork.CommitTransactionAsync();");
        source.Should().Contain("ReplaceStaffAssignmentsAsync");
    }

    private static string ReadRepositoryFile(params string[] pathSegments) =>
        File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. pathSegments]));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
