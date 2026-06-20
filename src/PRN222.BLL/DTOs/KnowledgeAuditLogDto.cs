using System;

namespace PRN222.BLL.DTOs;

public record KnowledgeAuditLogDto(
    int Id,
    int CourseId,
    string CourseName,
    string? DepartmentName,
    int? MessageId,
    string TriggeredQuestion,
    string SuggestedAnswer,
    string Status,
    string UpdatedByUserId,
    string UpdatedByUserEmail,
    string? ApprovedByUserId,
    string? ApprovedByUserEmail,
    string? RejectionReason,
    int? CreatedChunkId,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
