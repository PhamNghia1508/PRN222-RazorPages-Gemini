namespace PRN222.Web.Infrastructure;

public sealed class SeededHeadLecturerOptions
{
    private const string SectionName = "SeedAccounts:HeadLecturer";

    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password);

    public static SeededHeadLecturerOptions FromConfiguration(IConfiguration configuration)
    {
        return new SeededHeadLecturerOptions
        {
            Email = configuration[$"{SectionName}:Email"] ?? string.Empty,
            Password = configuration[$"{SectionName}:Password"] ?? string.Empty
        };
    }
}
