using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SRS_backend.Dtos.Analytics;

public sealed class TrackCartAdditionRequest
{
    [Required]
    [StringLength(24)]
    public required string ProductId { get; init; }

    [Range(1, 100)]
    [DefaultValue(1)]
    public int QuantityAdded { get; init; } = 1;
}
