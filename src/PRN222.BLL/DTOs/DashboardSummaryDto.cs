namespace PRN222.BLL.DTOs;

public record DocumentDashboardSummaryDto(
    int TotalDocuments,
    int IndexedDocuments,
    int FailedDocuments,
    int ProcessingDocuments,
    int UploadedDocuments,
    int IndexedChunks,
    IReadOnlyList<DocumentDto> RecentDocuments);

public record CourseDashboardSummaryDto(
    int TotalCourses,
    IReadOnlyList<int> CourseIds);
