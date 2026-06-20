namespace PRN222.Web.Infrastructure;

public sealed class SeededLecturerOptions
{
    private const string SectionName = "SeedAccounts:Lecturer";

    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password);

    public static SeededLecturerOptions FromConfiguration(IConfiguration configuration)
    {
        return new SeededLecturerOptions
        {
            Email = configuration[$"{SectionName}:Email"] ?? string.Empty,
            Password = configuration[$"{SectionName}:Password"] ?? string.Empty
        };
    }
}
