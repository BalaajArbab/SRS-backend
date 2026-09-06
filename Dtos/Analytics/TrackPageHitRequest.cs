using System.ComponentModel.DataAnnotations;
using SRS_backend.Models;

namespace SRS_backend.Dtos.Analytics;

public sealed class TrackPageHitRequest
{
    [Required]
    [EnumDataType(typeof(PageHitType))]
    public PageHitType? PageType { get; init; }

    [StringLength(24)]
    public string? ResourceId { get; init; }
}
