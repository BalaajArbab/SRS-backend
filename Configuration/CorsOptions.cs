using System.ComponentModel.DataAnnotations;

namespace SRS_backend.Configuration;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    [Required]
    [MinLength(1)]
    public string[] AllowedOrigins { get; init; } = [];
}
