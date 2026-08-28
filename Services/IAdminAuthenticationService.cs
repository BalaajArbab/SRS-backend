using SRS_backend.Dtos;

namespace SRS_backend.Services;

public interface IAdminAuthenticationService
{
    AdminLoginResponse? Authenticate(AdminLoginRequest request);
}
