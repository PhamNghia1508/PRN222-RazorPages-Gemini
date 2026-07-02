using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services;
using PRN222.BLL.Services.Interfaces;
using PRN222.BLL.Services.TextExtractors;
using PRN222.DAL.Entities;
using PRN222.DAL.Entities.Enums;
using PRN222.DAL.Repositories.Interfaces;
using Xunit;

namespace PRN222.Tests.Services;

public class DocumentServiceTests
{
    private readonly Mock<IDocumentRepository> _documentRepoMock;
    private readonly Mock<IChunkRepository> _chunkRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ITextExtractorService> _extractorMock;
    private readonly Mock<IChunkingService> _chunkingServiceMock;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<IGeminiVisionService> _visionServiceMock;
    private readonly Mock<ILogger<DocumentService>> _loggerMock;
    private readonly Mock<IDocumentRealtimeNotifier> _realtimeNotifierMock;
    private readonly TextExtractorFactory _extractorFactory;
    private readonly DocumentService _documentService;

    public DocumentServiceTests()
    {
        _documentRepoMock = new Mock<IDocumentRepository>();
        _chunkRepoMock = new Mock<IChunkRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _extractorMock = new Mock<ITextExtractorService>();
        _chunkingServiceMock = new Mock<IChunkingService>();
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _visionServiceMock = new Mock<IGeminiVisionService>();
        _loggerMock = new Mock<ILogger<DocumentService>>();
        _realtimeNotifierMock = new Mock<IDocumentRealtimeNotifier>();

        // Setup Extractor Factory with the mocked extractor
        _extractorMock.Setup(e => e.CanHandle(It.IsAny<string>())).Returns(true);
        _extractorFactory = new TextExtractorFactory(new[] { _extractorMock.Object });

        _embeddingServiceMock.Setup(e => e.ModelName).Returns("test-model");
        _embeddingServiceMock.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f });

        _documentService = new DocumentService(
            _documentRepoMock.Object,
            _chunkRepoMock.Object,
            _unitOfWorkMock.Object,
            _extractorFactory,
            _chunkingServiceMock.Object,
            _embeddingServiceMock.Object,
            _visionServiceMock.Object,
            _loggerMock.Object,
            realtimeNotifier: _realtimeNotifierMock.Object
        );
    }

    [Fact]
    public async Task UploadDocumentAsync_ShouldThrowNotSupportedException_WhenContentTypeIsNotSupported()
    {
        // Arrange
        var unsupportedExtractorMock = new Mock<ITextExtractorService>();
        unsupportedExtractorMock.Setup(e => e.CanHandle(It.IsAny<string>())).Returns(false);
        var unsupportedFactory = new TextExtractorFactory(new[] { unsupportedExtractorMock.Object });

        var customDocService = new DocumentService(
            _documentRepoMock.Object,
            _chunkRepoMock.Object,
            _unitOfWorkMock.Object,
            unsupportedFactory,
            _chunkingServiceMock.Object,
            _embeddingServiceMock.Object,
            _visionServiceMock.Object,
            _loggerMock.Object
        );

        var dto = new DocumentUploadDto
        {
            CourseId = 1,
            OriginalFileName = "test.pdf",
            ContentType = "application/pdf",
            FileSize = 100
        };
        using var stream = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            customDocService.UploadDocumentAsync(dto, stream));
    }

    [Fact]
    public async Task UploadDocumentAsync_ShouldThrowInvalidOperationException_WhenFileSignatureMismatch()
    {
        // Arrange
        var dto = new DocumentUploadDto
        {
            CourseId = 1,
            OriginalFileName = "fake.pdf",
            ContentType = "application/pdf",
            FileSize = 10
        };
        // Write invalid PDF bytes
        byte[] invalidBytes = System.Text.Encoding.UTF8.GetBytes("NOT_A_PDF_FILE");
        using var stream = new MemoryStream(invalidBytes);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _documentService.UploadDocumentAsync(dto, stream));

        exception.Message.Should().Contain("validation failed");
    }

    [Fact]
    public async Task UploadDocumentAsync_ShouldSucceed_WhenSignatureMatches()
    {
        // Arrange
        var dto = new DocumentUploadDto
        {
            CourseId = 1,
            OriginalFileName = "real.pdf",
            ContentType = "application/pdf",
            FileSize = 10
        };
        // Write valid PDF magic number
        byte[] pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x31, 0x2E, 0x34 }; // %PDF1.4
        using var stream = new MemoryStream(pdfBytes);

        var course = new Course { Id = 1, Name = "PRN222" };
        var document = new Document
        {
            Id = 10,
            CourseId = 1,
            OriginalFileName = "real.pdf",
            FileName = "stored.pdf",
            ContentType = "application/pdf",
            FileSize = 10,
            StoragePath = "dummy",
            Course = course
        };

        _documentRepoMock.Setup(r => r.GetWithCourseAsync(It.IsAny<int>()))
            .ReturnsAsync(document);

        // Act
        var result = await _documentService.UploadDocumentAsync(dto, stream);

        // Assert
        result.Should().NotBeNull();
        result.OriginalFileName.Should().Be("real.pdf");
        _realtimeNotifierMock.Verify(n => n.NotifyDocumentChangedAsync(
            It.Is<DocumentRealtimeNotification>(notification =>
                notification.DocumentId == 10 &&
                notification.CourseId == 1 &&
                notification.FileName == "real.pdf" &&
                notification.Status == nameof(DocumentStatus.Uploaded) &&
                notification.Action == "uploaded" &&
                notification.ChunkCount == 0),
            It.IsAny<CancellationToken>()), Times.Once);

        // Clean up physically uploaded files created by the test
        try
        {
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "uploads");
            if (Directory.Exists(uploadsDir))
            {
                var files = Directory.GetFiles(uploadsDir);
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
        }
        catch { }
    }

    [Fact]
    public async Task ProcessDocumentAsync_ShouldThrowInvalidOperationException_WhenDocumentNotFound()
    {
        // Arrange
        int docId = 99;
        _documentRepoMock.Setup(r => r.GetByIdAsync(docId))
            .ReturnsAsync((Document?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _documentService.ProcessDocumentAsync(docId));
    }

    [Fact]
    public async Task ProcessDocumentAsync_ShouldSucceed_WhenEverythingIsCorrect()
    {
        // Arrange
        int docId = 42;
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        await File.WriteAllTextAsync(tempFilePath, "Đây là nội dung tài liệu học tập PRN222.");

        try
        {
            var document = new Document
            {
                Id = docId,
                CourseId = 1,
                OriginalFileName = "tailieu.pdf",
                ContentType = "application/pdf",
                StoragePath = tempFilePath,
                Status = DocumentStatus.Uploaded
            };

            _documentRepoMock.Setup(r => r.GetByIdAsync(docId))
                .ReturnsAsync(document);

            _extractorMock.Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), "application/pdf"))
                .ReturnsAsync(new ExtractedContentDto { Text = "Đây là nội dung tài liệu học tập PRN222." });

            var chunkDtos = new List<ChunkDto>
            {
                new ChunkDto(0, 0, "Đây là nội dung", 3, null, null, null),
                new ChunkDto(0, 1, "tài liệu học tập PRN222.", 4, null, null, null)
            };

            _chunkingServiceMock.Setup(s => s.ChunkText("Đây là nội dung tài liệu học tập PRN222.", 512, 50))
                .Returns(chunkDtos);

            // Act
            await _documentService.ProcessDocumentAsync(docId);

            // Assert
            _documentRepoMock.Verify(r => r.UpdateStatusAsync(docId, DocumentStatus.Processing, null), Times.Once);
            _chunkRepoMock.Verify(r => r.DeleteByDocumentIdAsync(docId), Times.Once);
            _chunkRepoMock.Verify(r => r.AddRangeAsync(It.Is<IEnumerable<DocumentChunk>>(chunks => chunks.Count() == 2)), Times.Once);

            document.Status.Should().Be(DocumentStatus.Indexed);
            document.ChunkCount.Should().Be(2);
            document.ExtractedText.Should().Be("Đây là nội dung tài liệu học tập PRN222.");

            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Exactly(2)); // Once for processing, once for indexing
            _realtimeNotifierMock.Verify(n => n.NotifyDocumentChangedAsync(
                It.Is<DocumentRealtimeNotification>(notification =>
                    notification.DocumentId == docId &&
                    notification.CourseId == 1 &&
                    notification.FileName == "tailieu.pdf" &&
                    notification.Status == nameof(DocumentStatus.Indexed) &&
                    notification.Action == "indexed" &&
                    notification.ChunkCount == 2),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    [Fact]
    public async Task ProcessDocumentAsync_ShouldUseConfiguredChunkSizeAndOverlap()
    {
        const int docId = 43;
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        await File.WriteAllTextAsync(tempFilePath, "Large document body.");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Chunking:DefaultChunkSize"] = "2048",
                ["Chunking:DefaultOverlap"] = "150"
            })
            .Build();
        var documentService = new DocumentService(
            _documentRepoMock.Object,
            _chunkRepoMock.Object,
            _unitOfWorkMock.Object,
            _extractorFactory,
            _chunkingServiceMock.Object,
            _embeddingServiceMock.Object,
            _visionServiceMock.Object,
            _loggerMock.Object,
            configuration: configuration);

        try
        {
            var document = new Document
            {
                Id = docId,
                CourseId = 1,
                OriginalFileName = "large.pdf",
                ContentType = "application/pdf",
                StoragePath = tempFilePath,
                Status = DocumentStatus.Uploaded
            };
            _documentRepoMock.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(document);
            _extractorMock.Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), "application/pdf"))
                .ReturnsAsync(new ExtractedContentDto { Text = "Large document body." });
            _chunkingServiceMock.Setup(s => s.ChunkText("Large document body.", 2048, 150))
                .Returns(new List<ChunkDto>
                {
                    new(0, 0, "Large document body.", 3, null, null, null)
                });

            await documentService.ProcessDocumentAsync(docId);

            _chunkingServiceMock.Verify(
                s => s.ChunkText("Large document body.", 2048, 150),
                Times.Once);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    [Fact]
    public async Task ProcessDocumentAsync_ShouldStoreExtractedImagesPrivatelyBehindAuthorizedEndpoint()
    {
        const int documentId = 88;
        const int imageChunkId = 501;
        var sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
        await File.WriteAllBytesAsync(sourcePath, [0x25, 0x50, 0x44, 0x46]);
        var privateImagePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "App_Data",
            "uploads",
            "images",
            $"chunk-{imageChunkId}.png");
        DocumentChunk? capturedImageChunk = null;

        try
        {
            var document = new Document
            {
                Id = documentId,
                CourseId = 1,
                OriginalFileName = "diagram.pdf",
                ContentType = "application/pdf",
                StoragePath = sourcePath,
                Status = DocumentStatus.Uploaded
            };
            _documentRepoMock.Setup(r => r.GetByIdAsync(documentId)).ReturnsAsync(document);
            _extractorMock.Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), "application/pdf"))
                .ReturnsAsync(new ExtractedContentDto
                {
                    Images =
                    [
                        new ExtractedImageDto
                        {
                            Data = [0x89, 0x50, 0x4E, 0x47],
                            MimeType = "image/png",
                            PageNumber = 1
                        }
                    ]
                });
            _chunkingServiceMock.Setup(service => service.ChunkText(string.Empty, 512, 50))
                .Returns([]);
            _visionServiceMock.Setup(service => service.DescribeImageAsync(
                    It.IsAny<byte[]>(),
                    It.IsAny<string>()))
                .ReturnsAsync("UML diagram");
            _chunkRepoMock.Setup(repository => repository.AddRangeAsync(It.IsAny<IEnumerable<DocumentChunk>>()))
                .Callback<IEnumerable<DocumentChunk>>(chunks =>
                {
                    capturedImageChunk = chunks.Single();
                    capturedImageChunk.Id = imageChunkId;
                })
                .Returns(Task.CompletedTask);

            await _documentService.ProcessDocumentAsync(documentId);

            capturedImageChunk.Should().NotBeNull();
            capturedImageChunk!.ImageUrl.Should().Be($"/Chat/Session?handler=CitationImage&chunkId={imageChunkId}");
            File.Exists(privateImagePath).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(sourcePath))
            {
                File.Delete(sourcePath);
            }

            if (File.Exists(privateImagePath))
            {
                File.Delete(privateImagePath);
            }
        }
    }

    [Fact]
    public async Task ProcessDocumentAsync_ShouldRemoveChunkDependentsBeforeDeletingOldChunks_WhenReprocessing()
    {
        // Arrange
        int docId = 77;
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        await File.WriteAllTextAsync(tempFilePath, "Updated content for reprocessing.");

        var qaRepoMock = new Mock<IQAPairRepository>();
        var citationRepoMock = new Mock<IRepository<ChatCitation>>();

        try
        {
            var document = new Document
            {
                Id = docId,
                CourseId = 1,
                OriginalFileName = "reprocess.pdf",
                ContentType = "application/pdf",
                StoragePath = tempFilePath,
                Status = DocumentStatus.Indexed
            };

            var existingChunks = new List<DocumentChunk>
            {
                new() { Id = 10, DocumentId = docId, ChunkIndex = 0, Content = "Old chunk 1", TokenCount = 3 },
                new() { Id = 11, DocumentId = docId, ChunkIndex = 1, Content = "Old chunk 2", TokenCount = 3 }
            };

            var dependentQaPair = new QAPair
            {
                Id = 1,
                CourseId = 1,
                DocumentChunkId = 10,
                Question = "Old question",
                Answer = "Old answer"
            };

            var dependentCitation = new ChatCitation
            {
                Id = 1,
                ChunkId = 11,
                MessageId = 1,
                RelevanceScore = 0.8f
            };

            _documentRepoMock.Setup(r => r.GetByIdAsync(docId))
                .ReturnsAsync(document);

            _chunkRepoMock.Setup(r => r.GetByDocumentIdAsync(docId))
                .ReturnsAsync(existingChunks);

            _extractorMock.Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), "application/pdf"))
                .ReturnsAsync(new ExtractedContentDto { Text = "Updated content for reprocessing." });

            _chunkingServiceMock.Setup(s => s.ChunkText("Updated content for reprocessing.", 512, 50))
                .Returns(new List<ChunkDto>
                {
                    new(0, 0, "Updated content", 2, null, null, null)
                });

            qaRepoMock.Setup(r => r.GetQueryable())
                .Returns(new List<QAPair> { dependentQaPair }.AsQueryable());

            citationRepoMock.Setup(r => r.GetQueryable())
                .Returns(new List<ChatCitation> { dependentCitation }.AsQueryable());

            var qaDeletedBeforeChunks = false;
            var citationDeletedBeforeChunks = false;

            qaRepoMock.Setup(r => r.Delete(dependentQaPair))
                .Callback(() => qaDeletedBeforeChunks = true);

            citationRepoMock.Setup(r => r.Delete(dependentCitation))
                .Callback(() => citationDeletedBeforeChunks = true);

            _chunkRepoMock.Setup(r => r.DeleteByDocumentIdAsync(docId))
                .Callback(() =>
                {
                    qaDeletedBeforeChunks.Should().BeTrue("QAPairs keep a NoAction FK to DocumentChunks.");
                    citationDeletedBeforeChunks.Should().BeTrue("Chat citations keep a NoAction FK to DocumentChunks.");
                })
                .Returns(Task.CompletedTask);

            var documentService = new DocumentService(
                _documentRepoMock.Object,
                _chunkRepoMock.Object,
                _unitOfWorkMock.Object,
                _extractorFactory,
                _chunkingServiceMock.Object,
                _embeddingServiceMock.Object,
                _visionServiceMock.Object,
                _loggerMock.Object,
                qaPairRepository: qaRepoMock.Object,
                chatCitationRepository: citationRepoMock.Object);

            // Act
            await documentService.ProcessDocumentAsync(docId);

            // Assert
            qaRepoMock.Verify(r => r.Delete(dependentQaPair), Times.Once);
            citationRepoMock.Verify(r => r.Delete(dependentCitation), Times.Once);
            _chunkRepoMock.Verify(r => r.DeleteByDocumentIdAsync(docId), Times.Once);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    [Fact]
    public async Task ProcessDocumentAsync_ShouldRollbackAndMarkAsFailed_WhenExtractorFails()
    {
        // Arrange
        int docId = 42;
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        await File.WriteAllTextAsync(tempFilePath, "Dữ liệu rác.");

        try
        {
            var document = new Document
            {
                Id = docId,
                CourseId = 1,
                OriginalFileName = "tailieu_loi.pdf",
                ContentType = "application/pdf",
                StoragePath = tempFilePath,
                Status = DocumentStatus.Uploaded
            };

            _documentRepoMock.Setup(r => r.GetByIdAsync(docId))
                .ReturnsAsync(document);

            // Force extraction failure
            _extractorMock.Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), "application/pdf"))
                .ThrowsAsync(new InvalidOperationException("Lỗi đọc file PDF rồi!"));

            // Act
            Func<Task> act = () => _documentService.ProcessDocumentAsync(docId);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Lỗi đọc file PDF rồi!");

            _documentRepoMock.Verify(r => r.UpdateStatusAsync(docId, DocumentStatus.Processing, null), Times.Once);
            _documentRepoMock.Verify(r => r.UpdateStatusAsync(docId, DocumentStatus.Failed, "Lỗi đọc file PDF rồi!"), Times.Once);
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Never); // Fails before transaction starts
            _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(), Times.Never);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Exactly(2)); // Once for Processing status, once for Failed status
            _realtimeNotifierMock.Verify(n => n.NotifyDocumentChangedAsync(
                It.Is<DocumentRealtimeNotification>(notification =>
                    notification.DocumentId == docId &&
                    notification.CourseId == 1 &&
                    notification.FileName == "tailieu_loi.pdf" &&
                    notification.Status == nameof(DocumentStatus.Failed) &&
                    notification.Action == "failed" &&
                    notification.ChunkCount == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    [Fact]
    public async Task EnqueueProcessDocumentAsync_ShouldFallbackToSyncExecution_WhenScopeFactoryIsNull()
    {
        // Arrange
        int docId = 101;
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        await File.WriteAllTextAsync(tempFilePath, "Temp content.");

        try
        {
            var document = new Document
            {
                Id = docId,
                CourseId = 1,
                OriginalFileName = "test_sync_fallback.pdf",
                ContentType = "application/pdf",
                StoragePath = tempFilePath,
                Status = DocumentStatus.Uploaded
            };

            _documentRepoMock.Setup(r => r.GetByIdAsync(docId))
                .ReturnsAsync(document);

            // We will throw inside ExtractText to stop execution after status change, proving it reached sync ProcessDocumentAsync
            _extractorMock.Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), "application/pdf"))
                .ThrowsAsync(new InvalidOperationException("Stop here"));

            // Act
            Func<Task> act = () => _documentService.EnqueueProcessDocumentAsync(docId);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Stop here");

            // Verify status was changed to Processing and then Failed
            _documentRepoMock.Verify(r => r.UpdateStatusAsync(docId, DocumentStatus.Processing, null), Times.Exactly(2)); // Once in Enqueue, once in Process
            _documentRepoMock.Verify(r => r.UpdateStatusAsync(docId, DocumentStatus.Failed, "Stop here"), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Exactly(3)); // Save in Enqueue, Save in Process(Processing), Save in Process(Failed)
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    [Fact]
    public async Task EnqueueProcessDocumentAsync_ShouldAbortAndNotProcess_WhenDocumentIsAlreadyProcessing()
    {
        // Arrange
        int docId = 202;
        var document = new Document
        {
            Id = docId,
            CourseId = 1,
            OriginalFileName = "running.pdf",
            ContentType = "application/pdf",
            StoragePath = "path.pdf",
            Status = DocumentStatus.Processing // Already processing!
        };

        _documentRepoMock.Setup(r => r.GetByIdAsync(docId))
            .ReturnsAsync(document);

        // Act
        await _documentService.EnqueueProcessDocumentAsync(docId);

        // Assert
        // Verify we never update status or save changes since we aborted early
        _documentRepoMock.Verify(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<DocumentStatus>(), It.IsAny<string>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task EnqueueProcessDocumentAsync_ShouldRetry_WhenProcessingStatusIsStale()
    {
        // Arrange
        int docId = 203;
        var document = new Document
        {
            Id = docId,
            CourseId = 1,
            OriginalFileName = "stale.pdf",
            ContentType = "application/pdf",
            StoragePath = "path.pdf",
            Status = DocumentStatus.Processing,
            UpdatedAt = DateTime.UtcNow.AddHours(-2)
        };
        var queueMock = new Mock<IBackgroundTaskQueue>();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var customDocService = new DocumentService(
            _documentRepoMock.Object,
            _chunkRepoMock.Object,
            _unitOfWorkMock.Object,
            _extractorFactory,
            _chunkingServiceMock.Object,
            _embeddingServiceMock.Object,
            _visionServiceMock.Object,
            _loggerMock.Object,
            scopeFactory: scopeFactoryMock.Object,
            backgroundTaskQueue: queueMock.Object,
            realtimeNotifier: _realtimeNotifierMock.Object);

        _documentRepoMock.Setup(r => r.GetByIdAsync(docId))
            .ReturnsAsync(document);

        // Act
        await customDocService.EnqueueProcessDocumentAsync(docId);

        // Assert
        _documentRepoMock.Verify(r => r.UpdateStatusAsync(docId, DocumentStatus.Processing, null), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        queueMock.Verify(q => q.QueueAsync(It.IsAny<Func<IServiceProvider, CancellationToken, Task>>()), Times.Once);
    }

    [Fact]
    public async Task ArchiveDocumentAsync_ShouldPreserveDocumentAndStoredContent()
    {
        const int docId = 303;
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        await File.WriteAllTextAsync(tempFilePath, "Keep me");

        try
        {
            var document = new Document
            {
                Id = docId,
                CourseId = 1,
                OriginalFileName = "todelete.pdf",
                ContentType = "application/pdf",
                StoragePath = tempFilePath,
                Status = DocumentStatus.Indexed
            };

            _documentRepoMock.Setup(r => r.GetByIdAsync(docId))
                .ReturnsAsync(document);

            var beforeArchive = DateTime.UtcNow;
            await _documentService.ArchiveDocumentAsync(
                docId,
                "admin-user-id",
                "  Tài liệu không còn phù hợp cho RAG.  ");
            var afterArchive = DateTime.UtcNow;

            document.Status.Should().Be(DocumentStatus.Archived);
            document.ArchivedFromStatus.Should().Be(DocumentStatus.Indexed);
            document.ArchivedByUserId.Should().Be("admin-user-id");
            document.ArchiveReason.Should().Be("Tài liệu không còn phù hợp cho RAG.");
            document.ArchivedAt.Should().BeOnOrAfter(beforeArchive).And.BeOnOrBefore(afterArchive);
            document.ArchivedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
            document.UpdatedAt.Should().NotBeNull();
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
            _documentRepoMock.Verify(r => r.Delete(It.IsAny<Document>()), Times.Never);
            _chunkRepoMock.Verify(r => r.DeleteByDocumentIdAsync(It.IsAny<int>()), Times.Never);
            File.Exists(tempFilePath).Should().BeTrue();
            _realtimeNotifierMock.Verify(n => n.NotifyDocumentChangedAsync(
                It.Is<DocumentRealtimeNotification>(notification =>
                    notification.DocumentId == docId &&
                    notification.CourseId == 1 &&
                    notification.FileName == "todelete.pdf" &&
                    notification.Status == nameof(DocumentStatus.Archived) &&
                    notification.Action == "archived" &&
                    notification.ChunkCount == 0),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    [Fact]
    public async Task GetDocumentByIdAsync_ShouldMapArchiveAuditAndAllowLegacyNullAudit()
    {
        const int docId = 311;
        var archivedAt = new DateTime(2026, 7, 2, 3, 30, 0, DateTimeKind.Utc);
        var document = new Document
        {
            Id = docId,
            CourseId = 1,
            Course = new Course { Id = 1, Name = "PRN222" },
            Status = DocumentStatus.Archived,
            ArchivedByUserId = "admin-user-id",
            ArchivedByUser = new ApplicationUser { Id = "admin-user-id", Email = "admin@demo.local" },
            ArchivedAt = archivedAt,
            ArchiveReason = "Không còn phù hợp",
            ArchivedFromStatus = DocumentStatus.Indexed
        };
        _documentRepoMock.Setup(r => r.GetWithChunksAsync(docId)).ReturnsAsync(document);

        var result = await _documentService.GetDocumentByIdAsync(docId);

        result.Should().NotBeNull();
        result!.ArchivedByEmail.Should().Be("admin@demo.local");
        result.ArchivedAt.Should().Be(archivedAt);
        result.ArchiveReason.Should().Be("Không còn phù hợp");
        result.ArchivedFromStatus.Should().Be(nameof(DocumentStatus.Indexed));

        document.ArchivedByUserId = null;
        document.ArchivedByUser = null;
        document.ArchivedAt = null;
        document.ArchiveReason = null;
        document.ArchivedFromStatus = null;

        var legacyResult = await _documentService.GetDocumentByIdAsync(docId);

        legacyResult.Should().NotBeNull();
        legacyResult!.ArchivedByEmail.Should().BeNull();
        legacyResult.ArchivedAt.Should().BeNull();
        legacyResult.ArchiveReason.Should().BeNull();
        legacyResult.ArchivedFromStatus.Should().BeNull();
    }

    [Fact]
    public async Task ArchiveDocumentAsync_ShouldRejectProcessingDocument()
    {
        const int docId = 304;
        var document = new Document { Id = docId, Status = DocumentStatus.Processing };
        _documentRepoMock.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(document);

        var act = () => _documentService.ArchiveDocumentAsync(docId, "admin-user-id", "Đang xử lý");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Không thể tạm ẩn tài liệu khi hệ thống đang xử lý.");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ArchiveDocumentAsync_ShouldRejectAlreadyArchivedDocument()
    {
        const int docId = 305;
        var document = new Document { Id = docId, Status = DocumentStatus.Archived };
        _documentRepoMock.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(document);

        var act = () => _documentService.ArchiveDocumentAsync(docId, "admin-user-id", "Archive lại");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tài liệu đã được tạm ẩn khỏi RAG.");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
        _realtimeNotifierMock.Verify(
            n => n.NotifyDocumentChangedAsync(
                It.IsAny<DocumentRealtimeNotification>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ArchiveDocumentAsync_ShouldRejectMissingReason(string? reason)
    {
        var act = () => _documentService.ArchiveDocumentAsync(308, "admin-user-id", reason!);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Lý do tạm ẩn là bắt buộc.*");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ArchiveDocumentAsync_ShouldRejectReasonLongerThan1000Characters()
    {
        var act = () => _documentService.ArchiveDocumentAsync(309, "admin-user-id", new string('a', 1001));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Lý do tạm ẩn không được vượt quá 1000 ký tự.*");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ArchiveDocumentAsync_ShouldRejectMissingActor(string? actorUserId)
    {
        var act = () => _documentService.ArchiveDocumentAsync(310, actorUserId!, "Lý do hợp lệ");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Không xác định được tài khoản thực hiện tạm ẩn.");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ProcessDocumentAsync_ShouldRejectArchivedDocument()
    {
        const int docId = 306;
        _documentRepoMock.Setup(r => r.GetByIdAsync(docId))
            .ReturnsAsync(new Document { Id = docId, Status = DocumentStatus.Archived });

        var act = () => _documentService.ProcessDocumentAsync(docId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tài liệu đã tạm ẩn khỏi RAG nên không thể xử lý lại.");
        _documentRepoMock.Verify(
            r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<DocumentStatus>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task EnqueueProcessDocumentAsync_ShouldRejectArchivedDocument()
    {
        const int docId = 307;
        _documentRepoMock.Setup(r => r.GetByIdAsync(docId))
            .ReturnsAsync(new Document { Id = docId, Status = DocumentStatus.Archived });

        var act = () => _documentService.EnqueueProcessDocumentAsync(docId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tài liệu đã tạm ẩn khỏi RAG nên không thể xử lý lại.");
        _documentRepoMock.Verify(
            r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<DocumentStatus>(), It.IsAny<string?>()),
            Times.Never);
    }
}
