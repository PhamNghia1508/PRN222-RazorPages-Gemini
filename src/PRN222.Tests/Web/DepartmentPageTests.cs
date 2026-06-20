using FluentAssertions;

namespace PRN222.Tests.Web;

public class DepartmentPageTests
{
    [Fact]
    public void DepartmentPage_ShouldOnlyAssignStaffUsersToDepartments()
    {
        var source = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Department", "Index.cshtml.cs");

        source.Should().Contain("ApplicationRoles.HeadLecturer");
        source.Should().Contain("ApplicationRoles.Lecturer");
        source.Should().Contain("Chi duoc phan cong tai khoan giang vien hoac truong bo mon vao Khoa.");
    }

    [Fact]
    public void DepartmentPage_ShouldReportRevokedAssignmentsWhenMovingUsersOrCourses()
    {
        var source = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Department", "Index.cshtml.cs");

        source.Should().Contain("BuildAssignmentMessage");
        source.Should().Contain("thu hoi {revoked} mon khong con dung Khoa");
        source.Should().Contain("NotifyRemovedCourseAssignmentUsersAsync");
        source.Should().Contain("GetAssignedUsersForCourseAsync");
    }

    [Fact]
    public void DepartmentPage_ShouldPreventMultipleHeadLecturersInOneDepartment()
    {
        var source = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Department", "Index.cshtml.cs");

        source.Should().Contain("FindHeadLecturerInDepartmentAsync(departmentId, user.Id)");
        source.Should().Contain("Moi Khoa chi duoc co mot truong bo mon.");
    }

    [Fact]
    public void DepartmentSchema_ShouldPersistSingleHeadLecturerOwner()
    {
        var department = ReadRepositoryFile("src", "PRN222.DAL", "Entities", "Department.cs");
        var configuration = ReadRepositoryFile("src", "PRN222.DAL", "Data", "Configurations", "DepartmentConfiguration.cs");

        department.Should().Contain("HeadLecturerUserId");
        department.Should().Contain("ApplicationUser? HeadLecturer");
        configuration.Should().Contain("HasForeignKey(d => d.HeadLecturerUserId)");
        configuration.Should().Contain("HasIndex(d => d.HeadLecturerUserId)");
        configuration.Should().Contain("IsUnique()");
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
