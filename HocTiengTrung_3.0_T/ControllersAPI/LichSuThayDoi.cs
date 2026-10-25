using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12.QuanLyLichSu;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.ControllersAPI
{
    [Route("api/lichsuthaydoi")]
    [ApiController]
    public class LichSuThayDoi : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly ILogger<LichSuThayDoi> _logger;

        public LichSuThayDoi(TmdtContext context, ILogger<LichSuThayDoi> logger)
        {
            _context = context;
            _logger = logger;
        }

        // =========================================================================
        // GET: api/lichsuthaydoi/danh-sach
        // Lấy danh sách lịch sử thay đổi có phân trang và bộ lọc cô lập theo Trung tâm
        [HttpGet("danh-sach")]
        public async Task<IActionResult> GetDanhSachLichSu(
     [FromQuery] string? maTrungTam,
     [FromQuery] int? idTrungTam,
     [FromQuery] string? hanhDong,
     [FromQuery] string? keyword,
     [FromQuery] DateTime? tuNgay,
     [FromQuery] DateTime? denNgay,
     [FromQuery] int page = 1,
     [FromQuery] int pageSize = 15)
        {
            try
            {
                // 1. Lấy mã/ID trung tâm: Ưu tiên từ Claims/Session nội bộ API, nếu không có thì lấy từ tham số Client gửi lên
                string? claimIdTrungTamStr = User.FindFirst("IdTrungTam")?.Value
                                             ?? HttpContext.Session.GetString("IdTrungTam");
                string? claimMaTrungTam = User.FindFirst("MaTrungTam")?.Value
                                          ?? HttpContext.Session.GetString("MaTrungTam");

                int.TryParse(claimIdTrungTamStr, out int finalIdTrungTam);
                if (finalIdTrungTam <= 0 && idTrungTam.HasValue)
                {
                    finalIdTrungTam = idTrungTam.Value;
                }

                string finalMaTrungTam = (!string.IsNullOrWhiteSpace(claimMaTrungTam) && claimMaTrungTam != "0")
                                         ? claimMaTrungTam
                                         : (maTrungTam ?? string.Empty);

                // QUY TẮC BẢO VỆ: Nếu rỗng cả ID lẫn Mã trung tâm -> Trả về danh sách rỗng ngay lập tức
                if (finalIdTrungTam <= 0 && string.IsNullOrWhiteSpace(finalMaTrungTam))
                {
                    return Ok(new
                    {
                        success = true,
                        totalItems = 0,
                        totalPages = 0,
                        currentPage = 1,
                        pageSize = pageSize,
                        maTrungTamHienTai = "",
                        data = new List<LichSuThayDoiModels>()
                    });
                }

                var query = _context.LichSuThayDois
                    .Include(ls => ls.IdTaiKhoanNavigation)
                        .ThenInclude(tk => tk.IdTrungTamNavigation)
                    .Include(ls => ls.IdTaiKhoanNavigation)
                        .ThenInclude(tk => tk.NguoiDung)
                    .AsNoTracking()
                    .AsQueryable();

                // 2. BẮT BUỘC LỌC THEO TRUNG TÂM NÀY
                if (finalIdTrungTam > 0)
                {
                    query = query.Where(ls => ls.IdTaiKhoanNavigation != null
                                           && ls.IdTaiKhoanNavigation.IdTrungTam == finalIdTrungTam);
                }
                else if (!string.IsNullOrWhiteSpace(finalMaTrungTam))
                {
                    string cleanMa = finalMaTrungTam.Trim().ToLower();
                    query = query.Where(ls => ls.IdTaiKhoanNavigation != null
                                           && ls.IdTaiKhoanNavigation.IdTrungTamNavigation != null
                                           && ls.IdTaiKhoanNavigation.IdTrungTamNavigation.MaTrungTam.ToLower() == cleanMa);
                }

                // 3. Lọc theo hành động
                if (!string.IsNullOrWhiteSpace(hanhDong))
                {
                    query = query.Where(ls => ls.HanhDong.ToLower() == hanhDong.Trim().ToLower());
                }

                // 4. Lọc theo thời gian
                if (tuNgay.HasValue) query = query.Where(ls => ls.ThoiGian >= tuNgay.Value);
                if (denNgay.HasValue)
                {
                    var denNgayCuoi = denNgay.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(ls => ls.ThoiGian <= denNgayCuoi);
                }

                // 5. Tìm kiếm từ khóa
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    string key = keyword.Trim().ToLower();
                    query = query.Where(ls => (ls.MoTa != null && ls.MoTa.ToLower().Contains(key))
                                           || (ls.DiaChiIp != null && ls.DiaChiIp.Contains(key))
                                           || (ls.IdTaiKhoanNavigation != null && ls.IdTaiKhoanNavigation.TenDangNhap.ToLower().Contains(key))
                                           || (ls.IdTaiKhoanNavigation != null && ls.IdTaiKhoanNavigation.NguoiDung != null && ls.IdTaiKhoanNavigation.NguoiDung.HoTen.ToLower().Contains(key)));
                }

                int totalItems = await query.CountAsync();
                int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                var data = await query
                    .OrderByDescending(ls => ls.ThoiGian)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(ls => new LichSuThayDoiModels
                    {
                        IdLichSu = ls.IdLichSu,
                        IdTaiKhoan = ls.IdTaiKhoan,
                        TenTaiKhoan = ls.IdTaiKhoanNavigation != null ? ls.IdTaiKhoanNavigation.TenDangNhap : "Hệ thống",
                        TenNguoiDung = (ls.IdTaiKhoanNavigation != null && ls.IdTaiKhoanNavigation.NguoiDung != null)
                                        ? ls.IdTaiKhoanNavigation.NguoiDung.HoTen
                                        : null,
                        HanhDong = ls.HanhDong,
                        DuLieuCu = ls.DuLieuCu,
                        DuLieuMoi = ls.DuLieuMoi,
                        ThoiGian = ls.ThoiGian,
                        DiaChiIp = ls.DiaChiIp,
                        UserAgent = ls.UserAgent,
                        MoTa = ls.MoTa
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    totalItems = totalItems,
                    totalPages = totalPages,
                    currentPage = page,
                    pageSize = pageSize,
                    maTrungTamHienTai = finalMaTrungTam,
                    data = data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nạp nhật ký");
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = ex.Message });
            }
        }

        // =========================================================================
        // GET: api/lichsuthaydoi/chi-tiet/{id}
        // Xem chi tiết nội dung thay đổi (so sánh dữ liệu cũ & mới)
        // =========================================================================
        [HttpGet("chi-tiet/{id}")]
        public async Task<IActionResult> GetChiTietLichSu([FromRoute] long id)
        {
            try
            {
                string? claimIdTrungTamStr = User.FindFirst("IdTrungTam")?.Value
                                             ?? HttpContext.Session.GetString("IdTrungTam");
                int.TryParse(claimIdTrungTamStr, out int currentIdTrungTam);

                var log = await _context.LichSuThayDois
                    .Include(ls => ls.IdTaiKhoanNavigation)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ls => ls.IdLichSu == id);

                if (log == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy bản ghi lịch sử này." });
                }

                // Bảo mật: Nếu tài khoản thuộc Trung tâm A nhưng cố truy cập bản ghi của Trung tâm B
                if (currentIdTrungTam > 0 && log.IdTaiKhoanNavigation != null && log.IdTaiKhoanNavigation.IdTrungTam != currentIdTrungTam)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        success = false,
                        message = "Bạn không có quyền xem lịch sử của cơ sở/trung tâm khác."
                    });
                }

                var result = new LichSuThayDoiModels
                {
                    IdLichSu = log.IdLichSu,
                    IdTaiKhoan = log.IdTaiKhoan,
                    TenTaiKhoan = log.IdTaiKhoanNavigation?.TenDangNhap ?? "Hệ thống",
                    HanhDong = log.HanhDong,
                    DuLieuCu = log.DuLieuCu,
                    DuLieuMoi = log.DuLieuMoi,
                    ThoiGian = log.ThoiGian,
                    DiaChiIp = log.DiaChiIp,
                    UserAgent = log.UserAgent,
                    MoTa = log.MoTa
                };

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi lấy chi tiết lịch sử ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống: " + ex.Message
                });
            }
        }
    }
}