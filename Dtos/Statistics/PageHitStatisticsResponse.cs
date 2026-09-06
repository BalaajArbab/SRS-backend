using SRS_backend.Models;

namespace SRS_backend.Dtos.Statistics;

public sealed record PageHitStatisticsResponse(
    PageHitType PageType,
    string? ResourceId,
    string? Name,
    string? Slug,
    long HitCount);
