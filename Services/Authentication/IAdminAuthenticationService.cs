using SRS_backend.Dtos.Admin.Authentication;

namespace SRS_backend.Services.Authentication;

public interface IAdminAuthenticationService
{
    AdminLoginResponse? Authenticate(AdminLoginRequest request);
}
