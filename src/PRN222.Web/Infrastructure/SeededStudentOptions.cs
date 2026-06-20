namespace PRN222.Web.Infrastructure;

public sealed class SeededStudentOptions
{
    private const string SectionName = "SeedAccounts:Student";

    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password);

    public static SeededStudentOptions FromConfiguration(IConfiguration configuration)
    {
        return new SeededStudentOptions
        {
            Email = configuration[$"{SectionName}:Email"] ?? string.Empty,
            Password = configuration[$"{SectionName}:Password"] ?? string.Empty
        };
    }
}
