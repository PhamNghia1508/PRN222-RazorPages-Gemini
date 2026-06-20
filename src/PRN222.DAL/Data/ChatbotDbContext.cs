using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PRN222.DAL.Entities;

namespace PRN222.DAL.Data;

/// <summary>
/// Main database context for the Chatbot Q&A application.
/// </summary>
public class ChatbotDbContext : IdentityDbContext<ApplicationUser>
{
    public ChatbotDbContext(DbContextOptions<ChatbotDbContext> options) : base(options)
    {
    }

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<ChunkEmbedding> ChunkEmbeddings => Set<ChunkEmbedding>();
    public DbSet<EmbeddingModel> EmbeddingModels => Set<EmbeddingModel>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatCitation> ChatCitations => Set<ChatCitation>();
    public DbSet<BenchmarkRun> BenchmarkRuns => Set<BenchmarkRun>();
    public DbSet<BenchmarkResult> BenchmarkResults => Set<BenchmarkResult>();
    public DbSet<QAPair> QAPairs => Set<QAPair>();
    public DbSet<ApplicationUserCourse> ApplicationUserCourses => Set<ApplicationUserCourse>();
    public DbSet<KnowledgeAuditLog> KnowledgeAuditLogs => Set<KnowledgeAuditLog>();
    public DbSet<Department> Departments => Set<Department>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChatbotDbContext).Assembly);
    }
}
