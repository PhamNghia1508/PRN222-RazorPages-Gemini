using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.DAL.Entities.Enums;
using PRN222.DAL.Repositories.Interfaces;

namespace PRN222.BLL.Services;

public class KnowledgeCurationService : IKnowledgeCurationService
{
    private readonly IRepository<KnowledgeAuditLog> _logRepository;
    private readonly IRepository<Document> _documentRepository;
    private readonly IRepository<DocumentChunk> _chunkRepository;
    private readonly IRepository<ChunkEmbedding> _embeddingRepository;
    private readonly IRepository<ChatCitation> _citationRepository;
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IUnitOfWork _unitOfWork;

    public KnowledgeCurationService(
        IRepository<KnowledgeAuditLog> logRepository,
        IRepository<Document> documentRepository,
        IRepository<DocumentChunk> chunkRepository,
        IRepository<ChunkEmbedding> embeddingRepository,
        IRepository<ChatCitation> citationRepository,
        IRepository<ApplicationUser> userRepository,
        IEmbeddingService embeddingService,
        IUnitOfWork unitOfWork)
    {
        _logRepository = logRepository;
        _documentRepository = documentRepository;
        _chunkRepository = chunkRepository;
        _embeddingRepository = embeddingRepository;
        _citationRepository = citationRepository;
        _userRepository = userRepository;
        _embeddingService = embeddingService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<KnowledgeAuditLogDto>> GetPendingLogsAsync(IEnumerable<int> courseIds)
    {
        var logs = await _logRepository.GetQueryable()
            .Include(log => log.Course)
                .ThenInclude(c => c.Department)
            .Include(log => log.UpdatedByUser)
            .Include(log => log.ApprovedByUser)
            .Where(log => courseIds.Contains(log.CourseId) && log.Status == "Pending")
            .OrderByDescending(log => log.CreatedAt)
            .ToListAsync();

        return logs.Select(MapToDto);
    }

    public async Task<IEnumerable<KnowledgeAuditLogDto>> GetAuditHistoryAsync(IEnumerable<int> courseIds)
    {
        var logs = await _logRepository.GetQueryable()
            .Include(log => log.Course)
                .ThenInclude(c => c.Department)
            .Include(log => log.UpdatedByUser)
            .Include(log => log.ApprovedByUser)
            .Where(log => courseIds.Contains(log.CourseId) && (log.Status == "Approved" || log.Status == "Rejected"))
            .OrderByDescending(log => log.UpdatedAt ?? log.CreatedAt)
            .ToListAsync();

        return logs.Select(MapToDto);
    }

    public async Task<IEnumerable<KnowledgeAuditLogDto>> GetProposalsByUserAsync(string userId)
    {
        var logs = await _logRepository.GetQueryable()
            .Include(log => log.Course)
                .ThenInclude(c => c.Department)
            .Include(log => log.UpdatedByUser)
            .Include(log => log.ApprovedByUser)
            .Where(log => log.UpdatedByUserId == userId)
            .OrderByDescending(log => log.CreatedAt)
            .ToListAsync();

        return logs.Select(MapToDto);
    }

    public async Task<IEnumerable<KnowledgeAuditLogDto>> GetPendingProposalsAsync(string userId, bool isAdmin = false)
    {
        IQueryable<KnowledgeAuditLog> query = _logRepository.GetQueryable()
            .Include(log => log.Course)
                .ThenInclude(c => c.Department)
            .Include(log => log.UpdatedByUser)
            .Include(log => log.ApprovedByUser)
            .Where(log => log.Status == "Pending");

        if (!isAdmin)
        {
            var user = await _userRepository.GetQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.DepartmentId == null)
                return Enumerable.Empty<KnowledgeAuditLogDto>();

            var deptId = user.DepartmentId.Value;
            query = query.Where(log => log.Course.DepartmentId == deptId);
        }

        var logs = await query
            .OrderByDescending(log => log.CreatedAt)
            .ToListAsync();

        return logs.Select(MapToDto);
    }

    public async Task<IEnumerable<KnowledgeAuditLogDto>> GetAuditHistoryByUserAsync(string userId, bool isAdmin = false)
    {
        IQueryable<KnowledgeAuditLog> query = _logRepository.GetQueryable()
            .Include(log => log.Course)
                .ThenInclude(c => c.Department)
            .Include(log => log.UpdatedByUser)
            .Include(log => log.ApprovedByUser)
            .Where(log => log.Status == "Approved" || log.Status == "Rejected");

        if (!isAdmin)
        {
            var user = await _userRepository.GetQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.DepartmentId == null)
                return Enumerable.Empty<KnowledgeAuditLogDto>();

            var deptId = user.DepartmentId.Value;
            query = query.Where(log => log.Course.DepartmentId == deptId);
        }

