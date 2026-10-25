using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12;
using HocTiengTrung_3._0_T.Models12.LuyenNgheHinhAnh;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using MiniExcelLibs;
using System.Security.Claims;

namespace HocTiengTrung_3._0_T.ControllersAPI
{
    [Route("api/luyennghehinhanh")]
    [ApiController]
    public class LuyenNgheHinhAnhController : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<LuyenNgheHinhAnhController> _logger;
        private readonly IUserService _userService;

        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();

        public LuyenNgheHinhAnhController(TmdtContext context, IMemoryCache cache, ILogger<LuyenNgheHinhAnhController> logger, IUserService userService)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _userService = userService;
        }

        private static void ClearAllBaiHocCache()
        {
            var oldSignal = Interlocked.Exchange(ref _resetCacheSignal, new CancellationTokenSource());
            oldSignal.Cancel();
            oldSignal.Dispose();
        }
        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId)) return userId;

            var sessionUserId = HttpContext.Session.GetString("MaNguoiDung");
            if (!string.IsNullOrEmpty(sessionUserId) && int.TryParse(sessionUserId, out int sUserId)) return sUserId;

            return null;
        }
        private void ClearUserCache(int userId)
        {
            // Trong thực tế, MemoryCache không hỗ trợ xóa theo Pattern (Wildcard)
            // Cách đơn giản nhất cho đồ án/quy mô nhỏ là Reset Signal chung hoặc dùng một Prefix 
            // Ở đây mình chọn giải pháp: Ghi log và xóa signal nếu cần hoặc để cache tự hết hạn sau khi thao tác.
            // Nếu bạn muốn xóa triệt để, có thể dùng thư viện hỗ trợ hoặc quản lý danh sách Key.
            // Giải pháp nhanh: Reset signal để toàn bộ cache được làm mới (đảm bảo dữ liệu mới nhất)
            _resetCacheSignal.Cancel();
            _resetCacheSignal = new CancellationTokenSource();
            _logger.LogInformation($"Cache toàn hệ thống đã được làm mới sau khi User {userId} thay đổi dữ liệu.");
        }
        // =========================================================================
        // 1. API LẤY DANH SÁCH TIÊU ĐỀ BÀI HỌC THEO CẤP ĐỘ
        // =========================================================================
        [HttpGet("danhsach/{iDCapDo}")]
        public async Task<IActionResult> GetListLuyenNghe(
             [FromRoute] int iDCapDo,
             [FromQuery] int page = 1)
        {
            if (page < 1) page = 1;
            int pageSize = 20;

            string cacheKey = $"LuyenNghe_DS_CapDo_{iDCapDo}_Page_{page}";

            try
            {
                if (!_cache.TryGetValue(cacheKey, out object cacheResult))
                {
                    _logger.LogInformation($"Cache miss danh sách tiêu đề cấp độ {iDCapDo}: {cacheKey}");

                    var query = _context.LuyenNgheHinhAnhs
                        .Where(l => l.IdCapDo == iDCapDo)
                        .OrderBy(l => l.IdLuyenNghe);

                    int totalItems = await query.CountAsync();
                    int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                    var items = await query
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(b => new
                        {
                            IdLuyenNghe = b.IdLuyenNghe,
                            TieuDe = b.TieuDe,
                            IdCapDo = b.IdCapDo
                        })
                        .ToListAsync();

                    cacheResult = new
                    {
                        TotalItems = totalItems,
                        TotalPages = totalPages,
                        CurrentPage = page,
                        PageSize = pageSize,
                        Data = items
                    };

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromHours(2))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                    _cache.Set(cacheKey, cacheResult, cacheOptions);
                }

                return Ok(cacheResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi lấy danh sách tiêu đề cấp độ {iDCapDo}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Lỗi hệ thống.");
            }
        }

        [HttpGet("so-bai-hoc/{id}")]
        public async Task<IActionResult> GetSoBaiHocByLuyenNghe([FromRoute] int id, [FromQuery] bool isCapDo = false)
        {
            try
            {
                IQueryable<ChiTietLuyenNgheHinhAnh> query = _context.ChiTietLuyenNgheHinhAnhs;

                if (isCapDo)
                {
                    // Nếu truyền id là capdo -> Join với bảng LuyenNgheHinhAnh để lọc đúng cấp độ
                    query = from ct in _context.ChiTietLuyenNgheHinhAnhs
                            join ln in _context.LuyenNgheHinhAnhs on ct.IdLuyenNghe equals ln.IdLuyenNghe
                            where ln.IdCapDo == id && ct.SoBai.HasValue
                            select ct;
                }
                else
                {
                    // Lọc trực tiếp theo idLuyenNghe
                    query = query.Where(x => x.IdLuyenNghe == id && x.SoBai.HasValue);
                }

                var dsSoBai = await query
                    .Select(x => x.SoBai.Value)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync();

                int maxBai = dsSoBai.Any() ? dsSoBai.Max() : 0;

                return Ok(new
                {
                    success = true,
                    totalLessons = maxBai,
                    availableLessons = dsSoBai
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi lấy số bài học cho ID: {id}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống." });
            }
        }

        // =========================================================================
        // 2. API XEM CHI TIẾT (LỌC TIÊU ĐỀ VÀ TRẠNG THÁI BÀI THUỘC CẤP ĐỘ ĐANG CHỌN)
        // =========================================================================
        [HttpGet("chitiet/{iDCapDo}")]
        public async Task<IActionResult> GetChiTietLuyenNghe(
            [FromRoute] int iDCapDo,
            [FromQuery] int idUser,
            [FromQuery] int baiHoc,
            [FromQuery] int? idLuyenNghe = null,
           
            [FromQuery] bool? daThuoc = null,
            [FromQuery] int page = 1)
        {
            if (page < 1) page = 1;
            int pageSize = 20;

            string cacheKey = $"LuyenNghe_CT_CapDo_{iDCapDo}_User_{idUser}_LuyenNghe_{idLuyenNghe}_Bai_{baiHoc}_Status_{daThuoc}_Page_{page}";

            try
            {
                if (!_cache.TryGetValue(cacheKey, out object cacheResult))
                {
                    _logger.LogInformation($"Cache miss danh sách chi tiết: {cacheKey}");

                    // Chỉ lấy danh sách Chi tiết thuộc các bài có IdCapDo bằng iDCapDo đang chọn
                    var query = from ct in _context.ChiTietLuyenNgheHinhAnhs
                                join l in _context.LuyenNgheHinhAnhs on ct.IdLuyenNghe equals l.IdLuyenNghe
                                where l.IdCapDo == iDCapDo
                                select new { ct, l };

                    // Lọc theo IdLuyenNghe cụ thể (nếu truyền)
                    if (idLuyenNghe.HasValue)
                    {
                        query = query.Where(x => x.l.IdLuyenNghe == idLuyenNghe.Value);
                    }

                    // Lọc theo tiêu đề (chỉ lọc tiêu đề trong cấp độ đang chọn)
                    if(baiHoc > 0)
                    {
                        query = query.Where(x => x.ct.SoBai == baiHoc);
                    }

                    // Lọc theo trạng thái Đã thuộc / Chưa thuộc của từng chi tiết
                    if (daThuoc.HasValue)
                    {
                        if (daThuoc.Value)
                        {
                            query = query.Where(x => _context.LuyenNgheNguoiDungs
                                .Any(nd => nd.IdChiTiet == x.ct.IdChiTiet && nd.IdUser == idUser && nd.DaThuoc));
                        }
                        else
                        {
                            query = query.Where(x => !_context.LuyenNgheNguoiDungs
                                .Any(nd => nd.IdChiTiet == x.ct.IdChiTiet && nd.IdUser == idUser && nd.DaThuoc));
                        }
                    }

                    int totalItems = await query.CountAsync();
                    int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                    var items = await query
                        .OrderBy(x => x.l.IdLuyenNghe)
                        .ThenBy(x => x.ct.IdChiTiet)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(x => new
                        {
                            IdChiTiet = x.ct.IdChiTiet,
                            IdLuyenNghe = x.l.IdLuyenNghe,
                            TieuDe = x.l.TieuDe,
                            IdCapDo = x.l.IdCapDo,
                            FileAudio = x.ct.FileAudio,
                            HinhAnh = x.ct.HinhAnh,
                            PhienAmPinyin = x.ct.PhienAmPinyin,
                            ChieuHanText = x.ct.ChieuHanText,
                            NghiaTiengViet = x.ct.NghiaTiengViet,
                            SoBai = x.ct.SoBai,
                            DaThuoc = _context.LuyenNgheNguoiDungs
                                .Where(nd => nd.IdChiTiet == x.ct.IdChiTiet && nd.IdUser == idUser)
                                .Select(nd => (bool?)nd.DaThuoc)
                                .FirstOrDefault() ?? false
                        })
                        .ToListAsync();

                    cacheResult = new
                    {
                        TotalItems = totalItems,
                        TotalPages = totalPages,
                        CurrentPage = page,
                        PageSize = pageSize,
                        Data = items
                    };

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromHours(2))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                    _cache.Set(cacheKey, cacheResult, cacheOptions);
                }

                return Ok(cacheResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi lấy danh sách chi tiết cho cấp độ {iDCapDo}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Lỗi hệ thống.");
            }
        
        }

        
        // =========================================================================
        // 3. API THÊM MỚI TIÊU ĐỀ
        // =========================================================================
        [HttpPost("them-tieu-de")]
        public async Task<IActionResult> CreateTieuDe([FromBody] LuyenNgheHinhAnhModels model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TieuDe))
            {
                return BadRequest(new { Message = "Dữ liệu không hợp lệ hoặc tiêu đề bị trống." });
            }

            if (model.IdCapDo <= 0)
            {
                return BadRequest(new { Message = "ID cấp độ không hợp lệ." });
            }

            try
            {
                // Khởi tạo Entity của DbContext từ DTO Model truyền vào
                var entity = new HocTiengTrung_3._0_T.Models.LuyenNgheHinhAnh
                {
                    TieuDe = model.TieuDe,
                    IdCapDo = model.IdCapDo
                };

                _context.LuyenNgheHinhAnhs.Add(entity);
                await _context.SaveChangesAsync();

                ClearAllBaiHocCache();

                return Ok(new
                {
                    Message = "Thêm mới tiêu đề bài luyện nghe thành công.",
                    Data = new
                    {
                        IdLuyenNghe = entity.IdLuyenNghe,
                        TieuDe = entity.TieuDe,
                        IdCapDo = entity.IdCapDo
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm mới tiêu đề bài luyện nghe.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "Lỗi hệ thống khi thêm tiêu đề." });
            }
        }

        // =========================================================================
        // 4. API SỬA TIÊU ĐỀ
        // =========================================================================
        [HttpPut("sua-tieu-de/{id}")]
        public async Task<IActionResult> UpdateTieuDe([FromRoute] int id, [FromBody] LuyenNgheHinhAnhModels model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TieuDe))
            {
                return BadRequest(new { Message = "Dữ liệu không hợp lệ hoặc tiêu đề bị trống." });
            }

            try
            {
                var item = await _context.LuyenNgheHinhAnhs.FindAsync(id);
                if (item == null)
                {
                    return NotFound(new { Message = "Không tìm thấy bài luyện nghe cần cập nhật." });
                }

                // Cập nhật các trường trên entity tìm được trong DB
                item.TieuDe = model.TieuDe;
                if (model.IdCapDo > 0)
                {
                    item.IdCapDo = model.IdCapDo;
                }

                await _context.SaveChangesAsync();

                ClearAllBaiHocCache();

                return Ok(new
                {
                    Message = "Cập nhật tiêu đề bài luyện nghe thành công.",
                    Data = new
                    {
                        IdLuyenNghe = item.IdLuyenNghe,
                        TieuDe = item.TieuDe,
                        IdCapDo = item.IdCapDo,
                        
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi sửa tiêu đề bài luyện nghe ID: {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "Lỗi hệ thống khi sửa tiêu đề." });
            }
        }

       

        // =========================================================================
        // 5. API XÓA TIÊU ĐỀ BÀI LUYỆN NGHE
        // =========================================================================
        [HttpDelete("xoa-tieu-de/{id}")]
        public async Task<IActionResult> DeleteTieuDe([FromRoute] int id)
        {
            try
            {
                var item = await _context.LuyenNgheHinhAnhs.FindAsync(id);
                if (item == null)
                {
                    return NotFound(new { Message = "Không tìm thấy bài luyện nghe cần xóa." });
                }

                _context.LuyenNgheHinhAnhs.Remove(item);
                await _context.SaveChangesAsync();

                // Xóa toàn bộ cache để làm tươi dữ liệu
                ClearAllBaiHocCache();

                return Ok(new { Message = "Xóa bài luyện nghe thành công." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi xóa tiêu đề bài luyện nghe ID: {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "Lỗi hệ thống khi xóa bài luyện nghe." });
            }
        }

        [HttpPost("danh-dau-hinh-anh")]
        public async Task<IActionResult> DanhDauHinhAnh([FromBody] int idChiTietHinhAnh)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { success = false, message = "Vui lòng đăng nhập!" });

            try
            {
                var tuVung = await _context.ChiTietLuyenNgheHinhAnhs.FindAsync(idChiTietHinhAnh);
                if (tuVung == null) return NotFound(new { success = false, message = "Từ vựng không tồn tại." });

                var record = await _context.LuyenNgheNguoiDungs
                    .FirstOrDefaultAsync(x => x.IdUser == userId.Value && x.IdChiTiet == idChiTietHinhAnh);

                bool isLearned;
                if (record == null)
                {
                    _context.LuyenNgheNguoiDungs.Add(new LuyenNgheNguoiDung
                    {
                        IdUser = userId.Value,
                        IdChiTiet = idChiTietHinhAnh,
                        DaThuoc = true,
                        NgayDanhDau = DateTime.Now
                    });
                    isLearned = true;
                }
                else
                {
                    _context.LuyenNgheNguoiDungs.Remove(record);
                    isLearned = false;
                }

                await _context.SaveChangesAsync();

                // --- XÓA CACHE CỦA USER SAU KHI TÍCH ---
                // Vì IsLearned thay đổi, mọi CacheKey liên quan đến userId này phải bị xóa
                ClearUserCache(userId.Value);

                return Ok(new { success = true, isLearned = isLearned });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("import-excel")]
        public async Task<IActionResult> ImportExcel(IFormFile file, [FromQuery] int? idLuyenNgheMacDinh = null)
        {
            // 1. Kiểm tra file đầu vào
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, message = "Vui lòng chọn file Excel để tải lên." });
            }

            var extension = Path.GetExtension(file.FileName).ToLower();
            if (extension != ".xlsx" && extension != ".xls")
            {
                return BadRequest(new { success = false, message = "Định dạng file không hợp lệ. Vui lòng chọn file .xlsx hoặc .xls" });
            }

            try
            {
                // 2. Đọc dữ liệu từ file Excel bằng MiniExcel
                using (var stream = file.OpenReadStream())
                {
                    var rows = (await stream.QueryAsync<ChiTietImportExcelDto>()).ToList();

                    if (!rows.Any())
                    {
                        return BadRequest(new { success = false, message = "File Excel không có dữ liệu." });
                    }

                    var danhSachInsert = new List<ChiTietLuyenNgheHinhAnh>();

                    // 3. Chuyển đổi từ DTO sang Model Entity
                    foreach (var item in rows)
                    {
                        // Bỏ qua dòng trắng không có chữ Tiếng Hán
                        if (string.IsNullOrWhiteSpace(item.ChieuHanText))
                        {
                            continue;
                        }

                        // Lấy IdLuyenNghe từ file Excel, nếu để trống thì lấy từ idLuyenNgheMacDinh (nếu có)
                        int targetIdLuyenNghe = item.IdLuyenNghe ?? idLuyenNgheMacDinh ?? 0;

                        if (targetIdLuyenNghe <= 0)
                        {
                            return BadRequest(new
                            {
                                success = false,
                                message = $"Dòng có nội dung '{item.ChieuHanText}' thiếu ID_LuyenNghe hợp lệ."
                            });
                        }

                        var chiTiet = new ChiTietLuyenNgheHinhAnh
                        {
                            IdLuyenNghe = targetIdLuyenNghe,
                            ChieuHanText = item.ChieuHanText?.Trim(),
                            PhienAmPinyin = item.PhienAmPinyin?.Trim(),
                            NghiaTiengViet = item.NghiaTiengViet?.Trim(),
                            FileAudio = item.FileAudio?.Trim(),
                            HinhAnh = item.HinhAnh?.Trim(),
                            SoBai = item.SoBai
                        };

                        danhSachInsert.Add(chiTiet);
                    }

                    if (!danhSachInsert.Any())
                    {
                        return BadRequest(new { success = false, message = "Không tìm thấy dòng dữ liệu hợp lệ nào." });
                    }

                    // 4. Lưu vào Database
                    await _context.Set<ChiTietLuyenNgheHinhAnh>().AddRangeAsync(danhSachInsert);
                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        success = true,
                        message = $"Nhập thành công {danhSachInsert.Count} dòng dữ liệu!",
                        totalRows = danhSachInsert.Count
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Có lỗi xảy ra trong quá trình xử lý file.",
                    detail = ex.Message
                });
            }
        }

        [HttpPut("sua-chi-tiet/{idChiTiet}")]
        public async Task<IActionResult> UpdateChiTiet([FromRoute] int idChiTiet, [FromBody] UpdateChiTietLuyenNgheDto model)
        {
            if (model == null)
            {
                return BadRequest(new { success = false, message = "Dữ liệu cập nhật không hợp lệ." });
            }

            try
            {
                var item = await _context.ChiTietLuyenNgheHinhAnhs.FindAsync(idChiTiet);
                if (item == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy chi tiết cần cập nhật." });
                }

                if (model.IdLuyenNghe.HasValue && model.IdLuyenNghe.Value > 0)
                {
                    item.IdLuyenNghe = model.IdLuyenNghe.Value;
                }

                if (model.ChieuHanText != null) item.ChieuHanText = model.ChieuHanText.Trim();
                if (model.PhienAmPinyin != null) item.PhienAmPinyin = model.PhienAmPinyin.Trim();
                if (model.NghiaTiengViet != null) item.NghiaTiengViet = model.NghiaTiengViet.Trim();
                if (model.FileAudio != null) item.FileAudio = model.FileAudio.Trim();
                if (model.HinhAnh != null) item.HinhAnh = model.HinhAnh.Trim();
                if(model.SoBai.HasValue) item.SoBai = model.SoBai.Value;

                await _context.SaveChangesAsync();

                ClearAllBaiHocCache();

                return Ok(new
                {
                    success = true,
                    message = "Cập nhật chi tiết thành công.",
                    data = item
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi sửa chi tiết ID: {idChiTiet}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi hệ thống khi cập nhật chi tiết." });
            }
        }

        // =========================================================================
        // 7. API XÓA 1 CHI TIẾT BÀI LUYỆN NGHE (MỚI THÊM)
        // =========================================================================
        [HttpDelete("xoa-chi-tiet/{idChiTiet}")]
        public async Task<IActionResult> DeleteChiTiet([FromRoute] int idChiTiet)
        {
            try
            {
                var item = await _context.ChiTietLuyenNgheHinhAnhs.FindAsync(idChiTiet);
                if (item == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy chi tiết cần xóa." });
                }

                // Xóa các dữ liệu đánh dấu đã học liên quan trong LuyenNgheNguoiDung (nếu có)
                var userRecords = _context.LuyenNgheNguoiDungs.Where(x => x.IdChiTiet == idChiTiet);
                _context.LuyenNgheNguoiDungs.RemoveRange(userRecords);

                _context.ChiTietLuyenNgheHinhAnhs.Remove(item);
                await _context.SaveChangesAsync();

                ClearAllBaiHocCache();

                return Ok(new { success = true, message = "Xóa chi tiết thành công." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi xóa chi tiết ID: {idChiTiet}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi hệ thống khi xóa chi tiết." });
            }
        }

        // =========================================================================
        // 8. API XÓA TOÀN BỘ CHI TIẾT CỦA 1 BÀI LUYỆN NGHE (MỚI THÊM)
        // =========================================================================
        [HttpDelete("xoa-tat-ca-chi-tiet/{idLuyenNghe}")]
        public async Task<IActionResult> DeleteAllChiTietByLuyenNghe([FromRoute] int idLuyenNghe)
        {
            try
            {
                var danhSachChiTiet = await _context.ChiTietLuyenNgheHinhAnhs
                    .Where(x => x.IdLuyenNghe == idLuyenNghe)
                    .ToListAsync();

                if (!danhSachChiTiet.Any())
                {
                    return NotFound(new { success = false, message = $"Không tìm thấy chi tiết nào thuộc bài luyện nghe ID: {idLuyenNghe}" });
                }

                var listIdChiTiet = danhSachChiTiet.Select(x => x.IdChiTiet).ToList();

                // Xóa các bản ghi đánh dấu người dùng tương ứng
                var userRecords = _context.LuyenNgheNguoiDungs.Where(x => listIdChiTiet.Contains(x.IdChiTiet));
                _context.LuyenNgheNguoiDungs.RemoveRange(userRecords);

                // Xóa toàn bộ chi tiết
                _context.ChiTietLuyenNgheHinhAnhs.RemoveRange(danhSachChiTiet);
                await _context.SaveChangesAsync();

                ClearAllBaiHocCache();

                return Ok(new
                {
                    success = true,
                    message = $"Đã xóa thành công toàn bộ {danhSachChiTiet.Count} chi tiết của bài luyện nghe ID: {idLuyenNghe}."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi xóa toàn bộ chi tiết của IdLuyenNghe: {idLuyenNghe}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi hệ thống khi xóa danh sách chi tiết." });
            }
        }
    }
}