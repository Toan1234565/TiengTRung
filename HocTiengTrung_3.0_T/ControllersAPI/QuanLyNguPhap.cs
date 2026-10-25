using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12;
using HocTiengTrung_3._0_T.Models12.BaiTap;
using HocTiengTrung_3._0_T.Models12.NguPhap;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using MiniExcelLibs;
using OfficeOpenXml;

namespace HocTiengTrung_3._0_T.ControllersAPI
{
    [Route("api/quanlynguphap")]
    [ApiController]
    public class QuanLyNguPhap : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<QuanLyNguPhap> _logger;
        private readonly IUserService _userService;
        // Token dùng để Clear Cache đồng loạt khi có thay đổi dữ liệu
        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();

        public QuanLyNguPhap(TmdtContext context, IMemoryCache cache, ILogger<QuanLyNguPhap> logger, IUserService userService)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _userService = userService;
        }

        // Phương thức hỗ trợ làm mới Cache
        private void ClearCache()
        {
            var oldSignal = _resetCacheSignal;
            _resetCacheSignal = new CancellationTokenSource();
            oldSignal.Cancel();
            oldSignal.Dispose();
            _logger.LogInformation("Toàn bộ Cache của Ngữ Pháp đã được làm mới.");
        }

        // 1. Lấy danh sách ngữ pháp (Có Phân trang + Tìm kiếm + Cache + Lọc theo Mã/ID Trung tâm)
        [HttpGet("danh-sach-ngu-phap")]
        public async Task<IActionResult> DanhSachNguPhap(
            [FromQuery] int? capdo,
            [FromQuery] string? search,
            [FromQuery] int? idTrungTam,
            [FromQuery] string? maTrungTam,
            [FromQuery] int pagesize = 10,
            [FromQuery] int page = 1)
        {
            try
            {
                if (page < 1) page = 1;
                if (pagesize < 1) pagesize = 10;

                // 1. Xác định IdTrungTam từ query hoặc tự động lấy từ Claims/Session người dùng
                int? targetTrungTamId = (idTrungTam.HasValue && idTrungTam.Value > 0) ? idTrungTam.Value : null;

                if (!targetTrungTamId.HasValue)
                {
                    string? idTrungTamClaim = User.FindFirst("IdTrungTam")?.Value
                                              ?? HttpContext.Session.GetString("IdTrungTam");

                    if (int.TryParse(idTrungTamClaim, out int parsedTtId) && parsedTtId > 0)
                    {
                        targetTrungTamId = parsedTtId;
                    }
                    else
                    {
                        string? maTrungTamClaim = !string.IsNullOrWhiteSpace(maTrungTam)
                            ? maTrungTam.Trim()
                            : (User.FindFirst("MaTrungTam")?.Value ?? HttpContext.Session.GetString("MaTrungTam"));

                        if (!string.IsNullOrWhiteSpace(maTrungTamClaim))
                        {
                            var tt = await _context.TrungTams
                                .AsNoTracking()
                                .FirstOrDefaultAsync(t => t.MaTrungTam == maTrungTamClaim);
                            if (tt != null)
                            {
                                targetTrungTamId = tt.IdTrungTam;
                            }
                        }
                    }
                }

                // 2. Tạo Cache Key có gắn IdTrungTam để tránh cache chéo giữa các cơ sở
                string ttCacheKey = targetTrungTamId.HasValue ? $"TT_{targetTrungTamId.Value}" : "TT_Global";
                var cacheKey = $"DanhSachNguPhap_{capdo}_{search}_{ttCacheKey}_{pagesize}_{page}";

                if (!_cache.TryGetValue(cacheKey, out object result))
                {
                    var query = _context.NguPhaps.AsNoTracking().AsQueryable();

                    // Lọc theo Trung tâm
                    if (targetTrungTamId.HasValue)
                    {
                        query = query.Where(np => np.IdTrungTam == targetTrungTamId.Value);
                    }
                    else
                    {
                        query = query.Where(np => np.IdTrungTam == null || np.IdTrungTam == 0);
                    }

                    if (capdo.HasValue && capdo > 0)
                        query = query.Where(np => np.IdCapDo == capdo.Value);

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        string s = search.Trim();
                        query = query.Where(np => np.TenNguPhap.Contains(s) || np.CauTruc.Contains(s));
                    }

                    var totalItems = await query.CountAsync();

                    var data = await query
                        .OrderBy(np => np.IdNguPhap)
                        .Skip((page - 1) * pagesize)
                        .Take(pagesize)
                        .Select(np => new NguPhapModels
                        {
                            IdNguPhap = np.IdNguPhap,
                            TenNguPhap = np.TenNguPhap,
                            CauTruc = np.CauTruc,
                            GiaiThich = np.GiaiThich,
                            ViDu = np.ViDu,
                            IdCapDo = np.IdCapDo,
                            
                        }).ToListAsync();

                    result = new
                    {
                        success = true,
                        idTrungTam = targetTrungTamId,
                        totalItems,
                        data,
                        page,
                        pageSize = pagesize
                    };

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                    _cache.Set(cacheKey, result, cacheOptions);
                    _logger.LogInformation($"Cache miss: {cacheKey}");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách ngữ pháp theo trung tâm");
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi hệ thống." });
            }
        }

        // // chi tiết ngữ pháp theo ID (Có cache riêng cho chi tiết)
        // 2. Lấy thông tin chi tiết một ngữ pháp (Bao gồm câu hỏi và đáp án)
        [HttpGet("chi-tiet/{id}")]
        public async Task<IActionResult> ChiTiet(int id)
        {
            try
            {
                // 1. Tìm ngữ pháp theo ID và tải kèm câu hỏi, đáp án
                var nguPhap = await _context.NguPhaps
                    .AsNoTracking()
                    .Include(np => np.IdCauHois)
                        .ThenInclude(ch => ch.DapAns)
                    .FirstOrDefaultAsync(np => np.IdNguPhap == id);

                if (nguPhap == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy ngữ pháp yêu cầu." });
                }

                // 2. Map dữ liệu sang DTO (NguPhapModels) để trả về client
                var result = new NguPhapModels
                {
                    IdNguPhap = nguPhap.IdNguPhap,
                    TenNguPhap = nguPhap.TenNguPhap,
                    CauTruc = nguPhap.CauTruc,
                    GiaiThich = nguPhap.GiaiThich,
                    ViDu = nguPhap.ViDu,
                    IdCapDo = nguPhap.IdCapDo,

                    SelectedCauHoiIds = nguPhap.IdCauHois.Select(ch => new CauHoiModels
                    {
                        IdCauHoi = ch.IdCauHoi,
                        NoiDung = ch.NoiDung,
                        IdLoai = ch.IdLoai,
                        GiaiThich = ch.GiaiThich,
                        AmThanhPath = ch.AmThanhPath,
                        DapAns = ch.DapAns.Select(da => new DapAnModels
                        {
                            IdDapAn = da.IdDapAn,
                            NoiDungDapAn = da.NoiDungDapAn,
                            LaDapAnDung = da.LaDapAnDung ?? true
                        }).ToList()
                    }).ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi lấy chi tiết ngữ pháp ID: {id}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi tải dữ liệu." });
            }
        }
        [HttpPut("cap-nhat/{id}")]
        public async Task<IActionResult> CapNhat(int id, [FromBody] NguPhapUpdata updata)
        {
            // 1. Lấy ngữ pháp kèm theo danh sách câu hỏi và đáp án hiện tại
            var np = await _context.NguPhaps
                .Include(n => n.IdCauHois)
                    .ThenInclude(ch => ch.DapAns)
                .FirstOrDefaultAsync(n => n.IdNguPhap == id);

            if (np == null) return NotFound(new { success = false, message = "Không tìm thấy ngữ pháp." });

            try
            {
                // 2. Cập nhật thông tin cơ bản của Ngữ pháp
                np.TenNguPhap = updata.TenNguPhap;
                np.CauTruc = updata.CauTruc;
                np.GiaiThich = updata.GiaiThich;
                np.ViDu = updata.ViDu;
                np.IdCapDo = updata.IdCapDo;

                // 3. Xử lý danh sách câu hỏi (Bài tập)
                // Lấy danh sách ID câu hỏi mới từ Client
                var clientQuestionIds = updata.SelectedCauHoiIds
                    .Where(x => x.IdCauHoi > 0)
                    .Select(x => x.IdCauHoi)
                    .ToList();

                // Xóa các liên kết (hoặc câu hỏi) cũ không có trong danh sách mới gửi lên
                np.IdCauHois = np.IdCauHois
                    .Where(ch => clientQuestionIds.Contains(ch.IdCauHoi))
                    .ToList();

                foreach (var item in updata.SelectedCauHoiIds)
                {
                    if (item.IdCauHoi > 0)
                    {
                        // TRƯỜNG HỢP: Cập nhật câu hỏi đã tồn tại
                        var existingCH = await _context.CauHois
                            .Include(c => c.DapAns)
                            .FirstOrDefaultAsync(c => c.IdCauHoi == item.IdCauHoi);

                        if (existingCH != null)
                        {
                            existingCH.NoiDung = item.NoiDung;
                            existingCH.IdLoai = item.IdLoai;

                            // Làm mới danh sách đáp án cho câu hỏi này
                            _context.DapAns.RemoveRange(existingCH.DapAns);
                            existingCH.DapAns = item.DapAns.Select(da => new DapAn
                            {
                                NoiDungDapAn = da.NoiDungDapAn,
                                LaDapAnDung = da.LaDapAnDung
                            }).ToList();
                        }
                    }
                    else
                    {
                        // TRƯỜNG HỢP: Thêm mới câu hỏi hoàn toàn (IdCauHoi == 0)
                        var newCH = new CauHoi
                        {
                            NoiDung = item.NoiDung,
                            IdLoai = item.IdLoai,
                            DapAns = item.DapAns.Select(da => new DapAn
                            {
                                NoiDungDapAn = da.NoiDungDapAn,
                                LaDapAnDung = da.LaDapAnDung
                            }).ToList()
                        };
                        np.IdCauHois.Add(newCH);
                    }
                }

                // 4. Lưu thay đổi
                await _context.SaveChangesAsync();

                // Xóa cache (nếu có)
                ClearCache();

                return Ok(new { success = true, message = "Cập nhật ngữ pháp và bài tập thành công." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật ngữ pháp");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }
       
        // 6. Lấy danh sách câu hỏi theo ID Ngữ pháp và Loại câu hỏi (Dùng cho chức năng "Làm bài tập")
        [HttpGet("bai-tap-theo-ngu-phap/{id}")]
        public async Task<IActionResult> GetBaiTapTheoNguPhap(int id, [FromQuery] int? idLoai = 1)
        {
            try
            {
                string cacheKey = $"FullQuestions_NguPhap_{id}";

                if (!_cache.TryGetValue(cacheKey, out NguPhap nguPhap))
                {
                    nguPhap = await _context.NguPhaps
                        .AsNoTracking()
                        .Include(np => np.IdCauHois)
                            .ThenInclude(ch => ch.DapAns)
                        .FirstOrDefaultAsync(np => np.IdNguPhap == id);

                    if (nguPhap == null)
                        return NotFound(new { success = false, message = "Không tìm thấy ngữ pháp." });

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                    _cache.Set(cacheKey, nguPhap, cacheOptions);
                }

                // ================== LOGIC KIỂM TRA GIỚI HẠN VIP ==================
                // Lấy danh sách bài học cùng cấp độ để check vị trí (Nên cache hoặc truy vấn nhanh bằng Select)
                var danhSachIdCungCapDo = await _context.NguPhaps
                    .AsNoTracking()
                    .Where(np => np.IdCapDo == nguPhap.IdCapDo)
                    .OrderBy(np => np.IdNguPhap) // Sắp xếp tăng dần theo ID để định vị bài học
                    .Select(np => np.IdNguPhap)
                    .ToListAsync();

                int viTriBaiHoc = danhSachIdCungCapDo.IndexOf(id) + 1;

                // Nếu là bài học thứ 3 trở lên, bắt buộc phải là Premium
                if (viTriBaiHoc > 2)
                {
                    bool isVip = await _userService.IsUserPremiumAsync(User, HttpContext);

                    if (!isVip)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new
                        {
                            success = false,
                            isPremiumRestricted = true,
                            message = $"Chức năng làm bài tập của bài số {viTriBaiHoc} yêu cầu tài khoản VIP. Vui lòng nâng cấp để tiếp tục học!"
                        });
                    }
                }
                // =================================================================

                // 1. Lọc theo loại câu hỏi (Trắc nghiệm = 1, Sắp xếp câu = 3, ...)
                var queryCauHois = nguPhap.IdCauHois.AsEnumerable();
                if (idLoai.HasValue && idLoai > 0)
                {
                    queryCauHois = queryCauHois.Where(ch => ch.IdLoai == idLoai.Value);
                }

                // 2. Lấy TOÀN BỘ câu hỏi (Đã bỏ .Take(10)) 
                // Vẫn giữ OrderBy Guid để xáo trộn thứ tự câu hỏi cho khách quan
                var allQuestions = queryCauHois
                    .OrderBy(ch => Guid.NewGuid())
                    .Select(ch => new CauHoiModels
                    {
                        IdCauHoi = ch.IdCauHoi,
                        NoiDung = ch.NoiDung,
                        AmThanhPath = ch.AmThanhPath,
                        GiaiThich = ch.GiaiThich,
                        IdLoai = ch.IdLoai,
                        DapAns = ch.DapAns.Select(da => new DapAnModels
                        {
                            IdDapAn = da.IdDapAn,
                            NoiDungDapAn = da.NoiDungDapAn,
                            LaDapAnDung = da.LaDapAnDung ?? false
                        }).ToList()
                    }).ToList();

                // 3. Đóng gói kết quả trả về
                var finalResult = new NguPhapModels
                {
                    IdNguPhap = nguPhap.IdNguPhap,
                    TenNguPhap = nguPhap.TenNguPhap,
                    CauTruc = nguPhap.CauTruc,
                    GiaiThich = nguPhap.GiaiThich,
                    ViDu = nguPhap.ViDu,
                    IdCapDo = nguPhap.IdCapDo,
                    SelectedCauHoiIds = allQuestions // Chứa toàn bộ câu hỏi đã lọc
                };

                return Ok(new { success = true, total = allQuestions.Count, data = finalResult });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi API bài tập ID: {id}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi tải bài tập." });
            }
        }


        [HttpPost("nhap-excel")]
        public async Task<IActionResult> NhapExcel(IFormFile file)
        {
            if (file == null || file.Length <= 0)
            {
                return BadRequest(new { success = false, message = "Vui lòng chọn file Excel hợp lệ." });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                using var stream = file.OpenReadStream();

                // =========================================================================
                // 1. ĐỌC DÒNG 1 ĐỂ LẤY MÃ TRUNG TÂM (CỘT B1) VÀ TRA CỨU ID TRUNG TÂM
                // =========================================================================
                var allRowsAsDict = stream.Query(useHeaderRow: false).ToList();
                if (!allRowsAsDict.Any())
                {
                    return BadRequest(new { success = false, message = "File Excel không có dữ liệu." });
                }

                var firstRow = allRowsAsDict[0] as IDictionary<string, object>;
                string? maTrungTam = firstRow?.Values.ElementAtOrDefault(1)?.ToString()?.Trim();

                if (string.IsNullOrWhiteSpace(maTrungTam))
                {
                    return BadRequest(new { success = false, message = "Không tìm thấy Mã trung tâm ở hàng đầu tiên (ô B1)." });
                }

                // Tìm TrungTam trong CSDL để lấy IdTrungTam kiểu int
                var trungTam = await _context.TrungTams
                    .FirstOrDefaultAsync(t => t.MaTrungTam != null && t.MaTrungTam.ToLower() == maTrungTam.ToLower());

                if (trungTam == null)
                {
                    return BadRequest(new { success = false, message = $"Cơ sở đào tạo với mã '{maTrungTam}' không tồn tại trong hệ thống." });
                }

                int idTrungTam = trungTam.IdTrungTam;

                // =========================================================================
                // 2. ĐỌC DỮ LIỆU TỪ DÒNG 2 TRỞ ĐI (DÒNG 2 LÀ TIÊU ĐỀ CỘT)
                // =========================================================================
                stream.Position = 0;
                var rows = stream.Query<NguPhapExcel>(startCell: "A2").ToList();

                if (!rows.Any())
                {
                    return BadRequest(new { success = false, message = "File không có dữ liệu nội dung từ dòng 2." });
                }

                NguPhap? currentNguPhap = null;
                int nguPhapThemMoiCount = 0;
                int nguPhapCapNhatCount = 0;
                int cauHoiCount = 0;

                foreach (var row in rows)
                {
                    // Bỏ qua dòng trống hoàn toàn
                    if (string.IsNullOrWhiteSpace(row.TenNguPhap) && string.IsNullOrWhiteSpace(row.NoiDungCauHoi))
                    {
                        continue;
                    }

                    // =========================================================================
                    // 3. XỬ LÝ LÝ THUYẾT NGỮ PHÁP (KHI CÓ TÊN NGỮ PHÁP)
                    // =========================================================================
                    if (!string.IsNullOrWhiteSpace(row.TenNguPhap))
                    {
                        string tenNP = row.TenNguPhap.Trim();

                        // Kiểm tra xem ngữ pháp này đã tồn tại trong trung tâm này chưa
                        currentNguPhap = await _context.NguPhaps
                            .FirstOrDefaultAsync(n => n.TenNguPhap == tenNP && n.IdTrungTam == idTrungTam);

                        if (currentNguPhap == null)
                        {
                            // Tạo mới bản ghi Ngữ pháp
                            currentNguPhap = new NguPhap
                            {
                                TenNguPhap = tenNP,
                                CauTruc = row.CauTruc?.Trim(),
                                GiaiThich = row.GiaiThichNP?.Trim(),
                                ViDu = row.ViDuNP?.Trim(),
                                IdCapDo = row.IdCapDo ?? 1,
                                IdTrungTam = idTrungTam // Gán IdTrungTam kiểu int hợp lệ
                            };

                            _context.NguPhaps.Add(currentNguPhap);
                            // Lưu ngay lập tức để SQL sinh IdNguPhap phục vụ liên kết câu hỏi
                            await _context.SaveChangesAsync();
                            nguPhapThemMoiCount++;
                        }
                        else
                        {
                            // Nếu ngữ pháp đã có -> cập nhật lại nội dung lý thuyết mới nhất từ Excel
                            if (!string.IsNullOrWhiteSpace(row.CauTruc))
                                currentNguPhap.CauTruc = row.CauTruc.Trim();

                            if (!string.IsNullOrWhiteSpace(row.GiaiThichNP))
                                currentNguPhap.GiaiThich = row.GiaiThichNP.Trim();

                            if (!string.IsNullOrWhiteSpace(row.ViDuNP))
                                currentNguPhap.ViDu = row.ViDuNP.Trim();

                            if (row.IdCapDo.HasValue)
                                currentNguPhap.IdCapDo = row.IdCapDo.Value;

                            _context.NguPhaps.Update(currentNguPhap);
                            await _context.SaveChangesAsync();
                            nguPhapCapNhatCount++;
                        }
                    }

                    // =========================================================================
                    // 4. XỬ LÝ CÂU HỎI BÀI TẬP BỔ TRỢ
                    // =========================================================================
                    if (!string.IsNullOrWhiteSpace(row.NoiDungCauHoi) && currentNguPhap != null)
                    {
                        var cauHoi = new CauHoi
                        {
                            NoiDung = row.NoiDungCauHoi.Trim(),
                            IdCapDo = currentNguPhap.IdCapDo,
                            IdLoai = row.IdLoai ?? 3,
                            GiaiThich = row.GiaiThichCauHoi?.Trim(),
                            IsGeneratedOnly = false,
                            TrangThai = true,
                            IdTrungTam = idTrungTam, // Khóa câu hỏi vào cơ sở hiện tại
                            IdNguPhaps = new List<NguPhap> { currentNguPhap }
                        };

                        string correctChar = row.DapAnDung?.Trim().ToUpper() ?? "A";
                        var danhSachDA = new List<(string? Content, string Char)>
                        {
                            (row.DapAnA, "A"),
                            (row.DapAnB, "B"),
                            (row.DapAnC, "C"),
                            (row.DapAnD, "D")
                        };

                        foreach (var da in danhSachDA)
                        {
                            if (string.IsNullOrWhiteSpace(da.Content)) continue;

                            cauHoi.DapAns.Add(new DapAn
                            {
                                NoiDungDapAn = da.Content.Trim(),
                                LaDapAnDung = (da.Char == correctChar)
                            });
                        }

                        _context.CauHois.Add(cauHoi);
                        cauHoiCount++;
                    }
                }

                // Lưu toàn bộ danh sách câu hỏi và đáp án
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Xóa bộ nhớ cache (nếu controller có dùng hàm ClearCache)
                ClearCache();

                return Ok(new
                {
                    success = true,
                    maTrungTam = maTrungTam,
                    idTrungTam = idTrungTam,
                    message = $"Nhập dữ liệu thành công cho trung tâm [{trungTam.TenTrungTam}]! " +
                              $"Đã thêm mới {nguPhapThemMoiCount} ngữ pháp, cập nhật {nguPhapCapNhatCount} ngữ pháp và tạo {cauHoiCount} câu hỏi bài tập."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                var innerMsg = ex.InnerException?.InnerException?.Message
                               ?? ex.InnerException?.Message
                               ?? ex.Message;
                _logger.LogError(ex, "Lỗi khi nhập Excel ngữ pháp");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = $"Lỗi hệ thống khi nhập file: {innerMsg}"
                });
            }
        }

        // 8. API Tổng hợp cho Admin: Lấy danh sách ngữ pháp kèm theo toàn bộ câu hỏi và đáp án (Có Phân trang + Cache)
        [HttpGet("admin/danh-sach-kem-bai-tap")]
        public async Task<IActionResult> AdminGetDanhSachNguPhapKemBaiTap(
            [FromQuery] int? capdo = 1,
            [FromQuery] int page = 1,
            [FromQuery] int pagesize = 5) // Mặc định lấy 5 dòng như yêu cầu
        {
            try
            {
                // Tạo key cache duy nhất dựa trên bộ lọc và số trang
                var cacheKey = $"AdminNguPhapKemBaiTap_{capdo}_{page}_{pagesize}";

                if (!_cache.TryGetValue(cacheKey, out object result))
                {
                    var query = _context.NguPhaps.AsNoTracking().AsQueryable();

                    // 1. Lọc theo cấp độ HSK
                    if (capdo.HasValue && capdo > 0)
                        query = query.Where(np => np.IdCapDo == capdo.Value);

                    // 2. Tính tổng số bản ghi trước khi phân trang
                    var totalItems = await query.CountAsync();

                    // 3. Phân trang và Map dữ liệu trực tiếp sang DTO
                    var data = await query
                        .OrderBy(np => np.IdNguPhap)
                        .Skip((page - 1) * pagesize)
                        .Take(pagesize)
                        .Select(np => new NguPhapModels
                        {
                            IdNguPhap = np.IdNguPhap,
                            TenNguPhap = np.TenNguPhap,
                            CauTruc = np.CauTruc,
                            GiaiThich = np.GiaiThich,
                            ViDu = np.ViDu,
                            IdCapDo = np.IdCapDo,

                            // Map danh sách câu hỏi đi kèm (Sắp xếp cố định cho admin dễ xem)
                            SelectedCauHoiIds = np.IdCauHois
                                .OrderBy(ch => ch.IdCauHoi)
                                .Select(ch => new CauHoiModels
                                {
                                    IdCauHoi = ch.IdCauHoi,
                                    NoiDung = ch.NoiDung,
                                    IdLoai = ch.IdLoai,
                                    GiaiThich = ch.GiaiThich,
                                    AmThanhPath = ch.AmThanhPath,

                                    // Map danh sách đáp án
                                    DapAns = ch.DapAns.Select(da => new DapAnModels
                                    {
                                        IdDapAn = da.IdDapAn,
                                        NoiDungDapAn = da.NoiDungDapAn,
                                        LaDapAnDung = da.LaDapAnDung ?? true
                                    }).ToList()
                                }).ToList()
                        }).ToListAsync();

                    // Đóng gói kết quả trả về chuẩn phân trang
                    result = new { success = true, totalItems, data, page, pageSize = pagesize };

                    // Cấu hình Cache: Hết hạn sau 30 phút hoặc tự hủy khi gọi hàm ClearCache()
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
                        .AddExpirationToken(new Microsoft.Extensions.Primitives.CancellationChangeToken(_resetCacheSignal.Token));

                    _cache.Set(cacheKey, result, cacheOptions);
                    _logger.LogInformation($"Cache miss cho Admin: {cacheKey}");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin lỗi lấy danh sách ngữ pháp kèm bài tập tổng hợp");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi tải dữ liệu admin." });
            }
        }

        // 11. API Admin: Xóa ngữ pháp và toàn bộ câu hỏi, đáp án liên quan
        // 11. API Admin: Xóa ngữ pháp và toàn bộ câu hỏi, đáp án liên quan
        [HttpDelete("admin/xoa-ngu-phap/{idNguPhap}")]
        public async Task<IActionResult> AdminXoaNguPhap(int idNguPhap)
        {
            // Sử dụng Transaction để đảm bảo tính toàn vẹn dữ liệu
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Nạp ngữ pháp kèm theo danh sách câu hỏi và đáp án
                var nguPhap = await _context.NguPhaps
                    .Include(np => np.IdCauHois)
                        .ThenInclude(ch => ch.DapAns)
                    .FirstOrDefaultAsync(np => np.IdNguPhap == idNguPhap);

                if (nguPhap == null)
                    return NotFound(new { success = false, message = "Không tìm thấy ngữ pháp cần xóa." });

                var danhSachCauHoi = nguPhap.IdCauHois.ToList();

                if (danhSachCauHoi.Any())
                {
                    // Bước 1: Thu thập và xóa sạch toàn bộ Đáp án trước để tránh lỗi khóa ngoại (FK) với Câu hỏi
                    var dapAns = danhSachCauHoi.SelectMany(ch => ch.DapAns).ToList();
                    if (dapAns.Any())
                    {
                        _context.DapAns.RemoveRange(dapAns);
                    }

                    // Bước 2: Xóa toàn bộ các Câu hỏi thuộc về ngữ pháp này
                    _context.CauHois.RemoveRange(danhSachCauHoi);
                }

                // Bước 3: Xóa chính Ngữ pháp đó
                _context.NguPhaps.Remove(nguPhap);

                // Lưu tất cả thay đổi xuống Database
                await _context.SaveChangesAsync();

                // Xác nhận hoàn thành Transaction thành công
                await transaction.CommitAsync();

                // Bước 4: Xóa Cache hệ thống để làm mới dữ liệu cho Client
                ClearCache();

                return Ok(new { success = true, message = "Đã xóa thành công ngữ pháp và tất cả câu hỏi, đáp án liên quan." });
            }
            catch (Exception ex)
            {
                // Nếu xảy ra bất kỳ lỗi gì, rollback lại toàn bộ dữ liệu như cũ
                await transaction.RollbackAsync();

                _logger.LogError(ex, $"Admin lỗi nghiêm trọng khi xóa ngữ pháp ID: {idNguPhap}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi xóa dữ liệu bài viết: " + ex.Message });
            }
        }

        // 12. API Admin: Xóa một câu hỏi cụ thể trong ngữ pháp
        [HttpDelete("admin/xoa-cau-hoi/{idCauHoi}")]
        public async Task<IActionResult> AdminXoaCauHoi(int idCauHoi)
        {
            // Sử dụng Transaction để đảm bảo an toàn, tránh lỗi nửa chừng làm mất đồng bộ DB
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Tìm câu hỏi kèm theo danh sách đáp án của nó
                var cauHoi = await _context.CauHois
                    .Include(ch => ch.DapAns)
                    .FirstOrDefaultAsync(ch => ch.IdCauHoi == idCauHoi);

                if (cauHoi == null)
                    return NotFound(new { success = false, message = "Không tìm thấy câu hỏi cần xóa." });

                // Bước 1: Xóa sạch các đáp án của câu hỏi này trước
                if (cauHoi.DapAns != null && cauHoi.DapAns.Any())
                {
                    _context.DapAns.RemoveRange(cauHoi.DapAns);
                }

                // Bước 2: Xóa chính câu hỏi đó
                _context.CauHois.Remove(cauHoi);

                // Lưu thay đổi và commit transaction
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Bước 3: Làm mới bộ nhớ Cache
                ClearCache();

                return Ok(new { success = true, message = "Đã xóa câu hỏi và các đáp án liên quan thành công." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Admin lỗi khi xóa câu hỏi ID: {idCauHoi}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi xóa câu hỏi: " + ex.Message });
            }
        }

        // 13. API Admin: Thêm câu hỏi mới kèm đáp án vào một Ngữ pháp
        [HttpPost("admin/them-cau-hoi/{idNguPhap}")]
        public async Task<IActionResult> AdminThemCauHoi(int idNguPhap, [FromBody] CauHoiModels model)
        {
            // Kiểm tra tính hợp lệ sơ bộ của dữ liệu đầu vào
            if (model == null || string.IsNullOrWhiteSpace(model.NoiDung))
                return BadRequest(new { success = false, message = "Nội dung câu hỏi không được để trống." });

            try
            {
                // Kiểm tra ngữ pháp mục tiêu có tồn tại không
                var nguPhap = await _context.NguPhaps
                    .Include(np => np.IdCauHois)
                    .FirstOrDefaultAsync(np => np.IdNguPhap == idNguPhap);

                if (nguPhap == null)
                    return NotFound(new { success = false, message = "Không tìm thấy ngữ pháp yêu cầu." });

                // Khởi tạo Entity Câu hỏi mới từ DTO gửi lên
                var newCauHoi = new CauHoi // Thay bằng tên Class Entity chuẩn trong DB của bạn (ví dụ: CauHoi)
                {
                    NoiDung = model.NoiDung,
                    IdLoai = model.IdLoai,
                    GiaiThich = model.GiaiThich,
                    AmThanhPath = model.AmThanhPath,
                    IsGeneratedOnly = false,
                    

                    // Map danh sách đáp án đi kèm sang Entity Đáp án
                    DapAns = model.DapAns?.Select(da => new DapAn // Thay bằng tên Class Entity Đáp án của bạn
                    {
                        NoiDungDapAn = da.NoiDungDapAn,
                        LaDapAnDung = da.LaDapAnDung
                    }).ToList() ?? new List<DapAn>()
                };

                // Thêm thực thể câu hỏi mới trực tiếp vào bộ sưu tập liên kết của Ngữ pháp
                // Cách làm này chạy tốt cho cả mối quan hệ 1-Nhiều hoặc Nhiều-Nhiều (Join Table)
                nguPhap.IdCauHois.Add(newCauHoi);

                // Lưu xuống Database
                await _context.SaveChangesAsync();

                // Làm mới cache để cập nhật bài tập mới cho học viên
                ClearCache();

                return Ok(new
                {
                    success = true,
                    message = "Thêm câu hỏi và đáp án vào ngữ pháp thành công.",
                    idCauHoi = newCauHoi.IdCauHoi
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Admin lỗi khi thêm câu hỏi cho ngữ pháp ID: {idNguPhap}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi tạo câu hỏi mới: " + ex.Message });
            }
        }

        [HttpPost("admin/nhap-bai-tap-excel")]
        public async Task<IActionResult> NhapBaiTapExcel(IFormFile file)
        {
            if (file == null || file.Length <= 0)
                return BadRequest(new { success = false, message = "Vui lòng chọn file Excel." });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                using var stream = file.OpenReadStream();
                // Đọc toàn bộ các dòng ở dạng thô dạng Dictionary để dễ dàng bóc tách theo ký tự cột (A, B, C...)
                var rows = stream.Query(useHeaderRow: false)
                                 .Cast<IDictionary<string, object>>()
                                 .ToList();

                if (rows.Count < 2)
                    return BadRequest(new { success = false, message = "Cấu trúc file Excel không hợp lệ hoặc thiếu dữ liệu." });

                // Hàm Helper để lấy giá trị không phân biệt chữ hoa/thường của Key trong Dictionary MiniExcel
                string GetValue(IDictionary<string, object> row, string key)
                {
                    if (row == null) return null;
                    if (row.TryGetValue(key.ToUpper(), out var val) || row.TryGetValue(key.ToLower(), out val))
                    {
                        return val?.ToString()?.Trim();
                    }
                    return null;
                }

                // Lấy IdCapDo cố định tại ô B1 (Hàng 1, Cột B)
                string idCapDoStr = GetValue(rows[0], "B");
                if (!int.TryParse(idCapDoStr, out int idCapDo))
                    return BadRequest(new { success = false, message = "Cấp độ (IdCapDo) tại ô B1 không hợp lệ hoặc để trống." });

                NguPhap currentNguPhap = null;
                int totalCauHoiCount = 0;

                // Duyệt từ hàng thứ 2 (Index 1) trở đi để quét các khối Ngữ pháp và Câu hỏi
                for (int i = 1; i < rows.Count; i++)
                {
                    var row = rows[i];

                    // Đọc cột A và cột B của dòng hiện tại
                    string colA = GetValue(row, "A");
                    string colB = GetValue(row, "B");

                    // BỎ QUA dòng trống hoàn toàn hoặc dòng tiêu đề cột câu hỏi
                    if (string.IsNullOrWhiteSpace(colA) && string.IsNullOrWhiteSpace(colB)) continue;
                    if (colA != null && (colA.Equals("NoiDungCauHoi", StringComparison.OrdinalIgnoreCase) || colA.Contains("Nội dung câu hỏi"))) continue;

                    // TRƯỜNG HỢP 1: Phát hiện dòng khai báo Ngữ pháp mới
                    // SỬA LỖI: Nhận diện Ngữ pháp mới bằng cách check Cột A có tên, Cột B trống rỗng. 
                    // (Không check cột D, E nữa để tránh nhầm lẫn với câu hỏi loại 1017 có Cột C,D,E trống).
                    if (!string.IsNullOrWhiteSpace(colA) && string.IsNullOrWhiteSpace(colB))
                    {
                        // Tìm ngữ pháp này trong Database theo tên và IdCapDo đã lấy ở B1
                        currentNguPhap = await _context.NguPhaps
                            .FirstOrDefaultAsync(n => n.TenNguPhap == colA && n.IdCapDo == idCapDo);

                        // Nếu không thấy ngữ pháp, báo lỗi trực quan dừng tiến trình rollback dữ liệu
                        if (currentNguPhap == null)
                        {
                            await transaction.RollbackAsync();
                            return NotFound(new { success = false, message = $"Không tìm thấy ngữ pháp '{colA}' thuộc cấp độ {idCapDo} tại dòng {i + 1} trong hệ thống. Vui lòng tạo bài ngữ pháp này trước." });
                        }
                        continue; // Chuyển sang dòng kế tiếp để quét câu hỏi
                    }

                    // TRƯỜNG HỢP 2: Dòng dữ liệu câu hỏi (Phải thuộc một Ngữ pháp nào đó đã được nhận diện trước đó)
                    if (currentNguPhap != null && !string.IsNullOrWhiteSpace(colA))
                    {
                        // Bóc tách dữ liệu các cột câu hỏi theo hàng hiện tại từ cột B đến cột H
                        string dapAnA = GetValue(row, "B");
                        string dapAnB = GetValue(row, "C");
                        string dapAnC = GetValue(row, "D");
                        string dapAnD = GetValue(row, "E");
                        string dapAnDung = GetValue(row, "F")?.ToUpper();
                        string idLoaiStr = GetValue(row, "G");
                        string giaiThichCauHoi = GetValue(row, "H");

                        int idLoai = int.TryParse(idLoaiStr, out int parsedLoai) ? parsedLoai : 1;

                        // Khởi tạo thực thể câu hỏi mới
                        var cauHoi = new CauHoi
                        {
                            NoiDung = colA, // Nội dung câu hỏi nằm ở cột A
                            IdCapDo = idCapDo,
                            IdLoai = idLoai, // Sẽ nhận diện chuẩn type 1017 ở cột G
                            GiaiThich = giaiThichCauHoi,
                            IsGeneratedOnly = false,
                            // Thiết lập liên kết bảng trung gian N-N
                            IdNguPhaps = new List<NguPhap> { currentNguPhap }
                        };

                        // Gom danh sách đáp án
                        var danhSachDA = new List<(string Content, string Char)>
                {
                    (dapAnA, "A"), (dapAnB, "B"), (dapAnC, "C"), (dapAnD, "D")
                };

                        foreach (var da in danhSachDA)
                        {
                            // Đối với loại bài 1017, các đáp án B, C, D trống sẽ bị bỏ qua tại đây
                            // Chỉ lưu duy nhất đáp án A
                            if (string.IsNullOrWhiteSpace(da.Content)) continue;

                            cauHoi.DapAns.Add(new DapAn
                            {
                                NoiDungDapAn = da.Content,
                                LaDapAnDung = (da.Char == dapAnDung)
                            });
                        }

                        _context.CauHois.Add(cauHoi);
                        totalCauHoiCount++;
                    }
                }

                if (totalCauHoiCount == 0)
                    return BadRequest(new { success = false, message = "File không chứa câu hỏi nào hợp lệ để thêm hoặc các bài Ngữ Pháp chưa trùng khớp." });

                // Lưu thay đổi xuống cơ sở dữ liệu và hoàn tất giao dịch
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Xóa bộ nhớ cache của hệ thống
                // ClearCache(); // Bỏ comment nếu hàm này có tồn tại trong Controller của bạn

                return Ok(new
                {
                    success = true,
                    message = $"Thành công! Đã xử lý toàn bộ file và thêm {totalCauHoiCount} câu hỏi mới vào các cấu trúc ngữ pháp tương ứng."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống khi xử lý dữ liệu: {ex.Message}" });
            }
        }

        // API MỚI: Dùng riêng cho việc sửa đơn lẻ một câu hỏi từ modal bài tập
        [HttpPut("cap-nhat-cau-hoi/{id}")]
        public async Task<IActionResult> CapNhatCauHoi(int id, [FromBody] CauHoiUpdateDto dto)
        {
            // Kiểm tra DTO đầu vào tránh lỗi null reference
            if (dto == null)
            {
                return BadRequest(new { success = false, message = "Dữ liệu gửi lên không hợp lệ." });
            }

            // Kiểm tra nhanh điều kiện cơ bản để không cần đợi DbContext chạy
            if (string.IsNullOrWhiteSpace(dto.NoiDung))
            {
                return BadRequest(new { success = false, message = "Nội dung câu hỏi bài tập không được bỏ trống." });
            }

            try
            {
                // Lấy câu hỏi kèm danh sách đáp án hiện tại của nó từ Database
                var existingCH = await _context.CauHois
                    .Include(ch => ch.DapAns)
                    .FirstOrDefaultAsync(ch => ch.IdCauHoi == id);

                if (existingCH == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy câu hỏi cần cập nhật trên hệ thống." });
                }

                // 1. Cập nhật các thông tin cơ bản của câu hỏi
                existingCH.NoiDung = dto.NoiDung.Trim();
                existingCH.GiaiThich = dto.GiaiThich?.Trim();

                if (dto.IdLoai.HasValue)
                {
                    existingCH.IdLoai = dto.IdLoai.Value;
                }

                // 2. Làm mới danh sách đáp án cho câu hỏi này (Tránh trùng lặp hoặc sót ID cũ)
                if (existingCH.DapAns != null && existingCH.DapAns.Any())
                {
                    _context.DapAns.RemoveRange(existingCH.DapAns);
                }

                // 3. Nạp danh sách đáp án mới từ client gửi lên vào câu hỏi
                if (dto.DapAns != null && dto.DapAns.Any())
                {
                    existingCH.DapAns = dto.DapAns.Select(da => new DapAn
                    {
                        NoiDungDapAn = da.NoiDungDapAn?.Trim(),
                        LaDapAnDung = da.LaDapAnDung
                    }).ToList();
                }
                else
                {
                    existingCH.DapAns = new List<DapAn>();
                }

                // 4. Lưu tất cả thay đổi xuống SQL Server
                await _context.SaveChangesAsync();

                // 5. Giải phóng, làm mới toàn bộ Cache để học viên nhận được câu hỏi mới ngay lập tức
                ClearCache();

                return Ok(new { success = true, message = "Cập nhật thông tin câu hỏi và các lựa chọn đáp án thành công." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi nghiêm trọng khi cập nhật câu hỏi lẻ ID: {id}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi cập nhật dữ liệu: " + ex.Message });
            }
        }
    }
}

        
