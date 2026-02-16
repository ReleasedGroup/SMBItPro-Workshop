namespace Helpdesk.Light.Domain.Entities;

public sealed class PlatformSetting
{
    private PlatformSetting()
    {
        Key = string.Empty;
        Value = string.Empty;
    }

    public PlatformSetting(string key, string value, DateTime updatedUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        Key = key.Trim();
        Value = value ?? string.Empty;
        UpdatedUtc = updatedUtc;
    }

    public string Key { get; private set; }

    public string Value { get; private set; }

    public DateTime UpdatedUtc { get; private set; }

    public void UpdateValue(string value, DateTime updatedUtc)
    {
        Value = value ?? string.Empty;
        UpdatedUtc = updatedUtc;
    }
}
