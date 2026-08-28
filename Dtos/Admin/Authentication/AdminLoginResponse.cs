namespace SRS_backend.Dtos.Admin.Authentication;

public sealed record AdminLoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt);
