using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12;
using HocTiengTrung_3._0_T.Models12.QuanLyDeThi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using MiniExcelLibs;
using MiniExcelLibs.Attributes; // Đôi khi cần thiết cho các bản mới
using System.Security.Claims;
using System.Text.Json;
using static HocTiengTrung_3._0_T.Models12.QuanLyDeThi.JsonDeThi;

namespace HocTiengTrung_3._0_T.ControllersAPI
{
    [Route("api/quanlydethi")]
    [ApiController]
    public class QuanLyDeThi : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<QuanLyDeThi> _logger;
        private readonly IUserService _userService;
        // TokenSource để hủy toàn bộ cache liên quan đến câu hỏi khi có thay đổi (Add/Update/Delete)
        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();

        public QuanLyDeThi(TmdtContext context, IMemoryCache cache, ILogger<QuanLyDeThi> logger, IUserService userService)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _userService = userService;
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId)) return userId;

            var sessionUserId = HttpContext.Session.GetString("MaNguoiDung");
            if (!string.IsNullOrEmpty(sessionUserId) && int.TryParse(sessionUserId, out int sUserId)) return sUserId;

            return null;
        }

        [HttpGet("danh-sach-de-thi")]
        public IActionResult GetDanhSachDeThi(int? idCapDo) // Thêm param idCapDo (cho phép null)
        {
            try
            {
                // Tạo cacheKey linh hoạt: nếu có idCapDo thì cache riêng cho cấp độ đó
                var cacheKey = idCapDo.HasValue ? $"danh-sach-de-thi-capdo-{idCapDo}" : "danh-sach-de-thi-all";

                if (!_cache.TryGetValue(cacheKey, out List<DeThiModels> danhSachDeThi))
                {
                    // Bắt đầu truy vấn
                    var query = _context.DeThis.AsQueryable();

                    // Nếu người dùng có chọn cấp độ thì thêm điều kiện Where
                    if (idCapDo.HasValue)
                    {
                        query = query.Where(d => d.IdCapDo == idCapDo.Value);
                    }

                    danhSachDeThi = query
                        .Select(d => new DeThiModels
                        {
                            IdDeThi = d.IdDeThi,
                            TenDe = d.TenDe,
                            ThoiGianLamBai = d.ThoiGianLamBai,
                            TongSoCau = d.TongSoCau,
                            IdCapDo = d.IdCapDo,
                            NgayTao = d.NgayTao,
                            TrangThai = d.TrangThai,
                            IsPremium = d.IsPremium 
                        })
                        .ToList();

                    // Lưu vào cache
                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token))
                        .SetSlidingExpiration(TimeSpan.FromMinutes(30));

                    _cache.Set(cacheKey, danhSachDeThi, cacheEntryOptions);
                }

                return Ok(new { success = true, data = danhSachDeThi });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi lấy danh sách đề thi (CapDo: {idCapDo})");
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi lấy danh sách đề thi." });
            }
        }
       
        [HttpGet("chi-tiet-de-thi/{idDeThi}")]
        public async Task<IActionResult> GetChiTietDeThi(int idDeThi)
        {
            try
            {
                var cacheKey = $"chi-tiet-de-thi-{idDeThi}";

                if (!_cache.TryGetValue(cacheKey, out object result))
                {
                    // 1. Lấy thông tin thời gian từ bảng DeThi
                    var deThiInfo = _context.DeThis
                        .Where(d => d.IdDeThi == idDeThi)
                        .Select(d => new DeThiModels
                        {
                            IdDeThi = d.IdDeThi,
                            TenDe = d.TenDe,
                            IdCapDo = d.IdCapDo,
                            ThoiGianNghe = d.ThoiGianNghe,
                            ThoiGianDoc = d.ThoiGianDoc,
                            ThoiGianViet = d.ThoiGianViet,
                            ThoiGianChuyenTiep = d.ThoiGianChuyenTiep,
                            TongSoCau = d.TongSoCau,
                            IsPremium = d.IsPremium
                        }).FirstOrDefault();

                    if (deThiInfo == null)
                    {
                        return NotFound(new { success = false, message = "Không tìm thấy đề thi." });
                    }
                    
                    
                 
                    // 2. Lấy danh sách câu hỏi chi tiết
                    var chiTiet = _context.ChiTietDeThis
                        .Where(ct => ct.IdDeThi == idDeThi)
                        .OrderBy(ct => ct.ThuTu)
                        .Select(ct => new ChiTietDeThiModels
                        {
                            IdChiTiet = ct.IdChiTiet,
                            IdDeThi = ct.IdDeThi,
                            ThuTu = ct.ThuTu,
                            NoiDungJson = ct.NoiDungJson,
                            AmThanhPath = ct.AmThanhPath,
                            HinhAnhPath = ct.HinhAnhPath,
                            DapAnDung = ct.DapAnDung,
                            IdLoai = ct.IdLoai,
                            GiaiThich = ct.GiaiThich,
                            GhiChu = ct.GhiChu,
                            KyNang = ct.KyNang, // Đã có cột KyNang để phân trang 1, 2, 3
                            TenLoai = ct.IdLoaiNavigation != null ? ct.IdLoaiNavigation.TenLoai : ""
                        })
                        .ToList();

                    // 3. Tổ chức lại dữ liệu trả về: Thông tin đề + Danh sách câu hỏi
                    result = new
                    {
                        Info = deThiInfo,
                        Questions = chiTiet
                    };

                    // Lưu vào cache
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(60));

                    _cache.Set(cacheKey, result, cacheOptions);
                }

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi lấy chi tiết đề thi ID: {idDeThi}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống." });
            }
        }

        [HttpGet("danh-sach-loai-de-thi")]
        public IActionResult GetDanhSachLoaiDeThi()
        {
            try
            {
                var cacheKey = "danh-sach-loai-de-thi-all";

                // Kiểm tra xem danh sách loại đề đã có trong Cache chưa
                if (!_cache.TryGetValue(cacheKey, out List<LoaiDeThiModels> danhSachLoai))
                {
                    // Truy vấn lấy dữ liệu từ bảng LoaiDeThi
                    danhSachLoai = _context.LoaiDeThis
                        .Select(l => new LoaiDeThiModels
                        {
                            IdLoai = l.IdLoai,
                            TenLoai = l.TenLoai
                        })
                        .ToList();

                    // Cấu hình Cache (thường Loại đề thi ít thay đổi nên để thời gian dài hơn)
                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token))
                        .SetAbsoluteExpiration(TimeSpan.FromHours(24)); // Cache trong 24h

                    _cache.Set(cacheKey, danhSachLoai, cacheEntryOptions);
                }

                return Ok(new { success = true, data = danhSachLoai });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách loại đề thi.");
                return StatusCode(500, new { success = false, message = "Không thể lấy danh sách loại đề thi." });
            }
        }

        [HttpPost("import-excel")]
        public async Task<IActionResult> ImportExcel(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("File không hợp lệ.");

            var jsonOptions = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
                WriteIndented = false
            };

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                using var stream = file.OpenReadStream();
                var allRows = stream.Query(useHeaderRow: false).Cast<IDictionary<string, object>>().ToList();

                if (allRows.Count < 10) return BadRequest("File Excel thiếu dữ liệu Metadata.");

                // 1. Lấy Metadata (Cột B - Index 1)
                var deThi = new DeThi
                {
                    TenDe = allRows[0].Values.ElementAtOrDefault(1)?.ToString() ?? "Đề thi HSK",
                    IdCapDo = int.TryParse(allRows[1].Values.ElementAtOrDefault(1)?.ToString(), out int cd) ? cd : 3,
                    ThoiGianLamBai = int.TryParse(allRows[2].Values.ElementAtOrDefault(1)?.ToString(), out int tg) ? tg : 90,
                    TongSoCau = int.TryParse(allRows[3].Values.ElementAtOrDefault(1)?.ToString(), out int tsc) ? tsc : 0,
                    ThoiGianNghe = int.TryParse(allRows[4].Values.ElementAtOrDefault(1)?.ToString(), out int tgn) ? tgn : 0,
                    ThoiGianDoc = int.TryParse(allRows[5].Values.ElementAtOrDefault(1)?.ToString(), out int tgd) ? tgd : 0,
                    ThoiGianViet = int.TryParse(allRows[6].Values.ElementAtOrDefault(1)?.ToString(), out int tgv) ? tgv : 0,
                    ThoiGianChuyenTiep = int.TryParse(allRows[7].Values.ElementAtOrDefault(1)?.ToString(), out int tgct) ? tgct : 0,
                    NgayTao = DateTime.Now,
                    TrangThai = true
                };
                _context.DeThis.Add(deThi);
                await _context.SaveChangesAsync();

                // 2. Tìm dòng tiêu đề
                var headerIndex = allRows.FindIndex(r => r.Values.Any(v => v?.ToString()?.Trim() == "IdLoai"));
                var chiTietList = new List<ChiTietDeThi>();

                // 3. Duyệt dữ liệu câu hỏi
                for (int i = headerIndex + 1; i < allRows.Count; i++)
                {
                    var rowValues = allRows[i].Values.ToList();
                    string GetV(int index) => index < rowValues.Count ? rowValues[index]?.ToString()?.Trim() ?? "" : "";

                    if (!int.TryParse(GetV(1), out int idLoai) || idLoai == 0) continue;

                    string noiDung = GetV(3);
                    string dapAn = GetV(4);
                    string ghiChu = GetV(11);
                    string viDu = GetV(12);
                    string pinyin = GetV(13);
                    string hinhAnh = GetV(14);
                    string amThanh = GetV(15);
                    string giaiThich = GetV(16); // Cột Q - Giải thích
                    string noiDungJson = "";

                    // 4. Logic phân loại theo danh sách ID mới của Toán
                    switch (idLoai)
                    {
                        case 1: // Nghe - Chọn tranh
                            noiDungJson = JsonSerializer.Serialize(new
                            {
                                GhiChu = ghiChu,
                                ViDu = viDu,                              
                                Options = new Dictionary<string, string> {
                            {"A", GetV(5)}, {"B", GetV(6)}, {"C", GetV(7)}, {"D", GetV(8)}, {"E", GetV(9)}, {"F", GetV(10)}
                        }
                            }, jsonOptions);
                            break;

                        case 2: // Nghe - Đúng/Sai
                            noiDungJson = JsonSerializer.Serialize(new
                            {
                                CauHoi = noiDung,
                                ViDu = viDu,
                                
                            }, jsonOptions);
                            break;

                        case 3: // Nghe - Trắc nghiệm (A,B,C)
                        case 6: // Đọc - Trắc nghiệm
                            noiDungJson = JsonSerializer.Serialize(new
                            {
                                CauHoi = noiDung,
                                ViDu = viDu,
                                
                                Options = new Dictionary<string, string> {
                            {"A", GetV(5)}, {"B", GetV(6)}, {"C", GetV(7)}, {"D", GetV(8)}, {"E", GetV(9)}, {"F", GetV(10)}
                        }
                            }, jsonOptions);
                            break;

                        case 4: // Đọc - Ghép câu
                        case 5: // Đọc - Điền từ
                            noiDungJson = JsonSerializer.Serialize(new
                            {
                                CauHoi = noiDung,
                                GhiChu = ghiChu,
                                ViDu = viDu,
                                
                                Options = new Dictionary<string, string> {
                            {"A", GetV(5)}, {"B", GetV(6)}, {"C", GetV(7)}, {"D", GetV(8)}, {"E", GetV(9)}, {"F", GetV(10)}
                        }
                            }, jsonOptions);
                            break;

                        case 7: // Viết - Sắp xếp câu (Dùng dấu / để tách từ)
                            noiDungJson = JsonSerializer.Serialize(new
                            {
                                ViDu = viDu,
                                Words = noiDung.Split(new[] { '/', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
                                
                            }, jsonOptions);
                            break;

                        case 8: // Viết - Điền chữ Hán
                            noiDungJson = JsonSerializer.Serialize(new
                            {
                                Context = noiDung,
                                ViDu = viDu,
                                Pinyin = pinyin,
                                
                            }, jsonOptions);
                            break;

                        case 9: // Đọc - Sắp xếp đoạn văn (A-B-C)
                            noiDungJson = JsonSerializer.Serialize(new
                            {
                                Options = new Dictionary<string, string> { { "A", GetV(5) }, { "B", GetV(6) }, { "C", GetV(7) } },
                                ViDu = viDu,
                                
                            }, jsonOptions);
                            break;

                        default: // Các loại khác (10, 11, 12...)
                            noiDungJson = JsonSerializer.Serialize(new
                            {
                                NoiDung = noiDung,
                                GhiChu = ghiChu,
                                ViDu = viDu,
                                
                            }, jsonOptions);
                            break;
                    }

                    chiTietList.Add(new ChiTietDeThi
                    {
                        IdDeThi = deThi.IdDeThi,
                        ThuTu = int.TryParse(GetV(0), out int tt) ? tt : 0,
                        IdLoai = idLoai,
                        KyNang = int.TryParse(GetV(2), out int kn) ? kn : 1,
                        NoiDungJson = noiDungJson,
                        DapAnDung = dapAn,
                        HinhAnhPath = hinhAnh,
                        AmThanhPath = amThanh,
                        GiaiThich = giaiThich,
                        GhiChu = ghiChu,
                    });
                }

                if (chiTietList.Any())
                {
                    _context.ChiTietDeThis.AddRange(chiTietList);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // 1. Gửi tín hiệu hủy (Cancel) để xóa các cache đang lắng nghe _resetCacheSignal.Token
                    _resetCacheSignal.Cancel();

                    // 2. Giải phóng Token cũ và tạo Token mới cho các yêu cầu Cache tiếp theo
                    _resetCacheSignal.Dispose();
                    _resetCacheSignal = new CancellationTokenSource();

                    return Ok(new { success = true, message = $"Thành công! Đã import {chiTietList.Count} câu." });
                }
                return BadRequest("Không tìm thấy dữ liệu câu hỏi.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("nop-bai")]
        public async Task<IActionResult> NopBai([FromBody] NopBaiRequest request)
        {
            // Validate request cơ bản
            if (request == null || request.ExamId <= 0 || request.UserAnswers == null)
            {
                return BadRequest(new { success = false, message = "Dữ liệu nộp bài không hợp lệ." });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Lấy UserId từ Token hoặc Session 
                int? currentUserId = GetCurrentUserId(); // Đảm bảo ngài đã có hàm này trong BaseController

                // 2. Lấy danh sách câu hỏi và đáp án đúng để đối chiếu
                var questions = _context.ChiTietDeThis
                    .Where(ct => ct.IdDeThi == request.ExamId)
                    .ToList();

                if (!questions.Any())
                {
                    return NotFound(new { success = false, message = "Không tìm thấy câu hỏi cho đề thi này." });
                }

                int tongCauNghe = 0, soCauDungNghe = 0;
                int tongCauDoc = 0, soCauDungDoc = 0;
                int tongCauViet = 0, soCauDungViet = 0;

                // 3. Tạo đối tượng Kết quả tổng quát
                var ketQua = new KetQuaThi
                {
                    IdDeThi = request.ExamId,
                    IdUser = currentUserId,
                    HoTen = currentUserId.HasValue ? null : (request.HoTen ?? "Khách ẩn danh"),
                    TongSoCau = questions.Count,
                    ThoiGianBatDau = request.StartTime,
                    ThoiGianHoanThanh = DateTime.Now,
                    TrangThai = true // 1: Đã nộp bài
                };

                _context.KetQuaThis.Add(ketQua);
                await _context.SaveChangesAsync();

                // 4. Duyệt danh sách câu hỏi để chấm điểm
                var chiTietBaiLams = new List<ChiTietBaiLam>();
                foreach (var q in questions)
                {
                    int thuTu = q.ThuTu ?? 0;
                    int kyNang = q.KyNang ?? 1;

                    request.UserAnswers.TryGetValue(thuTu, out string userAns);

                    bool isCorrect = !string.IsNullOrEmpty(userAns) &&
                                     string.Equals(userAns.Trim(), q.DapAnDung?.Trim(), StringComparison.OrdinalIgnoreCase);

                    if (kyNang == 1)
                    {
                        tongCauNghe++;
                        if (isCorrect) soCauDungNghe++;
                    }
                    else if (kyNang == 2)
                    {
                        tongCauDoc++;
                        if (isCorrect) soCauDungDoc++;
                    }
                    else if (kyNang == 3)
                    {
                        tongCauViet++;
                        if (isCorrect) soCauDungViet++;
                    }

                    chiTietBaiLams.Add(new ChiTietBaiLam
                    {
                        IdKetQua = ketQua.IdKetQua,
                        IdChiTietDeThi = q.IdChiTiet,
                        DapAnChon = userAns ?? "",
                        LaDapAnDung = isCorrect
                    });
                }

                // 5. Tính điểm theo chuẩn HSK
                decimal diemNghe = tongCauNghe > 0 ? Math.Round((decimal)soCauDungNghe / tongCauNghe * 100, 2) : 0;
                decimal diemDoc = tongCauDoc > 0 ? Math.Round((decimal)soCauDungDoc / tongCauDoc * 100, 2) : 0;
                decimal diemViet = tongCauViet > 0 ? Math.Round((decimal)soCauDungViet / tongCauViet * 100, 2) : 0;

                ketQua.SoCauDung = soCauDungNghe + soCauDungDoc + soCauDungViet;
                ketQua.DiemSo = diemNghe + diemDoc + diemViet;

                _context.ChiTietBaiLams.AddRange(chiTietBaiLams);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    idKetQua = ketQua.IdKetQua,
                    soCauDung = ketQua.SoCauDung,
                    tongSoCau = ketQua.TongSoCau,
                    diemSo = ketQua.DiemSo,
                    chiTietDiem = new { diemNghe, diemDoc, diemViet },
                    message = "Nộp bài thành công!"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi nộp bài thi ID {ExamId}: {Message}", request.ExamId, ex.Message);
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi lưu kết quả." });
            }
        }

        [HttpGet("ket-qua-chi-tiet/{idKetQua}")]
        public IActionResult GetKetQuaChiTiet(int idKetQua)
        {
            try
            {
                // 1. Lấy thông tin tổng quát của bài thi
                var ketQua = _context.KetQuaThis
                    .Where(k => k.IdKetQua == idKetQua)
                    .Select(k => new KetQuaThiViewModel
                    {
                        IdKetQua = k.IdKetQua, // Sửa lỗi idKetQua -> IdKetQua
                        IdDeThi = k.IdDeThi,
                        IdUser = k.IdUser,
                        HoTen = k.HoTen,
                        SoCauDung = k.SoCauDung,
                        TongSoCau = k.TongSoCau,
                        DiemSo = k.DiemSo,
                        ThoiGianBatDau = k.ThoiGianBatDau,
                        ThoiGianHoanThanh = k.ThoiGianHoanThanh,
                        TrangThai = k.TrangThai
                    })
                    .FirstOrDefault();

                if (ketQua == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy kết quả bài thi." });
                }

                // 2. Lấy chi tiết từng câu trả lời và thông tin câu hỏi
                var chiTiet = _context.ChiTietBaiLams
                    .Where(ct => ct.IdKetQua == idKetQua)
                    .Join(_context.ChiTietDeThis,
                        bl => bl.IdChiTietDeThi,
                        dt => dt.IdChiTiet,
                        (bl, dt) => new ChiTietKetQuaModel
                        {
                            ThuTu = dt.ThuTu,
                            IdLoai = dt.IdLoai,
                            KyNang = dt.KyNang,
                            NoiDungJson = dt.NoiDungJson,
                            HinhAnh = dt.HinhAnhPath,
                            AmThanh = dt.AmThanhPath,
                            DapAnDung = dt.DapAnDung,
                            DapAnChon = bl.DapAnChon,
                            LaDapAnDung = bl.LaDapAnDung ?? false,
                            GiaiThich = dt.GiaiThich
                        })
                    .OrderBy(x => x.ThuTu)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    info = ketQua,
                    details = chiTiet
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi lấy chi tiết kết quả ID: {idKetQua}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi tải kết quả." });
            }
        }

        [HttpGet("lich-su-thi")]
        public IActionResult GetLichSuThi()
        {
            try
            {
                // 1. Lấy Id người dùng hiện tại
                int? userId = GetCurrentUserId();

                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "Bạn cần đăng nhập để xem lịch sử." });
                }

                // 2. Truy vấn danh sách kết quả thi
                var lichSu = _context.KetQuaThis
                    .Where(k => k.IdUser == userId)
                    .Join(_context.DeThis,
                        k => k.IdDeThi,
                        d => d.IdDeThi,
                        (k, d) => new KetQuaThiViewModel
                        {
                            IdKetQua = k.IdKetQua,
                            IdDeThi = k.IdDeThi,
                            TenDe = d.TenDe, // Lấy tên đề từ bảng DeThi
                            SoCauDung = k.SoCauDung,
                            TongSoCau = k.TongSoCau,
                            DiemSo = k.DiemSo,
                            ThoiGianBatDau = k.ThoiGianBatDau,
                            ThoiGianHoanThanh = k.ThoiGianHoanThanh,
                            TrangThai = k.TrangThai,
                            // Nếu ViewModel của bạn có thuộc tính ThoiGianLam (double hoặc decimal)
                            // ThoiGianLam = k.ThoiGianHoanThanh.HasValue && k.ThoiGianBatDau.HasValue
                            //               ? (decimal)Math.Round((k.ThoiGianHoanThanh.Value - k.ThoiGianBatDau.Value).TotalMinutes, 1)
                            //               : 0
                        })
                    .OrderByDescending(k => k.ThoiGianHoanThanh)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    data = lichSu
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy lịch sử thi của người dùng.");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi tải lịch sử thi." });
            }
        }

        [HttpDelete("xoa-de-thi/{idDeThi}")]
        public async Task<IActionResult> DeleteDeThi(int idDeThi)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var deThi = await _context.DeThis.FindAsync(idDeThi);
                if (deThi == null) return NotFound(new { success = false, message = "Không tìm thấy đề thi." });

                // 1. Tìm tất cả các kết quả thi của đề này
                var ketQuaIds = _context.KetQuaThis
                    .Where(k => k.IdDeThi == idDeThi)
                    .Select(k => k.IdKetQua)
                    .ToList();

                // 2. Xóa chi tiết bài làm của các kết quả đó
                if (ketQuaIds.Any())
                {
                    var chiTietBaiLams = _context.ChiTietBaiLams.Where(bl => ketQuaIds.Contains(bl.IdKetQua));
                    _context.ChiTietBaiLams.RemoveRange(chiTietBaiLams);

                    // 3. Xóa các kết quả thi
                    var ketQuas = _context.KetQuaThis.Where(k => k.IdDeThi == idDeThi);
                    _context.KetQuaThis.RemoveRange(ketQuas);
                }

                // 4. Xóa chi tiết đề thi (các câu hỏi)
                var chiTietDeThi = _context.ChiTietDeThis.Where(ct => ct.IdDeThi == idDeThi);
                _context.ChiTietDeThis.RemoveRange(chiTietDeThi);

                // 5. Cuối cùng xóa đề thi
                _context.DeThis.Remove(deThi);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Xóa cache
                _resetCacheSignal.Cancel();
                _resetCacheSignal.Dispose();
                _resetCacheSignal = new CancellationTokenSource();

                return Ok(new { success = true, message = "Đã xóa đề thi và toàn bộ dữ liệu liên quan." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Lỗi khi xóa đề thi ID: {idDeThi}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi xóa đề thi." });
            }
        }

        [HttpPut("cap-nhat-de-thi/{idDeThi}")]
        public async Task<IActionResult> UpdateDeThi(int idDeThi, [FromBody] DeThi updateModel)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Lấy đề thi hiện tại kèm theo danh sách câu hỏi cũ
                var deThi = await _context.DeThis
                    .Include(d => d.ChiTietDeThis)
                    .FirstOrDefaultAsync(d => d.IdDeThi == idDeThi);

                if (deThi == null) return NotFound(new { success = false, message = "Không tìm thấy đề thi." });

                // 2. Cập nhật thông tin cơ bản của Đề thi
                deThi.TenDe = updateModel.TenDe;
                deThi.ThoiGianLamBai = updateModel.ThoiGianLamBai;
                deThi.IdCapDo = updateModel.IdCapDo;
               
                deThi.ThoiGianNghe = updateModel.ThoiGianNghe;
                deThi.ThoiGianDoc = updateModel.ThoiGianDoc;
                deThi.ThoiGianViet = updateModel.ThoiGianViet;
                deThi.ThoiGianChuyenTiep = updateModel.ThoiGianChuyenTiep;
                deThi.TongSoCau = updateModel.TongSoCau;
                deThi.IsPremium = updateModel.IsPremium;

                // 3. Xử lý cập nhật bộ câu hỏi (ChiTietDeThi)
                var incomingQuestions = updateModel.ChiTietDeThis.ToList();
                var existingQuestions = deThi.ChiTietDeThis.ToList();

                // A. Xóa những câu hỏi không còn tồn tại trong danh sách mới gửi lên
                foreach (var oldQuestion in existingQuestions)
                {
                    if (!incomingQuestions.Any(q => q.IdChiTiet == oldQuestion.IdChiTiet))
                    {
                        // Lưu ý: Nếu câu hỏi này đã có người làm (trong bảng ChiTietBaiLam), 
                        // lệnh xóa này có thể gây lỗi FK. Bạn nên kiểm tra hoặc dùng Soft Delete.
                        _context.ChiTietDeThis.Remove(oldQuestion);
                    }
                }

                // B. Cập nhật hoặc Thêm mới
                foreach (var q in incomingQuestions)
                {
                    var existingQ = existingQuestions.FirstOrDefault(ex => ex.IdChiTiet == q.IdChiTiet && q.IdChiTiet != 0);

                    if (existingQ != null)
                    {
                        // Cập nhật câu hỏi cũ
                        existingQ.ThuTu = q.ThuTu;
                        existingQ.NoiDungJson = q.NoiDungJson;
                        existingQ.AmThanhPath = q.AmThanhPath;
                        existingQ.HinhAnhPath = q.HinhAnhPath;
                        existingQ.DapAnDung = q.DapAnDung;
                        existingQ.IdLoai = q.IdLoai;
                        existingQ.GiaiThich = q.GiaiThich;
                        existingQ.KyNang = q.KyNang;
                        existingQ.GhiChu = q.GhiChu;

                    }
                    else
                    {
                        // Thêm câu hỏi mới hoàn toàn
                        var newQuestion = new ChiTietDeThi
                        {
                            IdDeThi = idDeThi,
                            ThuTu = q.ThuTu,
                            NoiDungJson = q.NoiDungJson,
                            AmThanhPath = q.AmThanhPath,
                            HinhAnhPath = q.HinhAnhPath,
                            DapAnDung = q.DapAnDung,
                            IdLoai = q.IdLoai,
                            GiaiThich = q.GiaiThich,
                            KyNang = q.KyNang,
                            GhiChu = q.GhiChu
                        };
                        _context.ChiTietDeThis.Add(newQuestion);
                    }
                }

                // 4. Lưu tất cả thay đổi
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 5. Làm mới Cache
                _resetCacheSignal.Cancel();
                _resetCacheSignal.Dispose();
                _resetCacheSignal = new CancellationTokenSource();

                return Ok(new { success = true, message = "Cập nhật đề thi và bộ câu hỏi thành công." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Lỗi khi cập nhật đề thi ID: {idDeThi}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPut("cap-nhat-trang-thai/{id}")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] ExamStatusDto dto)
        {
            var deThi = await _context.DeThis.FindAsync(id);
            if (deThi == null)
                return NotFound(new { success = false, message = "Không tìm thấy đề thi." });

            deThi.TrangThai = dto.TrangThai;
            deThi.IsPremium = dto.IsPremium;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Cập nhật thành công!" });
        }

        public class ExamStatusDto
        {
            public bool TrangThai { get; set; }
            public bool IsPremium { get; set; }
        }
    }
}
