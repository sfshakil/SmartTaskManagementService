using System.ComponentModel.DataAnnotations;

namespace SmartTaskManagement.Infrastructure.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required, MinLength(32, ErrorMessage = "Jwt:SecretKey must be at least 32 characters long.")]
    public string SecretKey { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 7;
}