        var logs = await query
            .OrderByDescending(log => log.UpdatedAt ?? log.CreatedAt)
            .ToListAsync();

        return logs.Select(MapToDto);
    }

    public async Task<bool> ProposeCorrectionAsync(
        ProposeCorrectionDto dto,
        string userId,
        IEnumerable<int> allowedCourseIds,
        bool isAdmin = false)
    {
        if (dto.CourseId <= 0 ||
            string.IsNullOrWhiteSpace(dto.TriggeredQuestion) ||
            string.IsNullOrWhiteSpace(dto.SuggestedAnswer))
        {
            return false;
        }

        if (!isAdmin && !allowedCourseIds.Contains(dto.CourseId))
        {
            return false;
        }

        var log = new KnowledgeAuditLog
        {
            CourseId = dto.CourseId,
            MessageId = dto.MessageId,
            TriggeredQuestion = dto.TriggeredQuestion.Trim(),
            SuggestedAnswer = dto.SuggestedAnswer.Trim(),
            Status = "Pending",
            UpdatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        await _logRepository.AddAsync(log);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ApproveCorrectionAsync(
        int logId,
        string approvedByUserId,
        IEnumerable<int>? allowedCourseIds = null,
        bool isAdmin = false)
    {
        var log = await _logRepository.GetQueryable()
            .Include(l => l.Course)
            .FirstOrDefaultAsync(l => l.Id == logId);

        if (log == null || log.Status != "Pending")
        {
            return false;
        }

        // Department authorization guard
        if (!isAdmin)
        {
            EnforceCourseScope(log.CourseId, allowedCourseIds);
            await EnforceDepartmentGuardAsync(log, approvedByUserId);
        }

        var chunkContent = $"Câu hỏi/Chủ đề: {log.TriggeredQuestion}\nGiải thích bổ sung: {log.SuggestedAnswer}";
        var vector = await _embeddingService.GenerateEmbeddingAsync(chunkContent);
        var serializedVector = System.Text.Json.JsonSerializer.Serialize(vector);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
        // Find or create virtual Document for the Course
        var virtualDocFileName = $"clarifications_course_{log.CourseId}.txt";
        var virtualDoc = await _documentRepository.GetQueryable()
            .FirstOrDefaultAsync(d => d.CourseId == log.CourseId && d.FileName == virtualDocFileName);

        if (virtualDoc == null)
        {
            virtualDoc = new Document
            {
                CourseId = log.CourseId,
                FileName = virtualDocFileName,
                OriginalFileName = "Lưu ý bổ sung của Giảng viên",
                ContentType = "text/plain",
                FileSize = 0,
                StoragePath = string.Empty,
                ExtractedText = string.Empty,
                ChunkCount = 0,
                ChunkingStrategy = "Manual",
                Status = DocumentStatus.Indexed,
                CreatedAt = DateTime.UtcNow
            };
            await _documentRepository.AddAsync(virtualDoc);
            await _unitOfWork.SaveChangesAsync();
        }

        // Determine max ChunkIndex
        var maxChunkIndex = await _chunkRepository.GetQueryable()
            .Where(c => c.DocumentId == virtualDoc.Id)
            .Select(c => (int?)c.ChunkIndex)
            .MaxAsync() ?? -1;

        var nextChunkIndex = maxChunkIndex + 1;

        // Create DocumentChunk
        var chunk = new DocumentChunk
        {
            DocumentId = virtualDoc.Id,
            ChunkIndex = nextChunkIndex,
            Content = chunkContent,
            TokenCount = 0
        };
        await _chunkRepository.AddAsync(chunk);
        await _unitOfWork.SaveChangesAsync(); // save to generate chunk.Id

        // Update virtual Document ChunkCount
        virtualDoc.ChunkCount = nextChunkIndex + 1;
        _documentRepository.Update(virtualDoc);

        var chunkEmbedding = new ChunkEmbedding
        {
            ChunkId = chunk.Id,
            EmbeddingModelName = _embeddingService.ModelName,
            EmbeddingVector = serializedVector,
            CreatedAt = DateTime.UtcNow
        };
        await _embeddingRepository.AddAsync(chunkEmbedding);

        // Link and Approve Log
        log.CreatedChunkId = chunk.Id;
        log.Status = "Approved";
        log.ApprovedByUserId = approvedByUserId;
        log.UpdatedAt = DateTime.UtcNow;

        _logRepository.Update(log);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CommitTransactionAsync();

        return true;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<bool> RejectCorrectionAsync(
        int logId,
        string reason,
        string approvedByUserId,
        IEnumerable<int>? allowedCourseIds = null,
        bool isAdmin = false)
    {
        var log = await _logRepository.GetQueryable()
            .Include(l => l.Course)
            .FirstOrDefaultAsync(l => l.Id == logId);

        if (log == null || log.Status != "Pending")
        {
            return false;
        }

        // Department authorization guard
        if (!isAdmin)
        {
            EnforceCourseScope(log.CourseId, allowedCourseIds);
            await EnforceDepartmentGuardAsync(log, approvedByUserId);
        }

        log.Status = "Rejected";
        log.RejectionReason = reason;
        log.ApprovedByUserId = approvedByUserId;
        log.UpdatedAt = DateTime.UtcNow;

        _logRepository.Update(log);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<bool> RollbackCorrectionAsync(
        int logId,
        string rollbackById = "",
        IEnumerable<int>? allowedCourseIds = null,
        bool isAdmin = false)
    {
        var log = await _logRepository.GetQueryable()
            .Include(l => l.Course)
            .FirstOrDefaultAsync(l => l.Id == logId);

        if (log == null || log.Status != "Approved" || log.CreatedChunkId == null)
        {
            return false;
        }

        // Department authorization guard
        if (!isAdmin)
        {
            if (string.IsNullOrWhiteSpace(rollbackById))
            {
                throw new UnauthorizedAccessException("Người dùng không hợp lệ.");
            }
            EnforceCourseScope(log.CourseId, allowedCourseIds);
            await EnforceDepartmentGuardAsync(log, rollbackById);
        }

        var chunkId = log.CreatedChunkId.Value;
        var chunk = await _chunkRepository.GetByIdAsync(chunkId);

        if (chunk != null)
        {
            var documentId = chunk.DocumentId;

            // Fix FK constraint: delete ChatCitations that reference this chunk first
            var citations = await _citationRepository.GetQueryable()
                .Where(c => c.ChunkId == chunkId)
                .ToListAsync();

            foreach (var citation in citations)
            {
                _citationRepository.Delete(citation);
            }

            // Retrieve and delete embeddings
            var embeddings = await _embeddingRepository.GetQueryable()
                .Where(e => e.ChunkId == chunkId)
                .ToListAsync();

            foreach (var embedding in embeddings)
            {
                _embeddingRepository.Delete(embedding);
            }

            // Delete chunk
            _chunkRepository.Delete(chunk);
            await _unitOfWork.SaveChangesAsync();

            // Update remaining chunk count in the virtual document
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document != null)
            {
                var remainingCount = await _chunkRepository.GetQueryable()
                    .CountAsync(c => c.DocumentId == documentId);
                document.ChunkCount = remainingCount;
                _documentRepository.Update(document);
            }
        }

        log.Status = "Rejected";
        log.CreatedChunkId = null;
        log.UpdatedAt = DateTime.UtcNow;

        _logRepository.Update(log);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private async Task EnforceDepartmentGuardAsync(KnowledgeAuditLog log, string actingUserId)
    {
        var courseDeptId = log.Course?.DepartmentId;
        if (courseDeptId == null)
        {
            throw new UnauthorizedAccessException("Môn học không thuộc khoa nào.");
        }

        var actingUser = await _userRepository.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == actingUserId);

        if (actingUser?.DepartmentId == null)
        {
            throw new UnauthorizedAccessException("Người duyệt không thuộc khoa nào.");
        }

        if (actingUser.DepartmentId.Value != courseDeptId.Value)
        {
            throw new UnauthorizedAccessException(
                "Bạn không có thẩm quyền duyệt môn học thuộc khoa khác");
        }
    }

    private static void EnforceCourseScope(int courseId, IEnumerable<int>? allowedCourseIds)
    {
        if (allowedCourseIds == null || !allowedCourseIds.Contains(courseId))
        {
            throw new UnauthorizedAccessException("Bạn không được phân công phụ trách môn học này.");
        }
    }

    private KnowledgeAuditLogDto MapToDto(KnowledgeAuditLog log)
    {
        return new KnowledgeAuditLogDto(
            log.Id,
            log.CourseId,
            log.Course?.Name ?? string.Empty,
            log.Course?.Department?.Name,
            log.MessageId,
            log.TriggeredQuestion,
            log.SuggestedAnswer,
            log.Status,
            log.UpdatedByUserId,
            log.UpdatedByUser?.Email ?? string.Empty,
            log.ApprovedByUserId,
            log.ApprovedByUser?.Email,
            log.RejectionReason,
            log.CreatedChunkId,
            log.CreatedAt,
            log.UpdatedAt
        );
    }
}
