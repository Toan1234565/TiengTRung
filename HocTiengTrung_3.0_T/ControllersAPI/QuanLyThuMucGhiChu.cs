using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12;
using HocTiengTrung_3._0_T.Models12.QuanLyTaiKhoan;
using HocTiengTrung_3._0_T.Models12.QuanLyThuMucGhiChu;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace HocTiengTrung_3._0_T.ControllersAPI
{
    [Route("api/quanlythumucghichu")]
    [ApiController]
    public class QuanLyThuMucGhiChu : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<QuanLyThuMucGhiChu> _logger;
        private readonly IUserService _userService;

        private readonly Cloudinary _cloudinary = new Cloudinary(new Account(
            "dewlwufwi",
            "613671295333629",
            "kWq7T2Ls8fOjRUJQIWKALDj9BFQ"
        ));

        public QuanLyThuMucGhiChu(TmdtContext context, IMemoryCache cache, ILogger<QuanLyThuMucGhiChu> logger, IUserService userService)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _userService = userService;
        }

        // ==========================================
        // HÀM HỖ TRỢ: LẤY ID NGƯỜI DÙNG HIỆN TẠI
        // ==========================================
        private int? GetCurrentUserId()
        {
            var userIdStr = HttpContext.Session.GetString("MaNguoiDung");
            if (string.IsNullOrEmpty(userIdStr))
            {
                userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }
            if (string.IsNullOrEmpty(userIdStr) || userIdStr == "0")
            {
                return null;
            }
            return int.Parse(userIdStr);
        }

        // ==========================================
        // HÀM HỖ TRỢ: TẠO TÊN FILE TỪ TIÊU ĐỀ
        // ==========================================
        private string GenerateSafeFileName(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "untitled_note";

            // 1. Xóa dấu tiếng Việt
            string normalizedString = title.Normalize(NormalizationForm.FormD);
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }
            string safeName = stringBuilder.ToString().Normalize(NormalizationForm.FormC).ToLower();

            // 2. Xóa các ký tự đặc biệt, chỉ giữ lại chữ, số và khoảng trắng
            safeName = Regex.Replace(safeName, @"[^a-z0-9\s-]", "");

            // 3. Thay khoảng trắng và dấu gạch ngang liên tiếp bằng một dấu gạch dưới "_"
            safeName = Regex.Replace(safeName, @"[\s-]+", "_").Trim('_');

            return safeName;
        }

        // ==========================================
        // 1. API LẤY DANH SÁCH GHI CHÚ
        // ==========================================
        [HttpGet("danh-sach")]
        public async Task<IActionResult> GetDanhSachGhiChu([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { message = "Vui lòng đăng nhập lại." });

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _context.GhiChus.Where(g => g.IdUser == userId.Value);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            var danhSach = await query
                .OrderByDescending(g => g.NgayCapNhat)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(g => new GhiChuModels
                {
                    IdGhiChu = g.IdGhiChu,
                    IdUser = g.IdUser,
                    TieuDe = g.TieuDe,
                    TenFile = g.TenFile,
                    DuongLinkFile = g.DuongLinkFile,
                    KichThuocFile = g.KichThuocFile,
                    NgayTao = g.NgayTao,
                    NgayCapNhat = g.NgayCapNhat
                })
                .ToListAsync();

            return Ok(new
            {
                danhSach,
                page,
                pageSize,
                totalItems,
                totalPages
            });
        }

        // ==========================================
        // 2. API XEM CHI TIẾT 1 GHI CHÚ
        // ==========================================
        [HttpGet("chi-tiet/{id}")]
        public async Task<IActionResult> GetChiTietGhiChu(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { message = "Vui lòng đăng nhập lại." });

            var ghiChu = await _context.GhiChus.FirstOrDefaultAsync(g => g.IdGhiChu == id && g.IdUser == userId.Value);
            if (ghiChu == null) return NotFound(new { message = "Không tìm thấy ghi chú." });

            string noiDungHtml = "";
            try
            {
                using var httpClient = new HttpClient();
                noiDungHtml = await httpClient.GetStringAsync(ghiChu.DuongLinkFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải file ghi chú từ Cloudinary.");
            }

            return Ok(new { ThongTin = ghiChu, NoiDung = noiDungHtml });
        }

        // ==========================================
        // 3. API THÊM MỚI GHI CHÚ
        // ==========================================
        [HttpPost("them-moi")]
        public async Task<IActionResult> TaoGhiChu([FromBody] GhiChuRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { message = "Vui lòng đăng nhập lại." });

            if (string.IsNullOrWhiteSpace(request.TieuDe))
                return BadRequest(new { message = "Tiêu đề không được để trống." });

            // Tạo tên file từ tiêu đề người dùng nhập
            string tenFileBase = GenerateSafeFileName(request.TieuDe);
            string tenFileHtml = $"{tenFileBase}.html";

            // KIỂM TRA TỒN TẠI: Xem tên file đã có trong thư mục của User này chưa
            bool isFileExists = await _context.GhiChus
                .AnyAsync(g => g.IdUser == userId.Value && g.TenFile == tenFileHtml);

            if (isFileExists)
            {
                return BadRequest(new { message = $"Một ghi chú với tên file '{tenFileHtml}' đã tồn tại. Vui lòng đổi tiêu đề khác!" });
            }

            // PublicId chuẩn: QuanLyThuMucGhiChu/{userId}/{tên_file}
            string publicId = $"QuanLyThuMucGhiChu/{userId}/{tenFileBase}";

            byte[] byteArray = Encoding.UTF8.GetBytes(request.NoiDung ?? " ");
            using var stream = new MemoryStream(byteArray);

            var uploadParams = new RawUploadParams()
            {
                File = new FileDescription(tenFileHtml, stream),
                PublicId = publicId,
                Overwrite = false // Để false để an toàn hơn trên Cloudinary
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
                return StatusCode(500, new { message = "Lỗi khi tải file lên Cloudinary." });

            var ghiChuMoi = new GhiChu
            {
                IdUser = userId.Value,
                TieuDe = request.TieuDe,
                TenFile = tenFileHtml,
                DuongLinkFile = uploadResult.SecureUrl.ToString(),
                KichThuocFile = byteArray.Length,
                NgayTao = DateTime.Now,
                NgayCapNhat = DateTime.Now
            };

            _context.GhiChus.Add(ghiChuMoi);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Thêm ghi chú thành công", data = ghiChuMoi });
        }

        // ==========================================
        // 4. API CẬP NHẬT GHI CHÚ
        // ==========================================
        [HttpPut("cap-nhat/{id}")]
        public async Task<IActionResult> CapNhatGhiChu(int id, [FromBody] GhiChuRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { message = "Vui lòng đăng nhập lại." });

            var ghiChu = await _context.GhiChus.FirstOrDefaultAsync(g => g.IdGhiChu == id && g.IdUser == userId.Value);
            if (ghiChu == null) return NotFound(new { message = "Không tìm thấy ghi chú để sửa." });

            // 1. Kiểm tra tiêu đề có thay đổi không
            bool isTitleChanged = !string.IsNullOrWhiteSpace(request.TieuDe) && request.TieuDe.Trim() != ghiChu.TieuDe;

            // Lưu lại thông tin file cũ để tiến hành xóa sau khi đổi tên
            string oldTenFileBase = ghiChu.TenFile.Replace(".html", "");
            string oldPublicId = $"QuanLyThuMucGhiChu/{userId}/{oldTenFileBase}";

            // Tạo tên file mới nếu tiêu đề đổi, ngược lại giữ nguyên tên file cũ
            string newTenFileBase = isTitleChanged ? SanitizeFileName(request.TieuDe) : oldTenFileBase;
            string newTenFile = $"{newTenFileBase}.html";
            string newPublicId = $"QuanLyThuMucGhiChu/{userId}/{newTenFileBase}";

            // 2. Nếu có gửi Nội dung HOẶC có Đổi tiêu đề (làm đổi PublicId)
            if (request.NoiDung != null || isTitleChanged)
            {
                // Nội dung cần lưu (Lấy từ request, hoặc nếu chỉ đổi tiêu đề thì giữ nguyên nội dung trong DB/request)
                string contentToSave = request.NoiDung ?? "";
                byte[] byteArray = Encoding.UTF8.GetBytes(contentToSave);
                ghiChu.KichThuocFile = byteArray.Length;

                using var stream = new MemoryStream(byteArray);
                var uploadParams = new RawUploadParams()
                {
                    File = new FileDescription(newTenFile, stream),
                    PublicId = newPublicId,
                    Overwrite = true // Đè nếu cùng PublicId
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                if (uploadResult.Error != null)
                {
                    return BadRequest(new { message = "Lỗi khi upload Cloudinary: " + uploadResult.Error.Message });
                }

                // Upload thành công -> Cập nhật thông tin file mới
                ghiChu.DuongLinkFile = uploadResult.SecureUrl.ToString();
                ghiChu.TenFile = newTenFile;

                // 3. Nếu ĐỔI TIÊU ĐỀ (PublicID thay đổi) -> Xóa file cũ trên Cloudinary
                if (isTitleChanged && oldPublicId != newPublicId)
                {
                    var deletionParams = new DeletionParams(oldPublicId)
                    {
                        ResourceType = ResourceType.Raw // Bắt buộc dùng Raw vì file .html là Raw asset
                    };
                    await _cloudinary.DestroyAsync(deletionParams);
                }
            }

            // 4. Cập nhật DB
            if (!string.IsNullOrWhiteSpace(request.TieuDe))
            {
                ghiChu.TieuDe = request.TieuDe.Trim();
            }
            ghiChu.NgayCapNhat = DateTime.Now;

            _context.GhiChus.Update(ghiChu);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Cập nhật thành công", data = ghiChu });
        }

        /// <summary>
        /// Hàm phụ trợ: Chuyển tiếng Việt có dấu thành không dấu và xóa ký tự đặc biệt làm tên file safe URL
        /// </summary>
        private string SanitizeFileName(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "ghi-chu-unnamed";

            // Chuyển tiếng Việt có dấu thành không dấu
            string normalized = title.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var c in normalized)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            string result = sb.ToString().Normalize(NormalizationForm.FormC);

            // Thay thế 'đ' và 'Đ'
            result = result.Replace("đ", "d").Replace("Đ", "D");

            // Thay thế khoảng trắng và ký tự đặc biệt bằng dấu gạch ngang
            result = System.Text.RegularExpressions.Regex.Replace(result, @"[^a-zA-Z0-9\s-]", "");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", "-").Trim('-');

            return string.IsNullOrWhiteSpace(result) ? "ghi-chu" : result.ToLower();
        }

        // ==========================================
        // 5. API XÓA GHI CHÚ
        // ==========================================
        [HttpDelete("xoa/{id}")]
        public async Task<IActionResult> XoaGhiChu(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { message = "Vui lòng đăng nhập lại." });

            var ghiChu = await _context.GhiChus.FirstOrDefaultAsync(g => g.IdGhiChu == id && g.IdUser == userId.Value);
            if (ghiChu == null) return NotFound(new { message = "Không tìm thấy ghi chú." });

            try
            {
                string tenFileBase = ghiChu.TenFile.Replace(".html", "");
                // Xóa theo đúng đường dẫn mới
                string publicId = $"QuanLyThuMucGhiChu/{userId}/{tenFileBase}";

                var delParams = new DelResParams()
                {
                    PublicIds = new List<string> { publicId },
                    ResourceType = ResourceType.Raw // Lưu file .html dạng Text nên phải là Raw
                };
                await _cloudinary.DeleteResourcesAsync(delParams);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể xóa file trên Cloudinary.");
            }

            _context.GhiChus.Remove(ghiChu);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã xóa ghi chú." });
        }
    }

    public class GhiChuRequest
    {
        public string TieuDe { get; set; } = null!;
        public string NoiDung { get; set; } = null!;
    }
}