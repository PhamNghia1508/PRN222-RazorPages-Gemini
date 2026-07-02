using FluentAssertions;
using PRN222.BLL.DTOs;
using PRN222.Web.Models.Dashboard;
using Xunit;

namespace PRN222.Tests.Web;

public class OverviewDashboardFactoryTests
{
    [Fact]
    public void Build_ShouldCalculateMetricsAndPipelineReadiness()
    {
        var documents = new[]
        {
            new DocumentDto(1, "a.pdf", "A.pdf", "application/pdf", 1024, 4, "Indexed", "PRN222", 1, new DateTime(2026, 5, 28, 10, 0, 0)),
            new DocumentDto(2, "b.docx", "B.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", 2048, 0, "Uploaded", "PRN222", 1, new DateTime(2026, 5, 28, 11, 0, 0)),
            new DocumentDto(3, "c.pdf", "C.pdf", "application/pdf", 4096, 0, "Failed", "SWT301", 2, new DateTime(2026, 5, 28, 12, 0, 0))
        };
        var courses = new[]
        {
            new CourseDto(1, "PRN222", null, 2, new DateTime(2026, 5, 1)),
            new CourseDto(2, "SWT301", null, 1, new DateTime(2026, 5, 2))
        };

        var result = OverviewDashboardFactory.Build(documents, courses);

        result.TotalDocuments.Should().Be(3);
        result.TotalCourses.Should().Be(2);
        result.IndexedChunks.Should().Be(4);
        result.IndexedDocuments.Should().Be(1);
        result.FailedDocuments.Should().Be(1);
        result.EvaluationStatus.Should().Be("Sẵn sàng");
        result.EvaluationStatus.Should().NotContain("Sáºµn sÃ");
        result.RecentDocuments.Should().HaveCount(3);
        result.RecentDocuments.First().OriginalFileName.Should().Be("C.pdf");
        result.PipelineSteps.Should().ContainSingle(s => s.Key == "embed" && s.Status == "Ready");
        result.NextActions.Should().Contain(a => a.Label == "Kiểm tra tài liệu lỗi");
        result.NextActions.Should().ContainSingle(a => a.Label == "Thử Chat RAG" && a.IsEnabled);
        result.NextActions.Should().ContainSingle(a => a.Label == "Chuẩn bị benchmark" && a.IsEnabled);
    }

    [Fact]
    public void Build_ShouldShowUploadAction_WhenKnowledgeBaseIsEmpty()
    {
        var result = OverviewDashboardFactory.Build(Array.Empty<DocumentDto>(), Array.Empty<CourseDto>());

        result.TotalDocuments.Should().Be(0);
        result.PipelineSteps.Should().ContainSingle(s => s.Key == "upload" && s.Status == "Needs input");
        result.NextActions.Should().ContainSingle(a => a.Label == "Nạp tài liệu đầu tiên" && a.IsEnabled);
    }

    [Fact]
    public void Build_FromDashboardSummaries_ShouldUseValidVietnameseEvaluationStatus()
    {
        var documents = new DocumentDashboardSummaryDto(
            TotalDocuments: 1,
            IndexedDocuments: 1,
            FailedDocuments: 0,
            ProcessingDocuments: 0,
            UploadedDocuments: 0,
            IndexedChunks: 4,
            RecentDocuments: []);
        var courses = new CourseDashboardSummaryDto(TotalCourses: 1, CourseIds: [1]);

        var result = OverviewDashboardFactory.Build(documents, courses);

        result.EvaluationStatus.Should().Be("Sẵn sàng");
        result.EvaluationStatus.Should().NotContain("Sáºµn sÃ");
    }
}
