using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
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
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.ControllersAPI
{
    [Route("api/quanlytrungtam")]
    [ApiController]
    public class QuanLyTrungTam : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<QuanLyTrungTam> _logger;
        private readonly IAuditLogService _auditLogService;
        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();

        private readonly Cloudinary _cloudinary = new Cloudinary(new Account(
            "dewlwufwi",
            "613671295333629",
            "kWq7T2Ls8fOjRUJQIWKALDj9BFQ"
        ));

        public QuanLyTrungTam(TmdtContext context, IMemoryCache cache, ILogger<QuanLyTrungTam> logger, IAuditLogService auditLogService)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _auditLogService = auditLogService;
        }

        private string GenerateSafePublicId(string name)
        {
            string safeName = Regex.Replace(name.Normalize(System.Text.NormalizationForm.FormD), @"\p{M}", "");
            safeName = Regex.Replace(safeName, @"[^a-zA-Z0-9]", "_");
            return safeName;
        }

        [HttpGet("getalltrungtam")]
        public async Task<IActionResult> GetAllTrungTam(
             [FromQuery] string? search,
             [FromQuery] bool? trangThai,
             [FromQuery] int page = 1,
             [FromQuery] int pageSize = 10)
        {
            // Lấy claim TenVaiTro và Trim() sạch khoảng trắng
            var tenVaiTro = (User.FindFirst("TenVaiTro")?.Value ?? "").Trim();

            if (!tenVaiTro.Equals("Quản trị viên", StringComparison.OrdinalIgnoreCase) &&
                !tenVaiTro.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "Bạn không có quyền quản trị viên."
                });
            }

            try
            {
                var query = _context.TrungTams.AsQueryable();

                if (trangThai.HasValue)
                {
                    query = query.Where(x => x.TrangThai == trangThai.Value);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    string searchLower = search.Trim().ToLower();
                    query = query.Where(x => x.TenTrungTam.ToLower().Contains(searchLower)
                                          || (x.MaTrungTam != null && x.MaTrungTam.ToLower().Contains(searchLower))
                                          || (x.Email != null && x.Email.ToLower().Contains(searchLower))
                                          || (x.SoDienThoai != null && x.SoDienThoai.Contains(searchLower)));
                }

                int totalItems = await query.CountAsync();

                var danhSachTrungTam = await query
                    .OrderByDescending(x => x.IdTrungTam)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new TrungTamModels
                    {
                        IdTrungTam = x.IdTrungTam,
                        TenTrungTam = x.TenTrungTam,
                        MaTrungTam = x.MaTrungTam,
                        Subdomain = x.Subdomain,
                        SoDienThoai = x.SoDienThoai,
                        Email = x.Email,
                        DiaChi = x.DiaChi,
                        Logo = x.Logo,
                        NgayKichHoat = x.NgayKichHoat,
                        NgayHetHan = x.NgayHetHan,
                        TrangThai = x.TrangThai
                    })
                    .ToListAsync();

                return Ok(new
                {
                    TotalItems = totalItems,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                    Data = danhSachTrungTam
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách trung tâm");
                return StatusCode(500, new { message = "Đã xảy ra lỗi hệ thống khi lấy danh sách trung tâm." });
            }
        }
        // =========================================================================
        // 2. LẤY CHI TIẾT TRUNG TÂM THEO ID HOẶC MÃ TRUNG TÂM  
        // =========================================================================
        // =========================================================================
        // 2. LẤY CHI TIẾT TRUNG TÂM THEO ID HOẶC MÃ TRUNG TÂM (ĐÃ PHÂN QUYỀN)
        // =========================================================================
        [HttpGet("chitiettrungtam/{identifier}")]
        [Authorize] // Bắt buộc đăng nhập để xác thực danh tính
        public async Task<IActionResult> GetChiTietTrungTam(
            [FromRoute] string identifier,
            [FromQuery] string? maTrungTam = null)
        {
            // =========================================================================
            // 1. KIỂM TRA PHÂN QUYỀN TRUY CẬP
            // =========================================================================
            var tenVaiTro = (User.FindFirst("TenVaiTro")?.Value
                             ?? HttpContext.Session?.GetString("TenVaiTro")
                             ?? "").Trim();
            var idVaiTro = (User.FindFirst("IdVaiTro")?.Value
                            ?? HttpContext.Session?.GetString("IdVaiTro")
                            ?? "").Trim();
            var roleClaims = User.FindAll(ClaimTypes.Role).Select(c => c.Value.Trim()).ToList();

            string? currentIdTrungTamStr = User.FindFirst("IdTrungTam")?.Value
                                           ?? HttpContext.Session?.GetString("IdTrungTam");
            int.TryParse(currentIdTrungTamStr, out int currentIdTrungTam);

            string currentMaTrungTam = (User.FindFirst("MaTrungTam")?.Value
                                        ?? HttpContext.Session?.GetString("MaTrungTam")
                                        ?? "").Trim();

            bool isAdmin = tenVaiTro.Equals("Quản trị viên", StringComparison.OrdinalIgnoreCase)
                           || tenVaiTro.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                           || roleClaims.Any(r => r.Equals("Quản trị viên", StringComparison.OrdinalIgnoreCase)
                                               || r.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                                               || r == "1");

            bool isQuanLy = tenVaiTro.Equals("Quản lý trung tâm", StringComparison.OrdinalIgnoreCase)
                            || idVaiTro == "1002"
                            || roleClaims.Any(r => r.Equals("Quản lý trung tâm", StringComparison.OrdinalIgnoreCase) || r == "1002");

            bool isGiangVien = tenVaiTro.Equals("Giảng viên", StringComparison.OrdinalIgnoreCase)
                               || idVaiTro == "1003"
                               || roleClaims.Any(r => r.Equals("Giảng viên", StringComparison.OrdinalIgnoreCase) || r == "1003");

            // Chỉ Quản trị viên, Quản lý hoặc Giảng viên mới được phép xem chi tiết trung tâm
            if (!isAdmin && !isQuanLy && !isGiangVien)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    message = "Bạn không có quyền truy cập thông tin cơ sở này."
                });
            }

            // =========================================================================
            // 2. XÁC ĐỊNH TỪ KHÓA TRA CỨU (ID HOẶC MÃ)
            // =========================================================================
            string searchKey = !string.IsNullOrWhiteSpace(identifier) && identifier != "0"
                ? identifier.Trim()
                : (maTrungTam?.Trim() ?? string.Empty);

            if (string.IsNullOrWhiteSpace(searchKey))
            {
                return BadRequest(new { success = false, message = "Vui lòng cung cấp ID hoặc Mã trung tâm hợp lệ." });
            }

            bool isNumeric = int.TryParse(searchKey, out int idTrungTam);
            string cleanCode = searchKey.ToLower();

            // =========================================================================
            // 3. BẢO VỆ DỮ LIỆU ĐA CƠ SỞ (MULTI-TENANT ISOLATION)
            // =========================================================================
            // Nếu không phải Admin, người dùng chỉ được xem đúng trung tâm mà họ trực thuộc
            if (!isAdmin)
            {
                bool isMyCenter = false;

                if (isNumeric && currentIdTrungTam > 0 && idTrungTam == currentIdTrungTam)
                {
                    isMyCenter = true;
                }
                else if (!string.IsNullOrEmpty(currentMaTrungTam) && cleanCode == currentMaTrungTam.ToLower())
                {
                    isMyCenter = true;
                }

                if (!isMyCenter)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        success = false,
                        message = "Bạn chỉ có thể xem thông tin chi tiết của cơ sở đào tạo mình trực thuộc."
                    });
                }
            }

            string cacheKey = $"ChiTiet_TrungTam_{(isNumeric ? idTrungTam.ToString() : cleanCode)}";

            try
            {
                ChiTietTrungTamViewModel? chiTietTrungTam;

                // =========================================================================
                // 4. KIỂM TRA BỘ NHỚ CACHE
                // =========================================================================
                if (!_cache.TryGetValue(cacheKey, out object? cacheResult))
                {
                    _logger.LogInformation("Cache miss cho key: {Key}. Đang lấy dữ liệu từ DB...", cacheKey);

                    var query = _context.TrungTams
                        .AsNoTracking()
                        .Where(t => t.TrangThai == true);

                    TrungTam? tt;
                    if (isNumeric)
                    {
                        tt = await query.FirstOrDefaultAsync(t => t.IdTrungTam == idTrungTam || (t.MaTrungTam != null && t.MaTrungTam.ToLower() == cleanCode));
                    }
                    else
                    {
                        tt = await query.FirstOrDefaultAsync(t => t.MaTrungTam != null && t.MaTrungTam.ToLower() == cleanCode);
                    }

                    if (tt == null)
                    {
                        return NotFound(new
                        {
                            success = false,
                            message = $"Không tìm thấy trung tâm hoặc trung tâm đã bị vô hiệu hóa (Khóa tra cứu: '{searchKey}')."
                        });
                    }

                    // Truy vấn danh sách lớp an toàn
                    List<LopHocModels> danhSachLop = new List<LopHocModels>();
                    try
                    {
                        danhSachLop = await _context.LopHocs
                            .AsNoTracking()
                            .Where(l => l.IdTrungTam == tt.IdTrungTam && l.TrangThai == true)
                            .OrderByDescending(l => l.IdLopHoc)
                            .Select(l => new LopHocModels
                            {
                                IdLopHoc = l.IdLopHoc,
                                IdTrungTam = l.IdTrungTam,
                                TenLopHoc = l.TenLopHoc,
                                MaLopHoc = $"LH_{l.IdLopHoc}",
                                MoTa = l.MoTa,
                                NgayKhaiGiang = l.NgayKhaiGiang,
                                TrangThai = l.TrangThai
                            })
                            .ToListAsync();
                    }
                    catch (Exception exLop)
                    {
                        _logger.LogWarning("Không thể nạp danh sách lớp cho trung tâm {Id}: {Msg}", tt.IdTrungTam, exLop.Message);
                    }

                    // =========================================================================
                    // 5. MAPPING VIEWMODEL VÀ GIẢI MÃ BẢO MẬT (IN-MEMORY)
                    // =========================================================================
                    chiTietTrungTam = new ChiTietTrungTamViewModel
                    {
                        IdTrungTam = tt.IdTrungTam,
                        TenTrungTam = tt.TenTrungTam,
                        MaTrungTam = tt.MaTrungTam,
                        Subdomain = tt.Subdomain,
                        SoDienThoai = GiaiMaHelper.GiaiMaAnToan(tt.SoDienThoai),
                        Email = GiaiMaHelper.GiaiMaAnToan(tt.Email),
                        DiaChi = tt.DiaChi,
                        Logo = tt.Logo,
                        NgayKichHoat = tt.NgayKichHoat,
                        NgayHetHan = tt.NgayHetHan,
                        TrangThai = tt.TrangThai,
                        DanhSachLopHoc = danhSachLop
                    };

                    // Lưu Cache
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromHours(2))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                    _cache.Set(cacheKey, chiTietTrungTam, cacheOptions);
                }
                else
                {
                    _logger.LogInformation("Cache hit cho key: {Key}. Trả dữ liệu từ Cache.", cacheKey);
                    chiTietTrungTam = (ChiTietTrungTamViewModel)cacheResult!;
                }

                // =========================================================================
                // 6. KIỂM TRA HẠN DỊCH VỤ CỦA CƠ SỞ
                // =========================================================================
                if (chiTietTrungTam.NgayHetHan.HasValue && chiTietTrungTam.NgayHetHan.Value < DateTime.Now)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        success = false,
                        hetHan = true,
                        message = "Thời gian sử dụng của trung tâm này đã hết hạn. Vui lòng liên hệ quản trị viên để gia hạn dịch vụ!"
                    });
                }

                return Ok(chiTietTrungTam);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy chi tiết trung tâm với khóa: {Key} | Chi tiết: {Message}", searchKey, ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống khi lấy thông tin chi tiết trung tâm: " + ex.Message
                });
            }
        }
        // ==========================================
        // 3. THÊM MỚI TRUNG TÂM & TỰ ĐỘNG TẠO TÀI KHOẢN
        // ==========================================
        [HttpPost("addtrungtam")]
        public async Task<IActionResult> AddTrungTam([FromForm] TrungTamModels model, [FromForm] IFormFile? fileLogo)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Bắt đầu Transaction để đảm bảo tính toàn vẹn giữa TrungTam, TaiKhoan và NguoiDung
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Kiểm tra trùng tên trung tâm
                bool isDuplicateName = await _context.TrungTams.AnyAsync(t => t.TenTrungTam.ToLower() == model.TenTrungTam.ToLower());
                if (isDuplicateName)
                {
                    return BadRequest(new { message = "Tên trung tâm đã tồn tại trên hệ thống!" });
                }

                // 2. Xử lý MaTrungTam (dùng làm TenDangNhap và MatKhau)
                string maTrungTam = model.MaTrungTam?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(maTrungTam))
                {
                    // Nếu không nhập thì tự sinh mã duy nhất: TT_yyyyMMddHHmmss
                    maTrungTam = $"TT_{DateTime.Now:yyyyMMddHHmmss}";
                }

                bool isDuplicateCode = await _context.TrungTams.AnyAsync(t => t.MaTrungTam != null && t.MaTrungTam.ToLower() == maTrungTam.ToLower());
                if (isDuplicateCode)
                {
                    return BadRequest(new { message = "Mã trung tâm đã được sử dụng!" });
                }

                // Kiểm tra xem TenDangNhap này đã có trong bảng TaiKhoan chưa
                bool isDuplicateUsername = await _context.TaiKhoans.AnyAsync(tk => tk.TenDangNhap.ToLower() == maTrungTam.ToLower());
                if (isDuplicateUsername)
                {
                    return BadRequest(new { message = $"Tên đăng nhập tương ứng với mã '{maTrungTam}' đã tồn tại trong hệ thống tài khoản!" });
                }

                if (!string.IsNullOrWhiteSpace(model.Subdomain))
                {
                    bool isDuplicateSub = await _context.TrungTams.AnyAsync(t => t.Subdomain != null && t.Subdomain.ToLower() == model.Subdomain.ToLower());
                    if (isDuplicateSub)
                    {
                        return BadRequest(new { message = "Subdomain đã được đăng ký!" });
                    }
                }

                string logoUrl = model.Logo ?? "";

                // 3. Upload Logo lên Cloudinary (nếu có file tải lên)
                if (fileLogo != null && fileLogo.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                    var extension = Path.GetExtension(fileLogo.FileName).ToLower();
                    if (!allowedExtensions.Contains(extension))
                    {
                        return BadRequest(new { message = "Chỉ chấp nhận các định dạng ảnh: .jpg, .jpeg, .png, .webp" });
                    }

                    string safeName = GenerateSafePublicId(model.TenTrungTam);
                    using var stream = fileLogo.OpenReadStream();
                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(fileLogo.FileName, stream),
                        Folder = "TrungTamLogos",
                        PublicId = $"{safeName}_{Guid.NewGuid():N}_logo",
                        Overwrite = true
                    };

                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                    logoUrl = uploadResult.SecureUrl.ToString();
                }

                // Xử lý mã hóa Email & SĐT (chỉ mã hóa khi có dữ liệu)
                string? encryptedEmail = !string.IsNullOrWhiteSpace(model.Email)
                    ? SecurityHelper.Encrypt(model.Email.Trim())
                    : null;

                string? encryptedPhone = !string.IsNullOrWhiteSpace(model.SoDienThoai)
                    ? SecurityHelper.Encrypt(model.SoDienThoai.Trim())
                    : null;

                // 4. Lưu thực thể TrungTam
                var newTrungTam = new TrungTam
                {
                    TenTrungTam = model.TenTrungTam.Trim(),
                    MaTrungTam = maTrungTam,
                    Subdomain = model.Subdomain?.Trim(),
                    SoDienThoai = encryptedPhone,
                    Email = encryptedEmail,
                    DiaChi = model.DiaChi?.Trim(),
                    Logo = logoUrl,
                    NgayKichHoat = model.NgayKichHoat ?? DateTime.Now,
                    NgayHetHan = model.NgayHetHan,
                    TrangThai = model.TrangThai
                };

                _context.TrungTams.Add(newTrungTam);
                await _context.SaveChangesAsync(); // Lưu để sinh IdTrungTam

                // 5. Tìm vai trò Quản lý trung tâm (nếu có) hoặc lấy vai trò mặc định
                var vaiTroQuanLy = await _context.VaiTros.FirstOrDefaultAsync(v =>
                    v.TenVaiTro.ToLower().Contains("quản lý trung tâm") ||
                    v.TenVaiTro.ToLower().Contains("quan ly trung tam"));

                int? idVaiTro = vaiTroQuanLy?.IdVaiTro;
                if (!idVaiTro.HasValue)
                {
                    var defaultRole = await _context.VaiTros.FirstOrDefaultAsync();
                    idVaiTro = defaultRole?.IdVaiTro;
                }

                // 6. Tạo Tài khoản với Tên đăng nhập và Mật khẩu băm BCrypt từ MaTrungTam
                var newTaiKhoan = new TaiKhoan
                {
                    TenDangNhap = maTrungTam,
                    MatKhau = BCrypt.Net.BCrypt.HashPassword(maTrungTam),
                    TrangThai = true,
                    NgayTao = DateTime.Now,
                    IdVaiTro = idVaiTro,
                    IsVip = false,
                    IdTrungTam = newTrungTam.IdTrungTam
                };

                _context.TaiKhoans.Add(newTaiKhoan);
                await _context.SaveChangesAsync(); // Lưu để sinh IdTaiKhoan

                // 7. Tạo hồ sơ Người dùng tương ứng
                var newNguoiDung = new NguoiDung
                {
                    IdTaiKhoan = newTaiKhoan.IdTaiKhoan,
                    HoTen = $"Quản lý {newTrungTam.TenTrungTam}",
                    Email = newTrungTam.Email,
                    SoDienThoai = newTrungTam.SoDienThoai,
                    AnhDaiDien = string.IsNullOrEmpty(logoUrl) ? "default_avatar.png" : logoUrl
                };

                _context.NguoiDungs.Add(newNguoiDung);
                await _context.SaveChangesAsync();

                // Ghi vết lịch sử (Audit Log)
                if (_auditLogService != null)
                {
                    await _auditLogService.GhiLichSuAsync(
                        hanhDong: "INSERT_TRUNGTAM",
                        moTa: $"Thêm mới trung tâm: '{newTrungTam.TenTrungTam}' (Mã: {newTrungTam.MaTrungTam}) và tạo tài khoản quản trị '{newTaiKhoan.TenDangNhap}'",
                        duLieuCu: null,
                        duLieuMoi: new
                        {
                            IdTrungTam = newTrungTam.IdTrungTam,
                            TenTrungTam = newTrungTam.TenTrungTam,
                            MaTrungTam = newTrungTam.MaTrungTam,
                            TaiKhoanQuanTri = newTaiKhoan.TenDangNhap
                        }
                    );
                }

                await transaction.CommitAsync();

                // Xóa cache
                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                return Ok(new
                {
                    success = true,
                    message = "Thêm trung tâm và khởi tạo tài khoản quản trị thành công!",
                    data = new
                    {
                        trungTam = newTrungTam,
                        taiKhoan = new
                        {
                            idTaiKhoan = newTaiKhoan.IdTaiKhoan,
                            tenDangNhap = newTaiKhoan.TenDangNhap,
                            matKhauMacDinh = maTrungTam,
                            idVaiTro = newTaiKhoan.IdVaiTro
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi thêm trung tâm và tạo tài khoản");
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi tạo trung tâm mới: " + ex.Message });
            }
        }

        // ==========================================
        // 4. CẬP NHẬT TRUNG TÂM
        // ==========================================
        [HttpPut("updatetrungtam/{id}")]
        public async Task<IActionResult> UpdateTrungTam(int id, [FromForm] TrungTamModels model, [FromForm] IFormFile? fileLogo)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var existing = await _context.TrungTams.FindAsync(id);
                if (existing == null)
                {
                    return NotFound(new { message = "Không tìm thấy thông tin trung tâm cần cập nhật." });
                }

                // 1. Kiểm tra trùng lặp tên, mã, subdomain
                bool isDuplicateName = await _context.TrungTams.AnyAsync(t => t.TenTrungTam.ToLower() == model.TenTrungTam.ToLower() && t.IdTrungTam != id);
                if (isDuplicateName)
                {
                    return BadRequest(new { message = "Tên trung tâm đã trùng với một trung tâm khác!" });
                }

                if (!string.IsNullOrWhiteSpace(model.MaTrungTam))
                {
                    bool isDuplicateCode = await _context.TrungTams.AnyAsync(t => t.MaTrungTam != null && t.MaTrungTam.ToLower() == model.MaTrungTam.ToLower() && t.IdTrungTam != id);
                    if (isDuplicateCode)
                    {
                        return BadRequest(new { message = "Mã trung tâm đã trùng với trung tâm khác!" });
                    }
                }

                if (!string.IsNullOrWhiteSpace(model.Subdomain))
                {
                    bool isDuplicateSub = await _context.TrungTams.AnyAsync(t => t.Subdomain != null && t.Subdomain.ToLower() == model.Subdomain.ToLower() && t.IdTrungTam != id);
                    if (isDuplicateSub)
                    {
                        return BadRequest(new { message = "Subdomain này đã thuộc về trung tâm khác!" });
                    }
                }

                // 2. Chụp lại snapshot dữ liệu CŨ (ở dạng plaintext đã giải mã để so sánh đúng)
                var snapshotCu = new
                {
                    TenTrungTam = existing.TenTrungTam,
                    MaTrungTam = existing.MaTrungTam,
                    Subdomain = existing.Subdomain,
                    SoDienThoai = GiaiMaHelper.GiaiMaAnToan(existing.SoDienThoai) ?? "",
                    Email = GiaiMaHelper.GiaiMaAnToan(existing.Email) ?? "",
                    DiaChi = existing.DiaChi,
                    Logo = existing.Logo,
                    NgayKichHoat = existing.NgayKichHoat,
                    NgayHetHan = existing.NgayHetHan,
                    TrangThai = existing.TrangThai
                };

                // 3. Xử lý tải logo mới (nếu có)
                if (fileLogo != null && fileLogo.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                    var extension = Path.GetExtension(fileLogo.FileName).ToLower();
                    if (!allowedExtensions.Contains(extension))
                    {
                        return BadRequest(new { message = "Chỉ chấp nhận các định dạng ảnh: .jpg, .jpeg, .png, .webp" });
                    }

                    string safeName = GenerateSafePublicId(model.TenTrungTam);
                    using var stream = fileLogo.OpenReadStream();
                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(fileLogo.FileName, stream),
                        Folder = "TrungTamLogos",
                        PublicId = $"{safeName}_{Guid.NewGuid():N}_logo",
                        Overwrite = true
                    };

                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                    existing.Logo = uploadResult.SecureUrl.ToString();
                }

                // 4. Mã hóa Email & SĐT mới trước khi gán vào entity
                string? newEncryptedEmail = !string.IsNullOrWhiteSpace(model.Email)
                    ? SecurityHelper.Encrypt(model.Email.Trim())
                    : null;

                string? newEncryptedPhone = !string.IsNullOrWhiteSpace(model.SoDienThoai)
                    ? SecurityHelper.Encrypt(model.SoDienThoai.Trim())
                    : null;

                // Gán các giá trị mới vào entity
                existing.TenTrungTam = model.TenTrungTam.Trim();
                existing.MaTrungTam = model.MaTrungTam?.Trim();
                existing.Subdomain = model.Subdomain?.Trim();
                existing.SoDienThoai = newEncryptedPhone;
                existing.Email = newEncryptedEmail;
                existing.DiaChi = model.DiaChi?.Trim();
                existing.NgayKichHoat = model.NgayKichHoat;
                existing.NgayHetHan = model.NgayHetHan;
                existing.TrangThai = model.TrangThai;

                // 5. Chụp snapshot dữ liệu MỚI (dùng giá trị plaintext từ model để so sánh chính xác với snapshotCu)
                var snapshotMoi = new
                {
                    TenTrungTam = existing.TenTrungTam,
                    MaTrungTam = existing.MaTrungTam,
                    Subdomain = existing.Subdomain,
                    SoDienThoai = model.SoDienThoai?.Trim() ?? "",
                    Email = model.Email?.Trim() ?? "",
                    DiaChi = existing.DiaChi,
                    Logo = existing.Logo,
                    NgayKichHoat = existing.NgayKichHoat,
                    NgayHetHan = existing.NgayHetHan,
                    TrangThai = existing.TrangThai
                };

                // 6. Lấy các trường thực sự thay đổi
                var (duLieuCu, duLieuMoi) = KiemTraDuLieu.GetChanges(snapshotCu, snapshotMoi);

                // 7. Lưu thay đổi vào Database
                _context.TrungTams.Update(existing);
                await _context.SaveChangesAsync();

                // 8. Ghi log truy vết khi có thay đổi
                if (duLieuCu.Count > 0 && _auditLogService != null)
                {
                    var tenCacTruong = string.Join(", ", duLieuMoi.Keys);
                    await _auditLogService.GhiLichSuAsync(
                        hanhDong: "UPDATE",
                        moTa: $"Cập nhật cơ sở [{existing.TenTrungTam}] - Thay đổi: {tenCacTruong}",
                        duLieuCu: duLieuCu,
                        duLieuMoi: duLieuMoi
                    );
                }

                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                return Ok(new { message = "Cập nhật trung tâm thành công!", data = existing });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi cập nhật trung tâm ID {id}");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật thông tin: " + ex.Message });
            }
        }

        // ==========================================
        // 5. VÔ HIỆU HÓA / KÍCH HOẠT TRUNG TÂM (TOGGLE TRẠNG THÁI)
        // ==========================================
        [HttpPatch("toggle-trangthai/{id}")]
       
        public async Task<IActionResult> ToggleTrangThai(int id)
        {
            try
            {
                var existing = await _context.TrungTams.FindAsync(id);
                if (existing == null)
                {
                    return NotFound(new { message = "Không tìm thấy trung tâm cần thay đổi trạng thái." });
                }

                existing.TrangThai = !existing.TrangThai;
                _context.TrungTams.Update(existing);
                await _context.SaveChangesAsync();

                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                string statusText = existing.TrangThai ? "Kích hoạt" : "Vô hiệu hóa";
                return Ok(new { message = $"{statusText} trung tâm thành công!", trangThai = existing.TrangThai });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi thay đổi trạng thái trung tâm ID {id}");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật trạng thái." });
            }
        }

        // ==========================================
        // 6. XÓA TRUNG TÂM
        // ==========================================
        [HttpDelete("deletetrungtam/{id}")]
       
        public async Task<IActionResult> DeleteTrungTam(int id)
        {
            try
            {
                var existing = await _context.TrungTams.FindAsync(id);
                if (existing == null)
                {
                    return NotFound(new { message = "Không tìm thấy trung tâm cần xóa." });
                }

                // Kiểm tra xem trung tâm đã có lớp học nào chưa trước khi xóa
                bool hasClasses = await _context.LopHocs.AnyAsync(l => l.IdTrungTam == id);
                if (hasClasses)
                {
                    return BadRequest(new { message = "Không thể xóa trung tâm đã có lớp học trực thuộc. Vui lòng vô hiệu hóa thay vì xóa!" });
                }

                _context.TrungTams.Remove(existing);
                await _context.SaveChangesAsync();

                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                return Ok(new { message = "Xóa trung tâm thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi xóa trung tâm ID {id}");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xóa trung tâm." });
            }
        }

        [Authorize]
        [HttpGet("danh-sach-nhan-su")]
        public async Task<IActionResult> GetDanhSachNhanSuTrungTam(
            [FromQuery] int? idTrungTam,
            [FromQuery] string? maTrungTam,
            [FromQuery] string? tuKhoa,
            [FromQuery] int? idVaiTro,
            [FromQuery] bool? trangThai)
        {
            try
            {
                // 1. Xác định IdTrungTam từ Claims/Session của người dùng đang đăng nhập
                string? claimIdTrungTamStr = User.FindFirst("IdTrungTam")?.Value
                                             ?? HttpContext.Session.GetString("IdTrungTam");
                string? claimMaTrungTam = User.FindFirst("MaTrungTam")?.Value
                                          ?? HttpContext.Session.GetString("MaTrungTam");

                int.TryParse(claimIdTrungTamStr, out int currentIdTrungTam);

                // 2. Kiểm tra quyền và cô lập trung tâm
                // Nếu là quản lý cơ sở -> bắt buộc dùng currentIdTrungTam
                // Nếu là SuperAdmin (currentIdTrungTam == 0) -> cho phép lấy theo param truyền lên
                int targetIdTrungTam = currentIdTrungTam;

                if (targetIdTrungTam <= 0)
                {
                    if (idTrungTam.HasValue && idTrungTam.Value > 0)
                    {
                        targetIdTrungTam = idTrungTam.Value;
                    }
                    else if (!string.IsNullOrWhiteSpace(maTrungTam))
                    {
                        var tt = await _context.TrungTams
                            .AsNoTracking()
                            .FirstOrDefaultAsync(t => t.MaTrungTam != null && t.MaTrungTam.ToLower() == maTrungTam.Trim().ToLower());
                        if (tt != null)
                        {
                            targetIdTrungTam = tt.IdTrungTam;
                        }
                    }
                }

                if (targetIdTrungTam <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Không xác định được trung tâm cần lấy dữ liệu nhân sự!"
                    });
                }

                // 3. Khởi tạo truy vấn: Lọc tài khoản thuộc Trung tâm này
                var query = _context.TaiKhoans
                    .Include(t => t.IdVaiTroNavigation)
                    .Include(t => t.NguoiDung)
                    .AsNoTracking()
                    .Where(t => t.IdTrungTam == targetIdTrungTam);

                // 4. CHỈ LỌC VAI TRÒ LÀ GIẢNG VIÊN HOẶC QUẢN LÝ (Bỏ qua Học viên, Khách)
                query = query.Where(t => t.IdVaiTroNavigation != null && (
                    t.IdVaiTroNavigation.TenVaiTro.ToLower().Contains("giảng viên") ||
                    t.IdVaiTroNavigation.TenVaiTro.ToLower().Contains("giang vien") ||
                    t.IdVaiTroNavigation.TenVaiTro.ToLower().Contains("teacher") ||
                    (
                        (t.IdVaiTroNavigation.TenVaiTro.ToLower().Contains("quản lý") ||
                         t.IdVaiTroNavigation.TenVaiTro.ToLower().Contains("quan ly") ||
                         t.IdVaiTroNavigation.TenVaiTro.ToLower().Contains("manager"))
                        && !t.IdVaiTroNavigation.TenVaiTro.ToLower().Contains("hệ thống")
                        && !t.IdVaiTroNavigation.TenVaiTro.ToLower().Contains("super")
                    )
                ));

                // 5. Bộ lọc bổ sung theo vai trò cụ thể (nếu chọn)
                if (idVaiTro.HasValue && idVaiTro.Value > 0)
                {
                    query = query.Where(t => t.IdVaiTro == idVaiTro.Value);
                }

                // 6. Bộ lọc trạng thái hoạt động
                if (trangThai.HasValue)
                {
                    query = query.Where(t => t.TrangThai == trangThai.Value);
                }

                // 7. Tìm kiếm theo từ khóa (Tên đăng nhập, Họ tên, Email, SĐT)
                if (!string.IsNullOrWhiteSpace(tuKhoa))
                {
                    string key = tuKhoa.Trim().ToLower();
                    query = query.Where(t => t.TenDangNhap.ToLower().Contains(key)
                                          || (t.NguoiDung != null && t.NguoiDung.HoTen != null && t.NguoiDung.HoTen.ToLower().Contains(key))
                                          || (t.NguoiDung != null && t.NguoiDung.Email != null && t.NguoiDung.Email.ToLower().Contains(key))
                                          || (t.NguoiDung != null && t.NguoiDung.SoDienThoai != null && t.NguoiDung.SoDienThoai.Contains(key)));
                }

                // 8. Thực hiện lấy dữ liệu và map sang DTO
                var listNhanSu = await query
                    .OrderBy(t => t.IdVaiTro)
                    .ThenByDescending(t => t.NgayTao)
                    .Select(t => new NhanSuTrungTamModel
                    {
                        IdTaiKhoan = t.IdTaiKhoan,
                        IdUser = t.NguoiDung != null ? t.NguoiDung.IdUser : 0,
                        TenDangNhap = t.TenDangNhap,
                        HoTen = t.NguoiDung != null && !string.IsNullOrEmpty(t.NguoiDung.HoTen) ? t.NguoiDung.HoTen : t.TenDangNhap,
                        Email = GiaiMaHelper.GiaiMaAnToan(t.NguoiDung != null ? t.NguoiDung.Email : null),
                        SoDienThoai = GiaiMaHelper.GiaiMaAnToan(t.NguoiDung != null ? t.NguoiDung.SoDienThoai : null),
                        AnhDaiDien = t.NguoiDung != null ? t.NguoiDung.AnhDaiDien : "default_avatar.png",
                        IdVaiTro = t.IdVaiTro,
                        TenVaiTro = t.IdVaiTroNavigation != null ? t.IdVaiTroNavigation.TenVaiTro : "Chưa phân vai trò",
                        TrangThai = t.TrangThai ?? false,
                        NgayTao = t.NgayTao
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    idTrungTam = targetIdTrungTam,
                    tongSoLuong = listNhanSu.Count,
                    data = listNhanSu
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách nhân sự của trung tâm");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống: " + ex.Message
                });
            }
        }

        [Authorize]
        [HttpPost("tao-tai-khoan-noi-bo")]
        public async Task<IActionResult> TaoTaiKhoanNoiBo([FromBody] TaoTaiKhoanNoiBoRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // 1. LẤY ID TRUNG TÂM TỪ CLAIMS CỦA QUẢN LÝ ĐANG ĐĂNG NHẬP
            string? claimIdTrungTamStr = User.FindFirst("IdTrungTam")?.Value
                                         ?? HttpContext.Session.GetString("IdTrungTam");

            if (!int.TryParse(claimIdTrungTamStr, out int currentIdTrungTam) || currentIdTrungTam <= 0)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    message = "Tài khoản của bạn không thuộc trung tâm nào hoặc không có quyền tạo tài khoản!"
                });
            }

            // 2. Kiểm tra trùng lặp tên đăng nhập
            string tenDangNhapClean = request.TenDangNhap.Trim().ToLower();
            bool isDuplicateUsername = await _context.TaiKhoans.AnyAsync(tk => tk.TenDangNhap.ToLower() == tenDangNhapClean);
            if (isDuplicateUsername)
            {
                return BadRequest(new { success = false, message = "Tên đăng nhập này đã tồn tại trong hệ thống!" });
            }

            // 3. KIỂM TRA VAI TRÒ HỢP LỆ: CHỈ CHO PHÉP GIẢNG VIÊN HOẶC QUẢN LÝ TRUNG TÂM
            var vaiTro = await _context.VaiTros.FindAsync(request.IdVaiTro);
            if (vaiTro == null)
            {
                return BadRequest(new { success = false, message = "Vai trò được chọn không tồn tại trong hệ thống!" });
            }

            string tenVaiTroClean = vaiTro.TenVaiTro.Trim().ToLower();

            // Nhận diện vai trò Giảng viên
            bool isGiangVien = tenVaiTroClean.Contains("giảng viên")
                            || tenVaiTroClean.Contains("giang vien")
                            || tenVaiTroClean.Contains("teacher");

            // Nhận diện vai trò Quản lý trung tâm (loại trừ trường hợp Admin hệ thống tổng / Super Admin)
            bool isQuanLyTrungTam = (tenVaiTroClean.Contains("quản lý trung tâm") || tenVaiTroClean.Contains("quan ly") || tenVaiTroClean.Contains("manager"))
                                 && !tenVaiTroClean.Contains("hệ thống")
                                 && !tenVaiTroClean.Contains("super");

            // Nếu bạn có ID cố định trong database (ví dụ ID 2 là Quản lý, ID 3 là Giảng viên), có thể check trực tiếp:
            // bool isValidRole = request.IdVaiTro == 2 || request.IdVaiTro == 3;
            bool isValidRole = isGiangVien || isQuanLyTrungTam;

            if (!isValidRole)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Quản lý cơ sở chỉ được phép tạo tài khoản có vai trò là 'Giảng viên' hoặc 'Quản lý trung tâm'!"
                });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 4. Tạo tài khoản - Khóa chặt IdTrungTam của cơ sở hiện tại
                var newTaiKhoan = new TaiKhoan
                {
                    TenDangNhap = request.TenDangNhap.Trim(),
                    MatKhau = BCrypt.Net.BCrypt.HashPassword(request.MatKhau),
                    TrangThai = true,
                    NgayTao = DateTime.Now,
                    IdVaiTro = vaiTro.IdVaiTro,
                    IdTrungTam = currentIdTrungTam, // Khóa cố định trung tâm của người tạo
                    IsVip = false
                };

                _context.TaiKhoans.Add(newTaiKhoan);
                await _context.SaveChangesAsync();

                // 5. Tạo hồ sơ thông tin người dùng (NguoiDung)
                var newNguoiDung = new NguoiDung
                {
                    IdTaiKhoan = newTaiKhoan.IdTaiKhoan,
                    HoTen = request.HoTen.Trim(),
                    Email = request.Email?.Trim(),
                    SoDienThoai = request.SoDienThoai?.Trim(),
                    AnhDaiDien = "default_avatar.png"
                };

                _context.NguoiDungs.Add(newNguoiDung);
                await _context.SaveChangesAsync();

                // 6. Ghi Audit Log truy vết hành động của Quản lý
                if (_auditLogService != null)
                {
                    await _auditLogService.GhiLichSuAsync(
                        hanhDong: "CREATE_USER_INTERNAL",
                        moTa: $"Quản lý tạo tài khoản cơ sở: ({newNguoiDung.HoTen}) - Vai trò: {vaiTro.TenVaiTro}",
                        duLieuCu: null,
                        duLieuMoi: new
                        {
                            
                            HoTen = newNguoiDung.HoTen,
                           
                            VaiTro = vaiTro.TenVaiTro
                        }
                    );
                }

                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Tạo tài khoản {vaiTro.TenVaiTro} thành công!",
                    data = new
                    {
                        idTaiKhoan = newTaiKhoan.IdTaiKhoan,
                        tenDangNhap = newTaiKhoan.TenDangNhap,
                        hoTen = newNguoiDung.HoTen,
                        vaiTro = vaiTro.TenVaiTro
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi tạo tài khoản nội bộ cho trung tâm");
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi hệ thống: " + ex.Message });
            }
        }

        [Authorize]
        [HttpGet("chitiet-giangvien/{idTaiKhoan}")]
        public async Task<IActionResult> GetChiTietGiangVien([FromRoute] int idTaiKhoan)
        {
            if (idTaiKhoan <= 0)
            {
                return BadRequest(new { success = false, message = "Mã tài khoản giảng viên không hợp lệ." });
            }

            try
            {
                // =========================================================================
                // 1. TRÍCH XUẤT CLAIMS VÀ SESSION CỦA TÀI KHOẢN ĐANG GỌI REQUEST
                // =========================================================================
                var tenVaiTro = (User.FindFirst("TenVaiTro")?.Value
                                 ?? HttpContext.Session?.GetString("TenVaiTro")
                                 ?? "").Trim();

                var idVaiTroStr = (User.FindFirst("IdVaiTro")?.Value
                                   ?? HttpContext.Session?.GetString("IdVaiTro")
                                   ?? "").Trim();

                var roleClaims = User.FindAll(ClaimTypes.Role).Select(c => c.Value.Trim()).ToList();

                string? idTaiKhoanClaim = User.FindFirst("IdTaiKhoan")?.Value
                                          ?? HttpContext.Session?.GetString("IdTaiKhoan");
                int.TryParse(idTaiKhoanClaim, out int currentIdTaiKhoan);

                string? idTrungTamClaim = User.FindFirst("IdTrungTam")?.Value
                                          ?? HttpContext.Session?.GetString("IdTrungTam");
                int.TryParse(idTrungTamClaim, out int currentIdTrungTam);

                // Fallback đọc DB nếu HttpClient hoặc Session rớt Claims
                if (currentIdTaiKhoan > 0 && (string.IsNullOrEmpty(tenVaiTro) || currentIdTrungTam <= 0))
                {
                    var tkHienTai = await _context.TaiKhoans
                        .AsNoTracking()
                        .Include(t => t.IdVaiTroNavigation)
                        .FirstOrDefaultAsync(t => t.IdTaiKhoan == currentIdTaiKhoan);

                    if (tkHienTai != null)
                    {
                        if (string.IsNullOrEmpty(tenVaiTro) && tkHienTai.IdVaiTroNavigation != null)
                        {
                            tenVaiTro = tkHienTai.IdVaiTroNavigation.TenVaiTro?.Trim() ?? "";
                        }
                        if (currentIdTrungTam <= 0 && tkHienTai.IdTrungTam.HasValue)
                        {
                            currentIdTrungTam = tkHienTai.IdTrungTam.Value;
                        }
                        if (string.IsNullOrEmpty(idVaiTroStr) && tkHienTai.IdVaiTro.HasValue)
                        {
                            idVaiTroStr = tkHienTai.IdVaiTro.Value.ToString();
                        }
                    }
                }

                string roleClean = tenVaiTro.ToLower();

                // 1. Nhận diện Quản lý trung tâm
                bool isQuanLy = roleClean.Contains("quản lý")
                                || roleClean.Contains("quan ly")
                                || roleClean.Contains("manager")
                                || roleClaims.Any(r => r.Equals("Quản lý trung tâm", StringComparison.OrdinalIgnoreCase)
                                                    || r.Equals("Quan ly trung tam", StringComparison.OrdinalIgnoreCase));

                // 2. Nhận diện Giảng viên (Khớp theo tên Claim hoặc IdVaiTro = 1002 từ cookie của bạn)
                bool isGiangVien = roleClean.Contains("giảng viên")
                                   || roleClean.Contains("giang vien")
                                   || roleClean.Contains("teacher")
                                   || idVaiTroStr == "1002"
                                   || roleClaims.Any(r => r.Equals("Giảng viên", StringComparison.OrdinalIgnoreCase)
                                                       || r.Equals("Giang vien", StringComparison.OrdinalIgnoreCase)
                                                       || r == "1002");

                // CHỈ CHO PHÉP GIẢNG VIÊN HOẶC QUẢN LÝ TRUNG TÂM TRUY CẬP
                if (!isQuanLy && !isGiangVien)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        success = false,
                        message = "Chỉ Giảng viên và Quản lý trung tâm mới có quyền truy cập hồ sơ này."
                    });
                }

                // =========================================================================
                // 2. TRUY VẤN DỮ LIỆU TÀI KHOẢN (BỎ Include LopHoc ĐỂ TRÁNH LỖI MaLopHoc Ở SQL)
                // =========================================================================
                var giangVien = await _context.TaiKhoans
                    .AsNoTracking()
                    .Include(t => t.NguoiDung)
                    .Include(t => t.IdTrungTamNavigation)
                    .Include(t => t.IdVaiTroNavigation)
                    .Include(t => t.GiangVienLopHocs)
                    .FirstOrDefaultAsync(t => t.IdTaiKhoan == idTaiKhoan);

                if (giangVien == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Không tìm thấy thông tin tài khoản #{idTaiKhoan} trong hệ thống."
                    });
                }

                // =========================================================================
                // 3. KIỂM TRA ĐA CƠ SỞ: BẮT BUỘC PHẢI THUỘC CÙNG TRUNG TÂM
                // =========================================================================
                if (currentIdTrungTam <= 0 || giangVien.IdTrungTam != currentIdTrungTam)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        success = false,
                        message = "Bạn không có quyền xem thông tin nhân sự thuộc cơ sở đào tạo khác."
                    });
                }

                // =========================================================================
                // 4. TRUY VẤN LỚP HỌC AN TOÀN (FALLBACK MaLopHoc TRÁNH LỖI INVALID COLUMN)
                // =========================================================================
                var phanCongList = giangVien.GiangVienLopHocs?.ToList() ?? new List<GiangVienLopHoc>();
                var idLopHocIds = phanCongList.Select(pc => pc.IdLopHoc).Distinct().ToList();

                var danhSachLopDay = new List<LopDayCuaGiangVienDto>();

                if (idLopHocIds.Any())
                {
                    try
                    {
                        var thongTinLops = await _context.LopHocs
                            .AsNoTracking()
                            .Where(l => idLopHocIds.Contains(l.IdLopHoc))
                            .Select(l => new
                            {
                                l.IdLopHoc,
                                l.TenLopHoc,
                                l.MoTa,
                                l.NgayKhaiGiang,
                                l.NgayKetThuc,
                                l.TrangThai,
                                TongSoHocVien = l.HocVienLopHocs.Count
                            })
                            .ToListAsync();

                        danhSachLopDay = (from pc in phanCongList
                                          join l in thongTinLops on pc.IdLopHoc equals l.IdLopHoc
                                          orderby l.IdLopHoc descending
                                          select new LopDayCuaGiangVienDto
                                          {
                                              IdLopHoc = l.IdLopHoc,
                                              MaLopHoc = $"LH_{l.IdLopHoc}", // Fallback an toàn tránh lỗi SQL
                                              TenLopHoc = l.TenLopHoc,
                                              MoTa = l.MoTa,
                                              VaiTroDay = pc.VaiTroDay ?? "Giảng viên chính",
                                              NgayPhanCong = pc.NgayPhanCong,
                                              NgayKhaiGiang = l.NgayKhaiGiang,
                                              NgayKetThuc = l.NgayKetThuc,
                                              TrangThaiLop = l.TrangThai,
                                              TongSoHocVien = l.TongSoHocVien
                                          }).ToList();
                    }
                    catch (Exception exLop)
                    {
                        _logger.LogWarning("Không thể nạp danh sách lớp cho giảng viên #{Id}: {Msg}", idTaiKhoan, exLop.Message);
                    }
                }

                // =========================================================================
                // 5. MAPPING SANG DTO & GIẢI MÃ IN-MEMORY
                // =========================================================================
                var chiTietDto = new ChiTietGiangVienDto
                {
                    IdTaiKhoan = giangVien.IdTaiKhoan,
                    TenDangNhap = giangVien.TenDangNhap,
                    TrangThaiTaiKhoan = giangVien.TrangThai ?? false,
                    NgayTaoTaiKhoan = giangVien.NgayTao,
                    IdVaiTro = giangVien.IdVaiTro,
                    TenVaiTro = giangVien.IdVaiTroNavigation?.TenVaiTro ?? "Chưa phân vai trò",

                    IdUser = giangVien.NguoiDung?.IdUser ?? 0,
                    HoTen = !string.IsNullOrWhiteSpace(giangVien.NguoiDung?.HoTen)
                            ? giangVien.NguoiDung.HoTen
                            : giangVien.TenDangNhap,

                    Email = GiaiMaHelper.GiaiMaAnToan(giangVien.NguoiDung?.Email),
                    SoDienThoai = GiaiMaHelper.GiaiMaAnToan(giangVien.NguoiDung?.SoDienThoai),

                    NgaySinh = giangVien.NguoiDung?.NgaySinh,
                    AnhDaiDien = giangVien.NguoiDung?.AnhDaiDien ?? "default_avatar.png",

                    IdTrungTam = giangVien.IdTrungTam ?? 0,
                    TenTrungTam = giangVien.IdTrungTamNavigation?.TenTrungTam,
                    MaTrungTam = giangVien.IdTrungTamNavigation?.MaTrungTam,

                    DanhSachLopDay = danhSachLopDay,
                    TongSoLopPhuTrach = danhSachLopDay.Count
                };

                return Ok(new
                {
                    success = true,
                    data = chiTietDto
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy chi tiết thông tin giảng viên #{IdTaiKhoan}", idTaiKhoan);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra sự cố hệ thống khi lấy thông tin giảng viên: " + ex.Message
                });
            }
        }
    }
}