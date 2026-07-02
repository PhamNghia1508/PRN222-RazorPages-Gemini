using FluentAssertions;
using ChatIndexModel = PRN222.Web.Pages.Chat.IndexModel;
using ChatSessionModel = PRN222.Web.Pages.Chat.SessionModel;
using DocumentDetailsModel = PRN222.Web.Pages.Document.DetailsModel;
using DocumentIndexModel = PRN222.Web.Pages.Document.IndexModel;
using DocumentUploadModel = PRN222.Web.Pages.Document.UploadModel;

namespace PRN222.Tests.Web;

public class RagRazorPagesTests
{
    [Theory]
    [InlineData("Document", "Index.cshtml")]
    [InlineData("Document", "Details.cshtml")]
    [InlineData("Document", "Upload.cshtml")]
    [InlineData("Chat", "Index.cshtml")]
    [InlineData("Chat", "Session.cshtml")]
    public void RagPages_ShouldExist(params string[] pathSegments)
    {
        var root = FindRepositoryRoot();

        File.Exists(Path.Combine([root, "src", "PRN222.Web", "Pages", .. pathSegments]))
            .Should().BeTrue();
    }

    [Fact]
    public void DocumentPages_ShouldExposeHandlers()
    {
        typeof(DocumentIndexModel).GetMethod(nameof(DocumentIndexModel.OnGetAsync)).Should().NotBeNull();
        typeof(DocumentDetailsModel).GetMethod(nameof(DocumentDetailsModel.OnGetAsync)).Should().NotBeNull();
        typeof(DocumentDetailsModel).GetMethod(nameof(DocumentDetailsModel.OnPostProcessAsync)).Should().NotBeNull();
        typeof(DocumentDetailsModel).GetMethod(nameof(DocumentDetailsModel.OnGetStatusAsync)).Should().NotBeNull();
        typeof(DocumentDetailsModel).GetMethod("OnPostArchiveAsync").Should().NotBeNull();
        typeof(DocumentDetailsModel).GetMethod("OnPostDeleteAsync").Should().BeNull();
        typeof(DocumentUploadModel).GetMethod(nameof(DocumentUploadModel.OnGetAsync)).Should().NotBeNull();
        typeof(DocumentUploadModel).GetMethod(nameof(DocumentUploadModel.OnPostAsync)).Should().NotBeNull();
    }

    [Fact]
    public void DocumentDetails_ShouldExposeAdminOnlyArchiveUi()
    {
        var root = FindRepositoryRoot();
        var pageModel = File.ReadAllText(Path.Combine(
            root, "src", "PRN222.Web", "Pages", "Document", "Details.cshtml.cs"));
        var view = File.ReadAllText(Path.Combine(
            root, "src", "PRN222.Web", "Pages", "Document", "Details.cshtml"));
        var workspace = File.ReadAllText(Path.Combine(
            root, "src", "PRN222.Web", "Pages", "Document", "_DocumentWorkspace.cshtml"));

        pageModel.Should().Contain("User.IsInRole(ApplicationRoles.Admin)");
        pageModel.Should().Contain("return Forbid();");
        pageModel.Should().Contain("User.FindFirstValue(ClaimTypes.NameIdentifier)");
        pageModel.Should().Contain("ArchiveDocumentAsync(id, archivedByUserId, archiveReason!)");
        pageModel.Should().Contain("string? archiveReason");
        pageModel.Should().NotContain("string? archivedByUserId");
        pageModel.Should().NotContain("DeleteDocumentAsync");
        view.Should().Contain("Tạm ẩn khỏi RAG");
        view.Should().Contain("Đã tạm ẩn");
        view.Should().Contain("Tài liệu sẽ bị tạm ẩn khỏi RAG nhưng vẫn được giữ lại để truy vết. Bạn có chắc chắn không?");
        view.Should().Contain("asp-page-handler=\"Archive\"");
        view.Should().Contain("name=\"archiveReason\"");
        view.Should().Contain("maxlength=\"1000\"");
        view.Should().Contain("required");
        view.Should().Contain("Chưa có dữ liệu audit");
        view.Should().NotContain("Restore");
        view.Should().NotContain("asp-page-handler=\"Delete\"");
        workspace.Should().Contain("data-document-id");
        workspace.Should().NotContain("Restore");
        workspace.Should().Contain("\"archived\" => \"Đã tạm ẩn\"");
    }

    [Fact]
    public void ChatPages_ShouldExposeHandlers()
    {
        typeof(ChatIndexModel).GetMethod(nameof(ChatIndexModel.OnGetAsync)).Should().NotBeNull();
        typeof(ChatSessionModel).GetMethod(nameof(ChatSessionModel.OnGetAsync)).Should().NotBeNull();
        typeof(ChatSessionModel).GetMethod(nameof(ChatSessionModel.OnGetNewAsync)).Should().NotBeNull();
        typeof(ChatSessionModel).GetMethod(nameof(ChatSessionModel.OnPostAskAsync)).Should().NotBeNull();
        typeof(ChatSessionModel).GetMethod(nameof(ChatSessionModel.OnGetCourseDocumentsAsync)).Should().NotBeNull();
        typeof(ChatSessionModel).GetMethod(nameof(ChatSessionModel.OnGetCitationImageAsync)).Should().NotBeNull();
        typeof(ChatSessionModel).GetMethod(nameof(ChatSessionModel.OnPostFeedbackAsync)).Should().NotBeNull();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
