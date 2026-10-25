using HocTiengTrung_3._0_T;
using HocTiengTrung_3._0_T.Models; // Thay đổi theo namespace chứa DbContext của bạn
using HocTiengTrung_3._0_T.Models12;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

public class UserService : IUserService
{
    private readonly TmdtContext _context;
    private readonly ILogger<UserService> _logger; // Thêm logger để track lỗi

    public UserService(TmdtContext context, ILogger<UserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> GetCurrentTaiKhoanIdAsync(ClaimsPrincipal user, HttpContext httpContext)
    {
        try
        {
            // 1. ƯU TIÊN: Lấy từ Header (X-User-Id được Controller truyền qua)
            if (httpContext.Request.Headers.TryGetValue("X-User-Id", out var headerValue))
            {
                if (int.TryParse(headerValue, out int idFromHeader))
                {
                    _logger.LogInformation("Lấy IdTaiKhoan từ Header: {Id}", idFromHeader);
                    return idFromHeader;
                }
            }

            int idTaiKhoanFinal = 0;
            int idUserFinal = 0;

            // 2. Lấy IdTaiKhoan từ Claims hoặc Session
            var idTaiKhoanClaim = user.FindFirst("IdTaiKhoan")?.Value;
            if (!string.IsNullOrEmpty(idTaiKhoanClaim) && int.TryParse(idTaiKhoanClaim, out int tkClaim))
            {
                idTaiKhoanFinal = tkClaim;
            }
            else
            {
                idTaiKhoanFinal = httpContext.Session.GetInt32("IdTaiKhoan")
                                 ?? (int.TryParse(httpContext.Session.GetString("IdTaiKhoan"), out var idTK) ? idTK : 0);
            }

            // 3. Nếu chưa có IdTaiKhoan -> Thử lấy qua IdUser rồi truy vấn chéo
            if (idTaiKhoanFinal == 0)
            {
                var idUserClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(idUserClaim) && int.TryParse(idUserClaim, out int uClaim))
                {
                    idUserFinal = uClaim;
                }
                else
                {
                    var idUserSession = httpContext.Session.GetString("MaNguoiDung");
                    if (!string.IsNullOrEmpty(idUserSession) && int.TryParse(idUserSession, out int uSession))
                    {
                        idUserFinal = uSession;
                    }
                }

                if (idUserFinal > 0)
                {
                    var nguoiDung = await _context.NguoiDungs
                        .AsNoTracking()
                        .FirstOrDefaultAsync(n => n.IdUser == idUserFinal);

                    if (nguoiDung?.IdTaiKhoan.HasValue == true)
                    {
                        idTaiKhoanFinal = nguoiDung.IdTaiKhoan.Value;
                    }
                }
            }

            return idTaiKhoanFinal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi nghiêm trọng khi xác định IdTaiKhoan.");
            return 0;
        }
    }

    public async Task<bool> IsUserPremiumAsync(ClaimsPrincipal user, HttpContext httpContext)
    {
        int idTaiKhoan = await GetCurrentTaiKhoanIdAsync(user, httpContext);
        if (idTaiKhoan <= 0)
        {
            _logger.LogWarning("IsUserPremiumAsync: Không tìm thấy IdTaiKhoan hợp lệ.");
            return false;
        }

        var taiKhoan = await _context.TaiKhoans
            .AsNoTracking()
            .FirstOrDefaultAsync(tk => tk.IdTaiKhoan == idTaiKhoan);

        if (taiKhoan == null) return false;

        if (!taiKhoan.IsVip)
        {
            _logger.LogInformation($"Tài khoản {idTaiKhoan} chưa kích hoạt VIP.");
            return false;
        }

        // Kiểm tra hạn VIP sử dụng DateTime.UtcNow để tránh lệch múi giờ nếu có
        DateTime now = DateTime.Now;

        bool hopLeBatDau = !taiKhoan.NgayBatDauVip.HasValue || now >= taiKhoan.NgayBatDauVip.Value;
        bool hopLeKetThuc = !taiKhoan.NgayKetThucVip.HasValue || now <= taiKhoan.NgayKetThucVip.Value;

        return hopLeBatDau && hopLeKetThuc;
    }
}