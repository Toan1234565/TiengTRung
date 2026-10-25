using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12.QuanLyVaiTro;
using HocTiengTrung_3._0_T.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.ControllersAPI
{
    [Route("api/quanlyvaitro")]
    [ApiController]
    public class QuanLyVaiTro : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<QuanLyVaiTro> _logger;
        private readonly IAuditLogService _auditLogService;

        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();

        public QuanLyVaiTro(
            TmdtContext context,
            IMemoryCache cache,
            ILogger<QuanLyVaiTro> logger,
            IAuditLogService auditLogService)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _auditLogService = auditLogService;
        }

        // ==========================================
        // 1. LẤY TẤT CẢ VAI TRÒ (CÓ CACHING & PHÂN QUYỀN CHUẨN XÁC)
        // ==========================================
        [HttpGet("getallvaitro")]
        public async Task<IActionResult> GetAllVaiTro()
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

            // 2. XỬ LÝ LẤY DỮ LIỆU TỪ CACHE HOẶC DATABASE
            string cacheKey = "DanhSach_TatCa_VaiTro";

            try
            {
                if (!_cache.TryGetValue(cacheKey, out object cacheResult))
                {
                    _logger.LogInformation($"Cache miss: {cacheKey}. Đang lấy danh sách vai trò từ DB...");

                    var listVaiTro = await _context.VaiTros
                        .AsNoTracking()
                        .OrderBy(v => v.IdVaiTro)
                        .Select(v => new VaiTroModels
                        {
                            IdVaiTro = v.IdVaiTro,
                            TenVaiTro = v.TenVaiTro
                        })
                        .ToListAsync();

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromHours(4))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                    _cache.Set(cacheKey, listVaiTro, cacheOptions);
                    return Ok(new { success = true, data = listVaiTro });
                }

                _logger.LogInformation($"Cache hit: {cacheKey}. Trả dữ liệu từ Cache.");
                return Ok(new { success = true, data = cacheResult });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách vai trò.");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống khi lấy danh sách vai trò."
                });
            }
        }


        // ==========================================
        // 3. THÊM VAI TRÒ MỚI (TRUY VẾT: INSERT_VAITRO)
        // ==========================================
        [HttpPost("addvaitro")]
        public async Task<IActionResult> AddVaiTro([FromBody] VaiTroModels model)
        {
            if (string.IsNullOrWhiteSpace(model.TenVaiTro))
            {
                return BadRequest(new { success = false, message = "Tên vai trò không được để trống!" });
            }

            try
            {
                string tenNormalized = model.TenVaiTro.Trim();
                bool isExist = await _context.VaiTros.AnyAsync(v => v.TenVaiTro.ToLower() == tenNormalized.ToLower());
                if (isExist)
                {
                    return BadRequest(new { success = false, message = "Tên vai trò này đã tồn tại!" });
                }

                var entity = new VaiTro
                {
                    TenVaiTro = tenNormalized
                };

                _context.VaiTros.Add(entity);
                await _context.SaveChangesAsync();

                // Hủy cache
                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                // Ghi vết lịch sử tạo vai trò mới
                await _auditLogService.GhiLichSuAsync(
                    hanhDong: "INSERT_VAITRO",
                    moTa: $"Tạo mới vai trò: '{entity.TenVaiTro}' (ID: {entity.IdVaiTro})",
                    duLieuCu: null,
                    duLieuMoi: new { entity.IdVaiTro, entity.TenVaiTro }
                );

                return Ok(new
                {
                    success = true,
                    message = "Thêm vai trò thành công!",
                    data = new VaiTroModels
                    {
                        IdVaiTro = entity.IdVaiTro,
                        TenVaiTro = entity.TenVaiTro
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm vai trò mới.");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống khi thêm vai trò."
                });
            }
        }

        // ==========================================
        // 4. CẬP NHẬT VAI TRÒ (TRUY VẾT: UPDATE_VAITRO)
        // ==========================================
        [HttpPut("updatevaitro/{id}")]
        public async Task<IActionResult> UpdateVaiTro(int id, [FromBody] VaiTroModels model)
        {
            if (string.IsNullOrWhiteSpace(model.TenVaiTro))
            {
                return BadRequest(new { success = false, message = "Tên vai trò không được để trống!" });
            }

            try
            {
                var vaiTro = await _context.VaiTros.FindAsync(id);
                if (vaiTro == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy vai trò cần chỉnh sửa." });
                }

                string tenNormalized = model.TenVaiTro.Trim();
                bool isDuplicate = await _context.VaiTros.AnyAsync(v => v.IdVaiTro != id && v.TenVaiTro.ToLower() == tenNormalized.ToLower());
                if (isDuplicate)
                {
                    return BadRequest(new { success = false, message = "Tên vai trò này đã được sử dụng bởi vai trò khác!" });
                }

                // Lưu bản sao dữ liệu cũ trước khi sửa
                var duLieuCu = new { vaiTro.IdVaiTro, vaiTro.TenVaiTro };

                vaiTro.TenVaiTro = tenNormalized;
                _context.VaiTros.Update(vaiTro);
                await _context.SaveChangesAsync();

                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                // Ghi vết lịch sử chỉnh sửa
                await _auditLogService.GhiLichSuAsync(
                    hanhDong: "UPDATE_VAITRO",
                    moTa: $"Cập nhật tên vai trò ID {id} từ '{duLieuCu.TenVaiTro}' thành '{vaiTro.TenVaiTro}'",
                    duLieuCu: duLieuCu,
                    duLieuMoi: new { vaiTro.IdVaiTro, vaiTro.TenVaiTro }
                );

                return Ok(new
                {
                    success = true,
                    message = "Cập nhật vai trò thành công!",
                    data = new VaiTroModels
                    {
                        IdVaiTro = vaiTro.IdVaiTro,
                        TenVaiTro = vaiTro.TenVaiTro
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi cập nhật vai trò ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống khi cập nhật vai trò."
                });
            }
        }

        // ==========================================
        // 5. XÓA VAI TRÒ (TRUY VẾT: DELETE_VAITRO)
        // ==========================================
        [HttpDelete("deletevaitro/{id}")]
        public async Task<IActionResult> DeleteVaiTro(int id)
        {
            try
            {
                var vaiTro = await _context.VaiTros.FindAsync(id);
                if (vaiTro == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy vai trò cần xóa." });
                }

                bool hasAccounts = await _context.TaiKhoans.AnyAsync(tk => tk.IdVaiTro == id);
                if (hasAccounts)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Không thể xóa vai trò này vì đang có tài khoản sử dụng. Vui lòng chuyển vai trò của các tài khoản trước!"
                    });
                }

                // Lưu lại thông tin trước khi remove
                var duLieuCu = new { vaiTro.IdVaiTro, vaiTro.TenVaiTro };

                _context.VaiTros.Remove(vaiTro);
                await _context.SaveChangesAsync();

                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                // Ghi vết lịch sử xóa vai trò
                await _auditLogService.GhiLichSuAsync(
                    hanhDong: "DELETE_VAITRO",
                    moTa: $"Xóa vai trò: '{duLieuCu.TenVaiTro}' (ID: {duLieuCu.IdVaiTro})",
                    duLieuCu: duLieuCu,
                    duLieuMoi: null
                );

                return Ok(new { success = true, message = "Xóa vai trò thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi xóa vai trò ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống khi xóa vai trò."
                });
            }
        }

        // ==========================================
        // 6. GẮN VAI TRÒ CHO TÀI KHOẢN (TRUY VẾT: ASSIGN_ROLE)
        // ==========================================
        [HttpPost("gan-vaitro")]
        public async Task<IActionResult> GanVaiTroChoTaiKhoan([FromBody] GanVaiTroRequest request)
        {
            if (request == null || request.IdTaiKhoan <= 0 || request.IdVaiTro <= 0)
            {
                return BadRequest(new { success = false, message = "Vui lòng cung cấp ID tài khoản và ID vai trò hợp lệ." });
            }

            try
            {
                var taiKhoan = await _context.TaiKhoans
                    .Include(t => t.IdVaiTroNavigation)
                    .Include(nd=>nd.NguoiDung)
                    .FirstOrDefaultAsync(t => t.IdTaiKhoan == request.IdTaiKhoan);

                if (taiKhoan == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy tài khoản tương ứng!" });
                }

                var vaiTroMoi = await _context.VaiTros.FindAsync(request.IdVaiTro);
                if (vaiTroMoi == null)
                {
                    return NotFound(new { success = false, message = "Vai trò được chọn không tồn tại trong hệ thống!" });
                }

                // Lưu lại vai trò cũ
                var duLieuCu = new
                {
                    taiKhoan.IdTaiKhoan,
                    taiKhoan.TenDangNhap,
                    IdVaiTroCu = taiKhoan.IdVaiTro,
                    TenVaiTroCu = taiKhoan.IdVaiTroNavigation?.TenVaiTro ?? "Chưa có"
                };

                // Cập nhật vai trò mới
                taiKhoan.IdVaiTro = request.IdVaiTro;
                _context.TaiKhoans.Update(taiKhoan);
                await _context.SaveChangesAsync();

                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                var duLieuMoi = new
                {
                    taiKhoan.IdTaiKhoan,
                    taiKhoan.TenDangNhap,
                    IdVaiTroMoi = vaiTroMoi.IdVaiTro,
                    TenVaiTroMoi = vaiTroMoi.TenVaiTro
                };

                // Ghi vết lịch sử phân quyền
                await _auditLogService.GhiLichSuAsync(
                    hanhDong: "ASSIGN_ROLE",
                    moTa: $"Gán vai trò '{vaiTroMoi.TenVaiTro}' cho tài khoản '{taiKhoan.NguoiDung.HoTen}' (thay thế vai trò cũ '{duLieuCu.TenVaiTroCu}')",
                    duLieuCu: duLieuCu,
                    duLieuMoi: duLieuMoi
                );

                return Ok(new
                {
                    success = true,
                    message = $"Đã gán vai trò '{vaiTroMoi.TenVaiTro}' cho tài khoản '{taiKhoan.NguoiDung.HoTen}' thành công!",
                    data = duLieuMoi
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi gán vai trò {request.IdVaiTro} cho tài khoản {request.IdTaiKhoan}");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống khi gán vai trò cho tài khoản."
                });
            }
        }

        // ==========================================
        // 7. LẤY DANH MỤC VAI TRÒ NỘI BỘ (CHO QUẢN LÝ TẠO NHÂN SỰ)
        // GET: api/quanlyvaitro/danh-sach-vaitro-noibo
        // ==========================================
        [HttpGet("danh-sach-vaitro-noibo")]
        public async Task<IActionResult> GetDanhSachVaiTroNoiBo()
        {
            string cacheKey = "DanhSach_VaiTro_NoiBo";

            try
            {
                if (!_cache.TryGetValue(cacheKey, out object cacheResult))
                {
                    // Lọc chỉ lấy Giảng viên hoặc Quản lý trung tâm (loại trừ Học viên và Quản trị viên hệ thống)
                    var listVaiTro = await _context.VaiTros
                        .AsNoTracking()
                        .Where(v => v.TenVaiTro.ToLower().Contains("giảng viên")
                                 || v.TenVaiTro.ToLower().Contains("giang vien")
                                 || (v.TenVaiTro.ToLower().Contains("quản lý trung tâm") && !v.TenVaiTro.ToLower().Contains("hệ thống")))
                        .OrderBy(v => v.IdVaiTro)
                        .Select(v => new VaiTroModels
                        {
                            IdVaiTro = v.IdVaiTro,
                            TenVaiTro = v.TenVaiTro
                        })
                        .ToListAsync();

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromHours(4))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                    _cache.Set(cacheKey, listVaiTro, cacheOptions);
                    return Ok(new { success = true, data = listVaiTro });
                }

                return Ok(new { success = true, data = cacheResult });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách vai trò nội bộ.");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống khi nạp danh sách vai trò."
                });
            }
        }
    }
}