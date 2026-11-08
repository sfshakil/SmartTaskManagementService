namespace SmartTaskManagement.Infrastructure.Options;

public class SeedOptions
{
    public const string SectionName = "Seed";

    public string AdminFullName { get; set; } = "System Administrator";
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
}
