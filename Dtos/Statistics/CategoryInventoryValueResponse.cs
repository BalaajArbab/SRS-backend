namespace SRS_backend.Dtos.Statistics;

public sealed record CategoryInventoryValueResponse(
    string CategoryId,
    string Name,
    string Slug,
    bool IsActive,
    long InventoryValue);
