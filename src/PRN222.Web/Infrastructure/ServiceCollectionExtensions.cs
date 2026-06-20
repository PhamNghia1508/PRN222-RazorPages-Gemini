using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PRN222.BLL.Services;
using PRN222.BLL.Services.Interfaces;
using PRN222.BLL.Services.TextExtractors;
using PRN222.DAL.Data;
using PRN222.DAL.Entities;
using PRN222.DAL.Repositories;
using PRN222.DAL.Repositories.Interfaces;

namespace PRN222.Web.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationWeb(this IServiceCollection services)
    {
        services.AddRazorPages()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            });

        services.AddSignalR();
        services.AddMemoryCache();
        return services;
    }

    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ChatbotDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                builder =>
                {
                    builder.MigrationsAssembly("PRN222.DAL");
                    builder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                }));

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequiredLength = 6;
            })
            .AddEntityFrameworkStores<ChatbotDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IChunkRepository, ChunkRepository>();
        services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<IQAPairRepository, QAPairRepository>();

        return services;
    }

    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        services.AddScoped<ITextExtractorService, PdfTextExtractor>();
        services.AddScoped<ITextExtractorService, DocxTextExtractor>();
        services.AddScoped<ITextExtractorService, PptxTextExtractor>();
        services.AddScoped<ITextExtractorService, PptTextExtractor>();
        services.AddScoped<TextExtractorFactory>();

        services.AddScoped<IDocumentRealtimeNotifier, SignalRDocumentRealtimeNotifier>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<ICourseAssignmentService, CourseAssignmentService>();
        services.AddScoped<ICourseAccessService, CourseAccessService>();
        services.AddScoped<IKnowledgeCurationService, KnowledgeCurationService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IChunkingService, ChunkingService>();
        services.AddScoped<ICodeExecutionService, RoslynCodeExecutionService>();
        services.AddHttpClient();

        services.AddScoped<PRN222.BLL.Services.AI.GeminiEmbeddingService>();
        services.AddScoped<IEmbeddingService, PRN222.BLL.Services.AI.GeminiEmbeddingService>();
        services.AddScoped<PRN222.BLL.Services.AI.MultilingualE5EmbeddingService>();
        services.AddScoped<IEmbeddingService, PRN222.BLL.Services.AI.MultilingualE5EmbeddingService>();
        services.AddScoped<PRN222.BLL.Services.AI.OpenAIEmbeddingService>();
        services.AddScoped<IEmbeddingService, PRN222.BLL.Services.AI.OpenAIEmbeddingService>();
        services.AddScoped<IEmbeddingService>(sp =>
            sp.GetRequiredService<PRN222.BLL.Services.AI.GeminiEmbeddingService>());

        services.AddScoped<EmbeddingServiceFactory>();
        services.AddScoped<ILlmService, PRN222.BLL.Services.AI.GeminiLlmService>();
        services.AddScoped<IGeminiVisionService, PRN222.BLL.Services.AI.GeminiVisionService>();
        services.AddScoped<IFineTunedModelService, PRN222.BLL.Services.AI.HuggingFaceFineTunedModelService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IFinetuneService, FinetuneService>();
        services.AddScoped<IBenchmarkService, BenchmarkService>();
        services.AddScoped<IRagRetrievalService, PRN222.BLL.Services.Rag.RagRetrievalService>();
        services.AddScoped<ITestSetGeneratorService, TestSetGeneratorService>();
        services.AddSingleton<ITestSetGenerationJobManager, TestSetGenerationJobManager>();
        services.AddSingleton<BackgroundTaskQueue>();
        services.AddSingleton<IBackgroundTaskQueue>(sp => sp.GetRequiredService<BackgroundTaskQueue>());
        services.AddHostedService<QueuedHostedService>();

        return services;
    }

    public static IServiceCollection AddUploadLimits(this IServiceCollection services)
    {
        services.Configure<IISServerOptions>(options =>
        {
            options.MaxRequestBodySize = 50 * 1024 * 1024;
        });

        return services;
    }
}

