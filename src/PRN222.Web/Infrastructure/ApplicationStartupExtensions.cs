using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Data;
using PRN222.DAL.Entities;

namespace PRN222.Web.Infrastructure;

public static class ApplicationStartupExtensions
{
    public static void MigrateAndSeedEmbeddingModels(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ChatbotDbContext>();
        dbContext.Database.Migrate();
        SeedApplicationRoles(scope.ServiceProvider).GetAwaiter().GetResult();

        var adminOptions = SeededAdminOptions.FromConfiguration(app.Configuration);
        SeedDemoAccount(
            scope.ServiceProvider,
            adminOptions.Email,
            adminOptions.Password,
            adminOptions.IsConfigured,
            ApplicationRoles.Admin,
            "admin").GetAwaiter().GetResult();

        var headLecturerOptions = SeededHeadLecturerOptions.FromConfiguration(app.Configuration);
        SeedDemoAccount(
            scope.ServiceProvider,
            headLecturerOptions.Email,
            headLecturerOptions.Password,
            headLecturerOptions.IsConfigured,
            ApplicationRoles.HeadLecturer,
            "head lecturer").GetAwaiter().GetResult();

        var lecturerOptions = SeededLecturerOptions.FromConfiguration(app.Configuration);
        SeedDemoAccount(
            scope.ServiceProvider,
            lecturerOptions.Email,
            lecturerOptions.Password,
            lecturerOptions.IsConfigured,
            ApplicationRoles.Lecturer,
            "lecturer").GetAwaiter().GetResult();

        var studentOptions = SeededStudentOptions.FromConfiguration(app.Configuration);
        SeedDemoAccount(
            scope.ServiceProvider,
            studentOptions.Email,
            studentOptions.Password,
            studentOptions.IsConfigured,
            ApplicationRoles.Student,
            "student").GetAwaiter().GetResult();

        var realEmbeddingServices = scope.ServiceProvider
            .GetServices<IEmbeddingService>()
            .GroupBy(s => s.ModelName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Where(s => IsEmbeddingProviderConfigured(s.ModelName, app.Configuration))
            .ToList();

        var existingModels = dbContext.EmbeddingModels.ToList();
        foreach (var service in realEmbeddingServices)
        {
            var model = existingModels.FirstOrDefault(m =>
                m.Name.Equals(service.ModelName, StringComparison.OrdinalIgnoreCase));

            if (model is null)
            {
                dbContext.EmbeddingModels.Add(new EmbeddingModel
                {
                    Name = service.ModelName,
                    Provider = ResolveProvider(service.ModelName),
                    Dimensions = service.Dimensions,
                    IsActive = true
                });
            }
            else
            {
                model.Provider = ResolveProvider(service.ModelName);
                model.Dimensions = service.Dimensions;
                model.IsActive = true;
            }
        }

        var realModelNames = realEmbeddingServices
            .Select(s => s.ModelName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var model in existingModels.Where(m => !realModelNames.Contains(m.Name)))
        {
            model.IsActive = false;
        }

        dbContext.SaveChanges();
    }

    private static async Task SeedApplicationRoles(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in ApplicationRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task SeedDemoAccount(
        IServiceProvider serviceProvider,
        string email,
        string password,
        bool isConfigured,
        string role,
        string accountLabel)
    {
        if (!isConfigured)
        {
            return;
        }

        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Could not seed {accountLabel} account '{email}': {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var roleResult = await userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                var errors = string.Join("; ", roleResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Could not assign {role} role to '{email}': {errors}");
            }
        }
    }

    private static string ResolveProvider(string modelName) => modelName switch
    {
        "gemini-embedding-001" => "Google Gemini",
        "multilingual-e5-base" => "HuggingFace",
        "text-embedding-3-small" => "OpenAI",
        _ => "External"
    };

    private static bool IsEmbeddingProviderConfigured(string modelName, IConfiguration configuration) => modelName switch
    {
        "gemini-embedding-001" => !string.IsNullOrWhiteSpace(configuration["Gemini:ApiKey"]),
        "multilingual-e5-base" => !string.IsNullOrWhiteSpace(configuration["HuggingFace:ApiToken"]),
        "text-embedding-3-small" => !string.IsNullOrWhiteSpace(configuration["OpenAI:ApiKey"]),
        _ => true
    };
}
