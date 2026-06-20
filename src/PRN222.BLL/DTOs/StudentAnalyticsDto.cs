using System;
using System.Collections.Generic;

namespace PRN222.BLL.DTOs;

public record StudentAnalyticsDto(
    int TotalQueries,
    double HelpfulnessRate,
    double AvgConfidenceScore,
    List<FailedQueryDto> FailedQueries,
    List<TopDocumentDto> TopCitedDocuments
);

public record FailedQueryDto(
    int MessageId,
    string Question,
    string? Answer,
    float? ConfidenceScore,
    bool? IsHelpful,
    string CourseName,
    DateTime CreatedAt,
    int? CourseId = null
);

public record TopDocumentDto(
    int DocumentId,
    string DocumentName,
    string CourseName,
    int CitationCount
);
