namespace Helpdesk.Light.Infrastructure.Data;

public static class SeedDataConstants
{
    public static readonly Guid ContosoCustomerId = Guid.Parse("8a4ed0b5-3de2-470f-a779-9b96216e6dd8");
    public static readonly Guid FabrikamCustomerId = Guid.Parse("db7d5600-bf96-49d6-a9f4-eab6239cfafd");

    public const string AdminEmail = "admin@msp.local";
    public const string ContosoTechEmail = "tech@contoso.com";
    public const string FabrikamTechEmail = "tech@fabrikam.com";
    public const string DefaultPassword = "Pass!12345";
}
