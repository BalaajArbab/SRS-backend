using System.ComponentModel.DataAnnotations;

namespace SRS_backend.Configuration;

public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    [Required]
    [RegularExpression("^[a-z0-9](?:[a-z0-9-]{1,61}[a-z0-9])?$")]
    public string ContainerName { get; init; } = "images";

    // Category images are intended for storefront display. Blob-level public access
    // allows reads of a known URL but does not allow listing the container.
    public bool PublicBlobAccess { get; init; } = true;

    [Range(1, 4_096)]
    public int MaximumImageDimension { get; init; } = 1_600;

    [Range(0, 100)]
    public int WebpQuality { get; init; } = 82;
}
