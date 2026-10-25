using System.Security.Claims;

namespace HocTiengTrung_3._0_T.Models12
{
    public interface IUserService
    {
        Task<int> GetCurrentTaiKhoanIdAsync(ClaimsPrincipal user, HttpContext httpContext);
        Task<bool> IsUserPremiumAsync(ClaimsPrincipal user, HttpContext httpContext);
    }
}
