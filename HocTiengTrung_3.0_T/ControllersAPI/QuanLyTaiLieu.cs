using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12.QuanLyTaiLieu;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace HocTiengTrung_3._0_T.ControllersAPI
{
    [Route("api/quanlytailieu")]
    [ApiController]
    public class QuanLyTaiLieuController : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<QuanLyTaiLieuController> _logger;

        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();

        private readonly Cloudinary _cloudinary = new Cloudinary(new Account(
            "dewlwufwi",
            "613671295333629",
            "kWq7T2Ls8fOjRUJQIWKALDj9BFQ"
        ));

        public QuanLyTaiLieuController(TmdtContext context, IMemoryCache cache, ILogger<QuanLyTaiLieuController> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        private string GenerateSafePublicId(string name)
        {
            string safeName = Regex.Replace(name.Normalize(System.Text.NormalizationForm.FormD), @"\p{M}", "");
            safeName = Regex.Replace(safeName, @"[^a-zA-Z0-9]", "_");
            return safeName;
        }

        // ==========================================
        // 1. LẤY DANH SÁCH & TÌM KIẾM TÀI LIỆU
        // ==========================================
        [HttpGet("getalltailieu")]
        public async Task<IActionResult> GetAllTaiLieu(
            [FromQuery] int? capdo,
            [FromQuery] string? search,
            [FromQuery] bool? trangThai,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var query = _context.TaiLieus.AsQueryable();

                if (capdo.HasValue)
                {
                    query = query.Where(x => x.IdCapDo == capdo.Value);
                }

                if (trangThai.HasValue)
                {
                    query = query.Where(x => x.TrangThai == trangThai.Value);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(x => x.TenTaiLieu.Contains(search));
                }

                int totalItems = await query.CountAsync();

                var danhSachTaiLieu = await query
                    .OrderByDescending(x => x.NgayTao)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new TaiLieuModels
                    {
                        IdTaiLieu = x.IdTaiLieu,
                        TenTaiLieu = x.TenTaiLieu,
                        MoTa = x.MoTa,
                        DuongLink = x.DuongLink,
                        HinhAnh = x.HinhAnh,
                        IdCapDo = x.IdCapDo,
                        NgayTao = x.NgayTao,
                        TrangThai = x.TrangThai
                    })
                    .ToListAsync();

                return Ok(new
                {
                    TotalItems = totalItems,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                    Data = danhSachTaiLieu
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách tài liệu");
                return StatusCode(500, new { message = "Đã xảy ra lỗi hệ thống khi lấy dữ liệu." });
            }
        }

        // ==========================================
        // 2. LẤY CHI TIẾT TÀI LIỆU THEO ID
        // ==========================================
        [HttpGet("getbyid/{id}")]
        public async Task<IActionResult> GetTaiLieuById(int id)
        {
            try
            {
                var item = await _context.TaiLieus.FindAsync(id);
                if (item == null)
                    return NotFound(new { message = "Không tìm thấy tài liệu!" });

                var result = new TaiLieuModels
                {
                    IdTaiLieu = item.IdTaiLieu,
                    TenTaiLieu = item.TenTaiLieu,
                    MoTa = item.MoTa,
                    DuongLink = item.DuongLink,
                    HinhAnh = item.HinhAnh,
                    IdCapDo = item.IdCapDo,
                    NgayTao = item.NgayTao,
                    TrangThai = item.TrangThai
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi lấy thông tin tài liệu ID {id}");
                return StatusCode(500, new { message = "Đã xảy ra lỗi hệ thống." });
            }
        }

        // ==========================================
        // 3. THÊM TÀI LIỆU MỚI
        // ==========================================
        [HttpPost("addtailieu")]
        public async Task<IActionResult> AddTaiLieu([FromForm] TaiLieuModels model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (model.FileTaiLieu == null || model.FileTaiLieu.Length == 0)
                return BadRequest(new { message = "Vui lòng đính kèm file tài liệu!" });

            long maxFileSize = 15 * 1024 * 1024; // 15MB
            if (model.FileTaiLieu.Length > maxFileSize)
                return BadRequest(new { message = "File tài liệu không được vượt quá 15MB!" });

            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".zip", ".rar" };
            var extension = Path.GetExtension(model.FileTaiLieu.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { message = "Định dạng file không được hỗ trợ!" });

            try
            {
                bool isDuplicate = await _context.TaiLieus.AnyAsync(t => t.TenTaiLieu.ToLower() == model.TenTaiLieu.ToLower());
                if (isDuplicate)
                {
                    return BadRequest(new { message = "Tên tài liệu đã tồn tại. Vui lòng chọn tên khác!" });
                }

                string duongLinkUrl = "";
                string hinhAnhUrl = "";
                string safeName = GenerateSafePublicId(model.TenTaiLieu);

                // Upload File lên Cloudinary
                using (var stream = model.FileTaiLieu.OpenReadStream())
                {
                    var uploadParams = new RawUploadParams()
                    {
                        File = new FileDescription(model.FileTaiLieu.FileName, stream),
                        Folder = "TaiLieuHeThong",
                        PublicId = $"{safeName}_{Guid.NewGuid():N}_file",
                        Overwrite = true
                    };
                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                    duongLinkUrl = uploadResult.SecureUrl.ToString();
                }

                // Upload Ảnh (Nếu có)
                if (model.FileHinhAnh != null && model.FileHinhAnh.Length > 0)
                {
                    using (var stream = model.FileHinhAnh.OpenReadStream())
                    {
                        var uploadParams = new ImageUploadParams()
                        {
                            File = new FileDescription(model.FileHinhAnh.FileName, stream),
                            Folder = "TaiLieuHeThong",
                            PublicId = $"{safeName}_{Guid.NewGuid():N}_anh",
                            Overwrite = true
                        };
                        var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                        hinhAnhUrl = uploadResult.SecureUrl.ToString();
                    }
                }

                var newTaiLieu = new TaiLieu
                {
                    TenTaiLieu = model.TenTaiLieu,
                    MoTa = model.MoTa,
                    DuongLink = duongLinkUrl,
                    HinhAnh = hinhAnhUrl,
                    IdCapDo = model.IdCapDo,
                    NgayTao = DateTime.Now,
                    TrangThai = model.TrangThai
                };

                _context.TaiLieus.Add(newTaiLieu);
                await _context.SaveChangesAsync();

                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                return Ok(new { message = "Thêm tài liệu thành công!", data = newTaiLieu });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm tài liệu mới");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi thêm tài liệu." });
            }
        }

        // ==========================================
        // 4. CẬP NHẬT TÀI LIỆU
        // ==========================================
        [HttpPut("updatetailieu/{id}")]
        public async Task<IActionResult> UpdateTaiLieu(int id, [FromForm] TaiLieuModels model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var existingTaiLieu = await _context.TaiLieus.FindAsync(id);
                if (existingTaiLieu == null)
                {
                    return NotFound(new { message = "Không tìm thấy tài liệu cần cập nhật." });
                }

                bool isDuplicate = await _context.TaiLieus.AnyAsync(t =>
                    t.TenTaiLieu.ToLower() == model.TenTaiLieu.ToLower() && t.IdTaiLieu != id);
                if (isDuplicate)
                {
                    return BadRequest(new { message = "Tên tài liệu đã được sử dụng bởi tài liệu khác!" });
                }

                string safeName = GenerateSafePublicId(model.TenTaiLieu);

                // Upload File mới nếu người dùng chọn
                if (model.FileTaiLieu != null && model.FileTaiLieu.Length > 0)
                {
                    using var stream = model.FileTaiLieu.OpenReadStream();
                    var uploadParams = new RawUploadParams()
                    {
                        File = new FileDescription(model.FileTaiLieu.FileName, stream),
                        Folder = "TaiLieuHeThong",
                        PublicId = $"{safeName}_{Guid.NewGuid():N}_file",
                        Overwrite = true
                    };
                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                    existingTaiLieu.DuongLink = uploadResult.SecureUrl.ToString();
                }

                // Upload Ảnh mới nếu người dùng chọn
                if (model.FileHinhAnh != null && model.FileHinhAnh.Length > 0)
                {
                    using var stream = model.FileHinhAnh.OpenReadStream();
                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(model.FileHinhAnh.FileName, stream),
                        Folder = "TaiLieuHeThong",
                        PublicId = $"{safeName}_{Guid.NewGuid():N}_anh",
                        Overwrite = true
                    };
                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                    existingTaiLieu.HinhAnh = uploadResult.SecureUrl.ToString();
                }

                existingTaiLieu.TenTaiLieu = model.TenTaiLieu;
                existingTaiLieu.MoTa = model.MoTa;
                existingTaiLieu.IdCapDo = model.IdCapDo;
                existingTaiLieu.TrangThai = model.TrangThai;

                _context.TaiLieus.Update(existingTaiLieu);
                await _context.SaveChangesAsync();

                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                return Ok(new { message = "Cập nhật tài liệu thành công!", data = existingTaiLieu });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi cập nhật tài liệu ID {id}");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật tài liệu." });
            }
        }

        // ==========================================
        // 5. XÓA TÀI LIỆU
        // ==========================================
        [HttpDelete("deletetailieu/{id}")]
        public async Task<IActionResult> DeleteTaiLieu(int id)
        {
            try
            {
                var existingTaiLieu = await _context.TaiLieus.FindAsync(id);
                if (existingTaiLieu == null)
                {
                    return NotFound(new { message = "Không tìm thấy tài liệu cần xóa." });
                }

                _context.TaiLieus.Remove(existingTaiLieu);
                await _context.SaveChangesAsync();

                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                return Ok(new { message = "Xóa tài liệu thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi xóa tài liệu ID {id}");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xóa tài liệu." });
            }
        }
        [HttpGet("download-tai-lieu/{id}")]
        public async Task<IActionResult> DownloadTaiLieu(int id)
        {
            var taiLieu = await _context.TaiLieus.FindAsync(id); // Hoặc db query của bạn
            if (taiLieu == null || string.IsNullOrEmpty(taiLieu.DuongLink))
            {
                return NotFound(new { message = "Không tìm thấy tài liệu" });
            }

            // Redirect trực tiếp để trình duyệt tự tải file từ URL
            return Redirect(taiLieu.DuongLink);
        }
    }
}