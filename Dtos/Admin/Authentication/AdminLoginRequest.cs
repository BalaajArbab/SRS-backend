using System.ComponentModel.DataAnnotations;

namespace SRS_backend.Dtos.Admin.Authentication;

public sealed class AdminLoginRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(256)]
    public string Username { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(512)]
    public string Password { get; init; } = string.Empty;
}
