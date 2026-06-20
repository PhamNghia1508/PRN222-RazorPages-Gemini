using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.DAL.Repositories.Interfaces;
using PRN222.Tests.Services;
using Xunit;

namespace PRN222.Tests.BLL;

public class DepartmentServiceTests
{
    private readonly Mock<IRepository<Department>> _deptRepoMock;
    private readonly Mock<IRepository<Course>> _courseRepoMock;
    private readonly Mock<IRepository<ApplicationUser>> _userRepoMock;
    private readonly Mock<ICourseAccessService> _courseAccessServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly DepartmentService _departmentService;

    public DepartmentServiceTests()
    {
        _deptRepoMock = new Mock<IRepository<Department>>();
        _courseRepoMock = new Mock<IRepository<Course>>();
        _userRepoMock = new Mock<IRepository<ApplicationUser>>();
        _courseAccessServiceMock = new Mock<ICourseAccessService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _departmentService = new DepartmentService(
            _deptRepoMock.Object,
            _courseRepoMock.Object,
            _userRepoMock.Object,
            _courseAccessServiceMock.Object,
            _unitOfWorkMock.Object
        );
    }

    [Fact]
    public async Task AssignUserToDepartment_ShouldSetDepartmentId()
    {
        // Arrange
        var userId = "user-1";
        var deptId = 42;
        var user = new ApplicationUser { Id = userId, DepartmentId = null };
        var users = new List<ApplicationUser> { user };
        var departments = new List<Department> { new Department { Id = deptId, Name = "CNTT" } };

        _userRepoMock.Setup(r => r.GetQueryable())
            .Returns(users.AsAsyncQueryable());
        _deptRepoMock.Setup(r => r.GetQueryable())
            .Returns(departments.AsAsyncQueryable());

        // Act
        var revoked = await _departmentService.AssignUserToDepartmentAsync(userId, deptId);

        // Assert
        revoked.Should().Be(0);
        user.DepartmentId.Should().Be(deptId);
        _userRepoMock.Verify(r => r.Update(user), Times.Once);
        _courseAccessServiceMock.Verify(s => s.ReconcileUserAssignmentsAsync(userId), Times.Once);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Exactly(2));
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task AssignCourseToDepartment_ShouldSetDepartmentId()
    {
        // Arrange
        var courseId = 10;
        var deptId = 42;
        var course = new Course { Id = courseId, DepartmentId = null };
        var departments = new List<Department> { new Department { Id = deptId, Name = "CNTT" } };

        _courseRepoMock.Setup(r => r.GetByIdAsync(courseId))
            .ReturnsAsync(course);
        _deptRepoMock.Setup(r => r.GetQueryable())
            .Returns(departments.AsAsyncQueryable());

        // Act
        var revoked = await _departmentService.AssignCourseToDepartmentAsync(courseId, deptId);

        // Assert
        revoked.Should().Be(0);
        course.DepartmentId.Should().Be(deptId);
        _courseRepoMock.Verify(r => r.Update(course), Times.Once);
        _courseAccessServiceMock.Verify(s => s.ReconcileCourseAssignmentsAsync(courseId), Times.Once);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Exactly(2));
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task AssignUserToDepartmentAsync_WhenDepartmentChanges_RevokesInvalidAssignmentsInTransaction()
    {
        var userId = "staff-1";
        var departmentId = 20;
        var user = new ApplicationUser { Id = userId, DepartmentId = 10 };
        _userRepoMock.Setup(r => r.GetQueryable())
            .Returns(new List<ApplicationUser> { user }.AsAsyncQueryable());
        _deptRepoMock.Setup(r => r.GetQueryable())
            .Returns(new List<Department> { new Department { Id = departmentId, Name = "SE" } }.AsAsyncQueryable());
        _courseAccessServiceMock
            .Setup(s => s.ReconcileUserAssignmentsAsync(userId))
            .ReturnsAsync(2);

        var revoked = await _departmentService.AssignUserToDepartmentAsync(userId, departmentId);

        revoked.Should().Be(2);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
        _courseAccessServiceMock.Verify(s => s.ReconcileUserAssignmentsAsync(userId), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task AssignCourseToDepartmentAsync_WhenReconciliationFails_RollsBack()
    {
        var courseId = 5;
        var departmentId = 20;
        _courseRepoMock.Setup(r => r.GetByIdAsync(courseId))
            .ReturnsAsync(new Course { Id = courseId, DepartmentId = 10 });
        _deptRepoMock.Setup(r => r.GetQueryable())
            .Returns(new List<Department> { new Department { Id = departmentId, Name = "SE" } }.AsAsyncQueryable());
        _courseAccessServiceMock
            .Setup(s => s.ReconcileCourseAssignmentsAsync(courseId))
            .ThrowsAsync(new InvalidOperationException("reconcile failed"));

        var act = () => _departmentService.AssignCourseToDepartmentAsync(courseId, departmentId);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task GetCoursesInDepartment_ShouldReturnOnlySameDeptCourses()
    {
        // Arrange
        var deptId = 42;
        var courses = new List<Course>
        {
            new Course { Id = 1, Name = "C1", DepartmentId = deptId },
            new Course { Id = 2, Name = "C2", DepartmentId = deptId },
            new Course { Id = 3, Name = "C3", DepartmentId = 99 }
        };

        _courseRepoMock.Setup(r => r.GetQueryable())
            .Returns(courses.AsAsyncQueryable());

        // Act
        var result = await _departmentService.GetCoursesInDepartmentAsync(deptId);

        // Assert
        result.Should().HaveCount(2);
        result.Select(c => c.Id).Should().Contain(new[] { 1, 2 });
        result.Select(c => c.Id).Should().NotContain(3);
    }
}

public class KnowledgeCurationService_DepartmentGuardTests
{
    private readonly Mock<IRepository<KnowledgeAuditLog>> _logRepoMock;
    private readonly Mock<IRepository<Document>> _docRepoMock;
    private readonly Mock<IRepository<DocumentChunk>> _chunkRepoMock;
    private readonly Mock<IRepository<ChunkEmbedding>> _embeddingRepoMock;
    private readonly Mock<IRepository<ChatCitation>> _citationRepoMock;
    private readonly Mock<IRepository<ApplicationUser>> _userRepoMock;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly KnowledgeCurationService _curationService;

    public KnowledgeCurationService_DepartmentGuardTests()
    {
        _logRepoMock = new Mock<IRepository<KnowledgeAuditLog>>();
        _docRepoMock = new Mock<IRepository<Document>>();
        _chunkRepoMock = new Mock<IRepository<DocumentChunk>>();
        _embeddingRepoMock = new Mock<IRepository<ChunkEmbedding>>();
        _citationRepoMock = new Mock<IRepository<ChatCitation>>();
        _userRepoMock = new Mock<IRepository<ApplicationUser>>();
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _embeddingServiceMock.Setup(e => e.ModelName).Returns("test-embedding-model");
        _embeddingServiceMock.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f });

        _curationService = new KnowledgeCurationService(
            _logRepoMock.Object,
            _docRepoMock.Object,
            _chunkRepoMock.Object,
            _embeddingRepoMock.Object,
            _citationRepoMock.Object,
            _userRepoMock.Object,
            _embeddingServiceMock.Object,
            _unitOfWorkMock.Object
        );
    }

    [Fact]
    public async Task ApproveAsync_WhenSameDepartment_ShouldSucceed()
    {
        // Arrange
        var logId = 1;
        var approvedByUserId = "approver-1";
        var deptId = 10;

        var department = new Department { Id = deptId, Name = "CNTT" };
        var course = new Course { Id = 100, Name = "PRN222", DepartmentId = deptId, Department = department };
        var log = new KnowledgeAuditLog
        {
            Id = logId,
            CourseId = 100,
            Course = course,
            Status = "Pending",
            TriggeredQuestion = "What is xUnit?",
            SuggestedAnswer = "A testing framework."
        };

        var logs = new List<KnowledgeAuditLog> { log };
        var user = new ApplicationUser { Id = approvedByUserId, DepartmentId = deptId, Department = department };
        var users = new List<ApplicationUser> { user };

        _logRepoMock.Setup(r => r.GetQueryable())
            .Returns(logs.AsAsyncQueryable());

        _userRepoMock.Setup(r => r.GetQueryable())
            .Returns(users.AsAsyncQueryable());

        var virtualDocFileName = $"clarifications_course_{log.CourseId}.txt";
        var documents = new List<Document>();
        _docRepoMock.Setup(r => r.GetQueryable())
            .Returns(documents.AsAsyncQueryable());

        var chunks = new List<DocumentChunk>();
        _chunkRepoMock.Setup(r => r.GetQueryable())
            .Returns(chunks.AsAsyncQueryable());

        // Act
        var result = await _curationService.ApproveCorrectionAsync(
            logId,
            approvedByUserId,
            new[] { 100 },
            isAdmin: false);

        // Assert
        result.Should().BeTrue();
        log.Status.Should().Be("Approved");
        log.ApprovedByUserId.Should().Be(approvedByUserId);

        _docRepoMock.Verify(r => r.AddAsync(It.Is<Document>(d => d.FileName == virtualDocFileName)), Times.Once);
        _chunkRepoMock.Verify(r => r.AddAsync(It.Is<DocumentChunk>(c => c.Content.Contains("What is xUnit?"))), Times.Once);
        _embeddingRepoMock.Verify(r => r.AddAsync(It.IsAny<ChunkEmbedding>()), Times.Once);
        _logRepoMock.Verify(r => r.Update(log), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ApproveAsync_WhenEmbeddingFails_ShouldNotCreatePartialKnowledgeRecords()
    {
        var logId = 1;
        var approvedByUserId = "approver-1";
        var course = new Course { Id = 100, Name = "PRN222", DepartmentId = 10 };
        var log = new KnowledgeAuditLog
        {
            Id = logId,
            CourseId = 100,
            Course = course,
            Status = "Pending",
            TriggeredQuestion = "What is xUnit?",
            SuggestedAnswer = "A testing framework."
        };
        _logRepoMock.Setup(r => r.GetQueryable())
            .Returns(new List<KnowledgeAuditLog> { log }.AsAsyncQueryable());
        _userRepoMock.Setup(r => r.GetQueryable())
            .Returns(new List<ApplicationUser>
            {
                new() { Id = approvedByUserId, DepartmentId = 10 }
            }.AsAsyncQueryable());
        _docRepoMock.Setup(r => r.GetQueryable())
            .Returns(new List<Document>().AsAsyncQueryable());
        _chunkRepoMock.Setup(r => r.GetQueryable())
            .Returns(new List<DocumentChunk>().AsAsyncQueryable());
        _embeddingServiceMock.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Embedding unavailable"));

        var act = () => _curationService.ApproveCorrectionAsync(
            logId,
            approvedByUserId,
            new[] { 100 },
            isAdmin: false);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _docRepoMock.Verify(r => r.AddAsync(It.IsAny<Document>()), Times.Never);
        _chunkRepoMock.Verify(r => r.AddAsync(It.IsAny<DocumentChunk>()), Times.Never);
        _embeddingRepoMock.Verify(r => r.AddAsync(It.IsAny<ChunkEmbedding>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ApproveAsync_WhenDifferentDepartment_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var logId = 1;
        var approvedByUserId = "approver-1";

        var course = new Course { Id = 100, Name = "PRN222", DepartmentId = 10 };
        var log = new KnowledgeAuditLog
        {
            Id = logId,
            CourseId = 100,
            Course = course,
            Status = "Pending"
        };

        var logs = new List<KnowledgeAuditLog> { log };
        var user = new ApplicationUser { Id = approvedByUserId, DepartmentId = 20 };
        var users = new List<ApplicationUser> { user };

        _logRepoMock.Setup(r => r.GetQueryable())
            .Returns(logs.AsAsyncQueryable());

        _userRepoMock.Setup(r => r.GetQueryable())
            .Returns(users.AsAsyncQueryable());

        // Act & Assert
        var act = () => _curationService.ApproveCorrectionAsync(
            logId,
            approvedByUserId,
            new[] { 100 },
            isAdmin: false);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ApproveAsync_WhenApproverHasNoDepartment_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var logId = 1;
        var approvedByUserId = "approver-1";

        var course = new Course { Id = 100, Name = "PRN222", DepartmentId = 10 };
        var log = new KnowledgeAuditLog
        {
            Id = logId,
            CourseId = 100,
            Course = course,
            Status = "Pending"
        };

        var logs = new List<KnowledgeAuditLog> { log };
        var user = new ApplicationUser { Id = approvedByUserId, DepartmentId = null };
        var users = new List<ApplicationUser> { user };

        _logRepoMock.Setup(r => r.GetQueryable())
            .Returns(logs.AsAsyncQueryable());

        _userRepoMock.Setup(r => r.GetQueryable())
            .Returns(users.AsAsyncQueryable());

        // Act & Assert
        var act = () => _curationService.ApproveCorrectionAsync(
            logId,
            approvedByUserId,
            new[] { 100 },
            isAdmin: false);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ApproveAsync_WhenCourseHasNoDepartment_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var logId = 1;
        var approvedByUserId = "approver-1";

        var course = new Course { Id = 100, Name = "PRN222", DepartmentId = null };
        var log = new KnowledgeAuditLog
        {
            Id = logId,
            CourseId = 100,
            Course = course,
            Status = "Pending"
        };

        var logs = new List<KnowledgeAuditLog> { log };
        var user = new ApplicationUser { Id = approvedByUserId, DepartmentId = 10 };
        var users = new List<ApplicationUser> { user };

        _logRepoMock.Setup(r => r.GetQueryable())
            .Returns(logs.AsAsyncQueryable());

        _userRepoMock.Setup(r => r.GetQueryable())
            .Returns(users.AsAsyncQueryable());

        // Act & Assert
        var act = () => _curationService.ApproveCorrectionAsync(
            logId,
            approvedByUserId,
            new[] { 100 },
            isAdmin: false);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RejectAsync_WhenDifferentDepartment_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var logId = 1;
        var approvedByUserId = "approver-1";

        var course = new Course { Id = 100, Name = "PRN222", DepartmentId = 10 };
        var log = new KnowledgeAuditLog
        {
            Id = logId,
            CourseId = 100,
            Course = course,
            Status = "Pending"
        };

        var logs = new List<KnowledgeAuditLog> { log };
        var user = new ApplicationUser { Id = approvedByUserId, DepartmentId = 20 };
        var users = new List<ApplicationUser> { user };

        _logRepoMock.Setup(r => r.GetQueryable())
            .Returns(logs.AsAsyncQueryable());

        _userRepoMock.Setup(r => r.GetQueryable())
            .Returns(users.AsAsyncQueryable());

        // Act & Assert
        var act = () => _curationService.RejectCorrectionAsync(
            logId,
            "No reason",
            approvedByUserId,
            new[] { 100 },
            isAdmin: false);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RollbackAsync_WhenDifferentDepartment_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var logId = 1;
        var rollbackById = "approver-1";

        var course = new Course { Id = 100, Name = "PRN222", DepartmentId = 10 };
        var log = new KnowledgeAuditLog
        {
            Id = logId,
            CourseId = 100,
            Course = course,
            Status = "Approved",
            CreatedChunkId = 5
        };

        var logs = new List<KnowledgeAuditLog> { log };
        var user = new ApplicationUser { Id = rollbackById, DepartmentId = 20 };
        var users = new List<ApplicationUser> { user };

        _logRepoMock.Setup(r => r.GetQueryable())
            .Returns(logs.AsAsyncQueryable());

        _userRepoMock.Setup(r => r.GetQueryable())
            .Returns(users.AsAsyncQueryable());

        // Act & Assert
        var act = () => _curationService.RollbackCorrectionAsync(
            logId,
            rollbackById,
            new[] { 100 },
            isAdmin: false);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetPendingProposalsAsync_ShouldOnlyReturnSameDeptProposals()
    {
        // Arrange
        var userId = "user-1";
        var deptId = 10;

        var user = new ApplicationUser { Id = userId, DepartmentId = deptId };
        var users = new List<ApplicationUser> { user };

        var courseSame = new Course { Id = 100, DepartmentId = deptId };
        var courseDiff = new Course { Id = 200, DepartmentId = 20 };

        var logSame = new KnowledgeAuditLog { Id = 1, Course = courseSame, Status = "Pending" };
        var logDiff = new KnowledgeAuditLog { Id = 2, Course = courseDiff, Status = "Pending" };
        var logs = new List<KnowledgeAuditLog> { logSame, logDiff };

        _userRepoMock.Setup(r => r.GetQueryable())
            .Returns(users.AsAsyncQueryable());

        _logRepoMock.Setup(r => r.GetQueryable())
            .Returns(logs.AsAsyncQueryable());

        // Act
        var result = await _curationService.GetPendingProposalsAsync(userId, isAdmin: false);

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(1);
    }

    [Fact]
    public async Task GetAuditHistoryAsync_ShouldOnlyReturnSameDeptHistory()
    {
        // Arrange
        var userId = "user-1";
        var deptId = 10;

        var user = new ApplicationUser { Id = userId, DepartmentId = deptId };
        var users = new List<ApplicationUser> { user };

        var courseSame = new Course { Id = 100, DepartmentId = deptId };
        var courseDiff = new Course { Id = 200, DepartmentId = 20 };

        var logSame = new KnowledgeAuditLog { Id = 1, Course = courseSame, Status = "Approved" };
        var logDiff = new KnowledgeAuditLog { Id = 2, Course = courseDiff, Status = "Rejected" };
        var logs = new List<KnowledgeAuditLog> { logSame, logDiff };

        _userRepoMock.Setup(r => r.GetQueryable())
            .Returns(users.AsAsyncQueryable());

        _logRepoMock.Setup(r => r.GetQueryable())
            .Returns(logs.AsAsyncQueryable());

        // Act
        var result = await _curationService.GetAuditHistoryByUserAsync(userId, isAdmin: false);

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(1);
    }

    [Fact]
    public async Task ProposeCorrectionAsync_ShouldRejectCourseOutsideAssignedScope()
    {
        var dto = new ProposeCorrectionDto(200, null, "Question", "Correction");

        var result = await _curationService.ProposeCorrectionAsync(
            dto,
            "lecturer-1",
            new[] { 100 },
            isAdmin: false);

        result.Should().BeFalse();
        _logRepoMock.Verify(r => r.AddAsync(It.IsAny<KnowledgeAuditLog>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ProposeCorrectionAsync_ShouldRejectMissingCourseInsteadOfUsingFallback()
    {
        var dto = new ProposeCorrectionDto(0, null, "Question", "Correction");

        var result = await _curationService.ProposeCorrectionAsync(
            dto,
            "lecturer-1",
            new[] { 100 },
            isAdmin: false);

        result.Should().BeFalse();
        _docRepoMock.Verify(r => r.GetQueryable(), Times.Never);
        _logRepoMock.Verify(r => r.AddAsync(It.IsAny<KnowledgeAuditLog>()), Times.Never);
    }
}
