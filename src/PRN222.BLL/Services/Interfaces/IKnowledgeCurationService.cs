using System.Collections.Generic;
using System.Threading.Tasks;
using PRN222.BLL.DTOs;

namespace PRN222.BLL.Services.Interfaces;

public interface IKnowledgeCurationService
{
    Task<IEnumerable<KnowledgeAuditLogDto>> GetPendingLogsAsync(IEnumerable<int> courseIds);
    Task<IEnumerable<KnowledgeAuditLogDto>> GetAuditHistoryAsync(IEnumerable<int> courseIds);
    Task<IEnumerable<KnowledgeAuditLogDto>> GetProposalsByUserAsync(string userId);
    Task<bool> ProposeCorrectionAsync(
        ProposeCorrectionDto dto,
        string userId,
        IEnumerable<int> allowedCourseIds,
        bool isAdmin = false);
    Task<bool> ApproveCorrectionAsync(
        int logId,
        string approvedByUserId,
        IEnumerable<int>? allowedCourseIds = null,
        bool isAdmin = false);
    Task<bool> RejectCorrectionAsync(
        int logId,
        string reason,
        string approvedByUserId,
        IEnumerable<int>? allowedCourseIds = null,
        bool isAdmin = false);
    Task<bool> RollbackCorrectionAsync(
        int logId,
        string rollbackById = "",
        IEnumerable<int>? allowedCourseIds = null,
        bool isAdmin = false);
    Task<IEnumerable<KnowledgeAuditLogDto>> GetPendingProposalsAsync(string userId, bool isAdmin = false);
    Task<IEnumerable<KnowledgeAuditLogDto>> GetAuditHistoryByUserAsync(string userId, bool isAdmin = false);
}
