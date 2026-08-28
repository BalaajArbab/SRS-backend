namespace SRS_backend.Dtos;

public sealed record AdminLoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt);
