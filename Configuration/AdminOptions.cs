using System.ComponentModel.DataAnnotations;

namespace SRS_backend.Configuration;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    [Required(AllowEmptyStrings = false)]
    public string Username { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string PasswordHash { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string JwtSigningKey { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string JwtIssuer { get; init; } = "srs-backend";

    [Required(AllowEmptyStrings = false)]
    public string JwtAudience { get; init; } = "srs-backend-admin";

    [Range(1, 365)]
    public int TokenLifetimeDays { get; init; } = 30;
}
