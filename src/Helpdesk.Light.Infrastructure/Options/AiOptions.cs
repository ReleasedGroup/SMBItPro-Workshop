namespace Helpdesk.Light.Infrastructure.Options;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public string ModelId { get; init; } = "gpt-5.2";

    public string? OpenAIApiKey { get; init; }

    public bool EnableAi { get; init; } = true;

    public bool AutoRunOnCreateOrUpdate { get; init; } = true;
}
