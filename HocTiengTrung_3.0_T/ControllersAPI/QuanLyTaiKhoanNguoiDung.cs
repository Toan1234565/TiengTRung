using Azure.Core;
using BCrypt.Net;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Google.Apis.Util;
using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12.QuanLyTaiKhoan;
using HocTiengTrung_3._0_T.Models12.ThanhTich;
using HocTiengTrung_3._0_T.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace HocTiengTrung_3._0_T.ControllersAPI
{
    [Route("api/quanlytaikhoannguoidung")]
    [ApiController]
    public class QuanLyTaiKhoanNguoiDung : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<QuanLyTaiKhoanNguoiDung> _logger;
        private readonly IEmailService _emailService;
        private readonly IAuditLogService _auditLogService;

        private readonly Cloudinary _cloudinary = new Cloudinary(new Account(
              "dewlwufwi",
              "613671295333629",
              "kWq7T2Ls8fOjRUJQIWKALDj9BFQ"
          ));
        public QuanLyTaiKhoanNguoiDung(TmdtContext context, IMemoryCache cache, ILogger<QuanLyTaiKhoanNguoiDung> logger, IEmailService emailService, IAuditLogService auditLogService)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _emailService = emailService;
            _auditLogService = auditLogService;
        }

        #region ĐĂNG KÝ TÀI KHOẢN
        [HttpPost("tao-tai-khoan")]
        public async Task<IActionResult> TaoTaiKhoan([FromBody] Models12.QuanLyTaiKhoan.RegisterRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.TenDangNhap) ||
                string.IsNullOrEmpty(request.MatKhau) || string.IsNullOrEmpty(request.HoTen))
            {
                return BadRequest(new { message = "Vui lòng nhập đầy đủ Tên đăng nhập, Mật khẩu và Họ tên." });
            }

            var existingUser = await _context.TaiKhoans.AnyAsync(u => u.TenDangNhap == request.TenDangNhap);
            if (existingUser)
            {
                return Conflict(new { message = "Tên đăng nhập đã tồn tại." });
            }

            // 1. Đọc và phân tích an toàn IdTrungTam từ Claims hoặc Session
            int parsedIdTrungTam = 0;
            var rawIdTrungTam = User.FindFirst("IdTrungTam")?.Value ?? HttpContext.Session?.GetString("IdTrungTam");

            if (!string.IsNullOrEmpty(rawIdTrungTam) && int.TryParse(rawIdTrungTam.Trim(), out var idParsed))
            {
                parsedIdTrungTam = idParsed;
            }

            // 2. KIỂM TRA TỒN TẠI: Đảm bảo IDTrungTam thực sự có trong bảng TrungTam (tránh lỗi khóa ngoại)
            int IDTrungTam = 0;
            if (parsedIdTrungTam != 0)
            {
                bool trungTamExists = await _context.TrungTams.AnyAsync(t => t.IdTrungTam == parsedIdTrungTam);
                IDTrungTam = trungTamExists ? parsedIdTrungTam : 0;
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var now = DateTime.Now;

                // 3. Tạo tài khoản mới
                var newTaiKhoan = new TaiKhoan
                {
                    TenDangNhap = request.TenDangNhap,
                    MatKhau = BCrypt.Net.BCrypt.HashPassword(request.MatKhau),
                    TrangThai = true,
                    NgayTao = now,
                    IdVaiTro = 1, // Vai trò Học viên
                    IsVip = true,
                    NgayBatDauVip = now,
                    NgayKetThucVip = now.AddDays(10),
                    IdTrungTam = IDTrungTam
                };

                _context.TaiKhoans.Add(newTaiKhoan);
                await _context.SaveChangesAsync();

                // 4. Tạo hồ sơ người dùng
                var hoSoMoi = new NguoiDung
                {
                    IdTaiKhoan = newTaiKhoan.IdTaiKhoan,
                    HoTen = request.HoTen,
                    AnhDaiDien = "default_avatar.png"
                };

                _context.NguoiDungs.Add(hoSoMoi);
                await _context.SaveChangesAsync(); // Lưu để sinh ra khóa chính IdUser

                // 5. Xử lý tự động đưa vào lớp học tự do nếu IDTrungTam != 0
                if (IDTrungTam != 0)
                {
                    var lopHocTuDo = await _context.LopHocs
                        .FirstOrDefaultAsync(l => l.IdTrungTam == IDTrungTam &&
                                                  l.TrangThai &&
                                                  (l.TenLopHoc.Contains("tự do") || l.MaLopHoc == "TU_DO"));

                    if (lopHocTuDo != null)
                    {
                        var hocVienLopHoc = new HocVienLopHoc
                        {
                            IdUser = hoSoMoi.IdUser,
                            IdLopHoc = lopHocTuDo.IdLopHoc,
                            NgayThamGia = now
                        };

                        _context.HocVienLopHocs.Add(hocVienLopHoc);
                        await _context.SaveChangesAsync();
                    }
                }

                // Hoàn tất transaction
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Đăng ký thành công! Bạn nhận được 10 ngày sử dụng VIP miễn phí.",
                    idTaiKhoan = newTaiKhoan.IdTaiKhoan,
                    ngayKetThucVip = newTaiKhoan.NgayKetThucVip
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Lỗi đăng ký: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi hệ thống khi tạo tài khoản." });
            }
        }
        #endregion

        [HttpGet("check-auth-info")]
        public IActionResult CheckAuthInfo()
        {
            // 1. Kiểm tra trạng thái xác thực cơ bản
            var isAuthenticated = User.Identity?.IsAuthenticated ?? false;

            // 2. Lấy danh sách Claims từ Cookie (Nơi lưu trữ chính sau khi đăng nhập)
            var claims = User.Claims.Select(c => new
            {
                Type = c.Type,
                // Rút gọn tên Type để dễ đọc (ví dụ: http://.../nameidentifier thành NameIdentifier)
                ShortType = c.Type.Split('/').Last(),
                Value = c.Value
            }).ToList();

            // 3. Lấy dữ liệu từ Session (Hỗ trợ đọc cả dạng String lẫn Int32 để tránh sót dữ liệu)
            var sessionData = new Dictionary<string, string>();
            if (HttpContext.Session != null)
            {
                foreach (var key in HttpContext.Session.Keys)
                {
                    // Thử đọc theo kiểu String trước, nếu null thì thử đọc theo kiểu Int32
                    string stringValue = HttpContext.Session.GetString(key);
                    if (stringValue == null)
                    {
                        int? intValue = HttpContext.Session.GetInt32(key);
                        stringValue = intValue.HasValue ? intValue.Value.ToString() : "n/a";
                    }
                    sessionData.Add(key, stringValue);
                }
            }

            // 4. Kiểm tra Cookie thô trong Request Header
            var rawCookies = Request.Cookies.Select(c => new {
                Name = c.Key,
                Value = c.Key.Contains("AspNetCore.Cookies") ? "[PROTECTED_TOKEN]" : c.Value
            }).ToList();

            // =========================================================================
            // TRÍCH XUẤT CÁC ID ĐỂ KIỂM TRA NHANH (BỔ SUNG RẠCH RÒI TỪNG BIẾN)
            // =========================================================================

            // A. Lấy từ Claims (Cookie)
            var idUserFromClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var idTaiKhoanFromClaim = User.FindFirst("IdTaiKhoan")?.Value;

            // B. Lấy từ Session (Thử cả 2 phương thức phòng trường hợp bạn đổi kiểu dữ liệu)
            var idUserFromSession = HttpContext.Session.GetString("MaNguoiDung")
                                   ?? HttpContext.Session.GetInt32("IdUser")?.ToString()
                                   ?? "Không tìm thấy";

            var idTaiKhoanFromSession = HttpContext.Session.GetString("IdTaiKhoan")
                                      ?? HttpContext.Session.GetInt32("IdTaiKhoan")?.ToString()
                                      ?? "Không tìm thấy";
            var idTrungTamFromSession = HttpContext.Session.GetString("IdTrungTam")
                                      ?? HttpContext.Session.GetInt32("IdTrungTam")?.ToString()
                                      ?? "Không tìm thấy";
            var maTrungTamFromSession = HttpContext.Session.GetString("MaTrungTam") ?? "Không tìm thấy";

            var tenVaiTroFromSession = HttpContext.Session.GetString("TenVaiTro") ?? "Không tìm thấy";

            // 5. Trả về kết quả phân tích tường minh
            return Ok(new
            {
                Message = isAuthenticated ? "Đã xác thực thành công" : "Chưa xác thực (Cookie có thể hết hạn hoặc chưa lưu)",
                IsAuthenticated = isAuthenticated,
                AuthenticationType = User.Identity?.AuthenticationType,

                // --- KHU VỰC THAM CHIẾU NHANH ID ---
                KiemTra_ID_NguoiDung = new
                {
                    IdUser_Trong_Claim = idUserFromClaim ?? "Chưa có",
                    IdUser_Trong_Session = idUserFromSession
                },
                KiemTra_ID_TaiKhoan = new
                {
                    IdTaiKhoan_Trong_Claim = idTaiKhoanFromClaim ?? "Chưa có",
                    IdTaiKhoan_Trong_Session = idTaiKhoanFromSession
                },
                // ----------------------------------

                ClaimsCount = claims.Count,
                CookieClaims = claims,
                SessionValues = sessionData,
                BrowserCookies = rawCookies
            });
        }

        #region ĐĂNG NHẬP HOÀN THIỆN
        [HttpPost("dang-nhap-tai-khoan")]
        public async Task<IActionResult> TaiKhoanDangNhap([FromBody] Models12.QuanLyTaiKhoan.LoginRequest request)
        {
            // 1. Kiểm tra đầu vào
            if (request == null || string.IsNullOrWhiteSpace(request.TenDangNhap) || string.IsNullOrWhiteSpace(request.MatKhau))
            {
                return BadRequest(new { success = false, message = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu." });
            }

            try
            {
                string tenDangNhap = request.TenDangNhap.Trim();
                string matKhau = request.MatKhau.Trim();

                // 2. Tìm tài khoản
                var taiKhoan = await _context.TaiKhoans
                    .Include(u => u.NguoiDung)
                    .Include(tt => tt.IdTrungTamNavigation)
                    .Include(cv => cv.IdVaiTroNavigation)
                    .FirstOrDefaultAsync(u => u.TenDangNhap == tenDangNhap);

                // 3. Kiểm tra mật khẩu
                if (taiKhoan == null || string.IsNullOrEmpty(taiKhoan.MatKhau) || !BCrypt.Net.BCrypt.Verify(matKhau, taiKhoan.MatKhau))
                {
                    return Unauthorized(new { success = false, message = "Tên đăng nhập hoặc mật khẩu không chính xác." });
                }

                // 4. Kiểm tra trạng thái tài khoản
                if (taiKhoan.TrangThai == false)
                {
                    return StatusCode(403, new { success = false, message = "Tài khoản của bạn hiện đang bị khóa." });
                }

                // 5. Xử lý hồ sơ NguoiDung
                int finalUserId = 0;
                int finalIdTaiKhoan = taiKhoan.IdTaiKhoan;
                string hoTenDisplay = "";

                if (taiKhoan.NguoiDung == null)
                {
                    try
                    {
                        var newProfile = new NguoiDung
                        {
                            IdTaiKhoan = taiKhoan.IdTaiKhoan,
                            HoTen = taiKhoan.TenDangNhap,
                            AnhDaiDien = "default_avatar.png"
                        };
                        _context.NguoiDungs.Add(newProfile);
                        await _context.SaveChangesAsync();

                        finalUserId = newProfile.IdUser;
                        hoTenDisplay = newProfile.HoTen;
                    }
                    catch
                    {
                        finalUserId = taiKhoan.IdTaiKhoan;
                        hoTenDisplay = taiKhoan.TenDangNhap;
                    }
                }
                else
                {
                    finalUserId = taiKhoan.NguoiDung.IdUser;
                    hoTenDisplay = !string.IsNullOrWhiteSpace(taiKhoan.NguoiDung.HoTen) ? taiKhoan.NguoiDung.HoTen : taiKhoan.TenDangNhap;
                }

                // 6. KHAI BÁO CÁC GIÁ TRỊ AN TOÀN TUYỆT ĐỐI CHO TÀI KHOẢN KHÔNG CÓ TRUNG TÂM
                string idTrungTamStr = (taiKhoan.IdTrungTam ?? 0).ToString();
                string maTrungTamStr = taiKhoan.IdTrungTamNavigation?.MaTrungTam ?? "0"; // ✅ ĐÃ CÓ ? TRÁNH CRASH
                string tenTrungTamStr = taiKhoan.IdTrungTamNavigation?.TenTrungTam ?? "Hệ thống Tiếng Trung";
                string idVaiTroStr = (taiKhoan.IdVaiTro ?? 1).ToString();
                string tenVaiTroStr = taiKhoan.IdVaiTroNavigation?.TenVaiTro ?? "Người dùng";

                // 7. THIẾT LẬP CLAIMS
               

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, finalUserId.ToString()),
                    new Claim(ClaimTypes.Name, hoTenDisplay),
                    new Claim("TenDangNhap", taiKhoan.TenDangNhap),
                    new Claim("IdTaiKhoan", finalIdTaiKhoan.ToString()),
                    new Claim("IdTrungTam", idTrungTamStr),
                    new Claim("MaTrungTam", maTrungTamStr),
                    new Claim("TenTrungTam", tenTrungTamStr),

                    // BẮT BUỘC: Đưa cả Tên quyền và ID quyền vào ClaimTypes.Role chuẩn
                    new Claim(ClaimTypes.Role, tenVaiTroStr),
                    new Claim(ClaimTypes.Role, idVaiTroStr),

                    // Các claim phụ nếu cần
                    new Claim("TenVaiTro", tenVaiTroStr)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties
                );

                // 8. THIẾT LẬP SESSION
                if (HttpContext.Session != null)
                {
                    HttpContext.Session.SetString("MaNguoiDung", finalUserId.ToString());
                    HttpContext.Session.SetString("HoTenNhanVien", hoTenDisplay);
                    HttpContext.Session.SetString("TenDangNhap", taiKhoan.TenDangNhap);
                    HttpContext.Session.SetString("IdTaiKhoan", finalIdTaiKhoan.ToString());
                    HttpContext.Session.SetString("IdTrungTam", idTrungTamStr);
                    HttpContext.Session.SetString("MaTrungTam", maTrungTamStr);
                    HttpContext.Session.SetString("TenTrungTam", tenTrungTamStr);
                    HttpContext.Session.SetString("TenVaiTro", tenVaiTroStr);
                }

                // 9. TRẢ VỀ DỮ LIỆU
                return Ok(new
                {
                    success = true,
                    message = "Đăng nhập thành công!",
                    data = new
                    {
                        idUser = finalUserId,
                        hoTen = hoTenDisplay,
                        tenDangNhap = taiKhoan.TenDangNhap,
                        idTaiKhoan = finalIdTaiKhoan,
                        idVaiTro = taiKhoan.IdVaiTro ?? 1,
                        idTrungTam = taiKhoan.IdTrungTam ?? 0,
                        maTrungTam = maTrungTamStr,
                        tenTrungTam = tenTrungTamStr,
                        tenVaiTro = tenVaiTroStr
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }
        #endregion

        [HttpGet("thong-tin-ho-so")]
        public async Task<IActionResult> GetHoSo()
        {
            try
            {
                // 1. Lấy ID từ Session hoặc Claims
                var userIdStr = HttpContext.Session.GetString("MaNguoiDung")
                                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userIdStr) || userIdStr == "0")
                {
                    _logger.LogWarning("Truy cập hồ sơ thất bại: ID không hợp lệ hoặc chưa đăng nhập.");
                    return Unauthorized(new { message = "Vui lòng đăng nhập lại." });
                }

                int userId = int.Parse(userIdStr);

                // 2. Truy vấn Database
                var nguoiDung = await _context.NguoiDungs
                    .Include(tk => tk.IdTaiKhoanNavigation)
                    .Include(tt => tt.IdTaiKhoanNavigation.IdTrungTamNavigation)
                    .Include(n => n.TuVungNguoiDungs)
                        .ThenInclude(tv => tv.IdTuVungNavigation)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.IdUser == userId);

                if (nguoiDung == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy hồ sơ người dùng." });
                }

                // 3. Giải mã an toàn (có fallback nếu chuỗi không phải ciphertext)
                string emailGiaiMa = nguoiDung.Email ?? "";
                if (!string.IsNullOrEmpty(nguoiDung.Email))
                {
                    try { emailGiaiMa = SecurityHelper.Decrypt(nguoiDung.Email); }
                    catch { emailGiaiMa = nguoiDung.Email; }
                }

                string sdtGiaiMa = nguoiDung.SoDienThoai ?? "";
                if (!string.IsNullOrEmpty(nguoiDung.SoDienThoai))
                {
                    try { sdtGiaiMa = SecurityHelper.Decrypt(nguoiDung.SoDienThoai); }
                    catch { sdtGiaiMa = nguoiDung.SoDienThoai; }
                }

                // 4. Map dữ liệu
                var profile = new NguoiDungModels
                {
                    IdUser = nguoiDung.IdUser,
                    HoTen = nguoiDung.HoTen ?? "Chưa đặt tên",
                    Email = emailGiaiMa,
                    SoDienThoai = sdtGiaiMa,
                    NgaySinh = nguoiDung.NgaySinh,
                    AnhDaiDien = nguoiDung.AnhDaiDien ?? "default_avatar.png",
                    TenTrungTam = nguoiDung.IdTaiKhoanNavigation.IdTrungTamNavigation?.TenTrungTam,
                    DanhSachTuVungs = (nguoiDung.TuVungNguoiDungs ?? new List<TuVungNguoiDung>())
                        .Select(tv => new TuVungNguoiDungModels
                        {
                            IdTuVung = tv.IdTuVung,
                            TuVung = tv.IdTuVungNavigation?.ChuHan ?? "",
                            DaThuoc = tv.DaThuoc,
                            NgayDanhDau = tv.NgayDanhDau
                        }).ToList()
                };

                return Ok(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống tại GetHoSo");
                return StatusCode(500, new { message = "Lỗi xử lý hồ sơ: " + ex.Message });
            }
        }

        #region ĐĂNG XUẤT
        [HttpPost("dang-xuat")]
        public async Task<IActionResult> DangXuat()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return Ok(new { message = "Đã đăng xuất" });
        }
        #endregion

        #region THỐNG KÊ TRUY CẬP



        // 1. API Ghi nhận lượt truy cập (Frontend gọi khi load trang)
        [HttpPost("ghi-nhan-truy-cap")]
        public async Task<IActionResult> GhiNhanTruyCap()
        {
            try
            {
                // Lấy ID người dùng từ Session hoặc Cookie nếu có
                var userIdStr = HttpContext.Session.GetString("MaNguoiDung") ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int? userId = string.IsNullOrEmpty(userIdStr) ? null : int.Parse(userIdStr);

                var history = new LichSuTruyCap
                {
                    IdTaiKhoan = userId,
                    ThoiGian = DateTime.Now,
                    DiaChiIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    ThietBi = Request.Headers["User-Agent"].ToString()
                };

                _context.LichSuTruyCaps.Add(history);
                await _context.SaveChangesAsync();

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi ghi log truy cập: {ex.Message}");
                return StatusCode(500);
            }
        }

        // 2. API Lấy dữ liệu cho biểu đồ (Có bộ lọc ngày)
        [HttpGet("thong-ke-truy-cap")]
        public async Task<IActionResult> GetAccessStats([FromQuery] DateTime? tuNgay, [FromQuery] DateTime? denNgay)
        {
            try
            {
                DateTime start = tuNgay ?? DateTime.Now.AddDays(-7);
                DateTime end = denNgay ?? DateTime.Now;
                end = end.Date.AddDays(1).AddTicks(-1);

                // BƯỚC 1: Truy vấn Database (Chỉ lấy dữ liệu số và ngày thô)
                var rawData = await _context.LichSuTruyCaps
                    .Where(x => x.ThoiGian >= start && x.ThoiGian <= end)
                    .GroupBy(x => x.ThoiGian.Value.Date)
                    .Select(g => new
                    {
                        NgayTho = g.Key, // Giữ nguyên kiểu DateTime
                        SoLuotTruyCap = g.Count(),
                        SoNguoiDungDuyNhat = g.Select(x => x.IdTaiKhoan).Distinct().Count(),
                        SoIpDuyNhat = g.Select(x => x.DiaChiIp).Distinct().Count()
                    })
                    .OrderBy(x => x.NgayTho)
                    .ToListAsync(); // Thực thi và đưa dữ liệu về RAM

                // BƯỚC 2: Định dạng lại dữ liệu trên RAM (Sử dụng được .ToString)
                var formattedData = rawData.Select(x => new
                {
                    Ngay = x.NgayTho.ToString("yyyy-MM-dd"), // Bây giờ ToString sẽ chạy ok
                    x.SoLuotTruyCap,
                    x.SoNguoiDungDuyNhat,
                    x.SoIpDuyNhat
                }).ToList();

                return Ok(new
                {
                    success = true,
                    data = formattedData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi lấy thống kê: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi xử lý dữ liệu biểu đồ." });
            }
        }

        #endregion
        #region ĐỔI MẬT KHẨU (USER)
        [HttpPost("doi-mat-khau")]
        [Authorize] // Bắt buộc người dùng phải đăng nhập mới được gọi API này
        public async Task<IActionResult> DoiMatKhau([FromBody] DoiMatKhauRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.MatKhauCu) || string.IsNullOrEmpty(request.MatKhauMoi))
            {
                return BadRequest(new { success = false, message = "Vui lòng nhập đầy đủ mật khẩu cũ và mới." });
            }

            try
            {
                // 1. Lấy IdTaiKhoan của người dùng đang đăng nhập từ Claim (Cookie/Token)
                var idTaiKhoanClaim = User.FindFirst("IdTaiKhoan")?.Value;
                if (string.IsNullOrEmpty(idTaiKhoanClaim) || !int.TryParse(idTaiKhoanClaim, out int idTaiKhoan))
                {
                    return Unauthorized(new { success = false, message = "Không xác định được danh tính người dùng." });
                }

                // 2. Tìm tài khoản trong Database
                var taiKhoan = await _context.TaiKhoans.FindAsync(idTaiKhoan);
                if (taiKhoan == null)
                {
                    return NotFound(new { success = false, message = "Tài khoản không tồn tại." });
                }

                // 3. Kiểm tra mật khẩu cũ có khớp không (Dùng BCrypt.Verify)
                if (!BCrypt.Net.BCrypt.Verify(request.MatKhauCu, taiKhoan.MatKhau))
                {
                    return BadRequest(new { success = false, message = "Mật khẩu cũ không chính xác." });
                }

                // 4. Băm mật khẩu mới và lưu vào Database
                taiKhoan.MatKhau = BCrypt.Net.BCrypt.HashPassword(request.MatKhauMoi);

                _context.TaiKhoans.Update(taiKhoan);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Đổi mật khẩu thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi đổi mật khẩu: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi đổi mật khẩu." });
            }
        }
        #endregion

        [HttpPost("quen-mat-khau")]
        public async Task<IActionResult> QuenMatKhau([FromBody] Models12.QuanLyTaiKhoan.ForgotPasswordRequest request)
        {
            if (!ModelState.IsValid || request == null)
            {
                return BadRequest(new { message = "Dữ liệu yêu cầu không hợp lệ." });
            }

            try
            {
                // 1. Tìm tài khoản dựa trên Tên đăng nhập
                var taiKhoan = await _context.TaiKhoans
                    .Include(t => t.NguoiDung)
                    .FirstOrDefaultAsync(t => t.TenDangNhap == request.TenDangNhap);

                // Kiểm tra xem tài khoản có tồn tại không
                if (taiKhoan == null)
                {
                    return BadRequest(new { message = "Tên đăng nhập không tồn tại trong hệ thống." });
                }

                // Kiểm tra xem người dùng đã cập nhật Email chưa
                if (taiKhoan.NguoiDung == null || string.IsNullOrWhiteSpace(taiKhoan.NguoiDung.Email))
                {
                    return BadRequest(new
                    {
                        message = "Tài khoản của bạn chưa cập nhật Email. Vui lòng vào phần chỉnh sửa hồ sơ cá nhân để bổ sung Email trước khi sử dụng tính năng này."
                    });
                }

                // 2. Giải mã Email từ DB để so sánh với Email người dùng nhập vào
                string emailDaGiaiMa = string.Empty;
                try
                {
                    emailDaGiaiMa = SecurityHelper.Decrypt(taiKhoan.NguoiDung.Email);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Không thể giải mã email cho tài khoản {TenDangNhap}. Thử so sánh trực tiếp dạng plain-text.", request.TenDangNhap);
                    // Fallback: Nếu không giải mã được, giả định email trong DB có thể đang ở dạng plain-text
                    emailDaGiaiMa = taiKhoan.NguoiDung.Email;
                }

                // Kiểm tra Email nhập vào có khớp với Email trong DB không
                if (!string.Equals(emailDaGiaiMa.Trim(), request.Email.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = "Email xác nhận không chính xác." });
                }

                // 3. Sinh mật khẩu mới ngẫu nhiên (8 ký tự)
                string matKhauMoi = Guid.NewGuid().ToString("N").Substring(0, 8);
                string matKhauHash = BCrypt.Net.BCrypt.HashPassword(matKhauMoi);

                // 4. Gửi email trước khi cập nhật DB
                try
                {
                    await _emailService.SendForgotPasswordEmailAsync(
                        request.Email,
                        taiKhoan.NguoiDung.HoTen ?? "Người dùng",
                        taiKhoan.TenDangNhap,
                        matKhauMoi
                    );
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Lỗi khi gửi email lấy lại mật khẩu cho tài khoản: {TenDangNhap}", request.TenDangNhap);
                    return StatusCode(500, new { message = "Hệ thống gặp lỗi khi gửi email xác nhận. Mật khẩu của bạn chưa bị thay đổi, vui lòng thử lại sau hoặc liên hệ Admin." });
                }

                // 5. Gửi email thành công mới tiến hành cập nhật mật khẩu mới vào DB
                taiKhoan.MatKhau = matKhauHash;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Mật khẩu mới đã được gửi vào email của bạn thành công." });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Lỗi hệ thống không xác định tại API QuenMatKhau");
                return StatusCode(500, new { message = "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau." });
            }
        }

        #region CẤP LẠI MẬT KHẨU (ADMIN)
        [HttpPost("admin/cap-lai-mat-khau")]
        // [Authorize(Roles = "1")] // Mở comment nếu cần phân quyền
        public async Task<IActionResult> AdminCapLaiMatKhau([FromBody] CapLaiMatKhauRequest request)
        {
            if (request == null || request.IdTaiKhoan <= 0 || string.IsNullOrWhiteSpace(request.MatKhauMoi))
            {
                return BadRequest(new { success = false, message = "Vui lòng cung cấp ID tài khoản và mật khẩu mới hợp lệ." });
            }

            try
            {
                // 1. Lấy TÊN NGƯỜI THỰC HIỆN (Người đang đăng nhập cấp lại mật khẩu)
                string nguoiThucHien = HttpContext.Session.GetString("HoTenNhanVien")
                                       ?? User.FindFirst(ClaimTypes.Name)?.Value
                                       ?? HttpContext.Session.GetString("TenDangNhap")
                                       ?? User.FindFirst("TenDangNhap")?.Value
                                       ?? "Quản trị viên";

                // 2. Tìm tài khoản cần reset mật khẩu kèm theo thông tin Người dùng (để lấy Họ tên tài khoản A)
                var taiKhoan = await _context.TaiKhoans
                    .Include(tk => tk.NguoiDung)
                    .FirstOrDefaultAsync(tk => tk.IdTaiKhoan == request.IdTaiKhoan);

                if (taiKhoan == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy tài khoản này trong hệ thống." });
                }

                // Tên của tài khoản nhận mật khẩu mới (Ưu tiên Họ tên, nếu chưa có thì dùng Tên đăng nhập)
                string tenNguoiNhan = taiKhoan.NguoiDung?.HoTen ?? taiKhoan.TenDangNhap;

                // 3. Băm mật khẩu mới và cập nhật
                taiKhoan.MatKhau = BCrypt.Net.BCrypt.HashPassword(request.MatKhauMoi);
                _context.TaiKhoans.Update(taiKhoan);
                await _context.SaveChangesAsync();

                // 4. Ghi vết truy vết vào Lịch sử thay đổi (Audit Log)
                string moTaLog = $"{nguoiThucHien} đã cấp lại mật khẩu cho {tenNguoiNhan} (Tài khoản: {taiKhoan.TenDangNhap})";

                await _auditLogService.GhiLichSuAsync(
                    hanhDong: "RESET_PASSWORD",
                    moTa: moTaLog,
                    duLieuCu: null,
                    duLieuMoi: new { TaiKhoanDuocReset = taiKhoan.TenDangNhap, NguoiThucHien = nguoiThucHien }
                );

                // 5. Trả về thông báo chỉ hiển thị tên người dùng
                return Ok(new
                {
                    success = true,
                    nguoiThucHien = nguoiThucHien,
                    nguoiNhan = tenNguoiNhan,
                    message = $"{nguoiThucHien} đã cấp lại mật khẩu thành công cho {tenNguoiNhan}!"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi Admin cấp lại mật khẩu cho ID {request.IdTaiKhoan}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi thực hiện cấp lại mật khẩu." });
            }
        }
        #endregion
        [Authorize]
        [HttpPost("toggle-khoa-tai-khoan/{idTaiKhoan}")]
        public async Task<IActionResult> ToggleKhoaTaiKhoan([FromRoute] int idTaiKhoan)
        {
            if (idTaiKhoan <= 0)
            {
                return BadRequest(new { success = false, message = "ID tài khoản không hợp lệ." });
            }

            try
            {
                // 1. KIỂM TRA QUYỀN HẠN CỦA NGƯỜI ĐANG THAO TÁC
                string? tenVaiTroHienTai = User.FindFirst("TenVaiTro")?.Value
                                           ?? HttpContext.Session.GetString("TenVaiTro");
                string vaiTroClean = (tenVaiTroHienTai ?? "").Trim().ToLower();

                bool isQuanLyTrungTam = vaiTroClean.Contains("quản lý") || vaiTroClean.Contains("quan ly") || vaiTroClean.Contains("admin");
                if (!isQuanLyTrungTam)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        success = false,
                        message = "Bạn không có quyền thực hiện thao tác này. Chức năng chỉ dành cho Quản lý / Quản trị viên!"
                    });
                }

                // Lấy tên người thực hiện thao tác
                string nguoiThucHien = HttpContext.Session.GetString("HoTenNhanVien")
                                       ?? User.FindFirst(ClaimTypes.Name)?.Value
                                       ?? HttpContext.Session.GetString("TenDangNhap")
                                       ?? User.FindFirst("TenDangNhap")?.Value
                                       ?? "Quản lý cơ sở";

                // 2. TỰ BẢO VỆ: Không cho phép tự khóa chính tài khoản đang đăng nhập
                string? currentIdTaiKhoanStr = User.FindFirst("IdTaiKhoan")?.Value
                                               ?? HttpContext.Session.GetString("IdTaiKhoan");
                if (int.TryParse(currentIdTaiKhoanStr, out int currentIdTaiKhoan) && currentIdTaiKhoan == idTaiKhoan)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Bạn không thể tự khóa tài khoản của chính mình!"
                    });
                }

                // 3. TÌM TÀI KHOẢN CẦN KHÓA / MỞ KÈM THÔNG TIN HỌ TÊN NGƯỜI DÙNG
                var taiKhoan = await _context.TaiKhoans
                    .Include(t => t.IdVaiTroNavigation)
                    .Include(t => t.NguoiDung)
                    .FirstOrDefaultAsync(t => t.IdTaiKhoan == idTaiKhoan);

                if (taiKhoan == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy tài khoản cần thay đổi trạng thái." });
                }

                // Tên hiển thị của người bị khóa/mở
                string tenNguoiNhan = taiKhoan.NguoiDung?.HoTen ?? taiKhoan.TenDangNhap;

                // 4. BẢO MẬT TRUNG TÂM (Multi-tenancy): Quản lý cơ sở nào chỉ thao tác trong cơ sở đó
                string? claimIdTrungTamStr = User.FindFirst("IdTrungTam")?.Value
                                             ?? HttpContext.Session.GetString("IdTrungTam");
                if (int.TryParse(claimIdTrungTamStr, out int currentIdTrungTam) && currentIdTrungTam > 0)
                {
                    if (taiKhoan.IdTrungTam != currentIdTrungTam)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new
                        {
                            success = false,
                            message = "Bạn không có quyền khóa/mở tài khoản thuộc cơ sở khác!"
                        });
                    }
                }

                // 5. THỰC HIỆN ĐỔI TRẠNG THÁI (Nếu đang true -> thành false, và ngược lại)
                bool trangThaiCu = taiKhoan.TrangThai ?? true;
                bool trangThaiMoi = !trangThaiCu;
                taiKhoan.TrangThai = trangThaiMoi;

                _context.TaiKhoans.Update(taiKhoan);
                await _context.SaveChangesAsync();

                // 6. GHI NHẬT KÝ TRUY VẾT (AUDIT LOG) - CHỈ LƯU TRƯỜNG THAY ĐỔI
                string hanhDongLog = trangThaiMoi ? "UNLOCK_ACCOUNT" : "LOCK_ACCOUNT";
                string moTaLog = $"{nguoiThucHien} đã {(trangThaiMoi ? "mở khóa" : "khóa")} tài khoản của {tenNguoiNhan} (Tên đăng nhập: {taiKhoan.TenDangNhap})";

                await _auditLogService.GhiLichSuAsync(
                    hanhDong: hanhDongLog,
                    moTa: moTaLog,
                    duLieuCu: new { TrangThai = trangThaiCu ? "Hoạt động" : "Đã khóa" },
                    duLieuMoi: new { TrangThai = trangThaiMoi ? "Hoạt động" : "Đã khóa" }
                );

                // 7. XÓA BỘ NHỚ ĐỆM (CACHE)
                _cache.Remove("danhSachNguoiDung_vip_all");
                _cache.Remove("danhSachNguoiDung_vip_true");
                _cache.Remove("danhSachNguoiDung_vip_false");

                string thongBaoHanhDong = trangThaiMoi ? "Mở khóa tài khoản" : "Khóa tài khoản";

                return Ok(new
                {
                    success = true,
                    trangThai = trangThaiMoi,
                    nguoiThucHien = nguoiThucHien,
                    nguoiNhan = tenNguoiNhan,
                    message = $"{nguoiThucHien} đã {thongBaoHanhDong.ToLower()} thành công cho {tenNguoiNhan}!",
                    data = new
                    {
                        idTaiKhoan = taiKhoan.IdTaiKhoan,
                        tenDangNhap = taiKhoan.TenDangNhap,
                        trangThai = trangThaiMoi
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thực hiện thay đổi trạng thái tài khoản ID: {IdTaiKhoan}", idTaiKhoan);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống: " + ex.Message
                });
            }
        }
        #region CẬP NHẬT THÔNG TIN CÁ NHÂN
        [HttpPost("cap-nhat-thong-tin")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CapNhatThongTin(
    [FromForm] UpdateProfileRequest request,
    [FromForm] IFormFile? fileAnhDaiDien)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            try
            {
                // 1. Lấy IdUser từ Claims (ClaimTypes.NameIdentifier đã được lưu khi đăng nhập)
                var idUserClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(idUserClaim) || !int.TryParse(idUserClaim, out int idUser))
                {
                    return Unauthorized(new { success = false, message = "Không xác định được người dùng." });
                }

                // 2. Tìm hồ sơ người dùng trong Database
                var nguoiDung = await _context.NguoiDungs.FirstOrDefaultAsync(n => n.IdUser == idUser);
                if (nguoiDung == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy hồ sơ người dùng." });
                }

                // 3. Cập nhật thông tin cơ bản
                if (!string.IsNullOrWhiteSpace(request.HoTen))
                {
                    nguoiDung.HoTen = request.HoTen.Trim();
                }

                // Mã hóa dữ liệu trước khi lưu (chỉ mã hóa khi có nội dung để tránh chuỗi vô nghĩa)
                nguoiDung.Email = !string.IsNullOrWhiteSpace(request.Email)
                    ? SecurityHelper.Encrypt(request.Email.Trim())
                    : null;

                nguoiDung.SoDienThoai = !string.IsNullOrWhiteSpace(request.SoDienThoai)
                    ? SecurityHelper.Encrypt(request.SoDienThoai.Trim())
                    : null;

                nguoiDung.NgaySinh = request.NgaySinh;

                // 4. Xử lý Upload file ảnh đại diện lên Cloudinary (nếu có chọn file)
                if (fileAnhDaiDien != null && fileAnhDaiDien.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                    var extension = Path.GetExtension(fileAnhDaiDien.FileName).ToLower();

                    if (!allowedExtensions.Contains(extension))
                    {
                        return BadRequest(new { success = false, message = "Chỉ chấp nhận các định dạng ảnh: .jpg, .jpeg, .png, .webp" });
                    }

                    if (fileAnhDaiDien.Length > 5 * 1024 * 1024)
                    {
                        return BadRequest(new { success = false, message = "Dung lượng ảnh đại diện không được vượt quá 5MB!" });
                    }

                    using var stream = fileAnhDaiDien.OpenReadStream();
                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(fileAnhDaiDien.FileName, stream),
                        Folder = "UserAvatars",
                        PublicId = $"avatar_user_{idUser}_{Guid.NewGuid():N}",
                        Overwrite = true
                    };

                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                    if (uploadResult?.SecureUrl != null)
                    {
                        nguoiDung.AnhDaiDien = uploadResult.SecureUrl.ToString();
                    }
                }
                else if (!string.IsNullOrWhiteSpace(request.AnhDaiDien))
                {
                    // Trường hợp người dùng nhập link ảnh trực tiếp
                    nguoiDung.AnhDaiDien = request.AnhDaiDien.Trim();
                }

                // 5. Lưu vào Database
                _context.NguoiDungs.Update(nguoiDung);
                await _context.SaveChangesAsync();

                // Cập nhật lại Session để hiển thị ngay trên Header mà không cần đăng nhập lại
                HttpContext.Session?.SetString("HoTenNhanVien", nguoiDung.HoTen);

                return Ok(new
                {
                    success = true,
                    message = "Cập nhật thông tin cá nhân thành công!",
                    data = new
                    {
                        hoTen = nguoiDung.HoTen,
                        email = request.Email,
                        soDienThoai = request.SoDienThoai,
                        ngaySinh = nguoiDung.NgaySinh,
                        anhDaiDien = nguoiDung.AnhDaiDien
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi cập nhật thông tin người dùng ID: {IdUser}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống khi cập nhật: " + ex.Message
                });
            }
        }
        #endregion

        [HttpGet("danh-sach-tai-khoan")]
        public async Task<IActionResult> LayDanhSachNguoiDung(
             [FromQuery] bool? isVip,
             [FromQuery] string? searchTerm,
             [FromQuery] int page = 1) // Thêm tham số trang
        {
            // 1. Logic Cache (Giữ nguyên khóa cache)
            string vipStatus = isVip.HasValue ? isVip.Value.ToString().ToLower() : "all";
            string cacheKey = $"danhSachNguoiDung_vip_{vipStatus}";

            if (!_cache.TryGetValue(cacheKey, out List<NguoiDungModels> danhSach))
            {
                _logger.LogInformation("Đang tải dữ liệu gốc từ Database cho khóa: {Key}", cacheKey);

                var query = _context.NguoiDungs
                    .Include(n => n.IdTaiKhoanNavigation)
                    .AsNoTracking();

                if (isVip.HasValue)
                {
                    query = query.Where(n => n.IdTaiKhoanNavigation != null
                                          && n.IdTaiKhoanNavigation.IsVip == isVip.Value);
                }

                danhSach = await query.Select(n => new NguoiDungModels
                {
                    IdUser = n.IdUser,
                    HoTen = n.HoTen,
                    Email = n.Email,
                    SoDienThoai = n.SoDienThoai,
                    IdTaiKhoanNavigation = n.IdTaiKhoanNavigation != null ? new TaiKhoanModels
                    {
                        IdTaiKhoan = n.IdTaiKhoanNavigation.IdTaiKhoan,
                        TenDangNhap = n.IdTaiKhoanNavigation.TenDangNhap,
                        MatKhau = n.IdTaiKhoanNavigation.MatKhau,
                        NgayTao = n.IdTaiKhoanNavigation.NgayTao,
                        IsVip = n.IdTaiKhoanNavigation.IsVip
                    } : null
                }).ToListAsync();

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2));

                _cache.Set(cacheKey, danhSach, cacheOptions);
            }

            // 2. Logic Lọc dữ liệu (In-Memory)
            var resultList = danhSach.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var keyword = searchTerm.Trim().ToLower();
                resultList = resultList.Where(n =>
                    (n.HoTen != null && n.HoTen.ToLower().Contains(keyword)) ||
                    (n.Email != null && n.Email.ToLower().Contains(keyword)) ||
                    (n.SoDienThoai != null && n.SoDienThoai.Contains(keyword)) ||
                    (n.IdTaiKhoanNavigation != null && n.IdTaiKhoanNavigation.TenDangNhap.ToLower().Contains(keyword))
                );
            }

            // 3. Logic Phân trang
            int pageSize = 20;
            int totalItems = resultList.Count();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Đảm bảo page không vượt quá giới hạn
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var pagedItems = resultList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // 4. Trả về đối tượng đồng nhất
            return Ok(new
            {
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = page,
                PageSize = pageSize,
                Items = pagedItems
            });
        }

        [HttpGet("chi-tiet-tai-khoan/{id}")]
        public async Task<IActionResult> GetChiTietNguoiDung(int id)
        {
            if (id <= 0) return BadRequest("ID người dùng không hợp lệ.");

            var nguoiDung = await _context.NguoiDungs
                .Include(n => n.IdTaiKhoanNavigation)
                    .ThenInclude(tk => tk.GiaoDiches)
                        .ThenInclude(gd => gd.IdGoiNavigation)
                .Include(n => n.IdTaiKhoanNavigation)
                    .ThenInclude(tk => tk.IdVaiTroNavigation)   // <-- BẮT BUỘC CÓ DÒNG NÀY ĐỂ LẤY TenVaiTro
                .Include(n => n.IdTaiKhoanNavigation)
                    .ThenInclude(tk => tk.IdTrungTamNavigation) // <-- BỔ SUNG ĐỂ LẤY TÊN TRUNG TÂM
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.IdTaiKhoan == id);

            if (nguoiDung == null)
            {
                _logger.LogWarning("Không tìm thấy người dùng với ID: {id}", id);
                return NotFound(new { message = $"Không tìm thấy người dùng với ID: {id}" });
            }

            // Ánh xạ sang NguoiDungModels
            var result = new NguoiDungModels
            {
                IdUser = nguoiDung.IdUser,
                HoTen = nguoiDung.HoTen,
                Email = nguoiDung.Email,
                SoDienThoai = nguoiDung.SoDienThoai,
                NgaySinh = nguoiDung.NgaySinh,
                AnhDaiDien = nguoiDung.AnhDaiDien,
                IdTaiKhoanNavigation = nguoiDung.IdTaiKhoanNavigation != null ? new TaiKhoanModels
                {
                    IdTaiKhoan = nguoiDung.IdTaiKhoanNavigation.IdTaiKhoan,
                    TenDangNhap = nguoiDung.IdTaiKhoanNavigation.TenDangNhap,
                    NgayTao = nguoiDung.IdTaiKhoanNavigation.NgayTao,
                    IsVip = nguoiDung.IdTaiKhoanNavigation.IsVip,
                    NgayBatDauVip = nguoiDung.IdTaiKhoanNavigation.NgayBatDauVip,
                    NgayKetThucVip = nguoiDung.IdTaiKhoanNavigation.NgayKetThucVip,
                    TrangThai = nguoiDung.IdTaiKhoanNavigation.TrangThai,
                    MaVaiTro = nguoiDung.IdTaiKhoanNavigation.IdVaiTroNavigation.IdVaiTro, 
                    TenVaiTro = nguoiDung.IdTaiKhoanNavigation.IdVaiTroNavigation?.TenVaiTro,
                    TenTrungTam = nguoiDung.IdTaiKhoanNavigation.IdTrungTamNavigation?.TenTrungTam,
                  
                   
                } : null
            };

            return Ok(result);
        }

        [HttpPost("kich-hoat-vip-hoang-de/{idTaiKhoan}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KichHoatVipHoangDe(int idTaiKhoan)
        {
            if (idTaiKhoan <= 0)
            {
                return BadRequest(new { success = false, message = "ID tài khoản không hợp lệ!" });
            }

            try
            {
                // 1. Lấy thông tin gói VIP duy nhất đang hoạt động
                var goiVip = await _context.BangGiaTaiKhoans
                    .FirstOrDefaultAsync(g => g.TrangThai == true);

                if (goiVip == null)
                {
                    return BadRequest(new { success = false, message = "Hiện tại không có gói VIP nào khả dụng!" });
                }

                // 2. Kiểm tra tài khoản
                var taiKhoan = await _context.TaiKhoans
                    .FirstOrDefaultAsync(t => t.IdTaiKhoan == idTaiKhoan);

                if (taiKhoan == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy tài khoản!" });
                }

                // 3. Thiết lập thời gian VIP (Có logic cộng dồn)
                var homNay = DateTime.Now;
                taiKhoan.IsVip = true;

                // Kiểm tra xem tài khoản có đang VIP và thời hạn cũ còn hay không
                if (taiKhoan.NgayKetThucVip.HasValue && taiKhoan.NgayKetThucVip.Value > homNay)
                {
                    // Nếu còn hạn -> Cộng dồn từ ngày kết thúc cũ
                    taiKhoan.NgayKetThucVip = taiKhoan.NgayKetThucVip.Value.AddMonths(goiVip.SoThang);
                }
                else
                {
                    // Nếu đã hết hạn hoặc chưa từng có VIP -> Kích hoạt mới từ hôm nay
                    taiKhoan.NgayBatDauVip = homNay;
                    taiKhoan.NgayKetThucVip = homNay.AddMonths(goiVip.SoThang);
                }

                // 4. Tạo lịch sử giao dịch (Sử dụng ID_Goi lấy được từ database)
                var giaoDichHoangDe = new HocTiengTrung_3._0_T.Models.GiaoDich
                {
                    IdTaiKhoan = idTaiKhoan,
                    IdGoi = goiVip.IdGoi, // Tự động gắn ID gói từ bảng BangGiaTaiKhoan
                    NgayGiaoDich = homNay,
                    NoiDung = $"Kích hoạt/Gia hạn: {goiVip.TenGoi}", // Sửa nội dung một chút để bao quát cả việc gia hạn
                    TrangThai = "Thành công",
                    MaGiaoDich = "HD-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper()
                };

                _context.GiaoDiches.Add(giaoDichHoangDe);
                await _context.SaveChangesAsync();

                // 5. Xóa Cache
                _cache.Remove("danhSachNguoiDung_vip_all");
                _cache.Remove("danhSachNguoiDung_vip_true");
                _cache.Remove("danhSachNguoiDung_vip_false");

                return Ok(new
                {
                    success = true,
                    message = $"Kích hoạt/Gia hạn {goiVip.TenGoi} thành công!",
                    ngayKetThuc = taiKhoan.NgayKetThucVip?.ToString("dd/MM/yyyy")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kích hoạt VIP cho ID {IdTaiKhoan}", idTaiKhoan);
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống." });
            }
        }

        [HttpPost("thu-hoi-vip-hoang-de/{idTaiKhoan}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThuHoiVipHoangDe(int idTaiKhoan)
        {
            if (idTaiKhoan <= 0)
            {
                return BadRequest(new { success = false, message = "ID tài khoản không hợp lệ!" });
            }

            try
            {
                // 1. Kiểm tra tài khoản
                var taiKhoan = await _context.TaiKhoans
                    .FirstOrDefaultAsync(t => t.IdTaiKhoan == idTaiKhoan);

                if (taiKhoan == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy tài khoản!" });
                }

                // 2. Kiểm tra xem tài khoản có đang là VIP hay không
                if (taiKhoan.IsVip == false || (taiKhoan.NgayKetThucVip.HasValue && taiKhoan.NgayKetThucVip.Value < DateTime.Now))
                {
                    return BadRequest(new { success = false, message = "Tài khoản này hiện không có gói VIP hoặc đã hết hạn!" });
                }

                var homNay = DateTime.Now;

                // 3. Thực hiện thu hồi
                taiKhoan.IsVip = false;
                // Đặt ngày kết thúc về thời điểm hiện tại để lưu lại mốc thời gian bị thu hồi (hoặc bạn có thể gán = null)
                taiKhoan.NgayKetThucVip = homNay;

                // 4. Tạo lịch sử giao dịch ghi nhận việc thu hồi
                var giaoDichThuHoi = new HocTiengTrung_3._0_T.Models.GiaoDich
                {
                    IdTaiKhoan = idTaiKhoan,
                    // Có thể để null hoặc gán ID gói nếu bạn có lưu ID gói cũ trong bảng Tài Khoản
                    IdGoi = null,
                    NgayGiaoDich = homNay,
                    NoiDung = "Hệ thống thu hồi đặc quyền VIP",
                    TrangThai = "Thành công",
                    MaGiaoDich = "TH-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper()
                };

                _context.GiaoDiches.Add(giaoDichThuHoi);
                await _context.SaveChangesAsync();

                // 5. Xóa Cache để hệ thống cập nhật lại danh sách ngay lập tức
                _cache.Remove("danhSachNguoiDung_vip_all");
                _cache.Remove("danhSachNguoiDung_vip_true");
                _cache.Remove("danhSachNguoiDung_vip_false");

                return Ok(new
                {
                    success = true,
                    message = "Thu hồi gói VIP thành công!",
                    ngayThuHoi = homNay.ToString("dd/MM/yyyy HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi thu hồi VIP cho ID {IdTaiKhoan}", idTaiKhoan);
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống." });
            }
        }

        [HttpGet("kiem-tra-han-vip")]
        public async Task<IActionResult> KiemTraHanVip()
        {
            // 1. Lấy ID tài khoản từ Token hoặc Session của người dùng đang đăng nhập
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int idTaiKhoan))
            {
                return Unauthorized(new { success = false, message = "Chưa đăng nhập tài khoản." });
            }

            // 2. Tìm tài khoản trong Database
            var taiKhoan = await _context.TaiKhoans
                .FirstOrDefaultAsync(t => t.IdTaiKhoan == idTaiKhoan);

            if (taiKhoan == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy tài khoản." });
            }

            // 3. Nếu không phải VIP hoặc không có ngày kết thúc thì bỏ qua
            if (!taiKhoan.IsVip || taiKhoan.NgayKetThucVip == null)
            {
                return Ok(new { sapHetHan = false });
            }

            // 4. Tính toán thời gian còn lại từ bây giờ cho đến lúc hết hạn
            TimeSpan thoiGianConLai = taiKhoan.NgayKetThucVip.Value - DateTime.Now;
            double tongSoNgayConLai = thoiGianConLai.TotalDays;

            // 5. Điều kiện: Còn hạn (lớn hơn 0) và nằm trong khoảng <= 2 ngày (48 giờ)
            if (tongSoNgayConLai > 0 && tongSoNgayConLai <= 2)
            {
                int soGioConLai = (int)Math.Ceiling(thoiGianConLai.TotalHours);
                string textThongBao = soGioConLai > 24
                    ? $"🌟 Gói VIP của bạn chỉ còn chưa đầy 2 ngày nữa là hết hạn. Hãy gia hạn để không bị gián đoạn tiến trình học HSK 3.0 nhé!"
                    : $"🔥 CẢNH BÁO: Gói VIP của bạn chỉ còn {soGioConLai} giờ sử dụng! Gia hạn ngay hôm nay để giữ các quyền lợi.";

                return Ok(new
                {
                    sapHetHan = true,
                    message = textThongBao,
                    ngayHetThuc = taiKhoan.NgayKetThucVip.Value.ToString("dd/MM/yyyy HH:mm")
                });
            }

            // Nếu còn nhiều hơn 2 ngày hoặc đã hết hạn hoàn toàn (đã có Worker khóa xử lý)
            return Ok(new { sapHetHan = false });
        }
    

        [HttpGet("danh-sach-bang-gia")]
        public async Task<ActionResult> GetAllBangGia()
        {
            string cacheKey = "danhSachBangGia";

            // 1. Thử lấy dữ liệu từ cache
            if (_cache.TryGetValue(cacheKey, out IEnumerable<BangGiaTaiKhoanModels>? danhSach))
            {
                _logger.LogInformation("Lấy danh sách bảng giá từ Cache.");
                return Ok(danhSach);
            }

            try
            {
                _logger.LogInformation("Cache trống. Đang truy vấn từ Database...");

                // 2. Truy vấn từ Database
                danhSach = await _context.BangGiaTaiKhoans
                    .Select(x => new BangGiaTaiKhoanModels
                    {
                        IdGoi = x.IdGoi,
                        TenGoi = x.TenGoi,
                        SoTien = x.SoTien,
                        SoThang = x.SoThang,
                        MoTa = x.MoTa,
                        TrangThai = x.TrangThai,
                        NgayTao = x.NgayTao
                    })
                    .ToListAsync();

                // 3. Cấu hình Cache (thời gian sống là 10 phút)
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2)); // Reset nếu không truy cập trong 2 phút

                _cache.Set(cacheKey, danhSach, cacheEntryOptions);

                return Ok(danhSach);
            }
            catch (Exception ex)
            {
                // 4. Bắt lỗi và ghi log
                _logger.LogError(ex, "Lỗi khi lấy danh sách bảng giá từ Database.");
                return StatusCode(500, "Đã xảy ra lỗi nội bộ máy chủ khi xử lý yêu cầu.");
            }
        }
        [HttpPost("them-goi")]
        public async Task<IActionResult> ThemGoiDangKy([FromBody] CreateBangGiaModel model)
        {
            if (model == null || model.SoThang <= 0 || model.SoTien < 0)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ. Số tháng và số tiền phải lớn hơn 0." });
            }

            try
            {
                // 1. Kiểm tra trùng Số Tháng trong các gói đang hoạt động (TrangThai == true)
                bool isDuplicateSoThang = await _context.BangGiaTaiKhoans
                    .AnyAsync(x => x.SoThang == model.SoThang && x.TrangThai == true);

                if (isDuplicateSoThang)
                {
                    return Conflict(new { message = $"Số tháng ({model.SoThang} tháng) đã tồn tại trong hệ thống. Bạn chỉ có thể cập nhật gói này." });
                }

                // 2. Tạo gói mới
                var newGoi = new BangGiaTaiKhoan
                {
                    TenGoi = model.TenGoi,
                    SoTien = model.SoTien,
                    SoThang = model.SoThang,
                    MoTa = model.MoTa,
                    TrangThai = true,
                    NgayTao = DateTime.Now
                };

                _context.BangGiaTaiKhoans.Add(newGoi);
                await _context.SaveChangesAsync();

                // 3. Xóa cache
                _cache.Remove("danhSachBangGia");

                return Ok(new { message = "Thêm gói đăng ký thành công!", idGoi = newGoi.IdGoi });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm gói đăng ký mới.");
                return StatusCode(500, "Lỗi hệ thống: Không thể thêm gói đăng ký mới.");
            }
        }
        [HttpPost("cap-nhat-goi")]
        public async Task<IActionResult> UpdateBangGia([FromBody] UpdateBangGiaModel model)
        {
            if (model == null || model.SoThang <= 0 || model.SoTien < 0)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ." });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Tìm bản ghi hiện tại cần cập nhật
                var existingRecord = await _context.BangGiaTaiKhoans
                    .FirstOrDefaultAsync(x => x.IdGoi == model.IdGoi && x.TrangThai == true);

                if (existingRecord == null)
                {
                    return NotFound(new { message = "Không tìm thấy gói giá đang hoạt động để cập nhật." });
                }

                // 2. Nếu người dùng thay đổi số tháng, kiểm tra số tháng mới có bị trùng với gói active khác hay không
                if (existingRecord.SoThang != model.SoThang)
                {
                    bool isDuplicateSoThang = await _context.BangGiaTaiKhoans
                        .AnyAsync(x => x.SoThang == model.SoThang && x.TrangThai == true && x.IdGoi != model.IdGoi);

                    if (isDuplicateSoThang)
                    {
                        return Conflict(new { message = $"Gói có thời hạn {model.SoThang} tháng đã tồn tại trên hệ thống." });
                    }
                }

                // 3. Vô hiệu hóa bản ghi cũ để lưu vết lịch sử
                existingRecord.TrangThai = false;

                // 4. Tạo bản ghi mới chứa thông tin giá / số tháng mới
                var newRecord = new BangGiaTaiKhoan
                {
                    TenGoi = model.TenGoi,
                    SoTien = model.SoTien,
                    SoThang = model.SoThang,
                    MoTa = model.MoTa,
                    TrangThai = true, // Bản ghi mới active
                    NgayTao = DateTime.Now
                };

                _context.BangGiaTaiKhoans.Add(newRecord);

                // 5. Lưu thay đổi vào DB
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 6. Xóa cache
                _cache.Remove("danhSachBangGia");

                return Ok(new
                {
                    message = "Đã cập nhật gói đăng ký thành công (đã lưu lịch sử gói cũ).",
                    oldId = existingRecord.IdGoi,
                    newId = newRecord.IdGoi
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi cập nhật bảng giá.");
                return StatusCode(500, "Lỗi hệ thống: Không thể thực hiện cập nhật.");
            }
        }
        //[HttpPost("update-version")]
        //public async Task<IActionResult> UpdateBangGia([FromBody] BangGiaTaiKhoanModels model)
        //{
        //    // Sử dụng Transaction để đảm bảo tính toàn vẹn dữ liệu
        //    using var transaction = await _context.Database.BeginTransactionAsync();

        //    try
        //    {
        //        // 1. Tìm và vô hiệu hóa bản ghi cũ
        //        var existingRecord = await _context.BangGiaTaiKhoans
        //            .FirstOrDefaultAsync(x => x.IdGoi == model.IdGoi);

        //        if (existingRecord == null)
        //            return NotFound("Không tìm thấy gói giá để cập nhật.");

        //        existingRecord.TrangThai = false; // Chuyển trạng thái sang false

        //        // 2. Tạo bản ghi mới với dữ liệu cập nhật
        //        var newRecord = new BangGiaTaiKhoan
        //        {
        //            TenGoi = model.TenGoi,
        //            SoTien = model.SoTien,
        //            SoThang = model.SoThang,
        //            MoTa = model.MoTa,
        //            TrangThai = true, // Bản ghi mới mặc định là true
        //            NgayTao = DateTime.Now
        //        };

        //        _context.BangGiaTaiKhoans.Add(newRecord);

        //        // 3. Lưu thay đổi
        //        await _context.SaveChangesAsync();
        //        await transaction.CommitAsync();

        //        // 4. Xóa cache để dữ liệu mới được cập nhật
        //        _cache.Remove("danhSachBangGia");

        //        return Ok(new { Message = "Đã cập nhật phiên bản mới thành công", NewId = newRecord.IdGoi });
        //    }
        //    catch (Exception ex)
        //    {
        //        await transaction.RollbackAsync();
        //        _logger.LogError(ex, "Lỗi khi cập nhật bảng giá và tạo bản ghi mới.");
        //        return StatusCode(500, "Lỗi hệ thống: Không thể thực hiện cập nhật.");
        //    }
        //}

        //[HttpGet("so-tien-thanh-toan")]
        //public async Task<ActionResult> SoTienThanhToan()
        //{
        //    string cacheKey = "danhSachBangGia";

        //    // 1. Thử lấy dữ liệu từ cache
        //    if (_cache.TryGetValue(cacheKey, out IEnumerable<BangGiaTaiKhoanModels>? danhSach))
        //    {
        //        _logger.LogInformation("Lấy danh sách bảng giá từ Cache.");
        //        return Ok(danhSach);
        //    }

        //    try
        //    {
        //        _logger.LogInformation("Cache trống. Đang truy vấn từ Database...");

        //        // 2. Truy vấn từ Database
        //        danhSach = await _context.BangGiaTaiKhoans
        //            .Where(x=> x.TrangThai == true) // Chỉ lấy gói đang hoạt động
        //            .Select(x => new BangGiaTaiKhoanModels
        //            {
        //                IdGoi = x.IdGoi,
        //                TenGoi = x.TenGoi,
        //                SoTien = x.SoTien,
        //                SoThang = x.SoThang,                      
        //            })
        //            .ToListAsync();

        //        // 3. Cấu hình Cache (thời gian sống là 10 phút)
        //        var cacheEntryOptions = new MemoryCacheEntryOptions()
        //            .SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
        //            .SetSlidingExpiration(TimeSpan.FromMinutes(2)); // Reset nếu không truy cập trong 2 phút

        //        _cache.Set(cacheKey, danhSach, cacheEntryOptions);

        //        return Ok(danhSach);
        //    }
        //    catch (Exception ex)
        //    {
        //        // 4. Bắt lỗi và ghi log
        //        _logger.LogError(ex, "Lỗi khi lấy danh sách bảng giá từ Database.");
        //        return StatusCode(500, "Đã xảy ra lỗi nội bộ máy chủ khi xử lý yêu cầu.");
        //    }
        //}

        //[HttpPost("dang-ky-mua")]
        //public async Task<IActionResult> DangKyMuaVip()
        //{
        //    // 1. Kiểm tra đăng nhập để lấy IdTaiKhoan
        //    var idTaiKhoanStr = HttpContext.Session.GetString("IdTaiKhoan") ?? User.FindFirst("IdTaiKhoan")?.Value;
        //    if (string.IsNullOrEmpty(idTaiKhoanStr))
        //    {
        //        return Unauthorized(new { success = false, message = "Vui lòng đăng nhập để thực hiện giao dịch." });
        //    }
        //    int idTaiKhoan = int.Parse(idTaiKhoanStr);

        //    // 2. Lấy gói VIP duy nhất đang hoạt động từ DB
        //    var goiVip = await _context.BangGiaTaiKhoans.FirstOrDefaultAsync(g => g.TrangThai == true);
        //    if (goiVip == null)
        //    {
        //        return BadRequest(new { success = false, message = "Hiện không có gói dịch vụ VIP nào khả dụng." });
        //    }

        //    long tongTienThucTe = (long)goiVip.SoTien;
        //    string maGiaoDichNgoai = "VIP-" + DateTime.Now.Ticks;

        //    // 3. Khởi tạo bản ghi GiaoDich ở trạng thái Chờ thanh toán
        //    var giaoDichMoi = new GiaoDich
        //    {
        //        IdTaiKhoan = idTaiKhoan,
        //        IdGoi = goiVip.IdGoi,
        //        NgayGiaoDich = DateTime.Now,
        //        NoiDung = $"Đăng ký gói {goiVip.TenGoi}",
        //        TrangThai = "Chờ thanh toán",
        //        MaGiaoDich = maGiaoDichNgoai
        //    };

        //    _context.GiaoDiches.Add(giaoDichMoi);
        //    await _context.SaveChangesAsync();

        //    // 4. Phân loại xử lý theo phương thức thanh toán (MaPTTT)
        //    string bankId = "MB";
        //    string accountNo = "0833508903";
        //    string accountName = "NGUYEN DUC TOAN";
        //    string addInfo = $"Thanh toan don hang {maGiaoDichNgoai}";

        //    string qrCodeUrl = $"https://img.vietqr.io/image/{bankId}-{accountNo}-compact2.png?amount={tongTienThucTe}&addInfo={Uri.EscapeDataString(addInfo)}&accountName={Uri.EscapeDataString(accountName)}";

        //    return Ok(new { success = true, pttt = "VietQR", qrUrl = qrCodeUrl, maGiaoDich = maGiaoDichNgoai });

          
        //}
        
        //[HttpGet("kiem-tra-trang-thai/{maGiaoDich}")]
        //public async Task<IActionResult> KiemTraTrangThai(string maGiaoDich)
        //{
        //    if (string.IsNullOrEmpty(maGiaoDich))
        //        return BadRequest(new { success = false, message = "Mã giao dịch trống." });

        //    // Truy vấn trạng thái hiện tại của giao dịch
        //    var giaoDich = await _context.GiaoDiches
        //        .AsNoTracking()
        //        .FirstOrDefaultAsync(g => g.MaGiaoDich == maGiaoDich);

        //    if (giaoDich == null)
        //    {
        //        return NotFound(new { success = false, message = "Không tìm thấy giao dịch." });
        //    }

        //    // Trả về trạng thái để Frontend xử lý
        //    return Ok(new
        //    {
        //        success = true,
        //        trangThai = giaoDich.TrangThai, // "Chờ thanh toán", "Thành công" hoặc "Thất bại"
        //        isPaid = giaoDich.TrangThai == "Thành công"
        //    });
        //}

        // Model nhận cấu trúc dữ liệu từ bên dịch vụ Webhook ngân hàng (ví dụ minh họa dạng SePay/Casso đơn giản)
        public class BankTransferWebhookModel
        {
            public string Gateway { get; set; } = "MB";
            public decimal Amount { get; set; }
            public string Content { get; set; } = null!; // Nội dung chuyển khoản: "Thanh toan don hang VIP-63912..."
            public string TransactionDate { get; set; } = null!;
        }
        

        //[HttpPost("webhook-ngan-hang")]
        //public async Task<IActionResult> WebhookNganHang([FromBody] BankTransferWebhookModel body)
        //{
        //    try
        //    {
        //        _logger.LogInformation($"Nhận webhook ngân hàng: {body.Content} - Số tiền: {body.Amount}");

        //        if (string.IsNullOrEmpty(body.Content)) return BadRequest();

        //        // 1. Tìm mã giao dịch "VIP-..." nằm trong nội dung chuyển khoản
        //        // (Sử dụng Regex hoặc xử lý chuỗi cơ bản dựa trên addInfo bạn tạo ở API trước)
        //        var match = System.Text.RegularExpressions.Regex.Match(body.Content, @"VIP-\d+");
        //        if (!match.Success)
        //        {
        //            return BadRequest(new { message = "Nội dung chuyển khoản không chứa mã giao dịch hợp lệ." });
        //        }
        //        string maGiaoDichNgoai = match.Value;

        //        // 2. Tìm giao dịch trong DB khớp mã và đang ở trạng thái chờ
        //        var giaoDich = await _context.GiaoDiches
        //            .Include(g => g.IdGoiNavigation)
        //            .FirstOrDefaultAsync(g => g.MaGiaoDich == maGiaoDichNgoai && g.TrangThai == "Chờ thanh toán");

        //        if (giaoDich == null)
        //        {
        //            return NotFound(new { message = "Giao dịch không tồn tại hoặc đã được xử lý trước đó." });
        //        }

        //        // 3. Kiểm tra số tiền chuyển khoản có khớp với số tiền gói không (Bảo mật tránh sửa số tiền)
        //        if (body.Amount < giaoDich.IdGoiNavigation?.SoTien)
        //        {
        //            return BadRequest(new { message = "Số tiền thanh toán thiếu." });
        //        }

        //        // 4. CẬP NHẬT TRẠNG THÁI GIAO DỊCH
        //        giaoDich.TrangThai = "Thành công";

        //        // 5. KÍCH HOẠT VIP CHO TÀI KHOẢN
        //        var taiKhoan = await _context.TaiKhoans.FirstOrDefaultAsync(t => t.IdTaiKhoan == giaoDich.IdTaiKhoan);
        //        if (taiKhoan != null)
        //        {
        //            int soThangCong = giaoDich.IdGoiNavigation?.SoThang ?? 1;
        //            var homNay = DateTime.Now;

        //            taiKhoan.IsVip = true;

        //            // Nếu tài khoản đang còn VIP thì cộng tiếp nối ngày cũ, nếu hết hạn thì tính từ hôm nay
        //            if (taiKhoan.NgayKetThucVip.HasValue && taiKhoan.NgayKetThucVip.Value > homNay)
        //            {
        //                taiKhoan.NgayKetThucVip = taiKhoan.NgayKetThucVip.Value.AddMonths(soThangCong);
        //            }
        //            else
        //            {
        //                taiKhoan.NgayBatDauVip = homNay;
        //                taiKhoan.NgayKetThucVip = homNay.AddMonths(soThangCong);
        //            }
        //        }

        //        await _context.SaveChangesAsync();

        //        // 6. XÓA BỘ NHỚ ĐỆM CACHE ĐỂ ADMIN THẤY DANH SÁCH MỚI
        //        _cache.Remove("danhSachNguoiDung_vip_all");
        //        _cache.Remove("danhSachNguoiDung_vip_true");
        //        _cache.Remove("danhSachNguoiDung_vip_false");

        //        return Ok(new { success = true, message = "Xử lý hóa đơn và nâng cấp VIP thành công!" });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError($"Lỗi hệ thống khi xử lý webhook ngân hàng: {ex.Message}");
        //        return StatusCode(500, "Lỗi xử lý nội bộ.");
        //    }
        //}
    }
}