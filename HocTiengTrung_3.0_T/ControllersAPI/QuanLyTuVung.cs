using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12;
using HocTiengTrung_3._0_T.Models12.BaiTap;

using HocTiengTrung_3._0_T.Models12.TuVung;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using MiniExcelLibs;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace HocTiengTrung_3._0_T.ControllersAPI
{
    [Route("api/quanlytuvung")]
    [ApiController]
    public class QuanLyTuVung : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<QuanLyTuVung> _logger;
        private readonly IUserService _userService;


        // 1. Khai báo thêm một signal riêng cho từ vựng
        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();
        private static CancellationTokenSource _vocabularyCacheSignal = new CancellationTokenSource(); // Thêm cái này

        public QuanLyTuVung(TmdtContext context, IMemoryCache cache, ILogger<QuanLyTuVung> logger, IUserService userService)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _userService = userService;
        }

        [HttpGet("danhsachcapdo")]
        public async Task<IActionResult> LayDanhSachCapDo([FromQuery] bool? status = null)
        {
            try
            {
                // 1. Tạo cacheKey động theo trạng thái để tránh xung đột cache
                // Nếu status truyền vào là null thì trả về "All", ngược lại là True/False
                string statusKey = status.HasValue ? status.Value.ToString() : "All";
                string cacheKey = $"DanhSachCapDo_{statusKey}";

                var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
                {
                    entry.SetSlidingExpiration(TimeSpan.FromHours(1));
                    entry.AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                    // 2. Khởi tạo câu lệnh query từ DbContext
                    var query = _context.CapDos.AsNoTracking();

                    // 3. Nếu lọc theo trạng thái cụ thể (True hoặc False) thì bổ sung Where
                    if (status.HasValue)
                    {
                        query = query.Where(tt => tt.TrangThai == status.Value);
                    }

                    return await query
                        .Select(cd => new CapDoModels
                        {
                            IdCapDo = cd.IdCapDo,
                            TenCapDo = cd.TenCapDo,
                            TrangThai = cd.TrangThai // Thêm thuộc tính này nếu model CapDoModels của bạn có hỗ trợ
                        })
                        .ToListAsync();
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách cấp độ");
                return StatusCode(500, "Lỗi máy chủ nội bộ.");
            }
        }

        [HttpPost("ThemCapDo")]
        public async Task<IActionResult> CreateCapDo([FromBody] CapDoDto capDoDto)
        {
            if (capDoDto == null || string.IsNullOrEmpty(capDoDto.TenCapDo))
            {
                return BadRequest("Tên cấp độ không được để trống.");
            }

            var newCapDo = new CapDo
            {
                TenCapDo = capDoDto.TenCapDo,
                TrangThai = capDoDto.TrangThai ?? true
            };

            _context.CapDos.Add(newCapDo);
            await _context.SaveChangesAsync();

            // SỬA TẠI ĐÂY: Phát tín hiệu làm mới bộ nhớ Cache
            Interlocked.Exchange(ref _resetCacheSignal, new CancellationTokenSource())?.Cancel();

            return Ok(new { message = "Thêm cấp độ thành công!", data = newCapDo });
        }

        [HttpPut("CapNhatCapDo/{id}")]
        public async Task<IActionResult> UpdateCapDo(int id, [FromBody] CapDoDto capDoDto)
        {
            if (capDoDto == null || string.IsNullOrEmpty(capDoDto.TenCapDo))
            {
                return BadRequest("Dữ liệu không hợp lệ.");
            }

            var existingCapDo = await _context.CapDos.FindAsync(id);
            if (existingCapDo == null)
            {
                return NotFound($"Không tìm thấy cấp độ với ID = {id}");
            }

            existingCapDo.TenCapDo = capDoDto.TenCapDo;
            existingCapDo.TrangThai = capDoDto.TrangThai;

            try
            {
                _context.CapDos.Update(existingCapDo);
                await _context.SaveChangesAsync();

                // SỬA TẠI ĐÂY: Phát tín hiệu làm mới bộ nhớ Cache
                Interlocked.Exchange(ref _resetCacheSignal, new CancellationTokenSource())?.Cancel();
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(500, "Có lỗi xảy ra trong quá trình cập nhật dữ liệu.");
            }

            return Ok(new { message = "Cập nhật cấp độ thành công!", data = existingCapDo });
        }

        [HttpGet("danhsachtuvung/{capdo}")]
        public async Task<IActionResult> LayDanhSachTuVungTheoCapDo(
             int capdo = 1,
             [FromQuery] int page = 1,
             [FromQuery] string? loaiTu = null,
             [FromQuery] string? searchTerm = null,
             [FromQuery] string? trangThai = "all")
        {
            try
            {
                // 1. Kiểm tra hạ tầng trước khi chạy
                if (_context == null) return StatusCode(500, "DbContext is null. Kiểm tra lại injection.");
                if (_cache == null) return StatusCode(500, "MemoryCache is null.");

                if (page < 1) page = 1;
                int pageSize = 32;
                var userId = GetCurrentUserId();

                string cacheKey = $"TV_{userId ?? 0}_C{capdo}_P{page}_L{loaiTu}_S{searchTerm}_T{trangThai}";

                if (!_cache.TryGetValue(cacheKey, out object cachedResult))
                {
                    _logger.LogInformation($"Cache Miss: {cacheKey}. Đang tải dữ liệu từ DB...");

                    // 2. Sử dụng IQueryable để tối ưu truy vấn
                    var query = _context.TuVungs
                        .Where(tv => tv.IdCapDo == capdo)
                        .AsNoTracking();

                    // Lọc theo từ khóa
                    if (!string.IsNullOrWhiteSpace(searchTerm))
                    {
                        searchTerm = searchTerm.ToLower().Trim();
                        query = query.Where(tv => tv.ChuHan.Contains(searchTerm) || tv.NghiaViet.Contains(searchTerm));
                    }

                    // Lọc theo loại từ
                    if (!string.IsNullOrWhiteSpace(loaiTu))
                    {
                        query = query.Where(tv => tv.LoaiTu == loaiTu);
                    }

                    // Projection (Chuyển sang Model)
                    var finalQuery = query.Select(tv => new TuVungModes
                    {
                        IdTuVung = tv.IdTuVung,
                        ChuHan = tv.ChuHan,
                        Pinyin = tv.Pinyin,
                        NghiaViet = tv.NghiaViet,
                        LoaiTu = tv.LoaiTu,
                        AmThanhPath = tv.AmThanhPath,
                        IdCapDo = tv.IdCapDo,
                        AmHan = tv.AmHan,
                        // Kiểm tra trạng thái học tập
                        IsLearned = userId.HasValue && _context.TuVungNguoiDungs
                                    .Any(uv => uv.IdTuVung == tv.IdTuVung && uv.IdUser == userId.Value && uv.DaThuoc == true),
                        CauViDus = tv.CauViDus.Select(vd => new CauViDuModes
                        {
                            NoiDungHan = vd.NoiDungHan,
                            NoiDungPinyin = vd.NoiDungPinyin,
                            NoiDungViet = vd.NoiDungViet,
                            AmThanh = vd.AmThanhPath
                        }).ToList()
                    });

                    // Lọc theo trạng thái học (learned/not_learned)
                    if (userId.HasValue && !string.IsNullOrEmpty(trangThai) && trangThai != "all")
                    {
                        if (trangThai == "learned") finalQuery = finalQuery.Where(x => x.IsLearned);
                        else if (trangThai == "not_learned") finalQuery = finalQuery.Where(x => !x.IsLearned);
                    }

                    // 3. Thực thi truy vấn (Đây là nơi dễ xảy ra lỗi SQL nhất)
                    int totalItems = await finalQuery.CountAsync();
                    int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                    var items = await finalQuery
                        .OrderBy(tv => tv.IdTuVung)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

                    cachedResult = new { TotalItems = totalItems, TotalPages = totalPages, CurrentPage = page, Items = items };

                    // Cấu hình Cache
                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(30));

                    // Chỉ add token nếu signal không null để tránh crash
                    if (_vocabularyCacheSignal != null)
                    {
                        cacheEntryOptions.AddExpirationToken(new CancellationChangeToken(_vocabularyCacheSignal.Token));
                    }

                    _cache.Set(cacheKey, cachedResult, cacheEntryOptions);
                }

                return Ok(cachedResult);
            }
            catch (Microsoft.Data.SqlClient.SqlException sqlEx)
            {
                // Bắt riêng lỗi SQL để biết do Connection hay do Query
                _logger.LogError(sqlEx, "Lỗi kết nối SQL Server!");
                return StatusCode(500, new
                {
                    error = "SQL_ERROR",
                    detail = sqlEx.Message,
                    server = "SQL8005.site4now.net"
                });
            }
            catch (Exception ex)
            {
                // Bắt mọi lỗi khác và nhả ra InnerException (thứ chứa lỗi thực sự)
                var deepError = ex.InnerException?.Message ?? ex.Message;
                _logger.LogError(ex, "Lỗi nghiêm trọng tại LayDanhSachTuVungTheoCapDo");

                return StatusCode(500, new
                {
                    error = "SYSTEM_ERROR",
                    message = ex.Message,
                    detail = deepError // Đây là dòng sẽ cứu bạn!
                });
            }
        }

        [HttpPost("danh-dau-tu-vung")]
        public async Task<IActionResult> DanhDauTuVung([FromBody] int idTuVung)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { success = false, message = "Vui lòng đăng nhập!" });

            try
            {
                var tuVung = await _context.TuVungs.FindAsync(idTuVung);
                if (tuVung == null) return NotFound(new { success = false, message = "Từ vựng không tồn tại." });

                var record = await _context.TuVungNguoiDungs
                    .FirstOrDefaultAsync(x => x.IdUser == userId.Value && x.IdTuVung == idTuVung);

                bool isLearned;
                if (record == null)
                {
                    _context.TuVungNguoiDungs.Add(new TuVungNguoiDung
                    {
                        IdUser = userId.Value,
                        IdTuVung = idTuVung,
                        DaThuoc = true,
                        NgayDanhDau = DateTime.Now
                    });
                    isLearned = true;
                }
                else
                {
                    _context.TuVungNguoiDungs.Remove(record);
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

        /// <summary>
        /// API lấy danh sách các từ vựng người dùng ĐÃ ĐÁNH DẤU THUỘC
        /// </summary>
        [HttpGet("danhsach-dathuoc")]
        public async Task<IActionResult> LayDanhSachDaThuoc(
            [FromQuery] int page = 1, 
            [FromQuery] string? searchTerm = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return Unauthorized(new { message = "Vui lòng đăng nhập." });

                if (page < 1) page = 1;
                int pageSize = 32;

                // Tạo Cache Key riêng cho danh sách "Đã thuộc" của User
                string cacheKey = $"DaThuoc_{userId.Value}_P{page}_S{searchTerm}";

                if (!_cache.TryGetValue(cacheKey, out object cachedResult))
                {
                    // Truy vấn từ bảng TuVungNguoiDung kết hợp (Include) với bảng TuVung
                    var query = _context.TuVungNguoiDungs
                        .Where(uv => uv.IdUser == userId.Value && uv.DaThuoc == true)
                        .Include(uv => uv.IdTuVungNavigation)
                            .ThenInclude(t => t.CauViDus) // Lấy kèm câu ví dụ
                        .AsNoTracking();

                    // Tìm kiếm trong danh sách đã thuộc
                    if (!string.IsNullOrWhiteSpace(searchTerm))
                    {
                        searchTerm = searchTerm.ToLower().Trim();
                        query = query.Where(uv => uv.IdTuVungNavigation.ChuHan.Contains(searchTerm) 
                                               || uv.IdTuVungNavigation.NghiaViet.Contains(searchTerm));
                    }

                    // Projection về Model hiển thị
                    var projectionQuery = query.Select(uv => new TuVungModes
                    {
                        IdTuVung = uv.IdTuVungNavigation.IdTuVung,
                        ChuHan = uv.IdTuVungNavigation.ChuHan,
                        Pinyin = uv.IdTuVungNavigation.Pinyin,
                        NghiaViet = uv.IdTuVungNavigation.NghiaViet,
                        LoaiTu = uv.IdTuVungNavigation.LoaiTu,
                        AmThanhPath = uv.IdTuVungNavigation.AmThanhPath,
                        IdCapDo = uv.IdTuVungNavigation.IdCapDo,
                        AmHan = uv.IdTuVungNavigation.AmHan,
                        IsLearned = true, // Chắc chắn là đã thuộc
                        CauViDus = uv.IdTuVungNavigation.CauViDus.Select(vd => new CauViDuModes
                        {
                            NoiDungHan = vd.NoiDungHan,
                            NoiDungPinyin = vd.NoiDungPinyin,
                            NoiDungViet = vd.NoiDungViet,
                            AmThanh = vd.AmThanhPath
                        }).ToList()
                    });

                    int totalItems = await projectionQuery.CountAsync();
                    int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
                    
                    var items = await projectionQuery
                        .OrderByDescending(x => x.IdTuVung) // Ưu tiên từ mới đánh dấu lên đầu
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

                    cachedResult = new { 
                        TotalItems = totalItems, 
                        TotalPages = totalPages, 
                        CurrentPage = page, 
                        Items = items 
                    };

                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(20))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                    _cache.Set(cacheKey, cachedResult, cacheEntryOptions);
                }

                return Ok(cachedResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi tại LayDanhSachDaThuoc");
                return StatusCode(500, new { message = "Lỗi hệ thống." });
            }
        }

        [HttpGet("loai-cau-hoi")]
        public async Task<IActionResult> DanhSachLoaiCauHoi()
        {
            try
            {
                // 1. Lấy danh sách từ Database và map sang Model
                var danhSachLoai = await _context.LoaiCauHois
                    .AsNoTracking()
                    .Select(l => new LoaiCauHoiModels
                    {
                        IdLoai = l.IdLoai,
                        TenLoai = l.TenLoai
                    })
                    .ToListAsync();

                // 2. Trả về kết quả thành công
                return Ok(new
                {
                    success = true,
                    data = danhSachLoai
                });
            }
            catch (Exception ex)
            {
                // 3. Xử lý lỗi nếu quá trình truy vấn gặp vấn đề
                // Bạn có thể log ex.Message ở đây để debug
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách loại câu hỏi: " + ex.Message
                });
            }
        }
        
        [HttpGet("tao-bai-tap-thu-cong")]
        public async Task<IActionResult> TaoBaiTapThuCong([FromQuery] string? capDo, [FromQuery] int loaiMa, [FromQuery] int soLuong = 10)
        {
            try
            {
                // Sử dụng hàm helper GetCurrentUserId để đồng bộ cách lấy User
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized(new { success = false, message = "Vui lòng đăng nhập lại!" });

                // 1. Lấy danh sách ID từ vựng đã thuộc
                var listIdsTuVung = await _context.TuVungNguoiDungs
                    .AsNoTracking()
                    .Where(uv => uv.IdUser == userId && uv.DaThuoc == true)
                    .Select(uv => uv.IdTuVung)
                    .ToListAsync();

                if (!listIdsTuVung.Any())
                    return Ok(new { success = false, message = "Bạn chưa thuộc từ vựng nào để tạo bài tập!" });

                // 2. Query câu hỏi
                var query = _context.CauHois
                    .AsNoTracking()
                    .Include(ch => ch.DapAns)
                    .Where(ch => ch.IdLoai == loaiMa);

                // 3. Lọc theo cấp độ
                if (!string.IsNullOrEmpty(capDo))
                {
                    if (int.TryParse(capDo, out int capDoId))
                        query = query.Where(ch => ch.IdCapDo == capDoId);
                    else
                        query = query.Where(ch => ch.IdCapDoNavigation.TenCapDo == capDo);
                }

                // 4. Lọc câu hỏi dựa trên vốn từ và map sang Model
                var random = new Random();
                var filteredQuestions = await query
                    .Where(ch => ch.IdTuVungs.Any(tv => listIdsTuVung.Contains(tv.IdTuVung)))
                    .Select(ch => new CauHoiModels // Dùng chung Model với API lay-bai-tap
                    {
                        IdCauHoi = ch.IdCauHoi,
                        NoiDung = ch.NoiDung,
                        IdLoai = ch.IdLoai,
                        IdCapDo = ch.IdCapDo,
                        AmThanhPath = ch.AmThanhPath,
                        GiaiThich = ch.GiaiThich,
                        DapAns = ch.DapAns.Select(da => new DapAnModels
                        {
                            IdDapAn = da.IdDapAn,
                            NoiDungDapAn = da.NoiDungDapAn,
                            LaDapAnDung = da.LaDapAnDung ?? false
                        }).ToList()
                    })
                    .ToListAsync();

                // 5. Trộn ngẫu nhiên câu hỏi và đáp án
                var finalQuestions = filteredQuestions
                    .OrderBy(x => Guid.NewGuid())
                    .Take(soLuong)
                    .Select(ch => {
                        ch.DapAns = ch.DapAns.OrderBy(d => random.Next()).ToList();
                        return ch;
                    })
                    .ToList();

                if (!finalQuestions.Any())
                    return Ok(new { success = false, message = "Không có câu hỏi nào khớp với vốn từ của bạn." });

                // 6. TRẢ VỀ CẤU TRÚC GIỐNG API LAY-BAI-TAP (DẠNG MẢNG/LIST)
                var danhSachCacDe = new List<object>
        {
            new
            {
                TenDe = $"Luyện tập AI - {DateTime.Now:dd/MM HH:mm}",
                SoLuongCauHoi = finalQuestions.Count,
                CauHois = finalQuestions
            }
        };

                // Trả về trực tiếp mảng giống như return Ok(danhSachCacDe) ở API kia
                return Ok(danhSachCacDe);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Lỗi máy chủ nội bộ.");
            }
        }

        //[AllowAnonymous] // Cho phép khách truy cập
        //[HttpGet("on-luyen-ngau-nhien")]
        //public async Task<IActionResult> OnLuyenNgauNhien(
        //    [FromQuery] int? idCapDo = 1,
        //    [FromQuery] int loaiOnLuyen = 3,
        //    [FromQuery] int? soLuong = 20)
        //{
        //    try
        //    {
        //        var userId = GetCurrentUserId();

        //        // Bỏ Unauthorized. Nếu không login, userId sẽ null.
        //        bool isGuest = userId == null;

        //        bool laTatCaCapDo = !idCapDo.HasValue || idCapDo.Value == 0;
        //        bool layTatCaSoLuong = !soLuong.HasValue || soLuong.Value <= 0;

        //        // 1. Khởi tạo Query danh sách ID đã thuộc (Rỗng nếu là khách)
        //        IQueryable<int> queryDaThuoc;
        //        if (!isGuest)
        //        {
        //            queryDaThuoc = _context.TuVungNguoiDungs
        //                .AsNoTracking()
        //                .Where(uv => uv.IdUser == userId.Value && uv.DaThuoc == true)
        //                .Select(uv => uv.IdTuVung);
        //        }
        //        else
        //        {
        //            // Nếu là khách, tạo một query trả về tập rỗng để các câu lệnh .Contains phía dưới không bị lỗi
        //            queryDaThuoc = _context.TuVungNguoiDungs.AsNoTracking().Where(x => false).Select(x => x.IdTuVung);
        //        }

        //        // 2. Base query cho toàn bộ từ vựng
        //        var baseTuVungQuery = _context.TuVungs.AsNoTracking();
        //        if (!laTatCaCapDo)
        //        {
        //            baseTuVungQuery = baseTuVungQuery.Where(tv => tv.IdCapDo == idCapDo.Value);
        //        }

        //        // 3. Lấy list ID Đã thuộc và Chưa thuộc
        //        // Đối với khách: listIdsDaThuoc sẽ luôn rỗng, listIdsChuaThuoc sẽ chứa tất cả
        //        var listIdsDaThuoc = await baseTuVungQuery
        //            .Where(tv => queryDaThuoc.Contains(tv.IdTuVung))
        //            .Select(tv => tv.IdTuVung)
        //            .ToListAsync();

        //        var listIdsChuaThuoc = await baseTuVungQuery
        //            .Where(tv => !queryDaThuoc.Contains(tv.IdTuVung))
        //            .Select(tv => tv.IdTuVung)
        //            .ToListAsync();

        //        // Xáo trộn ngẫu nhiên
        //        var rng = new Random();
        //        listIdsDaThuoc = listIdsDaThuoc.OrderBy(_ => rng.Next()).ToList();
        //        listIdsChuaThuoc = listIdsChuaThuoc.OrderBy(_ => rng.Next()).ToList();

        //        // 4. Phân bổ số lượng theo lựa chọn
        //        var finalIds = new List<int>();
        //        int tongSoCachLay = layTatCaSoLuong ? int.MaxValue : soLuong.Value;

        //        switch (loaiOnLuyen)
        //        {
        //            case 1: // Chỉ lấy Đã thuộc (Khách chọn cái này sẽ ra rỗng -> Đúng logic)
        //                finalIds = listIdsDaThuoc.Take(tongSoCachLay).ToList();
        //                break;

        //            case 2: // Chỉ lấy Chưa thuộc (Khách sẽ lấy từ đây)
        //                finalIds = listIdsChuaThuoc.Take(tongSoCachLay).ToList();
        //                break;

        //            case 3: // Trộn 50/50
        //                if (layTatCaSoLuong)
        //                {
        //                    finalIds.AddRange(listIdsDaThuoc);
        //                    finalIds.AddRange(listIdsChuaThuoc);
        //                }
        //                else
        //                {
        //                    int nuaSoLuong = tongSoCachLay / 2;
        //                    var layDaThuoc = listIdsDaThuoc.Take(nuaSoLuong).ToList();

        //                    int soLuongChuaThuocCanLay = tongSoCachLay - layDaThuoc.Count;
        //                    var layChuaThuoc = listIdsChuaThuoc.Take(soLuongChuaThuocCanLay).ToList();

        //                    // Bù đắp nếu một bên thiếu (Khách sẽ bù hết sang "Chưa thuộc")
        //                    if (layChuaThuoc.Count < soLuongChuaThuocCanLay)
        //                    {
        //                        int thieu = soLuongChuaThuocCanLay - layChuaThuoc.Count;
        //                        layDaThuoc.AddRange(listIdsDaThuoc.Skip(nuaSoLuong).Take(thieu));
        //                    }

        //                    finalIds.AddRange(layDaThuoc);
        //                    finalIds.AddRange(layChuaThuoc);
        //                }
        //                break;
        //        }

        //        if (!finalIds.Any())
        //            return Ok(new { success = false, message = "Không tìm thấy dữ liệu phù hợp." });

        //        // 5. Query lấy chi tiết và Map sang Model
        //        var result = await _context.TuVungs
        //            .Where(tv => finalIds.Contains(tv.IdTuVung))
        //            .Include(tv => tv.CauViDus)
        //            .AsNoTracking()
        //            .Select(tv => new TuVungModes
        //            {
        //                IdTuVung = tv.IdTuVung,
        //                ChuHan = tv.ChuHan,
        //                Pinyin = tv.Pinyin,
        //                NghiaViet = tv.NghiaViet,
        //                LoaiTu = tv.LoaiTu,
        //                AmThanhPath = tv.AmThanhPath,
        //                IdCapDo = tv.IdCapDo,
        //                AmHan = tv.AmHan,
        //                // Check xem từ này có nằm trong list đã thuộc không
        //                IsLearned = !isGuest && queryDaThuoc.Contains(tv.IdTuVung),
        //                CauViDus = tv.CauViDus.Select(vd => new CauViDuModes
        //                {
        //                    NoiDungHan = vd.NoiDungHan,
        //                    NoiDungPinyin = vd.NoiDungPinyin,
        //                    NoiDungViet = vd.NoiDungViet,
        //                    AmThanh = vd.AmThanhPath
        //                }).ToList()
        //            })
        //            .ToListAsync();

        //        // Trộn ngẫu nhiên lần cuối để vị trí các từ Đã thuộc/Chưa thuộc đan xen nhau
        //        var finalData = result.OrderBy(_ => rng.Next()).ToList();

        //        return Ok(new { success = true, data = finalData });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Lỗi API ôn luyện ngẫu nhiên");
        //        return StatusCode(500, new { success = false, message = "Lỗi hệ thống." });
        //    }
        //}

        [AllowAnonymous] // Cho phép khách truy cập
        [HttpGet("on-luyen-ngau-nhien")]
        public async Task<IActionResult> OnLuyenNgauNhien(
     [FromQuery] int? idCapDo = 1,
     [FromQuery] int loaiOnLuyen = 3,
     [FromQuery] int? soLuong = 20)
        {
            try
            {
                var userId = GetCurrentUserId();
                bool isGuest = userId == null;

                // --- CẤU HÌNH SỐ LƯỢNG TỪ ---
                bool laTatCaCapDo = !idCapDo.HasValue || idCapDo.Value == 0;
                bool layTatCaSoLuong = !soLuong.HasValue || soLuong.Value <= 0;

                int tongSoCachLay = layTatCaSoLuong ? int.MaxValue : soLuong.Value;

                // 1. Khởi tạo Query danh sách ID đã thuộc (Rỗng nếu là khách)
                IQueryable<int> queryDaThuoc;
                if (!isGuest)
                {
                    queryDaThuoc = _context.TuVungNguoiDungs
                        .AsNoTracking()
                        .Where(uv => uv.IdUser == userId.Value && uv.DaThuoc == true)
                        .Select(uv => uv.IdTuVung);
                }
                else
                {
                    queryDaThuoc = _context.TuVungNguoiDungs.AsNoTracking().Where(x => false).Select(x => x.IdTuVung);
                }

                // 2. Base query cho toàn bộ từ vựng
                var baseTuVungQuery = _context.TuVungs.AsNoTracking();
                if (!laTatCaCapDo)
                {
                    baseTuVungQuery = baseTuVungQuery.Where(tv => tv.IdCapDo == idCapDo.Value);
                }

                // 3. Lấy list ID Đã thuộc và Chưa thuộc
                var listIdsDaThuoc = await baseTuVungQuery
                    .Where(tv => queryDaThuoc.Contains(tv.IdTuVung))
                    .Select(tv => tv.IdTuVung)
                    .ToListAsync();

                var listIdsChuaThuoc = await baseTuVungQuery
                    .Where(tv => !queryDaThuoc.Contains(tv.IdTuVung))
                    .Select(tv => tv.IdTuVung)
                    .ToListAsync();

                // Xáo trộn ngẫu nhiên danh sách ID ban đầu
                var rng = new Random();
                listIdsDaThuoc = listIdsDaThuoc.OrderBy(_ => rng.Next()).ToList();
                listIdsChuaThuoc = listIdsChuaThuoc.OrderBy(_ => rng.Next()).ToList();

                // 4. Phân bổ số lượng theo lựa chọn
                var finalIds = new List<int>();

                switch (loaiOnLuyen)
                {
                    case 1:
                        finalIds = listIdsDaThuoc.Take(tongSoCachLay).ToList();
                        break;
                    case 2:
                        finalIds = listIdsChuaThuoc.Take(tongSoCachLay).ToList();
                        break;
                    case 3:
                        if (layTatCaSoLuong)
                        {
                            finalIds.AddRange(listIdsDaThuoc);
                            finalIds.AddRange(listIdsChuaThuoc);
                        }
                        else
                        {
                            int nuaSoLuong = tongSoCachLay / 2;
                            var layDaThuoc = listIdsDaThuoc.Take(nuaSoLuong).ToList();
                            int soLuongChuaThuocCanLay = tongSoCachLay - layDaThuoc.Count;
                            var layChuaThuoc = listIdsChuaThuoc.Take(soLuongChuaThuocCanLay).ToList();

                            if (layChuaThuoc.Count < soLuongChuaThuocCanLay)
                            {
                                int thieu = soLuongChuaThuocCanLay - layChuaThuoc.Count;
                                layDaThuoc.AddRange(listIdsDaThuoc.Skip(nuaSoLuong).Take(thieu));
                            }

                            finalIds.AddRange(layDaThuoc);
                            finalIds.AddRange(layChuaThuoc);
                        }
                        break;
                }

                if (!finalIds.Any())
                    return Ok(new { success = false, message = "Không tìm thấy dữ liệu phù hợp." });

                // 5. Query lấy chi tiết và Map sang Model
                var result = await _context.TuVungs
                    .Where(tv => finalIds.Contains(tv.IdTuVung))
                    .Include(tv => tv.CauViDus)
                    .AsNoTracking()
                    .Select(tv => new TuVungModes
                    {
                        IdTuVung = tv.IdTuVung,
                        ChuHan = tv.ChuHan,
                        Pinyin = tv.Pinyin,
                        NghiaViet = tv.NghiaViet,
                        LoaiTu = tv.LoaiTu,
                        AmThanhPath = tv.AmThanhPath,
                        IdCapDo = tv.IdCapDo,
                        AmHan = tv.AmHan,
                        IsLearned = !isGuest && queryDaThuoc.Contains(tv.IdTuVung)
                    })
                    .ToListAsync();

                // 6. Trộn ngẫu nhiên lần cuối và gán chiều ôn luyện
                var finalData = result.OrderBy(_ => rng.Next()).ToList();

                foreach (var item in finalData)
                {
                    item.KieuCauHoi = rng.Next(1, 3);
                }

                return Ok(new { success = true, data = finalData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi API ôn luyện ngẫu nhiên");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống." });
            }
        }

        [AllowAnonymous]
        [HttpPost("kiem-tra-dap-an")]
        public async Task<IActionResult> KiemTraDapAn([FromBody] KiemTraDapAnRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.CauTraLoi))
                {
                    return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });
                }

                // Lấy từ vựng từ DB
                var tuVung = await _context.TuVungs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(tv => tv.IdTuVung == request.IdTuVung);

                if (tuVung == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy từ vựng." });
                }

                bool isCorrect = false;

                // Chuẩn hóa câu trả lời của người dùng (xóa ngoặc, xóa khoảng trắng, viết thường)
                string inputCleaned = CleanString(request.CauTraLoi);

                if (request.KieuCauHoi == 1) // Hỏi chữ Hán -> Trả lời Nghĩa Việt
                {
                    // Tách chuỗi nghĩa trong DB bằng dấu phẩy, chấm phẩy hoặc dấu gạch đứng '|'
                    var cacNghia = tuVung.NghiaViet.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var nghia in cacNghia)
                    {
                        if (CheckMatchWithParentheses(nghia, inputCleaned))
                        {
                            isCorrect = true;
                            break;
                        }
                    }
                }
                else if (request.KieuCauHoi == 2) // Hỏi Nghĩa Việt -> Trả lời Chữ Hán hoặc Pinyin
                {
                    // Tách các từ đồng nghĩa Chữ Hán bằng dấu gạch đứng '|'
                    var cacChuHan = tuVung.ChuHan.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var chuHan in cacChuHan)
                    {
                        if (CheckMatchWithParentheses(chuHan, inputCleaned))
                        {
                            isCorrect = true;
                            break;
                        }
                    }

                    // Nếu chữ Hán chưa đúng, kiểm tra tiếp với Pinyin
                    if (!isCorrect && !string.IsNullOrWhiteSpace(tuVung.Pinyin))
                    {
                        var cacPinyin = tuVung.Pinyin.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var pinyin in cacPinyin)
                        {
                            if (CheckMatchWithParentheses(pinyin, inputCleaned))
                            {
                                isCorrect = true;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    return BadRequest(new { success = false, message = "Kiểu câu hỏi không hợp lệ." });
                }

                return Ok(new
                {
                    success = true,
                    isCorrect = isCorrect,
                    // Trả về đáp án chuẩn để Frontend hiển thị nếu người dùng trả lời sai
                    correctAnswer = request.KieuCauHoi == 1 ? tuVung.NghiaViet : tuVung.ChuHan
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi API kiểm tra đáp án");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống." });
            }
        }

        /// <summary>
        /// Hàm xử lý logic: DB chứa A (B), người dùng nhập A, B hoặc AB đều chấp nhận
        /// </summary>
        private bool CheckMatchWithParentheses(string dbValue, string inputCleaned)
        {
            if (string.IsNullOrWhiteSpace(dbValue)) return false;

            // 1. Khớp toàn bộ chuỗi (sau khi đã xóa ngoặc) -> Khách nhập AB
            if (CleanString(dbValue) == inputCleaned) return true;

            // Pattern tìm nội dung trong ngoặc đơn (hỗ trợ cả ngoặc tiếng Anh () và tiếng Trung （）)
            string pattern = @"\((.*?)\)|（(.*?)）";

            if (Regex.IsMatch(dbValue, pattern))
            {
                // 2. Lấy phần BÊN NGOÀI ngoặc (Khách nhập A)
                string outside = Regex.Replace(dbValue, pattern, "").Trim();
                if (!string.IsNullOrWhiteSpace(outside) && CleanString(outside) == inputCleaned)
                {
                    return true;
                }

                // 3. Lấy phần BÊN TRONG ngoặc (Khách nhập B)
                var matches = Regex.Matches(dbValue, pattern);
                foreach (Match match in matches)
                {
                    // match.Groups[1] là nội dung của ngoặc (), match.Groups[2] là của ngoặc （）
                    string inside = !string.IsNullOrWhiteSpace(match.Groups[1].Value)
                        ? match.Groups[1].Value
                        : match.Groups[2].Value;

                    if (!string.IsNullOrWhiteSpace(inside) && CleanString(inside) == inputCleaned)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Hàm bổ trợ dùng để xóa bỏ khoảng trắng thừa, viết thường 
        /// và xóa sạch các loại dấu ngoặc đơn (), （）
        /// </summary>
        private string CleanString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            return input
                .Replace("(", "")
                .Replace(")", "")
                .Replace("（", "")  // Ngoặc mở Trung Quốc
                .Replace("）", "")  // Ngoặc đóng Trung Quốc
                .Trim()
                .ToLower();
        }

        [HttpPost("luu-ket-qua-on-luyen")]
        public async Task<IActionResult> LuuKetQuaOnLuyen([FromBody] List<int> listIdTuVungDung)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return Unauthorized(new { success = false, message = "Vui lòng đăng nhập để lưu kết quả." });

                if (listIdTuVungDung == null || !listIdTuVungDung.Any())
                {
                    return Ok(new { success = true, message = "Không có từ vựng nào cần lưu." });
                }

                // 1. Lấy những bản ghi từ vựng người dùng đã có trong CSDL
                var tuVungDaCo = await _context.TuVungNguoiDungs
                    .Where(uv => uv.IdUser == userId.Value && listIdTuVungDung.Contains(uv.IdTuVung))
                    .ToListAsync();

                var idsDaCo = tuVungDaCo.Select(x => x.IdTuVung).ToList();

                // 2. Cập nhật những từ đã có sẵn (chuyển DaThuoc thành true)
                foreach (var item in tuVungDaCo)
                {
                    if (item.DaThuoc != true)
                    {
                        item.DaThuoc = true;
                        item.NgayDanhDau = DateTime.Now;
                    }
                }

                // 3. Thêm mới những từ chưa từng có trong bảng TuVungNguoiDung
                var idsCanThemMoi = listIdTuVungDung.Except(idsDaCo).ToList();

                var listThemMoi = idsCanThemMoi.Select(id => new TuVungNguoiDung
                {
                    IdUser = userId.Value,
                    IdTuVung = id,
                    DaThuoc = true,
                    NgayDanhDau = DateTime.Now
                });

                if (listThemMoi.Any())
                {
                    await _context.TuVungNguoiDungs.AddRangeAsync(listThemMoi);
                }

                // 4. Lưu vào cơ sở dữ liệu
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = $"Đã lưu thành công {listIdTuVungDung.Count} từ vựng vào danh sách Đã Thuộc." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu kết quả ôn luyện từ vựng.");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi lưu kết quả." });
            }
        }

        [HttpPost("import-excel-mini")]
        public async Task<IActionResult> ImportExcelMini(IFormFile file)
        {
            if (file == null || file.Length <= 0)
                return BadRequest(new { success = false, message = "Vui lòng chọn file!" });

            try
            {
                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    stream.Position = 0; // Đưa con trỏ về đầu stream

                    // Đọc toàn bộ file excel thành một danh sách các object
                    var rows = stream.Query<TuVungExcelModel>().ToList();
                    int countSuccess = 0;

                    foreach (var row in rows)
                    {
                        if (string.IsNullOrEmpty(row.ChuHan)) continue;

                        // 1. Thêm từ vựng
                        var tuVung = new TuVung
                        {
                            ChuHan = row.ChuHan,
                            Pinyin = row.Pinyin,
                            NghiaViet = row.NghiaViet,
                            LoaiTu = row.LoaiTu,
                            AmHan = row.AmHan,
                            IdCapDo = row.IdCapDo,
                            AmThanhPath = row.AmThanhPath

                        };

                        _context.TuVungs.Add(tuVung);

                        // Lưu để có ID của từ vựng cho bảng câu ví dụ
                        await _context.SaveChangesAsync();

                        // 2. Thêm câu ví dụ (nếu có dữ liệu ở cột ví dụ)
                        if (!string.IsNullOrEmpty(row.NoiDungHan))
                        {
                            var cauViDu = new CauViDu
                            {
                                IdTuVung = tuVung.IdTuVung,
                                NoiDungHan = row.NoiDungHan,
                                NoiDungPinyin = row.NoiDungPinyin,
                                NoiDungViet = row.NoiDungViet,
                                AmThanhPath = row.AmThanhPathViDu
                            };
                            _context.CauViDus.Add(cauViDu);
                        }

                        countSuccess++;
                    }

                    await _context.SaveChangesAsync();

                    // Làm mới Cache hệ thống
                    _resetCacheSignal.Cancel();
                    _resetCacheSignal = new CancellationTokenSource();

                    return Ok(new { success = true, message = $"Đã nhập thành công {countSuccess} bộ từ vựng và ví dụ." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi dùng MiniExcel import");
                return StatusCode(500, new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpGet("chi-tiet-tu-vung/{id}")]
        public async Task<IActionResult> ChiTietTuVung(int id)
        {
            var tuVung = await _context.TuVungs
                .Include(t => t.CauViDus)
                .FirstOrDefaultAsync(t => t.IdTuVung == id);
            if (tuVung == null) return NotFound(new { success = false, message = "Không tìm thấy từ vựng." });
            var result = new TuVungModes
            {
                IdTuVung = tuVung.IdTuVung,
                ChuHan = tuVung.ChuHan,
                Pinyin = tuVung.Pinyin,
                NghiaViet = tuVung.NghiaViet,
                LoaiTu = tuVung.LoaiTu,
                AmThanhPath = tuVung.AmThanhPath,
                IdCapDo = tuVung.IdCapDo,
                AmHan = tuVung.AmHan,
                CauViDus = tuVung.CauViDus.Select(vd => new CauViDuModes
                {
                    NoiDungHan = vd.NoiDungHan,
                    NoiDungPinyin = vd.NoiDungPinyin,
                    NoiDungViet = vd.NoiDungViet,
                    AmThanh = vd.AmThanhPath
                }).ToList()
            };
            return Ok(new { success = true, data = result });
        }

        [HttpPut("sua-tu-vung/{id}")]
        public async Task<IActionResult> SuaTuVung(int id, [FromBody] EditTuVung model)
        {
            if (id != model.IdTuVung) return BadRequest(new { success = false, message = "ID không khớp." });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var tuVung = await _context.TuVungs
                    .Include(t => t.CauViDus)
                    .FirstOrDefaultAsync(t => t.IdTuVung == id);

                if (tuVung == null) return NotFound(new { success = false, message = "Không tìm thấy từ vựng." });

                // 1. Kiểm tra và cập nhật thông tin chính
                // SetValues sẽ chỉ đánh dấu Modified cho các property có giá trị khác biệt
                _context.Entry(tuVung).CurrentValues.SetValues(model);

                // 2. Xử lý câu ví dụ (Cập nhật thông minh)
                if (model.CauViDus != null)
                {
                    // Danh sách ID mới gửi lên từ client
                    var modelViduIds = model.CauViDus.Select(v => v.IdViDu).Where(vId => vId > 0).ToList();

                    // A. Xóa những câu ví dụ không còn tồn tại trong model gửi lên
                    var toRemove = tuVung.CauViDus.Where(v => !modelViduIds.Contains(v.IdViDu)).ToList();
                    _context.CauViDus.RemoveRange(toRemove);

                    foreach (var modelVd in model.CauViDus)
                    {
                        var existingVd = tuVung.CauViDus.FirstOrDefault(v => v.IdViDu == modelVd.IdViDu && v.IdViDu != 0);

                        if (existingVd != null)
                        {
                            // B. Nếu đã tồn tại -> Chỉ cập nhật nếu có sự thay đổi nội dung
                            _context.Entry(existingVd).CurrentValues.SetValues(modelVd);
                        }
                        else
                        {
                            // C. Nếu là câu ví dụ mới (ID = 0 hoặc không tìm thấy) -> Thêm mới
                            tuVung.CauViDus.Add(new CauViDu
                            {
                                IdTuVung = id,
                                NoiDungHan = modelVd.NoiDungHan,
                                NoiDungPinyin = modelVd.NoiDungPinyin,
                                NoiDungViet = modelVd.NoiDungViet,
                                AmThanhPath = modelVd.AmThanhPath
                            });
                        }
                    }
                }

                // Kiểm tra xem thực sự có thay đổi nào được track bởi EF không
                if (!_context.ChangeTracker.HasChanges())
                {
                    return Ok(new { success = true, message = "Không có thay đổi nào được thực hiện." });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 3. Reset Cache
                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                return Ok(new { success = true, message = "Cập nhật thành công!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Lỗi cập nhật ID: {id}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpDelete("xoa-tu-vung/{id}")]
        public async Task<IActionResult> XoaTuVung(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Lấy từ vựng bao gồm cả các mối quan hệ Many-to-Many nếu có
                var tuVung = await _context.TuVungs
                    .Include(t => t.IdCauHois)
                    .Include(bt=>bt.IdCauHois)
                    .FirstOrDefaultAsync(t => t.IdTuVung == id);

                if (tuVung == null) return NotFound(new { success = false, message = "Từ vựng không tồn tại." });

                // 1. Xóa câu ví dụ
                var cauViDus = _context.CauViDus.Where(x => x.IdTuVung == id);
                _context.CauViDus.RemoveRange(cauViDus);

                // 2. Xóa liên kết người dùng (Lịch sử học tập)
                var lienQuanNguoiDung = _context.TuVungNguoiDungs.Where(x => x.IdTuVung == id);
                _context.TuVungNguoiDungs.RemoveRange(lienQuanNguoiDung);

              

                // 3. Xử lý bảng Câu hỏi (Nếu là quan hệ n-n, chỉ cần xóa liên kết trong bảng trung gian)
                tuVung.IdCauHois.Clear();

                // 4. Xóa từ vựng chính
                _context.TuVungs.Remove(tuVung);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Làm mới cache
                _resetCacheSignal.Cancel();
                _resetCacheSignal = new CancellationTokenSource();

                return Ok(new { success = true, message = "Đã xóa vĩnh viễn từ vựng và các dữ liệu liên quan." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Lỗi khi xóa ID: {id}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi xóa dữ liệu." });
            }
        }


        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId)) return userId;

            var sessionUserId = HttpContext.Session.GetString("MaNguoiDung");
            if (!string.IsNullOrEmpty(sessionUserId) && int.TryParse(sessionUserId, out int sUserId)) return sUserId;

            return null;
        }

        // 2. Chỉnh sửa hàm ClearCache để chỉ xóa từ vựng
        [HttpPost("clear-vocabulary-cache")]
        public IActionResult ClearVocabularyCache()
        {
            _vocabularyCacheSignal.Cancel();
            _vocabularyCacheSignal = new CancellationTokenSource();
            return Ok("Cache danh sách từ vựng đã được làm mới.");
        }
    }
}