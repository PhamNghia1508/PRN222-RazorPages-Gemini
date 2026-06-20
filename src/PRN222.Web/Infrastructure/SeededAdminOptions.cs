namespace PRN222.Web.Infrastructure;

public sealed class SeededAdminOptions
{
    private const string SectionName = "SeedAccounts:Admin";

    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password);

    public static SeededAdminOptions FromConfiguration(IConfiguration configuration)
    {
        return new SeededAdminOptions
        {
            Email = configuration[$"{SectionName}:Email"] ?? string.Empty,
            Password = configuration[$"{SectionName}:Password"] ?? string.Empty
        };
    }
}
