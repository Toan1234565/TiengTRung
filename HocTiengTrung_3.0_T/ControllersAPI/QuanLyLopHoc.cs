using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12;
using HocTiengTrung_3._0_T.Models12.QuanLyLichSu;
using HocTiengTrung_3._0_T.Models12.QuanLyTaiKhoan;
using HocTiengTrung_3._0_T.Models12.QuanLyTrungTam;
using HocTiengTrung_3._0_T.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using MiniExcelLibs;
using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.ControllersAPI
{
    [Route("api/quanlylophoc")]
    [ApiController]
    public class QuanLyLopHoc : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<QuanLyLopHoc> _logger;

        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();
        private readonly IAuditLogService _auditLogService;

        public QuanLyLopHoc(TmdtContext context, IMemoryCache cache, ILogger<QuanLyLopHoc> logger, IAuditLogService auditLogService)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _auditLogService = auditLogService;
        }
        [HttpGet("danhsachlophoc")]
        [AllowAnonymous] // Cho phép xử lý nội bộ linh hoạt
        public async Task<IActionResult> GetDanhSachLopHoc(
    [FromQuery] int? idTrungTam,
    [FromQuery] int? idGiangVien,
    [FromQuery] string? keyword,
    [FromQuery] bool? trangThai)
        {
            try
            {
                // =========================================================================
                // 1. TRÍCH XUẤT CLAIMS VÀ SESSION
                // =========================================================================
                var tenVaiTro = (User.FindFirst("TenVaiTro")?.Value
                                 ?? HttpContext.Session?.GetString("TenVaiTro")
                                 ?? "").Trim();

                var idVaiTroStr = (User.FindFirst("IdVaiTro")?.Value
                                   ?? HttpContext.Session?.GetString("IdVaiTro")
                                   ?? "").Trim();

                var roleClaims = User.FindAll(ClaimTypes.Role)
                                     .Select(c => c.Value.Trim())
                                     .ToList();

                string? idTaiKhoanClaim = User.FindFirst("IdTaiKhoan")?.Value
                                          ?? HttpContext.Session?.GetString("IdTaiKhoan");
                int.TryParse(idTaiKhoanClaim, out int currentIdTaiKhoan);

                string? idTrungTamClaim = User.FindFirst("IdTrungTam")?.Value
                                          ?? HttpContext.Session?.GetString("IdTrungTam");
                int.TryParse(idTrungTamClaim, out int currentIdTrungTam);

                // =========================================================================
                // 2. FALLBACK DATABASE: CỨU CÁNH KHI BỊ RỚT CLAIMS TỪ HTTPCLIENT
                // =========================================================================
                if (currentIdTaiKhoan > 0 && (string.IsNullOrEmpty(tenVaiTro) || currentIdTrungTam <= 0))
                {
                    var tkDb = await _context.TaiKhoans
                        .AsNoTracking()
                        .Include(t => t.IdVaiTroNavigation)
                        .FirstOrDefaultAsync(t => t.IdTaiKhoan == currentIdTaiKhoan);

                    if (tkDb != null)
                    {
                        if (string.IsNullOrEmpty(tenVaiTro) && tkDb.IdVaiTroNavigation != null)
                        {
                            tenVaiTro = tkDb.IdVaiTroNavigation.TenVaiTro?.Trim() ?? "";
                        }
                        if (currentIdTrungTam <= 0 && tkDb.IdTrungTam.HasValue)
                        {
                            currentIdTrungTam = tkDb.IdTrungTam.Value;
                        }
                        if (string.IsNullOrEmpty(idVaiTroStr) && tkDb.IdVaiTro.HasValue)
                        {
                            idVaiTroStr = tkDb.IdVaiTro.Value.ToString();
                        }
                    }
                }

                // =========================================================================
                // 3. XÁC ĐỊNH VAI TRÒ TOÀN DIỆN (THEO CHUỖI VÀ THEO ID)
                // =========================================================================
                string roleClean = tenVaiTro.ToLower();

                bool isAdmin = roleClean.Contains("quản trị viên")
                               || roleClean.Contains("quan tri vien")
                               || roleClean.Contains("admin")
                               || idVaiTroStr == "1"
                               || roleClaims.Any(r => r.Equals("Quản trị viên", StringComparison.OrdinalIgnoreCase)
                                                   || r.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                                                   || r == "1");

                bool isQuanLy = !isAdmin && (roleClean.Contains("quản lý")
                                          || roleClean.Contains("quan ly")
                                          || roleClean.Contains("manager")
                                          || idVaiTroStr == "1002"
                                          || roleClaims.Any(r => r.Equals("Quản lý trung tâm", StringComparison.OrdinalIgnoreCase)
                                                              || r.Equals("Quan ly trung tam", StringComparison.OrdinalIgnoreCase)
                                                              || r == "1002"));

                bool isGiangVien = !isAdmin && !isQuanLy && (roleClean.Contains("giảng viên")
                                                         || roleClean.Contains("giang vien")
                                                         || roleClean.Contains("teacher")
                                                         || idVaiTroStr == "1003"
                                                         || roleClaims.Any(r => r.Equals("Giảng viên", StringComparison.OrdinalIgnoreCase)
                                                                             || r.Equals("Giang vien", StringComparison.OrdinalIgnoreCase)
                                                                             || r == "1003"));

                // =========================================================================
                // 4. KHỞI TẠO QUERY & CÔ LẬP DỮ LIỆU
                // =========================================================================
                var query = _context.LopHocs
                    .Include(l => l.IdTrungTamNavigation)
                    .Include(l => l.HocVienLopHocs)
                    .Include(l => l.GiangVienLopHocs)
                    .AsNoTracking()
                    .AsQueryable();

                if (isGiangVien)
                {
                    query = query.Where(l => l.GiangVienLopHocs.Any(gv => gv.IdTaiKhoan == currentIdTaiKhoan));
                }
                else if (isQuanLy)
                {
                    if (currentIdTrungTam > 0)
                    {
                        query = query.Where(l => l.IdTrungTam == currentIdTrungTam);
                    }
                }
                else if (isAdmin)
                {
                    if (idTrungTam.HasValue && idTrungTam.Value > 0)
                    {
                        query = query.Where(l => l.IdTrungTam == idTrungTam.Value);
                    }
                }
                else
                {
                    if (idTrungTam.HasValue && idTrungTam.Value > 0)
                    {
                        query = query.Where(l => l.IdTrungTam == idTrungTam.Value);
                    }
                }

                // =========================================================================
                // 5. CÁC BỘ LỌC TÙY CHỌN
                // =========================================================================
                if (idGiangVien.HasValue && idGiangVien.Value > 0)
                {
                    query = query.Where(l => l.GiangVienLopHocs.Any(gv => gv.IdTaiKhoan == idGiangVien.Value));
                }

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    string kw = keyword.Trim().ToLower();
                    query = query.Where(l => l.TenLopHoc.ToLower().Contains(kw)
                                          || (l.MoTa != null && l.MoTa.ToLower().Contains(kw)));
                }

                if (trangThai.HasValue)
                {
                    query = query.Where(l => l.TrangThai == trangThai.Value);
                }

                // =========================================================================
                // 6. TRUY VẤN VÀ MAPPING AN TOÀN (FALLBACK MALOPHOC TRÁNH LỖI DB)
                // =========================================================================
                var listLopHoc = await query
                    .OrderByDescending(l => l.IdLopHoc)
                    .Select(l => new LopHocModels
                    {
                        IdLopHoc = l.IdLopHoc,
                        IdTrungTam = l.IdTrungTam,
                        
                        MaLopHoc = $"LH_{l.IdLopHoc}", // Fallback an toàn tránh lỗi Invalid column name 'MaLopHoc'
                        TenLopHoc = l.TenLopHoc,
                        MoTa = l.MoTa,
                        NgayKhaiGiang = l.NgayKhaiGiang,
                        NgayKetThuc = l.NgayKetThuc,
                        TrangThai = l.TrangThai,
                        TongSoHocVien = l.HocVienLopHocs.Count
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    total = listLopHoc.Count,
                    userRole = isAdmin ? "Admin" : (isQuanLy ? "Quản lý trung tâm" : (isGiangVien ? "Giảng viên" : "Khác")),
                    currentTrungTam = currentIdTrungTam,
                    data = listLopHoc
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi nạp danh sách lớp học");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống khi nạp danh sách lớp học: " + ex.Message
                });
            }
        }
        // =========================================================================
        // CHI TIẾT LỚP HỌC (ĐÃ SỬA LỖI GIẢI MÃ IN-MEMORY & FALLBACK BẢO MẬT)
        // =========================================================================
        [HttpGet("chitietlophoc/{idLopHoc}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetChiTietLopHoc([FromRoute] int idLopHoc)
        {
            if (idLopHoc <= 0)
            {
                return BadRequest(new { success = false, message = "ID lớp học không hợp lệ." });
            }

            try
            {
                // =========================================================================
                // 1. TRÍCH XUẤT CLAIMS VÀ PHÂN QUYỀN
                // =========================================================================
                var tenVaiTro = (User.FindFirst("TenVaiTro")?.Value
                                 ?? HttpContext.Session?.GetString("TenVaiTro")
                                 ?? "").Trim();

                var idVaiTroStr = (User.FindFirst("IdVaiTro")?.Value
                                   ?? HttpContext.Session?.GetString("IdVaiTro")
                                   ?? "").Trim();

                var roleClaims = User.FindAll(ClaimTypes.Role)
                                     .Select(c => c.Value.Trim())
                                     .ToList();

                string? idTaiKhoanClaim = User.FindFirst("IdTaiKhoan")?.Value
                                          ?? HttpContext.Session?.GetString("IdTaiKhoan");
                int.TryParse(idTaiKhoanClaim, out int currentIdTaiKhoan);

                string? idTrungTamClaim = User.FindFirst("IdTrungTam")?.Value
                                          ?? HttpContext.Session?.GetString("IdTrungTam");
                int.TryParse(idTrungTamClaim, out int currentIdTrungTam);

                // Fallback đọc DB nếu rớt Claim thông tin cơ sở
                if (currentIdTaiKhoan > 0 && (string.IsNullOrEmpty(tenVaiTro) || currentIdTrungTam <= 0))
                {
                    var tkDb = await _context.TaiKhoans
                        .AsNoTracking()
                        .Include(t => t.IdVaiTroNavigation)
                        .FirstOrDefaultAsync(t => t.IdTaiKhoan == currentIdTaiKhoan);

                    if (tkDb != null)
                    {
                        if (string.IsNullOrEmpty(tenVaiTro) && tkDb.IdVaiTroNavigation != null)
                        {
                            tenVaiTro = tkDb.IdVaiTroNavigation.TenVaiTro?.Trim() ?? "";
                        }
                        if (currentIdTrungTam <= 0 && tkDb.IdTrungTam.HasValue)
                        {
                            currentIdTrungTam = tkDb.IdTrungTam.Value;
                        }
                        if (string.IsNullOrEmpty(idVaiTroStr) && tkDb.IdVaiTro.HasValue)
                        {
                            idVaiTroStr = tkDb.IdVaiTro.Value.ToString();
                        }
                    }
                }

                string roleClean = tenVaiTro.ToLower();

                bool isAdmin = roleClean.Contains("quản trị viên")
                               || roleClean.Contains("quan tri vien")
                               || roleClean.Contains("admin")
                               || idVaiTroStr == "1"
                               || roleClaims.Any(r => r.Equals("Quản trị viên", StringComparison.OrdinalIgnoreCase)
                                                   || r.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                                                   || r == "1");

                bool isQuanLy = !isAdmin && (roleClean.Contains("quản lý")
                                          || roleClean.Contains("quan ly")
                                          || roleClean.Contains("manager")
                                          || idVaiTroStr == "1002"
                                          || roleClaims.Any(r => r.Equals("Quản lý trung tâm", StringComparison.OrdinalIgnoreCase)
                                                              || r.Equals("Quan ly trung tam", StringComparison.OrdinalIgnoreCase)
                                                              || r == "1002"));

                bool isGiangVien = !isAdmin && !isQuanLy && (roleClean.Contains("giảng viên")
                                                         || roleClean.Contains("giang vien")
                                                         || roleClean.Contains("teacher")
                                                         || idVaiTroStr == "1003"
                                                         || roleClaims.Any(r => r.Equals("Giảng viên", StringComparison.OrdinalIgnoreCase)
                                                                             || r.Equals("Giang vien", StringComparison.OrdinalIgnoreCase)
                                                                             || r == "1003"));

                string cacheKey = $"ChiTiet_LopHoc_{idLopHoc}";
                ChiTietLopHocViewModel? chiTietLopHoc;

                // =========================================================================
                // 2. KIỂM TRA BỘ NHỚ CACHE
                // =========================================================================
                if (!_cache.TryGetValue(cacheKey, out object? cacheResult))
                {
                    _logger.LogInformation("Cache miss cho key: {Key}. Đang lấy dữ liệu chi tiết lớp học từ DB...", cacheKey);

                    // BƯỚC A: Truy vấn thô từ Database (KHÔNG gọi hàm C# giải mã bên trong câu lệnh SQL)
                    var lopHocDb = await _context.LopHocs
                        .AsNoTracking()
                        .Include(l => l.IdTrungTamNavigation)
                        .Include(l => l.GiangVienLopHocs)
                            .ThenInclude(gv => gv.IdTaiKhoanNavigation)
                                .ThenInclude(tk => tk.NguoiDung)
                        .Include(l => l.HocVienLopHocs)
                            .ThenInclude(hv => hv.IdUserNavigation)
                        .FirstOrDefaultAsync(l => l.IdLopHoc == idLopHoc);

                    if (lopHocDb == null)
                    {
                        return NotFound(new
                        {
                            success = false,
                            message = $"Không tìm thấy thông tin lớp học (ID: {idLopHoc})."
                        });
                    }

                    // BƯỚC B: Mapping ViewModel & giải mã an toàn In-Memory (trên RAM)
                    chiTietLopHoc = new ChiTietLopHocViewModel
                    {
                        IdLopHoc = lopHocDb.IdLopHoc,
                        IdTrungTam = lopHocDb.IdTrungTam,
                        TenTrungTam = lopHocDb.IdTrungTamNavigation != null ? lopHocDb.IdTrungTamNavigation.TenTrungTam : null,
                        TenLopHoc = lopHocDb.TenLopHoc,
                        MaLopHoc = $"LH_{lopHocDb.IdLopHoc}", // Fallback an toàn tránh lỗi cột MaLopHoc
                        MoTa = lopHocDb.MoTa,
                        NgayKhaiGiang = lopHocDb.NgayKhaiGiang,
                        NgayKetThuc = lopHocDb.NgayKetThuc,
                        TrangThai = lopHocDb.TrangThai,
                        TongSoHocVien = lopHocDb.HocVienLopHocs?.Count ?? 0,

                        // Giải mã danh sách giảng viên trên RAM
                        DanhSachGiangVien = (lopHocDb.GiangVienLopHocs ?? new List<GiangVienLopHoc>())
                            .OrderBy(gv => gv.VaiTroDay)
                            .Select(gv => new ChiTietGiangVienViewModel
                            {
                                IdTaiKhoan = gv.IdTaiKhoan,
                                IdUser = gv.IdTaiKhoanNavigation?.NguoiDung?.IdUser ?? 0,
                                TenDangNhap = gv.IdTaiKhoanNavigation?.TenDangNhap ?? "",
                                SoDienThoai = GiaiMaHelper.GiaiMaAnToan(gv.IdTaiKhoanNavigation?.NguoiDung?.SoDienThoai),
                                VaiTroDay = gv.VaiTroDay ?? "Giảng viên",
                                NgayPhanCong = gv.NgayPhanCong
                            })
                            .ToList(),

                        // Giải mã danh sách học viên trên RAM
                        DanhSachHocVien = (lopHocDb.HocVienLopHocs ?? new List<HocVienLopHoc>())
                            .OrderByDescending(h => h.NgayThamGia)
                            .Select(h => new ChiTietHocVienViewModel
                            {
                                IdUser = h.IdUser,
                                IdTaiKhoan = h.IdUserNavigation?.IdTaiKhoan ?? 0,
                                HoTen = h.IdUserNavigation?.HoTen ?? "Học viên",
                                Email = GiaiMaHelper.GiaiMaAnToan(h.IdUserNavigation?.Email),
                                SoDienThoai = GiaiMaHelper.GiaiMaAnToan(h.IdUserNavigation?.SoDienThoai),
                                NgaySinh = h.IdUserNavigation?.NgaySinh,
                                AnhDaiDien = h.IdUserNavigation?.AnhDaiDien ?? "default_avatar.png",
                                NgayThamGia = h.NgayThamGia
                            })
                            .ToList()
                    };

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromHours(2))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                    _cache.Set(cacheKey, chiTietLopHoc, cacheOptions);
                }
                else
                {
                    _logger.LogInformation("Cache hit cho key: {Key}. Trả dữ liệu từ Cache.", cacheKey);
                    chiTietLopHoc = (ChiTietLopHocViewModel)cacheResult!;
                }

                // =========================================================================
                // 3. XÁC MINH QUYỀN TRUY CẬP DỮ LIỆU (CÔ LẬP NỘI DUNG)
                // =========================================================================
                if (!isAdmin)
                {
                    // Quản lý trung tâm: Không được xem lớp của cơ sở khác
                    if (isQuanLy && currentIdTrungTam > 0 && chiTietLopHoc.IdTrungTam != currentIdTrungTam)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new
                        {
                            success = false,
                            message = "Bạn không có quyền xem thông tin lớp học thuộc trung tâm khác."
                        });
                    }

                    // Giảng viên: Không được xem chi tiết lớp nếu không được phân công
                    if (isGiangVien && currentIdTaiKhoan > 0 && !chiTietLopHoc.DanhSachGiangVien.Any(gv => gv.IdTaiKhoan == currentIdTaiKhoan))
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new
                        {
                            success = false,
                            message = "Bạn không được phân công phụ trách lớp học này."
                        });
                    }
                }

                return Ok(new
                {
                    success = true,
                    data = chiTietLopHoc
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy chi tiết lớp học ID: {IdLopHoc}", idLopHoc);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống khi lấy thông tin chi tiết lớp học: " + ex.Message
                });
            }
        }


        // Hàm loại bỏ dấu tiếng Việt và ký tự đặc biệt, chuyển thành chữ thường liền mạch
        private string LoaiBoDauTiengViet(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "hocvien";

            string normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            string clean = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
            // Xử lý đ, Đ
            clean = clean.Replace('đ', 'd').Replace('Đ', 'd');
            // Bỏ toàn bộ khoảng trắng và ký tự không phải chữ/số
            clean = Regex.Replace(clean, @"[^a-zA-Z0-9]", "").ToLower().Trim();

            return string.IsNullOrEmpty(clean) ? "hocvien" : clean;
        }
        // ==========================================
        // API THÊM LỚP HỌC THỦ CÔNG KÈM GIẢNG VIÊN & HỌC VIÊN (TÙY CHỌN)
        // POST: api/quanlylophoc/them-lophoc-thucong
        // ==========================================
        [HttpPost("them-lophoc-thucong")]
        public async Task<IActionResult> ThemLopHocThuCong([FromBody] ThemLopHocThuCongRequest request)
        {
            // 1. KIỂM TRA THÔNG TIN BẮT BUỘC CỦA LỚP HỌC
            if (request == null || string.IsNullOrWhiteSpace(request.TenLopHoc))
            {
                return BadRequest(new { success = false, message = "Vui lòng nhập đầy đủ tên lớp học!" });
            }

            if (request.NgayKhaiGiang.HasValue && request.NgayKetThuc.HasValue && request.NgayKetThuc.Value < request.NgayKhaiGiang.Value)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Ngày kết thúc ({request.NgayKetThuc:dd/MM/yyyy}) không thể trước ngày khai giảng ({request.NgayKhaiGiang:dd/MM/yyyy})!"
                });
            }

            // =========================================================================
            // 2. PHÂN QUYỀN MULTI-TENANT & XÁC ĐỊNH CƠ SỞ ĐÀO TẠO
            // =========================================================================
            var tenVaiTro = (User.FindFirst("TenVaiTro")?.Value
                             ?? HttpContext.Session?.GetString("TenVaiTro")
                             ?? "").Trim();

            var idVaiTroStr = (User.FindFirst("IdVaiTro")?.Value
                               ?? HttpContext.Session?.GetString("IdVaiTro")
                               ?? "").Trim();

            var roleClaims = User.FindAll(ClaimTypes.Role).Select(c => c.Value.Trim()).ToList();

            string? idTrungTamClaim = User.FindFirst("IdTrungTam")?.Value
                                      ?? HttpContext.Session?.GetString("IdTrungTam");
            int.TryParse(idTrungTamClaim, out int currentIdTrungTam);

            string roleClean = tenVaiTro.ToLower();
            bool isAdmin = roleClean.Contains("quản trị viên")
                           || roleClean.Contains("quan tri vien")
                           || roleClean.Contains("admin")
                           || idVaiTroStr == "1"
                           || roleClaims.Any(r => r.Equals("Quản trị viên", StringComparison.OrdinalIgnoreCase)
                                               || r.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                                               || r == "1");

            bool isQuanLy = !isAdmin && (roleClean.Contains("quản lý")
                                      || roleClean.Contains("quan ly")
                                      || roleClean.Contains("manager")
                                      || idVaiTroStr == "1002"
                                      || roleClaims.Any(r => r.Equals("Quản lý trung tâm", StringComparison.OrdinalIgnoreCase)
                                                          || r.Equals("Quan ly trung tam", StringComparison.OrdinalIgnoreCase)
                                                          || r == "1002"));

            if (!isAdmin && !isQuanLy)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    message = "Bạn không có quyền thêm mới lớp học!"
                });
            }

            int finalIdTrungTam = currentIdTrungTam;
            if (isAdmin)
            {
                if (request.IdTrungTam.HasValue && request.IdTrungTam.Value > 0)
                {
                    finalIdTrungTam = request.IdTrungTam.Value;
                }
                else if (finalIdTrungTam <= 0)
                {
                    return BadRequest(new { success = false, message = "Vui lòng chọn cơ sở đào tạo để tạo lớp học!" });
                }
            }
            else
            {
                if (finalIdTrungTam <= 0)
                {
                    return BadRequest(new { success = false, message = "Không xác định được cơ sở đào tạo của tài khoản quản lý!" });
                }
            }

            var trungTam = await _context.TrungTams.FirstOrDefaultAsync(t => t.IdTrungTam == finalIdTrungTam && t.TrangThai);
            if (trungTam == null)
            {
                return BadRequest(new { success = false, message = $"Cơ sở đào tạo (ID: {finalIdTrungTam}) không tồn tại hoặc đã bị ngừng hoạt động!" });
            }

            // =========================================================================
            // 3. XỬ LÝ MÃ LỚP HỌC (CHECK TRÙNG MÃ)
            // =========================================================================
            string finalMaLopHoc = request.MaLopHoc?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(finalMaLopHoc))
            {
                finalMaLopHoc = $"LH_{finalIdTrungTam}_{DateTime.Now:yyyyMMddHHmmss}";
            }
            else
            {
                bool daTonTaiMaLop = await _context.LopHocs.AnyAsync(l =>
                    l.IdTrungTam == finalIdTrungTam &&
                    l.MaLopHoc.ToLower() == finalMaLopHoc.ToLower());

                if (daTonTaiMaLop)
                {
                    return Conflict(new
                    {
                        success = false,
                        message = $"Cơ sở '{trungTam.TenTrungTam}' đã tồn tại mã lớp '{finalMaLopHoc}'. Vui lòng nhập mã lớp khác!"
                    });
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // =========================================================================
                // 4. TẠO MỚI LỚP HỌC
                // =========================================================================
                var lopHoc = new LopHoc
                {
                    IdTrungTam = finalIdTrungTam,
                    MaLopHoc = finalMaLopHoc,
                    TenLopHoc = request.TenLopHoc.Trim(),
                    MoTa = string.IsNullOrWhiteSpace(request.MoTa) ? $"Tạo thủ công ngày {DateTime.Now:dd/MM/yyyy HH:mm}" : request.MoTa.Trim(),
                    NgayKhaiGiang = request.NgayKhaiGiang ?? DateOnly.FromDateTime(DateTime.Now),
                    NgayKetThuc = request.NgayKetThuc,
                    TrangThai = request.TrangThai ?? true
                };

                _context.LopHocs.Add(lopHoc);
                await _context.SaveChangesAsync();

                // =========================================================================
                // 5. GÁN GIẢNG VIÊN (KHÔNG BẮT BUỘC)
                // =========================================================================
                var tenGiangViens = new List<string>();
                int countGiangVien = 0;

                if (request.DanhSachGiangVien != null && request.DanhSachGiangVien.Any())
                {
                    var dsGv = request.DanhSachGiangVien
                        .GroupBy(x => x.IdTaiKhoan)
                        .Select(g => g.First())
                        .ToList();

                    var listGvId = dsGv.Select(x => x.IdTaiKhoan).ToList();

                    var gvDbs = await _context.TaiKhoans
                        .Include(tk => tk.NguoiDung)
                        .Where(tk => listGvId.Contains(tk.IdTaiKhoan) && tk.TrangThai == true)
                        .ToListAsync();

                    foreach (var item in dsGv)
                    {
                        var tkGv = gvDbs.FirstOrDefault(t => t.IdTaiKhoan == item.IdTaiKhoan);
                        if (tkGv == null)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new
                            {
                                success = false,
                                message = $"Tài khoản giảng viên ID: {item.IdTaiKhoan} không tồn tại hoặc đã bị khóa!"
                            });
                        }

                        if (tkGv.IdTrungTam.HasValue && tkGv.IdTrungTam.Value != finalIdTrungTam)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new
                            {
                                success = false,
                                message = $"Giảng viên '{tkGv.NguoiDung?.HoTen ?? tkGv.TenDangNhap}' không thuộc cơ sở '{trungTam.TenTrungTam}'!"
                            });
                        }

                        _context.GiangVienLopHocs.Add(new GiangVienLopHoc
                        {
                            IdLopHoc = lopHoc.IdLopHoc,
                            IdTaiKhoan = tkGv.IdTaiKhoan,
                            VaiTroDay = string.IsNullOrWhiteSpace(item.VaiTroDay) ? "Giảng viên chính" : item.VaiTroDay.Trim(),
                            NgayPhanCong = DateTime.Now
                        });

                        countGiangVien++;
                        tenGiangViens.Add(tkGv.NguoiDung?.HoTen ?? tkGv.TenDangNhap);
                    }
                }

                // =========================================================================
                // 6. THÊM VÀ CẤP TÀI KHOẢN HỌC VIÊN (KHÔNG BẮT BUỘC)
                // =========================================================================
                int countHocVien = 0;
                var listResultTaiKhoanHocVien = new List<object>();

                if (request.DanhSachHocVien != null && request.DanhSachHocVien.Any())
                {
                    // Lấy vai trò học viên
                    var vaiTroHocVien = await _context.VaiTros
                        .FirstOrDefaultAsync(v => v.TenVaiTro.ToLower().Contains("học viên")
                                               || v.TenVaiTro.ToLower().Contains("hoc vien")
                                               || v.TenVaiTro.ToLower().Contains("user"));
                    int idVaiTroHocVien = vaiTroHocVien?.IdVaiTro ?? 1;

                    var usedUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var addedHocVienUserIds = new HashSet<int>();

                    foreach (var hv in request.DanhSachHocVien)
                    {
                        if (string.IsNullOrWhiteSpace(hv.HoTen)) continue;

                        string hoTenClean = hv.HoTen.Trim();
                        string rawEmail = hv.Email?.Trim() ?? "";
                        string rawPhone = hv.SoDienThoai?.Trim() ?? "";

                        // Mã hóa email & phone để tra cứu và lưu DB
                        string encEmail = !string.IsNullOrEmpty(rawEmail) ? SecurityHelper.Encrypt(rawEmail) : "";
                        string encPhone = !string.IsNullOrEmpty(rawPhone) ? SecurityHelper.Encrypt(rawPhone) : "";

                        // Kiểm tra học viên đã tồn tại trong CSDL chưa
                        NguoiDung? nguoiDungHienTai = null;
                        if (!string.IsNullOrEmpty(encEmail) || !string.IsNullOrEmpty(encPhone))
                        {
                            nguoiDungHienTai = await _context.NguoiDungs
                                .Include(nd => nd.IdTaiKhoanNavigation)
                                .FirstOrDefaultAsync(nd => (!string.IsNullOrEmpty(encEmail) && nd.Email == encEmail)
                                                        || (!string.IsNullOrEmpty(encPhone) && nd.SoDienThoai == encPhone));
                        }

                        int finalUserId = 0;
                        string finalUsername = "";
                        string matKhauExport = "";

                        if (nguoiDungHienTai != null)
                        {
                            finalUserId = nguoiDungHienTai.IdUser;
                            finalUsername = nguoiDungHienTai.IdTaiKhoanNavigation?.TenDangNhap ?? LoaiBoDauTiengViet(hoTenClean);
                            matKhauExport = "[Mật khẩu cũ của học viên]";
                        }
                        else
                        {
                            // Tự sinh username không trùng lặp
                            string baseUsername = LoaiBoDauTiengViet(hoTenClean);
                            finalUsername = baseUsername;
                            string ngaySinhSuffix = hv.NgaySinh.HasValue ? hv.NgaySinh.Value.ToString("ddMMyyyy") : "";

                            if (usedUsernames.Contains(finalUsername) || await _context.TaiKhoans.AnyAsync(tk => tk.TenDangNhap == finalUsername))
                            {
                                if (!string.IsNullOrEmpty(ngaySinhSuffix))
                                {
                                    finalUsername = $"{baseUsername}{ngaySinhSuffix}";
                                    int counterDob = 1;
                                    while (usedUsernames.Contains(finalUsername) || await _context.TaiKhoans.AnyAsync(tk => tk.TenDangNhap == finalUsername))
                                    {
                                        finalUsername = $"{baseUsername}{ngaySinhSuffix}{counterDob}";
                                        counterDob++;
                                    }
                                }
                                else
                                {
                                    int counter = 1;
                                    while (usedUsernames.Contains(finalUsername) || await _context.TaiKhoans.AnyAsync(tk => tk.TenDangNhap == finalUsername))
                                    {
                                        finalUsername = $"{baseUsername}{counter}";
                                        counter++;
                                    }
                                }
                            }

                            usedUsernames.Add(finalUsername);
                            matKhauExport = finalUsername;

                            var taiKhoanMoi = new TaiKhoan
                            {
                                TenDangNhap = finalUsername,
                                MatKhau = BCrypt.Net.BCrypt.HashPassword(matKhauExport),
                                TrangThai = true,
                                NgayTao = DateTime.Now,
                                IdVaiTro = idVaiTroHocVien,
                                IsVip = false,
                                IdTrungTam = finalIdTrungTam
                            };
                            _context.TaiKhoans.Add(taiKhoanMoi);
                            await _context.SaveChangesAsync();

                            var nguoiDungMoi = new NguoiDung
                            {
                                IdTaiKhoan = taiKhoanMoi.IdTaiKhoan,
                                HoTen = hoTenClean,
                                Email = string.IsNullOrEmpty(encEmail) ? null : encEmail,
                                SoDienThoai = string.IsNullOrEmpty(encPhone) ? null : encPhone,
                                NgaySinh = hv.NgaySinh,
                                AnhDaiDien = "default_avatar.png"
                            };
                            _context.NguoiDungs.Add(nguoiDungMoi);
                            await _context.SaveChangesAsync();

                            finalUserId = nguoiDungMoi.IdUser;
                        }

                        // Ghi danh học viên vào lớp (tránh trùng trong cùng payload)
                        if (!addedHocVienUserIds.Contains(finalUserId))
                        {
                            addedHocVienUserIds.Add(finalUserId);

                            _context.HocVienLopHocs.Add(new HocVienLopHoc
                            {
                                IdLopHoc = lopHoc.IdLopHoc,
                                IdUser = finalUserId,
                                NgayThamGia = DateTime.Now
                            });

                            countHocVien++;

                            listResultTaiKhoanHocVien.Add(new
                            {
                                idUser = finalUserId,
                                hoTen = hoTenClean,
                                tenDangNhap = finalUsername,
                                matKhau = matKhauExport,
                                email = rawEmail,
                                soDienThoai = rawPhone
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // =========================================================================
                // 7. GHI LOG AUDIT & LÀM MỚI CACHE
                // =========================================================================
                string nguoiThucHien = HttpContext.Session.GetString("HoTenNhanVien")
                                       ?? User.FindFirst(ClaimTypes.Name)?.Value
                                       ?? HttpContext.Session.GetString("TenDangNhap")
                                       ?? User.FindFirst("TenDangNhap")?.Value
                                       ?? "Quản lý cơ sở";

                string gvLog = countGiangVien > 0 ? $" (Phụ trách: {string.Join(", ", tenGiangViens)})" : "";
                string hvLog = countHocVien > 0 ? $", thêm {countHocVien} học viên" : "";
                string moTaLog = $"{nguoiThucHien} đã tạo thủ công lớp học '{lopHoc.TenLopHoc}' (Mã: {lopHoc.MaLopHoc} - #{lopHoc.IdLopHoc}) tại cơ sở '{trungTam.TenTrungTam}'{gvLog}{hvLog}.";

                await _auditLogService.GhiLichSuAsync(
                    hanhDong: "INSERT_LOPHOC_THUCONG",
                    moTa: moTaLog,
                    duLieuCu: null,
                    duLieuMoi: new
                    {
                        
                        TenLopHoc = lopHoc.TenLopHoc,
                        NgayKhaiGiang = lopHoc.NgayKhaiGiang?.ToString("dd/MM/yyyy"),
                        NgayKetThuc = lopHoc.NgayKetThuc?.ToString("dd/MM/yyyy"),
                        
                    }
                );

                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                return Ok(new
                {
                    success = true,
                    message = $"Tạo lớp học '{lopHoc.TenLopHoc}' thành công!",
                    data = new
                    {
                        idLopHoc = lopHoc.IdLopHoc,
                        idTrungTam = lopHoc.IdTrungTam,
                        tenTrungTam = trungTam.TenTrungTam,
                        maLopHoc = lopHoc.MaLopHoc,
                        tenLopHoc = lopHoc.TenLopHoc,
                        moTa = lopHoc.MoTa,
                        ngayKhaiGiang = lopHoc.NgayKhaiGiang,
                        ngayKetThuc = lopHoc.NgayKetThuc,
                        trangThai = lopHoc.TrangThai,
                        soLuongGiangVien = countGiangVien,
                        giangViens = tenGiangViens,
                        soLuongHocVien = countHocVien,
                        danhSachHocVien = listResultTaiKhoanHocVien
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi tạo lớp học thủ công");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống khi tạo lớp học: " + ex.Message
                });
            }
        }
        // ==========================================
        // API NHẬP EXCEL TẠO LỚP HỌC, HỌC VIÊN & GÁN GIẢNG VIÊN (CÓ ĐỌC MÃ LỚP HỌC & CHECK TRÙNG)
        // ==========================================
        [HttpPost("import-excel-lophoc")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportExcelLopHoc(IFormFile fileExcel)
        {
            // Lấy claim TenVaiTro và Trim() sạch khoảng trắng
            var tenVaiTro = (User.FindFirst("TenVaiTro")?.Value ?? "").Trim();

            if (!tenVaiTro.Equals("Quản lý trung tâm", StringComparison.OrdinalIgnoreCase) &&
                !tenVaiTro.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "Bạn không có quyền quản lý trung tâm."
                });
            }
            if (fileExcel == null || fileExcel.Length == 0)
            {
                return BadRequest(new { success = false, message = "Vui lòng chọn file Excel hợp lệ!" });
            }

            var extension = Path.GetExtension(fileExcel.FileName).ToLower();
            if (extension != ".xlsx" && extension != ".xls")
            {
                return BadRequest(new { success = false, message = "Chỉ chấp nhận file định dạng Excel (.xlsx, .xls)!" });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                using var stream = fileExcel.OpenReadStream();
                var rows = stream.Query(useHeaderRow: false).ToList();

                if (rows.Count < 7)
                {
                    return BadRequest(new { success = false, message = "File Excel không đúng cấu trúc quy định!" });
                }

                // 1. Đọc thông tin Trung tâm (Hàng 1: index 0, ô B1)
                IDictionary<string, object> row1 = rows[0] as IDictionary<string, object>;
                string maTrungTamExcel = row1 != null && row1.ContainsKey("B") ? row1["B"]?.ToString()?.Trim() ?? "" : "";

                bool isNumber = int.TryParse(maTrungTamExcel, out int idTt);
                var trungTam = await _context.TrungTams.FirstOrDefaultAsync(t =>
                    t.MaTrungTam == maTrungTamExcel || (isNumber && t.IdTrungTam == idTt)
                );

                if (trungTam == null)
                {
                    return BadRequest(new { success = false, message = $"Không tìm thấy cơ sở đào tạo có mã/ID '{maTrungTamExcel}' trong hệ thống!" });
                }

                // 2. Đọc Mã lớp học (Hàng 2: index 1, ô B2)
                IDictionary<string, object> row2 = rows[1] as IDictionary<string, object>;
                string maLopHocExcel = row2 != null && row2.ContainsKey("B") ? row2["B"]?.ToString()?.Trim() ?? "" : "";

                if (string.IsNullOrWhiteSpace(maLopHocExcel))
                {
                    return BadRequest(new { success = false, message = "Mã lớp học (ID_LopHoc) tại ô B2 không được để trống!" });
                }

                // [CHECK TRÙNG MÃ LỚP HỌC TRONG CÙNG CƠ SỞ]:
                bool daTonTaiMaLop = await _context.LopHocs.AnyAsync(l =>
                    l.IdTrungTam == trungTam.IdTrungTam &&
                    l.MaLopHoc.ToLower() == maLopHocExcel.ToLower()
                );

                if (daTonTaiMaLop)
                {
                    return Conflict(new
                    {
                        success = false,
                        message = $"Cơ sở '{trungTam.TenTrungTam}' đã tồn tại mã lớp '{maLopHocExcel}'. Vui lòng đổi mã lớp khác!"
                    });
                }

                // 3. Đọc Tên Lớp học (Hàng 3: index 2, ô B3) & Mô tả (Hàng 4: index 3, ô B4)
                IDictionary<string, object> row3 = rows[2] as IDictionary<string, object>;
                string tenLopHoc = row3 != null && row3.ContainsKey("B") ? row3["B"]?.ToString()?.Trim() ?? "" : "";

                IDictionary<string, object> row4 = rows[3] as IDictionary<string, object>;
                string moTaLopHoc = row4 != null && row4.ContainsKey("B") ? row4["B"]?.ToString()?.Trim() ?? "" : "";

                if (string.IsNullOrWhiteSpace(tenLopHoc))
                {
                    return BadRequest(new { success = false, message = "Tên lớp học tại ô B3 không được để trống!" });
                }

                

                // 4. Đọc Ngày bắt đầu (ô B5) & Ngày kết thúc (ô C5) (Hàng 5: index 4)
                DateOnly? ngayBatDau = null;
                DateOnly? ngayKetThuc = null;

                if (rows.Count > 4)
                {
                    IDictionary<string, object> row5 = rows[4] as IDictionary<string, object>;
                    if (row5 != null)
                    {
                        string ngayBatDauRaw = row5.ContainsKey("B") ? row5["B"]?.ToString()?.Trim() ?? "" : "";
                        if (DateTime.TryParse(ngayBatDauRaw, out DateTime dtBatDau))
                        {
                            ngayBatDau = DateOnly.FromDateTime(dtBatDau);
                        }

                        string ngayKetThucRaw = row5.ContainsKey("C") ? row5["C"]?.ToString()?.Trim() ?? "" : "";
                        if (DateTime.TryParse(ngayKetThucRaw, out DateTime dtKetThuc))
                        {
                            ngayKetThuc = DateOnly.FromDateTime(dtKetThuc);
                        }
                    }
                }

                if (!ngayBatDau.HasValue)
                {
                    ngayBatDau = DateOnly.FromDateTime(DateTime.Now);
                }

                // [CHECK HỢP LỆ THỜI GIAN]
                if (ngayKetThuc.HasValue && ngayKetThuc.Value < ngayBatDau.Value)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Ngày kết thúc ({ngayKetThuc:dd/MM/yyyy}) không thể diễn ra trước ngày khai giảng ({ngayBatDau:dd/MM/yyyy})!"
                    });
                }

                // 5. TẠO LỚP HỌC MỚI (LƯU MaLopHoc VÀO DATABASE)
                var lopHoc = new LopHoc
                {
                    IdTrungTam = trungTam.IdTrungTam,
                    MaLopHoc = maLopHocExcel, // <-- Lưu mã lớp từ ô B2
                    TenLopHoc = tenLopHoc,
                    NgayKhaiGiang = ngayBatDau,
                    NgayKetThuc = ngayKetThuc,
                    TrangThai = true,
                    MoTa = string.IsNullOrWhiteSpace(moTaLopHoc)
                        ? $"Mã: {maLopHocExcel} - Import ngày {DateTime.Now:dd/MM/yyyy HH:mm}"
                        : moTaLopHoc
                };
                _context.LopHocs.Add(lopHoc);
                await _context.SaveChangesAsync();

                // 6. Lấy vai trò Học viên hợp lệ
                var vaiTroHocVien = await _context.VaiTros
                    .FirstOrDefaultAsync(v => v.TenVaiTro.ToLower().Contains("học viên")
                                           || v.TenVaiTro.ToLower().Contains("hoc vien")
                                           || v.TenVaiTro.ToLower().Contains("user"));
                int? idVaiTro = vaiTroHocVien?.IdVaiTro;
                if (!idVaiTro.HasValue)
                {
                    var firstRole = await _context.VaiTros.FirstOrDefaultAsync();
                    idVaiTro = firstRole?.IdVaiTro ?? 1;
                }

                // 7. Quản lý kiểm tra trùng lặp danh sách
                var usedUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var usedEmailsInExcel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var usedPhonesInExcel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var addedGiangVienIds = new HashSet<int>();
                var addedHocVienUserIds = new HashSet<int>();

                var listResultTaiKhoan = new List<ExportTaiKhoanHocVienResult>();

                int countHocVienMoi = 0;
                int countGiangVienMoi = 0;
                var tenGiangViens = new List<string>();

                // Duyệt từ hàng thứ 8 trở đi (index = 7)
                for (int i = 7; i < rows.Count; i++)
                {
                    IDictionary<string, object> rowData = rows[i] as IDictionary<string, object>;
                    if (rowData == null) continue;

                    int sttDongExcel = i + 1;

                    // ---------------------------------------------------------
                    // XỬ LÝ VÀ KIỂM TRA GIẢNG VIÊN (CỘT E & F)
                    // ---------------------------------------------------------
                    string maGvRaw = rowData.ContainsKey("E") ? rowData["E"]?.ToString()?.Trim() ?? "" : "";
                    string vaiTroDay = rowData.ContainsKey("F") ? rowData["F"]?.ToString()?.Trim() ?? "" : "";

                    if (!string.IsNullOrWhiteSpace(maGvRaw))
                    {
                        bool isGvNumber = int.TryParse(maGvRaw, out int idGv);

                        var taiKhoanGv = await _context.TaiKhoans
                            .Include(tk => tk.NguoiDung)
                            .Include(tk => tk.IdTrungTamNavigation)
                            .FirstOrDefaultAsync(tk => (isGvNumber && tk.IdTaiKhoan == idGv)
                                                    || tk.TenDangNhap.ToLower() == maGvRaw.ToLower());

                        if (taiKhoanGv == null)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new
                            {
                                success = false,
                                message = $"Lỗi tại dòng {sttDongExcel}: Mã tài khoản giảng viên '{maGvRaw}' không tồn tại trong hệ thống!"
                            });
                        }

                        if (taiKhoanGv.IdTrungTam != trungTam.IdTrungTam)
                        {
                            await transaction.RollbackAsync();
                            string tenTtCuaGv = taiKhoanGv.IdTrungTamNavigation?.TenTrungTam ?? $"ID: {taiKhoanGv.IdTrungTam}";
                            return BadRequest(new
                            {
                                success = false,
                                message = $"Lỗi tại dòng {sttDongExcel}: Giảng viên '{taiKhoanGv.NguoiDung?.HoTen ?? taiKhoanGv.TenDangNhap}' thuộc cơ sở '{tenTtCuaGv}', không thuộc cơ sở '{trungTam.TenTrungTam}'!"
                            });
                        }

                        if (!addedGiangVienIds.Contains(taiKhoanGv.IdTaiKhoan))
                        {
                            addedGiangVienIds.Add(taiKhoanGv.IdTaiKhoan);

                            var gvLopHoc = new GiangVienLopHoc
                            {
                                IdLopHoc = lopHoc.IdLopHoc,
                                IdTaiKhoan = taiKhoanGv.IdTaiKhoan,
                                VaiTroDay = string.IsNullOrWhiteSpace(vaiTroDay) ? "Giảng viên chính" : vaiTroDay,
                                NgayPhanCong = DateTime.Now
                            };
                            _context.GiangVienLopHocs.Add(gvLopHoc);
                            countGiangVienMoi++;
                            tenGiangViens.Add(taiKhoanGv.NguoiDung?.HoTen ?? taiKhoanGv.TenDangNhap);
                        }
                    }

                    // ---------------------------------------------------------
                    // XỬ LÝ DANH SÁCH HỌC VIÊN (CỘT A, B, C, D)
                    // ---------------------------------------------------------
                    string hoTen = rowData.ContainsKey("A") ? rowData["A"]?.ToString()?.Trim() ?? "" : "";
                   
                    string ngaySinhRaw = rowData.ContainsKey("D") ? rowData["D"]?.ToString()?.Trim() ?? "" : "";
                    // 1. Đọc và làm sạch chuỗi thô từ file/dữ liệu đầu vào
                    string rawEmail = rowData.ContainsKey("B") ? rowData["B"]?.ToString()?.Trim() ?? "" : "";
                    string rawSoDienThoai = rowData.ContainsKey("C") ? rowData["C"]?.ToString()?.Trim() ?? "" : "";

                    // 2. Mã hóa trước khi gán vào đối tượng lưu database
                    // (Chỉ mã hóa nếu có dữ liệu, tránh mã hóa chuỗi rỗng gây tốn dung lượng)
                    string email = !string.IsNullOrWhiteSpace(rawEmail)
                        ? SecurityHelper.Encrypt(rawEmail)
                        : "";

                    string soDienThoai = !string.IsNullOrWhiteSpace(rawSoDienThoai)
                        ? SecurityHelper.Encrypt(rawSoDienThoai)
                        : "";
                    if (string.IsNullOrWhiteSpace(hoTen)) continue;

                    // [CHECK TRÙNG TRONG CÙNG FILE EXCEL]
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        if (usedEmailsInExcel.Contains(email))
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new
                            {
                                success = false,
                                message = $"Lỗi tại dòng {sttDongExcel}: Email '{email}' bị trùng lặp nhiều lần trong file Excel!"
                            });
                        }
                        usedEmailsInExcel.Add(email);
                    }

                    if (!string.IsNullOrWhiteSpace(soDienThoai))
                    {
                        if (usedPhonesInExcel.Contains(soDienThoai))
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new
                            {
                                success = false,
                                message = $"Lỗi tại dòng {sttDongExcel}: Số điện thoại '{soDienThoai}' bị trùng lặp nhiều lần trong file Excel!"
                            });
                        }
                        usedPhonesInExcel.Add(soDienThoai);
                    }

                    DateOnly? ngaySinh = null;
                    string ngaySinhSuffix = "";
                    if (DateTime.TryParse(ngaySinhRaw, out DateTime dtParsed))
                    {
                        ngaySinh = DateOnly.FromDateTime(dtParsed);
                        ngaySinhSuffix = dtParsed.ToString("ddMMyyyy");
                    }

                    // [CHECK TRÙNG HỌC VIÊN VỚI DATABASE]
                    NguoiDung nguoiDungHienTai = null;
                    if (!string.IsNullOrWhiteSpace(email) || !string.IsNullOrWhiteSpace(soDienThoai))
                    {
                        nguoiDungHienTai = await _context.NguoiDungs
                            .Include(nd => nd.IdTaiKhoanNavigation)
                            .FirstOrDefaultAsync(nd => (!string.IsNullOrEmpty(email) && nd.Email == email)
                                                    || (!string.IsNullOrEmpty(soDienThoai) && nd.SoDienThoai == soDienThoai));
                    }

                    int finalUserId = 0;
                    string finalUsername = "";
                    string matKhauXuatExcel = "";
                    string trangThaiKetQua = "Thành công (Tạo mới)";

                    if (nguoiDungHienTai != null)
                    {
                        finalUserId = nguoiDungHienTai.IdUser;
                        finalUsername = nguoiDungHienTai.IdTaiKhoanNavigation?.TenDangNhap ?? LoaiBoDauTiengViet(hoTen);
                        matKhauXuatExcel = "[Mật khẩu cũ của học viên]";
                        trangThaiKetQua = "Thành công (Tài khoản cũ)";
                    }
                    else
                    {
                        string baseUsername = LoaiBoDauTiengViet(hoTen);
                        finalUsername = baseUsername;

                        bool isDuplicateUsername = usedUsernames.Contains(finalUsername)
                                                || await _context.TaiKhoans.AnyAsync(tk => tk.TenDangNhap == finalUsername);

                        if (isDuplicateUsername)
                        {
                            if (!string.IsNullOrEmpty(ngaySinhSuffix))
                            {
                                finalUsername = $"{baseUsername}{ngaySinhSuffix}";
                                int counterDob = 1;
                                while (usedUsernames.Contains(finalUsername) || await _context.TaiKhoans.AnyAsync(tk => tk.TenDangNhap == finalUsername))
                                {
                                    finalUsername = $"{baseUsername}{ngaySinhSuffix}{counterDob}";
                                    counterDob++;
                                }
                            }
                            else
                            {
                                int counter = 1;
                                while (usedUsernames.Contains(finalUsername) || await _context.TaiKhoans.AnyAsync(tk => tk.TenDangNhap == finalUsername))
                                {
                                    finalUsername = $"{baseUsername}{counter}";
                                    counter++;
                                }
                            }
                        }

                        usedUsernames.Add(finalUsername);
                        string matKhau = finalUsername;
                        matKhauXuatExcel = matKhau;

                        var taiKhoan = new TaiKhoan
                        {
                            TenDangNhap = finalUsername,
                            MatKhau = BCrypt.Net.BCrypt.HashPassword(matKhau),
                            TrangThai = true,
                            NgayTao = DateTime.Now,
                            IdVaiTro = idVaiTro,
                            IsVip = false,
                            IdTrungTam = trungTam.IdTrungTam
                        };
                        _context.TaiKhoans.Add(taiKhoan);
                        await _context.SaveChangesAsync();

                        var nguoiDung = new NguoiDung
                        {
                            IdTaiKhoan = taiKhoan.IdTaiKhoan,
                            HoTen = hoTen,
                            Email = string.IsNullOrWhiteSpace(email) ? null : email,
                            SoDienThoai = string.IsNullOrWhiteSpace(soDienThoai) ? null : soDienThoai,
                            NgaySinh = ngaySinh,
                            AnhDaiDien = "default_avatar.png"
                        };
                        _context.NguoiDungs.Add(nguoiDung);
                        await _context.SaveChangesAsync();

                        finalUserId = nguoiDung.IdUser;
                    }

                    // [CHECK TRÙNG THÀNH VIÊN TRONG LỚP]
                    if (!addedHocVienUserIds.Contains(finalUserId))
                    {
                        addedHocVienUserIds.Add(finalUserId);

                        var hocVienLopHoc = new HocVienLopHoc
                        {
                            IdLopHoc = lopHoc.IdLopHoc,
                            IdUser = finalUserId,
                            NgayThamGia = DateTime.Now
                        };
                        _context.HocVienLopHocs.Add(hocVienLopHoc);
                        await _context.SaveChangesAsync();

                        countHocVienMoi++;
                    }

                    listResultTaiKhoan.Add(new ExportTaiKhoanHocVienResult
                    {
                        TenHocVien = hoTen,
                        TenDangNhap = finalUsername,
                        MatKhau = matKhauXuatExcel,
                        Email = email,
                        SoDienThoai = soDienThoai,
                        TenLopHoc = lopHoc.TenLopHoc,
                        TrangThai = trangThaiKetQua
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 8. GHI AUDIT LOG
                string nguoiThucHien = HttpContext.Session.GetString("HoTenNhanVien")
                                        ?? User.FindFirst(ClaimTypes.Name)?.Value
                                        ?? HttpContext.Session.GetString("TenDangNhap")
                                        ?? User.FindFirst("TenDangNhap")?.Value
                                        ?? "Quản lý cơ sở";

                string thongTinGvLog = countGiangVienMoi > 0
                    ? $" (Phụ trách: {string.Join(", ", tenGiangViens)})"
                    : " (Chưa xếp giảng viên phụ trách)";

                string moTaLog = $"{nguoiThucHien} đã nhập Excel tạo mới lớp '{lopHoc.TenLopHoc}' (Mã: {lopHoc.MaLopHoc} - #{lopHoc.IdLopHoc}){thongTinGvLog}, thời gian ({lopHoc.NgayKhaiGiang:dd/MM/yyyy} - {lopHoc.NgayKetThuc:dd/MM/yyyy}) và thêm {countHocVienMoi} học viên.";

                await _auditLogService.GhiLichSuAsync(
                    hanhDong: "IMPORT_EXCEL_LOPHOC",
                    moTa: moTaLog,
                    duLieuCu: null,
                    duLieuMoi: new
                    {
                        MaLopHoc = lopHoc.MaLopHoc,
                        TenLopHoc = lopHoc.TenLopHoc,
                        NgayKhaiGiang = lopHoc.NgayKhaiGiang?.ToString("dd/MM/yyyy"),
                        NgayKetThuc = lopHoc.NgayKetThuc?.ToString("dd/MM/yyyy"),
                        SoGiangVienPhanCong = countGiangVienMoi,
                        DanhSachGiangVien = tenGiangViens,
                        SoHocVienImport = countHocVienMoi
                    }
                );

                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                // 9. Xuất file Excel danh sách tài khoản đã cấp
                var memoryStream = new MemoryStream();
                await memoryStream.SaveAsAsync(listResultTaiKhoan);
                memoryStream.Seek(0, SeekOrigin.Begin);

                string fileName = $"DanhSachTaiKhoan_{lopHoc.MaLopHoc}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                return File(memoryStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi import file Excel lớp học");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi trong quá trình xử lý file Excel: " + ex.Message
                });
            }
        }

        // ==========================================
        // 1. CẬP NHẬT LỚP HỌC (TRUY VẾT: UPDATE_LOPHOC - CHỈ LƯU TRƯỜNG THAY ĐỔI)
        // PUT: api/quanlylophoc/update-lophoc/{idLopHoc}
        // ==========================================
        [HttpPut("update-lophoc/{idLopHoc}")]
        public async Task<IActionResult> UpdateLopHoc([FromRoute] int idLopHoc, [FromBody] LopHocModels model)
        {
            if (idLopHoc <= 0 || model == null)
            {
                return BadRequest(new { success = false, message = "Dữ liệu yêu cầu không hợp lệ." });
            }

            if (string.IsNullOrWhiteSpace(model.TenLopHoc))
            {
                return BadRequest(new { success = false, message = "Tên lớp học không được để trống!" });
            }

            try
            {
                // 1. Lấy lớp học cần sửa kèm danh sách phân công giảng viên hiện tại
                var lopHoc = await _context.LopHocs
                    .Include(l => l.IdTrungTamNavigation)
                    .Include(l => l.GiangVienLopHocs)
                    .FirstOrDefaultAsync(l => l.IdLopHoc == idLopHoc);

                if (lopHoc == null)
                {
                    return NotFound(new { success = false, message = $"Không tìm thấy lớp học với mã #{idLopHoc}." });
                }

                // 2. Bảo mật Multi-tenant: Quản lý chỉ được sửa lớp thuộc trung tâm mình
                var tenVaiTro = (User.FindFirst("TenVaiTro")?.Value
                                 ?? HttpContext.Session?.GetString("TenVaiTro")
                                 ?? "").Trim();
                bool isAdmin = tenVaiTro.Equals("Quản lý trung tâm", StringComparison.OrdinalIgnoreCase)
                               || tenVaiTro.Equals("Admin", StringComparison.OrdinalIgnoreCase);

                string? claimIdTrungTamStr = User.FindFirst("IdTrungTam")?.Value
                                             ?? HttpContext.Session?.GetString("IdTrungTam");

                if (!isAdmin && int.TryParse(claimIdTrungTamStr, out int currentIdTrungTam) && currentIdTrungTam > 0)
                {
                    if (lopHoc.IdTrungTam != currentIdTrungTam)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new
                        {
                            success = false,
                            message = "Bạn không có quyền chỉnh sửa lớp học thuộc cơ sở khác!"
                        });
                    }
                }

                // 3. Snapshot dữ liệu CŨ trước khi sửa
                var snapshotCu = new
                {
                    TenLopHoc = lopHoc.TenLopHoc,
                    MoTa = lopHoc.MoTa,
                    NgayKhaiGiang = lopHoc.NgayKhaiGiang,
                    NgayKetThuc = lopHoc.NgayKetThuc,
                    TrangThai = lopHoc.TrangThai,
                    GiangVienCu = string.Join(", ", lopHoc.GiangVienLopHocs.Select(g => $"{g.IdTaiKhoan} ({g.VaiTroDay})"))
                };

                // 4. Gán giá trị mới cho thông tin lớp
                lopHoc.TenLopHoc = model.TenLopHoc.Trim();
                lopHoc.MoTa = model.MoTa?.Trim();
                lopHoc.NgayKhaiGiang = model.NgayKhaiGiang;
                lopHoc.NgayKetThuc = model.NgayKetThuc;
                lopHoc.TrangThai = model.TrangThai;

                // 5. CẬP NHẬT GẮN GIẢNG VIÊN VÀO LỚP HỌC
                if (model.DanhSachGiangVien != null)
                {
                    // Lọc danh sách IdTaiKhoan hợp lệ gửi lên (loại bỏ trùng lặp)
                    var dsMoi = model.DanhSachGiangVien
                        .GroupBy(x => x.IdTaiKhoan)
                        .Select(g => g.First())
                        .ToList();

                    var listIdTaiKhoanMoi = dsMoi.Select(x => x.IdTaiKhoan).ToList();

                    // Kiểm tra các tài khoản giảng viên này có hợp lệ không
                    if (listIdTaiKhoanMoi.Any())
                    {
                        var hopLeCount = await _context.TaiKhoans
                            .Where(t => listIdTaiKhoanMoi.Contains(t.IdTaiKhoan) && t.TrangThai == true)
                            .CountAsync();

                        if (hopLeCount != listIdTaiKhoanMoi.Count)
                        {
                            return BadRequest(new
                            {
                                success = false,
                                message = "Có giảng viên trong danh sách không hợp lệ hoặc đã bị vô hiệu hóa!"
                            });
                        }
                    }

                    // Xóa những phân công không còn nằm trong danh sách mới
                    var phanCongXoa = lopHoc.GiangVienLopHocs
                        .Where(gv => !listIdTaiKhoanMoi.Contains(gv.IdTaiKhoan))
                        .ToList();

                    if (phanCongXoa.Any())
                    {
                        _context.GiangVienLopHocs.RemoveRange(phanCongXoa);
                    }

                    // Thêm mới hoặc cập nhật vai trò dạy cho giảng viên
                    foreach (var item in dsMoi)
                    {
                        var phanCongHienTai = lopHoc.GiangVienLopHocs
                            .FirstOrDefault(gv => gv.IdTaiKhoan == item.IdTaiKhoan);

                        if (phanCongHienTai != null)
                        {
                            // Cập nhật lại vai trò dạy nếu thay đổi (ví dụ đổi từ Trợ giảng sang Giảng viên chính)
                            phanCongHienTai.VaiTroDay = string.IsNullOrWhiteSpace(item.VaiTroDay) ? "Giảng viên chính" : item.VaiTroDay.Trim();
                        }
                        else
                        {
                            // Thêm mới liên kết
                            lopHoc.GiangVienLopHocs.Add(new GiangVienLopHoc
                            {
                                IdLopHoc = lopHoc.IdLopHoc,
                                IdTaiKhoan = item.IdTaiKhoan,
                                VaiTroDay = string.IsNullOrWhiteSpace(item.VaiTroDay) ? "Giảng viên chính" : item.VaiTroDay.Trim(),
                                NgayPhanCong = DateTime.Now
                            });
                        }
                    }
                }

                // 6. Snapshot dữ liệu MỚI
                var snapshotMoi = new
                {
                    TenLopHoc = lopHoc.TenLopHoc,
                    MoTa = lopHoc.MoTa,
                    NgayKhaiGiang = lopHoc.NgayKhaiGiang,
                    NgayKetThuc = lopHoc.NgayKetThuc,
                    TrangThai = lopHoc.TrangThai,
                    GiangVienMoi = model.DanhSachGiangVien != null
                        ? string.Join(", ", model.DanhSachGiangVien.Select(g => $"{g.IdTaiKhoan} ({g.VaiTroDay})"))
                        : "Không đổi"
                };

                // 7. Kiểm tra thay đổi và lưu vào DB
                var (duLieuCu, duLieuMoi) = KiemTraDuLieu.GetChanges(snapshotCu, snapshotMoi);

                _context.LopHocs.Update(lopHoc);
                await _context.SaveChangesAsync();

                // 8. Ghi log truy vết nếu có thay đổi
                if (duLieuCu.Count > 0)
                {
                    string nguoiThucHien = HttpContext.Session?.GetString("HoTenNhanVien")
                                           ?? User.FindFirst(ClaimTypes.Name)?.Value
                                           ?? HttpContext.Session?.GetString("TenDangNhap")
                                           ?? User.FindFirst("TenDangNhap")?.Value
                                           ?? "Quản lý cơ sở";

                    var tenCacTruong = string.Join(", ", duLieuMoi.Keys);

                    await _auditLogService.GhiLichSuAsync(
                        hanhDong: "UPDATE_LOPHOC",
                        moTa: $"{nguoiThucHien} đã cập nhật lớp học '{lopHoc.TenLopHoc}' (#{lopHoc.IdLopHoc}) - Các mục đổi: {tenCacTruong}",
                        duLieuCu: duLieuCu,
                        duLieuMoi: duLieuMoi
                    );
                }

                // 9. Xóa Cache lớp học để các API xem chi tiết nhận dữ liệu mới ngay
                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();
                _cache.Remove($"ChiTiet_LopHoc_{idLopHoc}");

                return Ok(new
                {
                    success = true,
                    message = $"Cập nhật lớp học '{lopHoc.TenLopHoc}' và phân công giảng viên thành công!",
                    data = new
                    {
                        idLopHoc = lopHoc.IdLopHoc,
                        idTrungTam = lopHoc.IdTrungTam,
                        tenLopHoc = lopHoc.TenLopHoc,
                        moTa = lopHoc.MoTa,
                        ngayKhaiGiang = lopHoc.NgayKhaiGiang,
                        ngayKetThuc = lopHoc.NgayKetThuc,
                        trangThai = lopHoc.TrangThai,
                        soLuongGiangVien = lopHoc.GiangVienLopHocs.Count
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật lớp học #{IdLopHoc}", idLopHoc);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống khi cập nhật thông tin lớp học: " + ex.Message
                });
            }
        }

        // ==========================================
        // 2. XÓA LỚP HỌC (TRUY VẾT: DELETE_LOPHOC)
        // DELETE: api/quanlylophoc/delete-lophoc/{idLopHoc}
        // ==========================================
        [HttpDelete("delete-lophoc/{idLopHoc}")]
        public async Task<IActionResult> DeleteLopHoc([FromRoute] int idLopHoc)
        {
            if (idLopHoc <= 0)
            {
                return BadRequest(new { success = false, message = "ID lớp học không hợp lệ." });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Tìm lớp học kèm danh sách học viên & giảng viên
                var lopHoc = await _context.LopHocs
                    .Include(l => l.IdTrungTamNavigation)
                    .Include(l => l.HocVienLopHocs)
                    .Include(l => l.GiangVienLopHocs)
                    .FirstOrDefaultAsync(l => l.IdLopHoc == idLopHoc);

                if (lopHoc == null)
                {
                    return NotFound(new { success = false, message = $"Không tìm thấy lớp học với mã #{idLopHoc}." });
                }

                // 2. Bảo mật Multi-tenant: Quản lý chỉ được xóa lớp thuộc cơ sở mình
                string? claimIdTrungTamStr = User.FindFirst("IdTrungTam")?.Value
                                             ?? HttpContext.Session.GetString("IdTrungTam");
                if (int.TryParse(claimIdTrungTamStr, out int currentIdTrungTam) && currentIdTrungTam > 0)
                {
                    if (lopHoc.IdTrungTam != currentIdTrungTam)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new
                        {
                            success = false,
                            message = "Bạn không có quyền xóa lớp học thuộc cơ sở khác!"
                        });
                    }
                }

                int soLuongHocVien = lopHoc.HocVienLopHocs?.Count ?? 0;
                int soLuongGiangVien = lopHoc.GiangVienLopHocs?.Count ?? 0;

                // 3. Snapshot dữ liệu cũ trước khi xóa để lưu vết
                var duLieuXoa = new
                {
                    IdLopHoc = lopHoc.IdLopHoc,
                    TenLopHoc = lopHoc.TenLopHoc,
                    IdTrungTam = lopHoc.IdTrungTam,
                    TenTrungTam = lopHoc.IdTrungTamNavigation?.TenTrungTam,
                    NgayKhaiGiang = lopHoc.NgayKhaiGiang?.ToString("dd/MM/yyyy"),
                    NgayKetThuc = lopHoc.NgayKetThuc?.ToString("dd/MM/yyyy"),
                    
                    MoTa = lopHoc.MoTa,
                    TongSoHocVien = soLuongHocVien,
                    TongSoGiangVien = soLuongGiangVien
                };

                // 4. Xóa các liên kết bảng trung gian trước (nếu cascade delete chưa bật)
                if (lopHoc.HocVienLopHocs != null && lopHoc.HocVienLopHocs.Any())
                {
                    _context.HocVienLopHocs.RemoveRange(lopHoc.HocVienLopHocs);
                }

                if (lopHoc.GiangVienLopHocs != null && lopHoc.GiangVienLopHocs.Any())
                {
                    _context.GiangVienLopHocs.RemoveRange(lopHoc.GiangVienLopHocs);
                }

                // 5. Xóa lớp học
                _context.LopHocs.Remove(lopHoc);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 6. Ghi vết truy vết vào Audit Log
                string nguoiThucHien = HttpContext.Session.GetString("HoTenNhanVien")
                                       ?? User.FindFirst(ClaimTypes.Name)?.Value
                                       ?? HttpContext.Session.GetString("TenDangNhap")
                                       ?? User.FindFirst("TenDangNhap")?.Value
                                       ?? "Quản lý cơ sở";

                await _auditLogService.GhiLichSuAsync(
                    hanhDong: "DELETE_LOPHOC",
                    moTa: $"{nguoiThucHien} đã xóa lớp học '{duLieuXoa.TenLopHoc}' (#{duLieuXoa.IdLopHoc}) khỏi cơ sở '{duLieuXoa.TenTrungTam}'. (Bao gồm xóa liên kết {soLuongHocVien} học viên & {soLuongGiangVien} giảng viên)",
                    duLieuCu: duLieuXoa,
                    duLieuMoi: null
                );

                // Hủy cache danh sách lớp
                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();
                _cache.Remove($"ChiTiet_LopHoc_{idLopHoc}");

                return Ok(new
                {
                    success = true,
                    message = $"Đã xóa thành công lớp học '{duLieuXoa.TenLopHoc}'!"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi xóa lớp học #{IdLopHoc}", idLopHoc);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống khi xóa lớp học: " + ex.Message
                });
            }
        }
        // ==========================================
        // API THÊM HỌC VIÊN THỦ CÔNG & TỰ CẤP TÀI KHOẢN (TRUY VẾT: INSERT_HOCVIEN_LOPHOC)
        // POST: api/quanlylophoc/them-hoc-vien
        // ==========================================
        [HttpPost("them-hoc-vien")]
        public async Task<IActionResult> ThemHocVien([FromBody] ThemHocVienVaoLopRequest request)
        {
            if (request == null || request.IdLopHoc <= 0 || string.IsNullOrWhiteSpace(request.HoTen))
            {
                return BadRequest(new { success = false, message = "Vui lòng nhập đầy đủ thông tin học viên và mã lớp học hợp lệ." });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Kiểm tra lớp học tồn tại
                var lopHoc = await _context.LopHocs
                    .Include(l => l.IdTrungTamNavigation)
                    .FirstOrDefaultAsync(l => l.IdLopHoc == request.IdLopHoc);

                if (lopHoc == null)
                {
                    return NotFound(new { success = false, message = $"Không tìm thấy lớp học với mã #{request.IdLopHoc}." });
                }

                // 2. Bảo mật cơ sở (Multi-tenant)
                string? claimIdTrungTamStr = User.FindFirst("IdTrungTam")?.Value
                                             ?? HttpContext.Session.GetString("IdTrungTam");
                if (int.TryParse(claimIdTrungTamStr, out int currentIdTrungTam) && currentIdTrungTam > 0)
                {
                    if (lopHoc.IdTrungTam != currentIdTrungTam)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new
                        {
                            success = false,
                            message = "Bạn không có quyền thêm học viên vào lớp học của cơ sở khác!"
                        });
                    }
                }

                // 3. Lấy vai trò Học viên
                var vaiTroHocVien = await _context.VaiTros
                    .FirstOrDefaultAsync(v => v.TenVaiTro.ToLower().Contains("học viên")
                                           || v.TenVaiTro.ToLower().Contains("hoc vien")
                                           || v.TenVaiTro.ToLower().Contains("user"));
                int? idVaiTro = vaiTroHocVien?.IdVaiTro;
                if (!idVaiTro.HasValue)
                {
                    var firstRole = await _context.VaiTros.FirstOrDefaultAsync();
                    idVaiTro = firstRole?.IdVaiTro;
                }

                // 4. Sinh tên đăng nhập không trùng
                string hoTenClean = request.HoTen.Trim();
                string baseUsername = LoaiBoDauTiengViet(hoTenClean);
                string finalUsername = baseUsername;

                string ngaySinhSuffix = request.NgaySinh.HasValue
                    ? request.NgaySinh.Value.ToString("ddMMyyyy")
                    : "";

                bool isDuplicate = await _context.TaiKhoans.AnyAsync(tk => tk.TenDangNhap == finalUsername);
                if (isDuplicate)
                {
                    if (!string.IsNullOrEmpty(ngaySinhSuffix))
                    {
                        finalUsername = $"{baseUsername}{ngaySinhSuffix}";
                        int counterDob = 1;
                        while (await _context.TaiKhoans.AnyAsync(tk => tk.TenDangNhap == finalUsername))
                        {
                            finalUsername = $"{baseUsername}{ngaySinhSuffix}{counterDob}";
                            counterDob++;
                        }
                    }
                    else
                    {
                        int counter = 1;
                        while (await _context.TaiKhoans.AnyAsync(tk => tk.TenDangNhap == finalUsername))
                        {
                            finalUsername = $"{baseUsername}{counter}";
                            counter++;
                        }
                    }
                }

                string matKhauMacDinh = finalUsername;

                // 5. Khởi tạo tài khoản đăng nhập
                var taiKhoan = new TaiKhoan
                {
                    TenDangNhap = finalUsername,
                    MatKhau = BCrypt.Net.BCrypt.HashPassword(matKhauMacDinh),
                    TrangThai = true,
                    NgayTao = DateTime.Now,
                    IdVaiTro = idVaiTro,
                    IsVip = false,
                    IdTrungTam = lopHoc.IdTrungTam
                };
                _context.TaiKhoans.Add(taiKhoan);
                await _context.SaveChangesAsync();

                // 6. Khởi tạo thông tin người dùng
                var nguoiDung = new NguoiDung
                {
                    IdTaiKhoan = taiKhoan.IdTaiKhoan,
                    HoTen = hoTenClean,
                    Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                    SoDienThoai = string.IsNullOrWhiteSpace(request.SoDienThoai) ? null : request.SoDienThoai.Trim(),
                    NgaySinh = request.NgaySinh
                };
                _context.NguoiDungs.Add(nguoiDung);
                await _context.SaveChangesAsync();

                // 7. Ghi danh vào lớp học
                var hocVienLopHoc = new HocVienLopHoc
                {
                    IdLopHoc = lopHoc.IdLopHoc,
                    IdUser = nguoiDung.IdUser,
                    NgayThamGia = DateTime.Now
                };
                _context.HocVienLopHocs.Add(hocVienLopHoc);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // 8. Ghi log truy vết
                string nguoiThucHien = HttpContext.Session.GetString("HoTenNhanVien")
                                       ?? User.FindFirst(ClaimTypes.Name)?.Value
                                       ?? HttpContext.Session.GetString("TenDangNhap")
                                       ?? User.FindFirst("TenDangNhap")?.Value
                                       ?? "Quản lý cơ sở";

                await _auditLogService.GhiLichSuAsync(
                    hanhDong: "INSERT_HOCVIEN_LOPHOC",
                    moTa: $"{nguoiThucHien} đã thêm học viên '{nguoiDung.HoTen}' (Tài khoản: {taiKhoan.TenDangNhap}) vào lớp '{lopHoc.TenLopHoc}' (#{lopHoc.IdLopHoc}).",
                    duLieuCu: null,
                    duLieuMoi: new
                    {
                        IdLopHoc = lopHoc.IdLopHoc,
                        TenLopHoc = lopHoc.TenLopHoc,
                        IdUser = nguoiDung.IdUser,
                        HoTen = nguoiDung.HoTen,
                        IdTaiKhoan = taiKhoan.IdTaiKhoan,
                        TenDangNhap = taiKhoan.TenDangNhap
                    }
                );

                // Hủy cache chi tiết lớp học
                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();
                _cache.Remove($"ChiTiet_LopHoc_{lopHoc.IdLopHoc}");

                return Ok(new
                {
                    success = true,
                    message = $"Thêm học viên '{nguoiDung.HoTen}' vào lớp thành công!",
                    data = new
                    {
                        idUser = nguoiDung.IdUser,
                        idTaiKhoan = taiKhoan.IdTaiKhoan,
                        tenDangNhap = taiKhoan.TenDangNhap,
                        matKhauMacDinh = matKhauMacDinh,
                        hoTen = nguoiDung.HoTen,
                        email = nguoiDung.Email,
                        soDienThoai = nguoiDung.SoDienThoai,
                        ngaySinh = nguoiDung.NgaySinh?.ToString("dd/MM/yyyy"),
                        ngayThamGia = hocVienLopHoc.NgayThamGia?.ToString("dd/MM/yyyy HH:mm")
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi thêm học viên vào lớp học #{IdLopHoc}", request.IdLopHoc);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống khi thêm học viên: " + ex.Message
                });
            }
        }

        // ==========================================
        // API XÓA HỌC VIÊN RA KHỎI LỚP (TRUY VẾT: REMOVE_HOCVIEN_LOPHOC)
        // DELETE: api/quanlylophoc/xoa-hoc-vien?idLopHoc={idLopHoc}&idUser={idUser}
        // ==========================================
        [HttpDelete("xoa-hoc-vien")]
        public async Task<IActionResult> XoaHocVien([FromQuery] int idLopHoc, [FromQuery] int idUser)
        {
            if (idLopHoc <= 0 || idUser <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "ID lớp học và ID học viên không hợp lệ!"
                });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Tìm bản ghi học viên trong lớp học kèm thông tin lớp & người dùng
                var hocVienLop = await _context.HocVienLopHocs
                    .Include(h => h.IdLopHocNavigation)
                    .Include(h => h.IdUserNavigation)
                    .FirstOrDefaultAsync(h => h.IdLopHoc == idLopHoc && h.IdUser == idUser);

                if (hocVienLop == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Không tìm thấy học viên này trong lớp học!"
                    });
                }

                // 2. Bảo mật Multi-tenant: Kiểm tra lớp có thuộc quyền quản lý của trung tâm người thao tác không
                string? claimIdTrungTamStr = User.FindFirst("IdTrungTam")?.Value
                                             ?? HttpContext.Session.GetString("IdTrungTam");
                if (int.TryParse(claimIdTrungTamStr, out int currentIdTrungTam) && currentIdTrungTam > 0)
                {
                    if (hocVienLop.IdLopHocNavigation != null && hocVienLop.IdLopHocNavigation.IdTrungTam != currentIdTrungTam)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new
                        {
                            success = false,
                            message = "Bạn không có quyền xóa học viên thuộc lớp của cơ sở khác!"
                        });
                    }
                }

                string tenHocVien = hocVienLop.IdUserNavigation?.HoTen ?? $"Học viên #{idUser}";
                string tenLop = hocVienLop.IdLopHocNavigation?.TenLopHoc ?? $"Lớp #{idLopHoc}";

                // 3. Snapshot dữ liệu cũ để phục vụ ghi nhật ký kiểm tra dữ liệu
                var duLieuCu = new
                {
                   
                    TenLopHoc = tenLop,
                   
                    HoTen = tenHocVien,
                   
                };

                // 4. Xóa liên kết học viên khỏi lớp
                _context.HocVienLopHocs.Remove(hocVienLop);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 5. Ghi vết kiểm tra lịch sử (Audit Log)
                string nguoiThucHien = HttpContext.Session.GetString("HoTenNhanVien")
                                       ?? User.FindFirst(ClaimTypes.Name)?.Value
                                       ?? HttpContext.Session.GetString("TenDangNhap")
                                       ?? User.FindFirst("TenDangNhap")?.Value
                                       ?? "Quản lý cơ sở";

                await _auditLogService.GhiLichSuAsync(
                    hanhDong: "REMOVE_HOCVIEN_LOPHOC",
                    moTa: $"{nguoiThucHien} đã xóa học viên '{tenHocVien}' ra khỏi lớp '{tenLop}' (#{idLopHoc}).",
                    duLieuCu: duLieuCu,
                    duLieuMoi: null
                );

                // 6. Xóa và làm mới cache chi tiết lớp học
                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();
                _cache.Remove($"ChiTiet_LopHoc_{idLopHoc}");

                return Ok(new
                {
                    success = true,
                    message = $"Đã xóa học viên '{tenHocVien}' ra khỏi lớp học thành công!"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi xóa học viên #{IdUser} ra khỏi lớp #{IdLopHoc}", idUser, idLopHoc);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống khi xóa học viên: " + ex.Message
                });
            }
        }


    }
}