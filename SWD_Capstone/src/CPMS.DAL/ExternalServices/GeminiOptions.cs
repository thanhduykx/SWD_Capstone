namespace CPMS.DAL.ExternalServices;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = "gemini-2.5-flash";
    public int TimeoutSeconds { get; init; } = 30;
}
