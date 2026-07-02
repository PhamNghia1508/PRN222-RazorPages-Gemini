using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.BLL.Services.TextExtractors;
using PRN222.DAL.Entities;
using PRN222.DAL.Entities.Enums;
using PRN222.DAL.Repositories.Interfaces;

using Microsoft.Extensions.Configuration;

namespace PRN222.BLL.Services;

/// <summary>
/// Service implementing the full Document Management workflow:
/// Upload → Save File → Extract Text → Chunk → Save Chunks
/// </summary>
public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IChunkRepository _chunkRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TextExtractorFactory _extractorFactory;
    private readonly IChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IGeminiVisionService _geminiVisionService;
    private readonly ILogger<DocumentService> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly IBackgroundTaskQueue? _backgroundTaskQueue;
    private readonly IRepository<ChatCitation>? _chatCitationRepository;
    private readonly IQAPairRepository? _qaPairRepository;
    private readonly IDocumentRealtimeNotifier? _realtimeNotifier;
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;
    private readonly TimeSpan _processingRetryAfter;

    public DocumentService(
        IDocumentRepository documentRepository,
        IChunkRepository chunkRepository,
        IUnitOfWork unitOfWork,
        TextExtractorFactory extractorFactory,
        IChunkingService chunkingService,
        IEmbeddingService embeddingService,
        IGeminiVisionService geminiVisionService,
        ILogger<DocumentService> logger,
        IServiceScopeFactory? scopeFactory = null,
        IBackgroundTaskQueue? backgroundTaskQueue = null,
        IRepository<ChatCitation>? chatCitationRepository = null,
        IQAPairRepository? qaPairRepository = null,
        IDocumentRealtimeNotifier? realtimeNotifier = null,
        IConfiguration? configuration = null)
    {
        _documentRepository = documentRepository;
        _chunkRepository = chunkRepository;
        _unitOfWork = unitOfWork;
        _extractorFactory = extractorFactory;
        _chunkingService = chunkingService;
        _embeddingService = embeddingService;
        _geminiVisionService = geminiVisionService;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _backgroundTaskQueue = backgroundTaskQueue;
        _chatCitationRepository = chatCitationRepository;
        _qaPairRepository = qaPairRepository;
        _realtimeNotifier = realtimeNotifier;
        _chunkSize = GetIntSetting(configuration, "Chunking:DefaultChunkSize", 512);
        _chunkOverlap = GetIntSetting(configuration, "Chunking:DefaultOverlap", 50);
        _processingRetryAfter = TimeSpan.FromMinutes(
            GetIntSetting(configuration, "DocumentProcessing:StaleProcessingMinutes", 30));
    }

    public async Task<IEnumerable<DocumentDto>> GetAllDocumentsAsync()
    {
        var documents = await _documentRepository.GetAllAsync();
        return documents.Select(MapToDto);
    }

    public async Task<DocumentDashboardSummaryDto> GetDashboardSummaryAsync(IEnumerable<int>? courseIds = null)
    {
        var courseIdList = courseIds?.Distinct().ToList();
        var query = _documentRepository.GetQueryable().AsNoTracking();
        if (courseIdList is not null)
        {
            query = query.Where(document => courseIdList.Contains(document.CourseId));
        }

        var aggregate = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalDocuments = group.Count(),
                IndexedDocuments = group.Count(document => document.Status == DocumentStatus.Indexed),
                FailedDocuments = group.Count(document => document.Status == DocumentStatus.Failed),
                ProcessingDocuments = group.Count(document => document.Status == DocumentStatus.Processing),
                UploadedDocuments = group.Count(document => document.Status == DocumentStatus.Uploaded),
                IndexedChunks = group.Sum(document =>
                    document.Status == DocumentStatus.Indexed ? document.ChunkCount : 0)
            })
            .FirstOrDefaultAsync();

        var recentDocuments = await query
            .OrderByDescending(document => document.CreatedAt)
            .Take(5)
            .Select(document => new DocumentDto(
                document.Id,
                document.FileName,
                document.OriginalFileName,
                document.ContentType,
                document.FileSize,
                document.ChunkCount,
                document.Status.ToString(),
                document.Course.Name,
                document.CourseId,
                document.CreatedAt))
            .ToListAsync();

        return new DocumentDashboardSummaryDto(
            aggregate?.TotalDocuments ?? 0,
            aggregate?.IndexedDocuments ?? 0,
            aggregate?.FailedDocuments ?? 0,
            aggregate?.ProcessingDocuments ?? 0,
            aggregate?.UploadedDocuments ?? 0,
            aggregate?.IndexedChunks ?? 0,
            recentDocuments);
    }

    private static int GetIntSetting(IConfiguration? configuration, string key, int defaultValue)
    {
        if (configuration == null)
        {
            return defaultValue;
        }

        var value = configuration[key];
        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : defaultValue;
    }

    public async Task<DocumentDetailDto?> GetDocumentByIdAsync(int id)
    {
        var document = await _documentRepository.GetWithChunksAsync(id);
        if (document == null) return null;

        return new DocumentDetailDto
        {
            Id = document.Id,
            OriginalFileName = document.OriginalFileName,
            ContentType = document.ContentType,
            FileSize = document.FileSize,
            ChunkCount = document.ChunkCount,
            Status = document.Status.ToString(),
            ChunkingStrategy = document.ChunkingStrategy,
            ErrorMessage = document.ErrorMessage,
            CourseName = document.Course.Name,
            CourseId = document.CourseId,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            ArchivedByEmail = document.ArchivedByUser?.Email,
            ArchivedAt = document.ArchivedAt,
            ArchiveReason = document.ArchiveReason,
            ArchivedFromStatus = document.ArchivedFromStatus?.ToString(),
            ExtractedTextPreview = document.ExtractedText?.Length > 2000
                ? document.ExtractedText[..2000] + "..."
                : document.ExtractedText,
            Chunks = document.Chunks.Select(c => new ChunkDto(
                c.Id, c.ChunkIndex, c.Content, c.TokenCount,
                c.StartPage, c.EndPage, c.ChapterSection
            )).ToList()
        };
    }

    public async Task<IEnumerable<DocumentDto>> GetDocumentsByCourseAsync(int courseId)
    {
        var documents = await _documentRepository.GetByCourseIdAsync(courseId);
        return documents.Select(MapToDto);
    }

    public async Task<DocumentImageDto?> GetChunkImageAsync(int chunkId)
    {
        var chunk = await _chunkRepository.GetQueryable()
            .AsNoTracking()
            .Include(item => item.Document)
            .FirstOrDefaultAsync(item => item.Id == chunkId && item.ImageUrl != null);
        if (chunk == null)
        {
            return null;
        }

        var imagePath = FindChunkImagePath(chunkId);
        if (imagePath == null)
        {
            return null;
        }

        return new DocumentImageDto(
            await File.ReadAllBytesAsync(imagePath),
            GetImageContentType(Path.GetExtension(imagePath)),
            chunk.Document.CourseId);
    }

    public async Task<DocumentDto> UploadDocumentAsync(DocumentUploadDto dto, Stream fileStream)
    {
        // Validate file type
        if (!_extractorFactory.IsSupported(dto.ContentType))
        {
            throw new NotSupportedException(
                $"File type '{dto.ContentType}' is not supported. Please upload PDF, DOCX, PPTX, or PPT files.");
        }

        // Verify binary file signature (magic number) for security
        if (!VerifyFileSignature(fileStream, dto.ContentType))
        {
            throw new InvalidOperationException(
                "Định dạng nội dung file không khớp với Content-Type (Magic Number validation failed).");
        }

        // Generate unique file name
        var fileExtension = GetFileExtension(dto.ContentType);
        var storedFileName = $"{Guid.NewGuid()}{fileExtension}";

        // Determine storage path
        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "uploads");
        Directory.CreateDirectory(uploadsDir);
        var storagePath = Path.Combine(uploadsDir, storedFileName);

        // Save file to disk
        using (var fileStreamOutput = new FileStream(storagePath, FileMode.Create))
        {
            await fileStream.CopyToAsync(fileStreamOutput);
        }

        // Create document entity
        var document = new Document
        {
            CourseId = dto.CourseId,
            FileName = storedFileName,
            OriginalFileName = dto.OriginalFileName,
            ContentType = dto.ContentType,
            FileSize = dto.FileSize,
            StoragePath = storagePath,
            ChunkingStrategy = "FixedSize",
            Status = DocumentStatus.Uploaded,
            CreatedAt = DateTime.UtcNow
        };

        await _documentRepository.AddAsync(document);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Document uploaded: {FileName} (ID: {DocumentId})",
            document.OriginalFileName, document.Id);

        // Load course for DTO mapping
        var savedDoc = await _documentRepository.GetWithCourseAsync(document.Id);
        await NotifyDocumentChangedAsync(savedDoc!, "uploaded");
        return MapToDto(savedDoc!);
    }

    public async Task ProcessDocumentAsync(int documentId)
    {
        var document = await _documentRepository.GetByIdAsync(documentId);
        if (document == null)
        {
            throw new InvalidOperationException($"Document with ID {documentId} not found.");
        }
        if (document.Status == DocumentStatus.Archived)
        {
            throw new InvalidOperationException("Tài liệu đã tạm ẩn khỏi RAG nên không thể xử lý lại.");
        }

        try
        {
            // Update status to Processing
            await _documentRepository.UpdateStatusAsync(documentId, DocumentStatus.Processing);
            await _unitOfWork.SaveChangesAsync();
            await NotifyDocumentChangedAsync(document, "processing", DocumentStatus.Processing);

            _logger.LogInformation("Processing document: {FileName} (ID: {DocumentId})",
                document.OriginalFileName, documentId);

            // Step 1: Extract text and images
            using var fileStream = new FileStream(document.StoragePath, FileMode.Open, FileAccess.Read);
            var extractor = _extractorFactory.GetExtractor(document.ContentType);
            var extractedContent = await extractor.ExtractTextAsync(fileStream, document.ContentType);

            if (string.IsNullOrWhiteSpace(extractedContent.Text) && extractedContent.Images.Count == 0)
            {
                throw new InvalidOperationException("No content could be extracted from the document.");
            }

            // Step 2: Chunk the text
            var chunks = _chunkingService.ChunkText(extractedContent.Text, _chunkSize, _chunkOverlap).ToList();

            _logger.LogInformation("Extracted {CharCount} characters and {ImageCount} images, created {ChunkCount} text chunks for document {DocumentId}",
                extractedContent.Text.Length, extractedContent.Images.Count, chunks.Count, documentId);

            // Step 3: Save chunks to database
            await _unitOfWork.BeginTransactionAsync();
            var chunkEntities = new List<DocumentChunk>();
            var existingChunkIds = new List<int>();
            try
            {
                // Delete existing chunk dependents before deleting chunks.
                var existingChunks = await _chunkRepository.GetByDocumentIdAsync(documentId);
                existingChunkIds = existingChunks.Select(c => c.Id).ToList();
                DeleteChunkDependents(existingChunks.Select(c => c.Id));

                // Delete existing chunks
                await _chunkRepository.DeleteByDocumentIdAsync(documentId);

                // Create chunk entities and generate embeddings
                var imageChunks = new List<(DocumentChunk Chunk, ExtractedImageDto Image)>();

                // Add text chunks
                foreach (var c in chunks)
                {
                    var chunkEntity = new DocumentChunk
                    {
                        DocumentId = documentId,
                        ChunkIndex = c.ChunkIndex,
                        Content = c.Content,
                        TokenCount = c.TokenCount,
                        StartPage = c.StartPage,
                        EndPage = c.EndPage,
                        ChapterSection = c.ChapterSection
                    };

                    var vector = await _embeddingService.GenerateEmbeddingAsync(c.Content);
                    chunkEntity.Embeddings = new List<ChunkEmbedding>
                    {
                        new ChunkEmbedding
                        {
                            EmbeddingModelName = _embeddingService.ModelName,
                            EmbeddingVector = System.Text.Json.JsonSerializer.Serialize(vector),
                            CreatedAt = DateTime.UtcNow
                        }
                    };

                    chunkEntities.Add(chunkEntity);
                }

                // Add image chunks (Vision-Augmented RAG)
                int imageIndex = chunks.Count;
                foreach (var img in extractedContent.Images)
                {
                    // Generate description via Gemini Vision
                    var description = await _geminiVisionService.DescribeImageAsync(img.Data,
                        $"Tài liệu: {document.OriginalFileName}, Trang: {img.PageNumber}");

                    var imageChunk = new DocumentChunk
                    {
                        DocumentId = documentId,
                        ChunkIndex = imageIndex++,
                        Content = $"[Hình ảnh - Trang {img.PageNumber}]\n{description}",
                        TokenCount = description.Length / 4, // Rough estimate
                        StartPage = img.PageNumber,
                        EndPage = img.PageNumber
                    };

                    // Embed the AI-generated description
                    var vector = await _embeddingService.GenerateEmbeddingAsync(imageChunk.Content);
                    imageChunk.Embeddings = new List<ChunkEmbedding>
                    {
                        new ChunkEmbedding
                        {
                            EmbeddingModelName = _embeddingService.ModelName,
                            EmbeddingVector = System.Text.Json.JsonSerializer.Serialize(vector),
                            CreatedAt = DateTime.UtcNow
                        }
                    };

                    chunkEntities.Add(imageChunk);
                    imageChunks.Add((imageChunk, img));
                }

                await _chunkRepository.AddRangeAsync(chunkEntities);
                if (imageChunks.Count > 0)
                {
                    await _unitOfWork.SaveChangesAsync();
                    foreach (var (imageChunk, image) in imageChunks)
                    {
                        var imagePath = GetChunkImagePath(imageChunk.Id, image.MimeType);
                        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
                        await File.WriteAllBytesAsync(imagePath, image.Data);
                        imageChunk.ImageUrl = $"/Chat/Session?handler=CitationImage&chunkId={imageChunk.Id}";
                        _chunkRepository.Update(imageChunk);
                    }
                }

                // Update document
                document.ExtractedText = extractedContent.Text;
                document.ChunkCount = chunkEntities.Count;
                document.Status = DocumentStatus.Indexed;
                document.UpdatedAt = DateTime.UtcNow;
                document.ErrorMessage = null;
                _documentRepository.Update(document);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();
                await NotifyDocumentChangedAsync(document, "indexed");
                DeleteChunkImageFiles(existingChunkIds);

                _logger.LogInformation("Document processed successfully: {FileName} (ID: {DocumentId}), {ChunkCount} chunks created",
                    document.OriginalFileName, documentId, chunks.Count);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                DeleteChunkImageFiles(
                    chunkEntities
                        .Where(chunk => chunk.Id > 0)
                        .Select(chunk => chunk.Id));
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process document {DocumentId}: {ErrorMessage}",
                documentId, ex.Message);

            await _documentRepository.UpdateStatusAsync(documentId, DocumentStatus.Failed, ex.Message);
            await _unitOfWork.SaveChangesAsync();
            await NotifyDocumentChangedAsync(document, "failed", DocumentStatus.Failed);
            throw;
        }
    }

    public async Task EnqueueProcessDocumentAsync(int documentId)
    {
        var document = await _documentRepository.GetByIdAsync(documentId);
        if (document == null)
        {
            throw new InvalidOperationException($"Document with ID {documentId} not found.");
        }
        if (document.Status == DocumentStatus.Archived)
        {
            throw new InvalidOperationException("Tài liệu đã tạm ẩn khỏi RAG nên không thể xử lý lại.");
        }

        // Concurrency Guard: If the document is already in Processing status, do not enqueue again.
        // A stale Processing status can happen after an app restart or worker crash, so allow retry
        // after a conservative timeout.
        if (document.Status == DocumentStatus.Processing && !IsProcessingStale(document))
        {
            _logger.LogWarning("Document {DocumentId} is already being processed. Aborting new request to prevent concurrent race conditions.", documentId);
            return;
        }

        if (document.Status == DocumentStatus.Processing)
        {
            _logger.LogWarning(
                "Document {DocumentId} has stale Processing status from {UpdatedAt}. Re-queueing processing.",
                documentId,
                document.UpdatedAt);
        }

        // 1. Update status to Processing synchronously so DB is immediately updated
        await _documentRepository.UpdateStatusAsync(documentId, DocumentStatus.Processing, null);
        await _unitOfWork.SaveChangesAsync();

        // 2. If scope factory is not present (e.g. in simple unit tests), fallback to sync execution
        if (_scopeFactory == null)
        {
            _logger.LogWarning("ScopeFactory is null. Executing document processing synchronously in the current thread.");
            await ProcessDocumentAsync(documentId);
            return;
        }

        if (_backgroundTaskQueue != null)
        {
            await _backgroundTaskQueue.QueueAsync(async (serviceProvider, cancellationToken) =>
            {
                var scopedService = serviceProvider.GetRequiredService<IDocumentService>();
                var logger = serviceProvider.GetRequiredService<ILogger<DocumentService>>();

                logger.LogInformation("Queued document processing started for document {DocumentId}", documentId);
                await scopedService.ProcessDocumentAsync(documentId);
                logger.LogInformation("Queued document processing completed successfully for document {DocumentId}", documentId);
            });

            return;
        }

        // 3. Fallback for tests or hosts that have not registered the queue yet.
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var scopedService = scope.ServiceProvider.GetRequiredService<IDocumentService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DocumentService>>();

            logger.LogInformation("Inline fallback processing started for document {DocumentId}", documentId);
            await scopedService.ProcessDocumentAsync(documentId);
            logger.LogInformation("Inline fallback processing completed successfully for document {DocumentId}", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inline fallback processing failed for document {DocumentId}", documentId);
        }
    }

    private bool IsProcessingStale(Document document)
    {
        if (document.UpdatedAt is null)
        {
            return false;
        }

        return document.UpdatedAt.Value <= DateTime.UtcNow.Subtract(_processingRetryAfter);
    }

    public async Task ArchiveDocumentAsync(int id, string archivedByUserId, string archiveReason)
    {
        if (string.IsNullOrWhiteSpace(archivedByUserId))
        {
            throw new InvalidOperationException("Không xác định được tài khoản thực hiện tạm ẩn.");
        }

        if (string.IsNullOrWhiteSpace(archiveReason))
        {
            throw new ArgumentException("Lý do tạm ẩn là bắt buộc.", nameof(archiveReason));
        }

        var normalizedReason = archiveReason.Trim();
        if (normalizedReason.Length > 1000)
        {
            throw new ArgumentException(
                "Lý do tạm ẩn không được vượt quá 1000 ký tự.",
                nameof(archiveReason));
        }

        var document = await _documentRepository.GetByIdAsync(id);
        if (document == null)
        {
            throw new InvalidOperationException($"Document with ID {id} not found.");
        }

        if (document.Status == DocumentStatus.Archived)
        {
            throw new InvalidOperationException("Tài liệu đã được tạm ẩn khỏi RAG.");
        }

        if (document.Status == DocumentStatus.Processing)
        {
            throw new InvalidOperationException("Không thể tạm ẩn tài liệu khi hệ thống đang xử lý.");
        }

        var archivedAt = DateTime.UtcNow;
        document.ArchivedFromStatus = document.Status;
        document.ArchivedByUserId = archivedByUserId;
        document.ArchiveReason = normalizedReason;
        document.ArchivedAt = archivedAt;
        document.Status = DocumentStatus.Archived;
        document.UpdatedAt = archivedAt;
        await _unitOfWork.SaveChangesAsync();

        await NotifyDocumentChangedAsync(document, "archived", DocumentStatus.Archived);

        _logger.LogInformation("Document archived from RAG: {FileName} (ID: {DocumentId})",
            document.OriginalFileName, id);
    }


    private async Task NotifyDocumentChangedAsync(
        Document document,
        string action,
        DocumentStatus? statusOverride = null)
    {
        if (_realtimeNotifier is null)
        {
            return;
        }

        var status = statusOverride ?? document.Status;
        try
        {
            var task = _realtimeNotifier.NotifyDocumentChangedAsync(new DocumentRealtimeNotification(
                DocumentId: document.Id,
                CourseId: document.CourseId,
                FileName: document.OriginalFileName,
                Status: status.ToString(),
                Action: action,
                ChunkCount: document.ChunkCount,
                OccurredAt: DateTimeOffset.UtcNow));

            await (task ?? Task.CompletedTask);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Document realtime notification failed for action {Action} on document {DocumentId}.",
                action,
                document.Id);
        }
    }
    private static DocumentDto MapToDto(Document doc)
    {
        return new DocumentDto(
            Id: doc.Id,
            FileName: doc.FileName,
            OriginalFileName: doc.OriginalFileName,
            ContentType: doc.ContentType,
            FileSize: doc.FileSize,
            ChunkCount: doc.ChunkCount,
            Status: doc.Status.ToString(),
            CourseName: doc.Course?.Name ?? "Unknown",
            CourseId: doc.CourseId,
            CreatedAt: doc.CreatedAt
        );
    }

    private void DeleteChunkDependents(IEnumerable<int> chunkIds)
    {
        var chunkIdSet = chunkIds.ToHashSet();
        if (chunkIdSet.Count == 0)
        {
            return;
        }

        if (_chatCitationRepository != null)
        {
            var citations = _chatCitationRepository.GetQueryable()
                .Where(c => chunkIdSet.Contains(c.ChunkId))
                .ToList();

            foreach (var citation in citations)
            {
                _chatCitationRepository.Delete(citation);
            }
        }

        if (_qaPairRepository != null)
        {
            var generatedQaPairs = _qaPairRepository.GetQueryable()
                .Where(q => q.DocumentChunkId != null && chunkIdSet.Contains(q.DocumentChunkId.Value))
                .ToList();

            foreach (var qaPair in generatedQaPairs)
            {
                _qaPairRepository.Delete(qaPair);
            }
        }
    }

    private static string GetChunkImagePath(int chunkId, string mimeType)
    {
        var extension = mimeType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            _ => ".png"
        };

        return Path.Combine(GetPrivateImageDirectory(), $"chunk-{chunkId}{extension}");
    }

    private static string? FindChunkImagePath(int chunkId)
    {
        var directory = GetPrivateImageDirectory();
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return Directory.GetFiles(directory, $"chunk-{chunkId}.*")
            .SingleOrDefault();
    }

    private static void DeleteChunkImageFiles(IEnumerable<int> chunkIds)
    {
        foreach (var chunkId in chunkIds.Distinct())
        {
            var imagePath = FindChunkImagePath(chunkId);
            if (imagePath != null)
            {
                File.Delete(imagePath);
            }
        }
    }

    private static string GetPrivateImageDirectory() =>
        Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "uploads", "images");

    private static string GetImageContentType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/png"
        };

    private static string GetFileExtension(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/msword" => ".doc",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
            "application/vnd.ms-powerpoint" => ".ppt",
            _ => ".bin"
        };
    }

    private static bool VerifyFileSignature(Stream stream, string contentType)
    {
        if (stream == null || !stream.CanRead)
            return false;

        // Skip validation for empty streams (commonly used in tests)
        if (stream.Length == 0)
            return true;

        if (!stream.CanSeek)
            return true;

        long originalPosition = stream.Position;
        try
        {
            byte[] buffer = new byte[4];
            int read = stream.Read(buffer, 0, 4);
            stream.Position = originalPosition; // Reset position immediately

            if (read < 4)
                return false;

            // PDF signature: %PDF (0x25, 0x50, 0x44, 0x46)
            if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                return buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46;
            }

            // OpenXML signature: PK.. (0x50, 0x4B, 0x03, 0x04)
            if (contentType.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase) ||
                contentType.Equals("application/vnd.openxmlformats-officedocument.presentationml.presentation", StringComparison.OrdinalIgnoreCase))
            {
                return buffer[0] == 0x50 && buffer[1] == 0x4B && buffer[2] == 0x03 && buffer[3] == 0x04;
            }

            // Legacy PPT signature: D0 CF 11 E0 (OLE Compound File)
            if (contentType.Equals("application/vnd.ms-powerpoint", StringComparison.OrdinalIgnoreCase))
            {
                return buffer[0] == 0xD0 && buffer[1] == 0xCF && buffer[2] == 0x11 && buffer[3] == 0xE0;
            }

            return true;
        }
        catch
        {
            try { stream.Position = originalPosition; } catch { }
            return false;
        }
    }
}
