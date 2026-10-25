using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models.AI;
using HocTiengTrung_3._0_T.Models12.BaiTap;
using HocTiengTrung_3._0_T.Models12.LuuLichSu;
using HocTiengTrung_3._0_T.Models12.TuVung;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using MiniExcelLibs;
using System.Security.Claims;

namespace HocTiengTrung_3._0_T.ControllersAPI
{
    [Route("api/quanlybaitap")]
    [ApiController]
    public class QuanLyBaiTap : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<QuanLyBaiTap> _logger;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        // TokenSource để hủy toàn bộ cache liên quan đến câu hỏi khi có thay đổi (Add/Update/Delete)
        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();

        public QuanLyBaiTap(TmdtContext context, IMemoryCache cache, ILogger<QuanLyBaiTap> logger, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// API lấy danh sách loại bài tập - Cache lâu vì dữ liệu ít thay đổi
        /// </summary>
        [HttpGet("danhsachloaibaitap")]
        public async Task<IActionResult> LayDanhSachBaiTap([FromQuery] string chuongTrinh = null)
        {
            try
            {
                // 1. Chuẩn hóa chuỗi lọc đầu vào
                string filterChuongTrinh = string.IsNullOrWhiteSpace(chuongTrinh) ? "3" : chuongTrinh.Trim();

                // Đảm bảo cache key cũng phân tách rạch ròi theo giá trị được định hình
                string cacheKey = $"Shared_DanhSachLoaiBaiTap_{filterChuongTrinh}";

                var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
                {
                    entry.SetAbsoluteExpiration(TimeSpan.FromDays(1)); // Cache 24h
                    entry.AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                    _logger.LogInformation($"Cache miss: LayDanhSachBaiTap với bộ lọc: {filterChuongTrinh}");

                    var query = _context.LoaiCauHois.AsQueryable();

                    // ================== LOGIC LỌC TÁCH BIỆT RẠCH RÒI ==================
                    if (filterChuongTrinh.Equals("ChuongTrinhCu", StringComparison.OrdinalIgnoreCase) || filterChuongTrinh == "2" || filterChuongTrinh == "2.0")
                    {
                        // Chỉ lấy duy nhất các loại thuộc chương trình 2
                        query = query.Where(cd => cd.ChuongTrinh == 2);
                    }
                    else
                    {
                        // Tất cả các trường hợp còn lại (bao gồm cả khi client truyền null hoặc chuỗi bất kỳ)
                        // Đều ÉP BUỘC chỉ lấy duy nhất các loại thuộc chương trình 3
                        query = query.Where(cd => cd.ChuongTrinh == 3);
                    }
                    // Tuyệt đối không còn nhánh lấy tất cả dữ liệu ra ngoài nữa
                    // =================================================================

                    return await query
                        .AsNoTracking()
                        .Select(cd => new LoaiCauHoiModels
                        {
                            IdLoai = cd.IdLoai,
                            TenLoai = cd.TenLoai
                        })
                        .ToListAsync();
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách loại bài tập");
                return StatusCode(500, "Lỗi máy chủ nội bộ.");
            }
        }

        /// <summary>
        /// API lấy bài tập: Cache ngân hàng câu hỏi gốc, nhưng xáo trộn ngẫu nhiên mỗi lần gọi
        /// </summary>
        [HttpGet("lay-bai-tap/{idCapDo}/{idLoai}")]
        public async Task<IActionResult> LayDanhSachBaiTapTheoLoai(
             int idCapDo,
             int idLoai,
             [FromQuery] bool shuffle = false,
             [FromQuery] int? idTrungTam = null)
        {
            try
            {
                // =========================================================================
                // 1. XÁC ĐỊNH ID TRUNG TÂM TỪ QUERY HOẶC TỪ TÀI KHOẢN ĐANG ĐĂNG NHẬP
                // =========================================================================
                int? targetTrungTamId = idTrungTam;

                if (!targetTrungTamId.HasValue || targetTrungTamId <= 0)
                {
                    string? idTrungTamClaim = User.FindFirst("IdTrungTam")?.Value
                                             ?? HttpContext.Session.GetString("IdTrungTam");

                    if (int.TryParse(idTrungTamClaim, out int parsedTtId) && parsedTtId > 0)
                    {
                        targetTrungTamId = parsedTtId;
                    }
                }

                // =========================================================================
                // 2. TẠO CACHE KEY KÈM ID TRUNG TÂM & LẤY DỮ LIỆU TỪ DATABASE
                // =========================================================================
                string ttCacheKey = targetTrungTamId.HasValue ? $"TT_{targetTrungTamId.Value}" : "TT_Global";
                string rawCacheKey = $"RawQuestions_{idCapDo}_{idLoai}_{ttCacheKey}";

                var rawData = await _cache.GetOrCreateAsync(rawCacheKey, async entry =>
                {
                    entry.SetSlidingExpiration(TimeSpan.FromMinutes(60));
                    entry.AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                    var query = _context.CauHois
                        .Where(c => c.IdCapDo == idCapDo
                                 && c.IdLoai == idLoai
                                 && c.IsGeneratedOnly == true
                                 && c.TrangThai == true);

                    // BỘ LỌC THEO ID TRUNG TÂM
                    if (targetTrungTamId.HasValue)
                    {
                        // Lấy bài tập thuộc cơ sở này
                        query = query.Where(c => c.IdTrungTam == targetTrungTamId.Value);
                    }
                    else
                    {
                        // Nếu tài khoản vãng lai/hệ thống chung không có trung tâm
                        query = query.Where(c => c.IdTrungTam == null || c.IdTrungTam == 0);
                    }

                    return await query
                        .Include(c => c.DapAns)
                        .Include(c => c.IdDoanVanNavigation)
                        .AsNoTracking()
                        .ToListAsync();
                });

                if (rawData == null || !rawData.Any())
                    return NotFound("Không tìm thấy bài tập phù hợp cho cơ sở này.");

                var random = new Random();

                // =========================================================================
                // 3. MAPPING DỮ LIỆU SANG MODEL
                // =========================================================================
                var allMappedQuestions = rawData.Select(c => new CauHoiModels
                {
                    IdCauHoi = c.IdCauHoi,
                    IdLoai = c.IdLoai,
                    IdCapDo = c.IdCapDo,
                    NoiDung = c.NoiDung,
                    AmThanhPath = c.AmThanhPath,
                    GiaiThich = c.GiaiThich,
                    IdDoanVan = c.IdDoanVan,
                    DoanVan = c.IdDoanVanNavigation != null ? new DoanVanModels
                    {
                        IdDoanVan = c.IdDoanVanNavigation.IdDoanVan,
                        TieuDe = c.IdDoanVanNavigation.TieuDe,
                        NoiDungHan = c.IdDoanVanNavigation.NoiDungHan,
                        NoiDungPinyin = c.IdDoanVanNavigation.NoiDungPinyin,
                        NoiDungViet = c.IdDoanVanNavigation.NoiDungViet,
                        AnhMinhHoa = c.IdDoanVanNavigation.AnhMinhHoa
                    } : null,
                    DapAns = (shuffle ? c.DapAns.OrderBy(d => random.Next()) : c.DapAns.OrderBy(d => d.IdDapAn))
                        .Select(d => new DapAnModels
                        {
                            IdDapAn = d.IdDapAn,
                            IdCauHoi = d.IdCauHoi,
                            NoiDungDapAn = d.NoiDungDapAn,
                            LaDapAnDung = d.LaDapAnDung ?? false
                        }).ToList()
                }).ToList();

                // =========================================================================
                // 4. PHÂN CHIA ĐỀ THI
                // =========================================================================
                List<dynamic> danhSachCacDeRaw = new List<dynamic>();

                if (idLoai == 1004 || idLoai == 1011) // Đọc hiểu
                {
                    var groupDe = allMappedQuestions
                        .Where(q => q.IdDoanVan != null && q.IdDoanVan != 0)
                        .GroupBy(q => q.IdDoanVan)
                        .OrderBy(g => shuffle ? random.Next() : 0)
                        .Chunk(1)
                        .Select((pair, index) => new
                        {
                            TenDe = $"Đề Đọc hiểu {index + 1}",
                            SoLuongDoanVan = pair.Count(),
                            SoLuongCauHoi = pair.Sum(p => p.Count()),
                            CauHois = pair.SelectMany(p => p).ToList()
                        }).ToList();

                    danhSachCacDeRaw.AddRange(groupDe);
                }
                else if (idLoai == 1012 || idLoai == 1013) // Dạng bài 5 câu / đề
                {
                    var processedQuestions = shuffle
                        ? allMappedQuestions.OrderBy(x => random.Next()).ToList()
                        : allMappedQuestions.OrderBy(x => x.IdCauHoi).ToList();

                    var groupDe = processedQuestions
                        .Chunk(5)
                        .Select((group, index) => new
                        {
                            TenDe = $"Đề số {index + 1}",
                            SoLuongCauHoi = group.Length,
                            CauHois = group.ToList()
                        }).ToList();

                    danhSachCacDeRaw.AddRange(groupDe);
                }
                else // Các dạng bài tập trắc nghiệm / điền từ khác (10 câu / đề)
                {
                    var processedQuestions = shuffle
                        ? allMappedQuestions.OrderBy(x => random.Next()).ToList()
                        : allMappedQuestions.OrderBy(x => x.IdCauHoi).ToList();

                    var groupDe = processedQuestions
                        .Chunk(10)
                        .Select((group, index) => new
                        {
                            TenDe = $"Đề số {index + 1}",
                            SoLuongCauHoi = group.Length,
                            CauHois = group.ToList()
                        }).ToList();

                    danhSachCacDeRaw.AddRange(groupDe);
                }

                // =========================================================================
                // 5. TRẢ VỀ DỮ LIỆU MIỄN PHÍ HOÀN TOÀN (KHÔNG KHÓA ĐỀ)
                // =========================================================================
                var danhSachCacDeFinal = danhSachCacDeRaw.Select((de, index) => new
                {
                    TenDe = de.TenDe,
                    SoLuongCauHoi = de.SoLuongCauHoi,
                    IsLocked = false, // Luôn mở khóa 100% cho mọi đề
                    CauHois = de.CauHois // Luôn trả đầy đủ nội dung câu hỏi
                }).ToList();

                return Ok(danhSachCacDeFinal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xử lý bài tập cho Cấp độ: {IdCapDo}, Loại: {IdLoai}", idCapDo, idLoai);
                return StatusCode(StatusCodes.Status500InternalServerError, "Lỗi máy chủ nội bộ.");
            }
        }

        [HttpGet("danh-sach-lich-su")]
        public async Task<IActionResult> GetLichSu([FromQuery] int? idHocVien = null)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                    return Unauthorized(new { success = false, message = "Vui lòng đăng nhập." });

                // NẾU CÓ TRUYỀN idHocVien -> XEM CỦA HỌC VIÊN
                // NẾU KHÔNG TRUYỀN (null) -> TỰ XEM CỦA CHÍNH MÌNH
                int targetUserId = idHocVien ?? currentUserId.Value;

                // KIỂM TRA QUYỀN: Chỉ khi xem của người khác mới cần xét quyền Giảng viên/Quản lý
                if (idHocVien.HasValue && idHocVien.Value != currentUserId.Value)
                {
                    var vaiTro = User.FindFirst("TenVaiTro")?.Value ?? "";
                    bool coQuyen = vaiTro.Contains("Quản lý") || vaiTro.Contains("Giảng viên") || vaiTro.Contains("Admin");

                    if (!coQuyen)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new
                        {
                            success = false,
                            message = "Bạn không có quyền xem tiến độ của học viên này."
                        });
                    }
                }

                // Cache theo từng targetUserId để không bị lẫn giữa các học viên với nhau
                string cacheKey = $"History_User_{targetUserId}";

                var data = await _cache.GetOrCreateAsync(cacheKey, async entry =>
                {
                    entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

                    return await _context.LichSuLamBais
                        .Where(x => x.IdNguoiDung == targetUserId)
                        .Include(x => x.IdCapDoNavigation)
                        .Include(x => x.IdLoaiNavigation)
                        .OrderByDescending(x => x.NgayLam)
                        .Select(x => new LichSuLamBaiModels
                        {
                            IdLichSu = x.IdLichSu,
                            IdNguoiDung = x.IdNguoiDung,
                            TenDe = x.TenDe ?? "Bài tập thực hành",
                            IdCapDo = x.IdCapDo,
                            TenCapDo = x.IdCapDoNavigation != null ? x.IdCapDoNavigation.TenCapDo : "HSK " + (x.IdCapDo ?? 1),
                            IdLoai = x.IdLoai,
                            TenLoai = x.IdLoaiNavigation != null ? x.IdLoaiNavigation.TenLoai : "Tổng hợp",
                            SoCauDung = x.SoCauDung ?? 0,
                            TongSoCau = x.TongSoCau ?? 0,
                            NgayLam = x.NgayLam
                        })
                        .ToListAsync();
                });

                return Ok(new
                {
                    success = true,
                    isXemCuaMinh = (targetUserId == currentUserId.Value),
                    idTargetUser = targetUserId,
                    data = data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách lịch sử làm bài");
                return StatusCode(500, new { success = false, message = "Không thể tải lịch sử." });
            }
        }



        [HttpGet("chi-tiet-lich-su/{idLichSu}")]
        public async Task<IActionResult> GetChiTietLichSu(int idLichSu)
        {
            try
            {
                // Dữ liệu chi tiết lịch sử thường chỉ xem 1 lần, không nhất thiết phải cache 
                // trừ khi hệ thống bị spam request cực lớn.
                var details = await _context.ChiTietLichSus
                    .Where(ct => ct.IdLichSu == idLichSu)
                    .Include(ct => ct.IdCauHoiNavigation)
                        .ThenInclude(q => q.DapAns)
                    .AsNoTracking()
                    .ToListAsync();

                if (details == null || !details.Any())
                    return NotFound(new { success = false, message = "Không tìm thấy chi tiết." });

                var result = details.Select(ct => new ChiTietLichSuModels
                {
                    IdChiTiet = ct.IdChiTiet,
                    IdLichSu = ct.IdLichSu,
                    IdCauHoi = ct.IdCauHoi,
                    CauTraLoiNguoiDung = ct.CauTraLoiNguoiDung,
                    LaDung = ct.LaDung,
                    IdCauHoiNavigation = ct.IdCauHoiNavigation != null ? new CauHoiModels
                    {
                        IdCauHoi = ct.IdCauHoiNavigation.IdCauHoi,
                        NoiDung = ct.IdCauHoiNavigation.NoiDung,
                        AmThanhPath = ct.IdCauHoiNavigation.AmThanhPath,
                        GiaiThich = ct.IdCauHoiNavigation.GiaiThich,
                        IdLoai = ct.IdCauHoiNavigation.IdLoai,
                        IdCapDo = ct.IdCauHoiNavigation.IdCapDo,
                        DapAns = ct.IdCauHoiNavigation.DapAns.Select(d => new DapAnModels
                        {
                            IdDapAn = d.IdDapAn,
                            IdCauHoi = d.IdCauHoi,
                            NoiDungDapAn = d.NoiDungDapAn,
                            LaDapAnDung = d.LaDapAnDung ?? false
                        }).ToList()
                    } : null
                }).ToList();

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi lấy chi tiết lịch sử ID: {idLichSu}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống." });
            }
        }

        [HttpPost("luu-ket-qua")]
        public async Task<IActionResult> LuuKetQua([FromBody] PhienLamBaiDto dto)
        {
            if (dto == null || dto.ChiTiets == null || !dto.ChiTiets.Any())
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { success = false, message = "Đăng nhập để lưu kết quả." });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var lichSu = new LichSuLamBai
                {
                    IdNguoiDung = userId.Value,
                    IdCapDo = dto.IdCapDo,
                    IdLoai = dto.IdLoai,
                    TenDe = !string.IsNullOrEmpty(dto.TenDe)
                    ? dto.TenDe
                    : $"Luyện tập HSK {dto.IdCapDo} {dto.IdLoai}",
                    SoCauDung = dto.SoCauDung,
                    TongSoCau = dto.TongSoCau,
                    NgayLam = DateTime.Now,
                    DanhSachIdCauHoi = string.Join(",", dto.ChiTiets.Select(x => x.IdCauHoi))
                };

                _context.LichSuLamBais.Add(lichSu);
                await _context.SaveChangesAsync();

                var danhSachChiTiet = dto.ChiTiets.Select(x => new ChiTietLichSu
                {
                    IdLichSu = lichSu.IdLichSu,
                    IdCauHoi = x.IdCauHoi,
                    CauTraLoiNguoiDung = x.CauTraLoiNguoiDung ?? "",
                    LaDung = x.LaDung
                }).ToList();

                _context.ChiTietLichSus.AddRange(danhSachChiTiet);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // XÓA CACHE lịch sử của User này để lần sau họ load lại sẽ thấy bài mới nhất
                _cache.Remove($"History_User_{userId}");

                return Ok(new { success = true, message = "Kết quả đã được lưu!", idLichSu = lichSu.IdLichSu });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi lưu kết quả User: {UserId}", userId);
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi lưu." });
            }
        }

        [HttpPost("luu-bai-tap")]
        public async Task<IActionResult> LuuBaiTap([FromBody] CauHoiModels cauHoi)
        {
            if (cauHoi == null || cauHoi.DapAns == null || !cauHoi.DapAns.Any())
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });

            try
            {
                var newCauHoi = new CauHoi
                {
                    NoiDung = cauHoi.NoiDung,
                    AmThanhPath = cauHoi.AmThanhPath,
                    GiaiThich = cauHoi.GiaiThich,
                    IdLoai = cauHoi.IdLoai,
                    IdCapDo = cauHoi.IdCapDo,
                    IsGeneratedOnly = true // Đảm bảo thuộc tính này khớp với query lấy bài tập
                };

                _context.CauHois.Add(newCauHoi);
                await _context.SaveChangesAsync();

                var daps = cauHoi.DapAns.Select(d => new DapAn
                {
                    IdCauHoi = newCauHoi.IdCauHoi,
                    NoiDungDapAn = d.NoiDungDapAn,
                    LaDapAnDung = d.LaDapAnDung
                });

                _context.DapAns.AddRange(daps);
                await _context.SaveChangesAsync();

                // PHÁT TÍN HIỆU RESET CACHE TOÀN HỆ THỐNG CÂU HỎI
                ResetGlobalCache();

                return Ok(new { success = true, id = newCauHoi.IdCauHoi, message = "Lưu thành công & Đã làm mới Cache." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu bài tập mới");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống." });
            }
        }

        [HttpPost("import-excel-baitap")]
        public async Task<IActionResult> ImportExcelFull(IFormFile file)
        {
            if (file == null || file.Length <= 0)
                return BadRequest(new { success = false, message = "Vui lòng chọn file Excel." });

            var extension = Path.GetExtension(file.FileName).ToLower();
            if (extension != ".xlsx" && extension != ".xls")
            {
                return BadRequest(new { success = false, message = "Chỉ chấp nhận file Excel (.xlsx, .xls)!" });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                using var stream = file.OpenReadStream();
                // Đọc toàn bộ các hàng dưới dạng danh sách
                var allRows = stream.Query(useHeaderRow: false).ToList();

                if (allRows.Count < 3)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "File Excel không hợp lệ. Phải bao gồm Hàng 1 (ID_TrungTam), Hàng 2 (Tiêu đề cột) và dữ liệu bài tập từ Hàng 3 trở đi."
                    });
                }

                // --- BƯỚC 1: ĐỌC & KIỂM TRA MÃ/ID TRUNG TÂM TẠI HÀNG 1 (Ô B1) ---
                var row1 = allRows[0] as IDictionary<string, object>;
                string? maTrungTamExcel = row1 != null && row1.ContainsKey("B")
                    ? row1["B"]?.ToString()?.Trim()
                    : row1?.Values.Skip(1).FirstOrDefault()?.ToString()?.Trim();

                if (string.IsNullOrWhiteSpace(maTrungTamExcel))
                {
                    return BadRequest(new { success = false, message = "Lỗi Hàng 1: Mã/ID Trung tâm tại ô B1 không được để trống!" });
                }

                bool isTtNumber = int.TryParse(maTrungTamExcel, out int idTtParsed);
                var trungTam = await _context.TrungTams.FirstOrDefaultAsync(t =>
                    (isTtNumber && t.IdTrungTam == idTtParsed) || t.MaTrungTam == maTrungTamExcel
                );


                // Kiểm tra phân quyền: Quản lý chỉ được nhập bài tập cho trung tâm của mình
                string? claimIdTrungTamStr = User.FindFirst("IdTrungTam")?.Value
                                             ?? HttpContext.Session.GetString("IdTrungTam");
                if (int.TryParse(claimIdTrungTamStr, out int currentIdTrungTam) && currentIdTrungTam > 0)
                {
                    if (trungTam.IdTrungTam != currentIdTrungTam)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new
                        {
                            success = false,
                            message = $"Bạn không có quyền nhập bài tập cho cơ sở '{trungTam.TenTrungTam}'!"
                        });
                    }
                }

                // --- BƯỚC 2: DUYỆT DỮ LIỆU CÂU HỎI TỪ HÀNG 3 TRỞ ĐI (index = 2) ---
                var validLoaiIds = await _context.LoaiCauHois.Select(l => l.IdLoai).ToListAsync();
                var validCapDoIds = await _context.CapDos.Select(c => c.IdCapDo).ToListAsync();

                int countSuccess = 0;

                for (int i = 2; i < allRows.Count; i++)
                {
                    var cellValues = (allRows[i] as IDictionary<string, object>)?.Values.ToList();
                    if (cellValues == null || cellValues.Count == 0) continue;

                    string? idLoaiStr = cellValues.ElementAtOrDefault(0)?.ToString();
                    string? idCapDoStr = cellValues.ElementAtOrDefault(1)?.ToString();

                    // Nếu ID_Loai hoặc ID_CapDo không hợp lệ thì dừng vòng lặp
                    if (!int.TryParse(idLoaiStr, out int idLoai) || idLoai <= 0) break;
                    if (!int.TryParse(idCapDoStr, out int idCapDo) || idCapDo <= 0) break;

                    if (!validLoaiIds.Contains(idLoai) || !validCapDoIds.Contains(idCapDo)) break;

                    string? tenChuDe = cellValues.ElementAtOrDefault(2)?.ToString();
                    string? tieuDe = cellValues.ElementAtOrDefault(3)?.ToString();
                    string? noiDungHan = cellValues.ElementAtOrDefault(4)?.ToString();
                    string? noiDungPinyin = cellValues.ElementAtOrDefault(5)?.ToString();
                    string? noiDungViet = cellValues.ElementAtOrDefault(6)?.ToString();

                    string? cauHoiText = cellValues.ElementAtOrDefault(7)?.ToString();
                    string? dapan1 = cellValues.ElementAtOrDefault(8)?.ToString();
                    string? dapan2 = cellValues.ElementAtOrDefault(9)?.ToString();
                    string? dapan3 = cellValues.ElementAtOrDefault(10)?.ToString();
                    string? dapan4 = cellValues.ElementAtOrDefault(11)?.ToString();
                    string? isDung = cellValues.ElementAtOrDefault(12)?.ToString();
                    string? giaiThich = cellValues.ElementAtOrDefault(13)?.ToString();
                    string? amThanh = cellValues.ElementAtOrDefault(14)?.ToString();

                    // 2.1. Xử lý Chủ đề (ChuDe)
                    int? currentChuDeId = null;
                    if (!string.IsNullOrWhiteSpace(tenChuDe))
                    {
                        var existingCD = await _context.ChuDes.FirstOrDefaultAsync(c => c.TenChuDe == tenChuDe.Trim());
                        if (existingCD == null)
                        {
                            var newCD = new ChuDe { TenChuDe = tenChuDe.Trim() };
                            _context.ChuDes.Add(newCD);
                            await _context.SaveChangesAsync();
                            currentChuDeId = newCD.IdChuDe;
                        }
                        else
                        {
                            currentChuDeId = existingCD.IdChuDe;
                        }
                    }

                    // 2.2. Xử lý Đoạn văn (DoanVan)
                    int? currentDoanVanId = null;
                    if (!string.IsNullOrWhiteSpace(tieuDe) && !string.IsNullOrWhiteSpace(noiDungHan))
                    {
                        var existingDV = await _context.DoanVans
                            .FirstOrDefaultAsync(d => d.NoiDungHan == noiDungHan.Trim() && d.TieuDe == tieuDe.Trim());

                        if (existingDV == null)
                        {
                            var newDV = new DoanVan
                            {
                                TieuDe = tieuDe.Trim(),
                                NoiDungHan = noiDungHan.Trim(),
                                NoiDungPinyin = noiDungPinyin?.Trim(),
                                NoiDungViet = noiDungViet?.Trim(),
                                IdChuDe = currentChuDeId
                            };
                            _context.DoanVans.Add(newDV);
                            await _context.SaveChangesAsync();
                            currentDoanVanId = newDV.IdDoanVan;
                        }
                        else
                        {
                            currentDoanVanId = existingDV.IdDoanVan;
                        }
                    }

                    // 2.3. Tạo Câu hỏi kèm theo IdTrungTam
                    var cauHoi = new CauHoi
                    {
                        IdTrungTam = trungTam.IdTrungTam, // Gán IdTrungTam bóc tách từ Hàng 1
                        NoiDung = cauHoiText?.Trim(),
                        AmThanhPath = amThanh?.Trim(),
                        GiaiThich = giaiThich?.Trim(),
                        IdLoai = idLoai,
                        IdCapDo = idCapDo,
                        IdDoanVan = currentDoanVanId,
                        IsGeneratedOnly = true,
                        DapAns = new List<DapAn>(),
                        TrangThai = true
                    };

                    // 2.4. Xử lý các đáp án
                    string?[] danhSachNoiDung = { dapan1, dapan2, dapan3, dapan4 };
                    for (int k = 0; k < danhSachNoiDung.Length; k++)
                    {
                        if (!string.IsNullOrWhiteSpace(danhSachNoiDung[k]))
                        {
                            cauHoi.DapAns.Add(new DapAn
                            {
                                NoiDungDapAn = danhSachNoiDung[k]!.Trim(),
                                LaDapAnDung = (k + 1).ToString() == isDung?.Trim()
                            });
                        }
                    }

                    _context.CauHois.Add(cauHoi);
                    countSuccess++;
                }

                // --- BƯỚC 3: LƯU & GHI NHẬN KẾT QUẢ ---
                if (countSuccess > 0)
                {
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    ResetGlobalCache();

                    return Ok(new
                    {
                        success = true,
                        message = $"Đã Import thành công {countSuccess} câu hỏi cho cơ sở '{trungTam.TenTrungTam}'."
                    });
                }

                await transaction.RollbackAsync();
                return BadRequest(new { success = false, message = "Không tìm thấy dữ liệu bài tập hợp lệ để Import." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi import file Excel bài tập.");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Lỗi hệ thống: " + ex.Message
                });
            }
        }

        [Authorize] // Bắt buộc xác thực tài khoản
        [HttpGet("danh-sach-quan-ly")]
        public async Task<IActionResult> LayDanhSachQuanLy(
             [FromQuery] bool TrangThai = true,
             [FromQuery] int page = 1,
             [FromQuery] int? idLoai = 1,
             [FromQuery] int? idCapDo = 1,
             [FromQuery] string? maTrungTam = null,
             [FromQuery] int? idTrungTam = null)
        {
            try
            {
                if (page < 1) page = 1;
                int pageSize = 20;

                // =========================================================================
                // 1. TRÍCH XUẤT CLAIMS / SESSION VÀ XÁC ĐỊNH PHÂN QUYỀN TRUY CẬP
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

                string? currentIdTrungTamStr = User.FindFirst("IdTrungTam")?.Value
                                               ?? HttpContext.Session?.GetString("IdTrungTam");
                int.TryParse(currentIdTrungTamStr, out int currentIdTrungTam);

                // Fallback đọc dữ liệu DB nếu cookie HttpOnly bị rớt Claims
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

                // 1.1. Nhận diện Admin / Quản trị viên
                bool isAdmin = roleClean.Contains("quản trị viên")
                               || roleClean.Contains("quan tri vien")
                               || roleClean.Contains("admin")
                               || idVaiTroStr == "1"
                               || roleClaims.Any(r => r.Equals("Quản trị viên", StringComparison.OrdinalIgnoreCase)
                                                   || r.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                                                   || r == "1");

                // 1.2. Nhận diện Quản lý trung tâm
                bool isQuanLy = !isAdmin && (roleClean.Contains("quản lý")
                                          || roleClean.Contains("quan ly")
                                          || roleClean.Contains("manager")
                                          || roleClaims.Any(r => r.Equals("Quản lý trung tâm", StringComparison.OrdinalIgnoreCase)
                                                              || r.Equals("Quan ly trung tam", StringComparison.OrdinalIgnoreCase)));

                // CHỈ CÓ ADMIN HOẶC QUẢN LÝ TRUNG TÂM MỚI ĐƯỢC PHÉP TRUY CẬP
                if (!isAdmin && !isQuanLy)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        success = false,
                        message = "Bạn không có quyền truy cập trang quản lý câu hỏi này."
                    });
                }

                // =========================================================================
                // 2. XÁC ĐỊNH ID TRUNG TÂM & CÔ LẬP THEO QUYỀN HẠN
                // =========================================================================
                int? targetTrungTamId = null;

                if (isQuanLy)
                {
                    // Quản lý trung tâm: Khóa chặt ID trung tâm của chính họ
                    targetTrungTamId = currentIdTrungTam;

                    if (!targetTrungTamId.HasValue || targetTrungTamId.Value <= 0)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new
                        {
                            success = false,
                            message = "Tài khoản của bạn chưa được liên kết với cơ sở đào tạo nào."
                        });
                    }
                }
                else if (isAdmin)
                {
                    // Admin: Được lọc theo idTrungTam hoặc maTrungTam gửi lên nếu muốn
                    if (idTrungTam.HasValue && idTrungTam.Value > 0)
                    {
                        targetTrungTamId = idTrungTam.Value;
                    }
                    else if (!string.IsNullOrWhiteSpace(maTrungTam))
                    {
                        var tt = await _context.TrungTams
                            .AsNoTracking()
                            .FirstOrDefaultAsync(t => t.MaTrungTam != null && t.MaTrungTam.ToLower() == maTrungTam.Trim().ToLower());

                        if (tt != null)
                        {
                            targetTrungTamId = tt.IdTrungTam;
                        }
                    }
                }

                // =========================================================================
                // 3. KHỞI TẠO QUERY & LỌC CÔ LẬP
                // =========================================================================
                var query = _context.CauHois
                    .AsNoTracking()
                    .Where(c => c.IsGeneratedOnly == true && c.TrangThai == TrangThai);

                if (isQuanLy)
                {
                    // Quản lý cơ sở: CHỈ LẤY CÂU HỎI THUỘC ĐÚNG TRUNG TÂM NÀY
                    query = query.Where(c => c.IdTrungTam == targetTrungTamId!.Value);
                }
                else if (isAdmin)
                {
                    // Admin: Nếu có lọc theo trung tâm cụ thể thì lọc, không thì lấy bài tập chung
                    if (targetTrungTamId.HasValue && targetTrungTamId.Value > 0)
                    {
                        query = query.Where(c => c.IdTrungTam == targetTrungTamId.Value);
                    }
                    else
                    {
                        // Mặc định Admin xem bài tập hệ thống dùng chung
                        query = query.Where(c => c.IdTrungTam == null || c.IdTrungTam == 0);
                    }
                }

                // Lọc linh hoạt theo Loại bài tập và Cấp độ
                if (idLoai.HasValue && idLoai > 0)
                {
                    query = query.Where(c => c.IdLoai == idLoai.Value);
                }

                if (idCapDo.HasValue && idCapDo > 0)
                {
                    query = query.Where(c => c.IdCapDo == idCapDo.Value);
                }

                // =========================================================================
                // 4. PHÂN TRANG VÀ MAPPING DỮ LIỆU
                // =========================================================================
                var totalItems = await query.CountAsync();

                var data = await query
                    .OrderByDescending(x => x.IdCauHoi)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new CauHoiModels
                    {
                        IdCauHoi = c.IdCauHoi,
                        NoiDung = c.NoiDung,
                        IdLoai = c.IdLoai,
                        IdCapDo = c.IdCapDo,
                        AmThanhPath = c.AmThanhPath,
                        GiaiThich = c.GiaiThich,
                        IdDoanVan = c.IdDoanVan,
                       
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = data,
                    totalItems = totalItems,
                    totalPages = (int)Math.Ceiling((double)totalItems / pageSize),
                    currentPage = page
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách quản lý câu hỏi");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Lỗi hệ thống khi lấy danh sách câu hỏi: " + ex.Message
                });
            }
        }

        [HttpGet("chi-tiet-cau-hoi/{maCauHoi}")]
        public async Task<IActionResult> LayChiTietCauHoi(int maCauHoi)
        {
            try
            {
                var cauHoi = await _context.CauHois
                    .AsNoTracking()
                    .Where(c => c.IdCauHoi == maCauHoi)
                    .Select(c => new CauHoiModels
                    {
                        IdCauHoi = c.IdCauHoi,
                        NoiDung = c.NoiDung,
                        IdLoai = c.IdLoai,
                        IdCapDo = c.IdCapDo,
                        AmThanhPath = c.AmThanhPath,
                        GiaiThich = c.GiaiThich,
                        IdDoanVan = c.IdDoanVan,
                        IsGeneratedOnly = c.IsGeneratedOnly,
                        // Mapping Đoạn văn nếu có
                        DoanVan = c.IdDoanVanNavigation != null ? new DoanVanModels
                        {
                            IdDoanVan = c.IdDoanVanNavigation.IdDoanVan,
                            TieuDe = c.IdDoanVanNavigation.TieuDe,
                            NoiDungHan = c.IdDoanVanNavigation.NoiDungHan,
                            NoiDungPinyin = c.IdDoanVanNavigation.NoiDungPinyin,
                            NoiDungViet = c.IdDoanVanNavigation.NoiDungViet,
                            AnhMinhHoa = c.IdDoanVanNavigation.AnhMinhHoa
                        } : null,
                        // Mapping danh sách đáp án
                        DapAns = c.DapAns.Select(d => new DapAnModels
                        {
                            IdDapAn = d.IdDapAn,
                            NoiDungDapAn = d.NoiDungDapAn,
                            LaDapAnDung = d.LaDapAnDung ?? false
                        }).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (cauHoi == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy câu hỏi này." });
                }

                return Ok(new { success = true, data = cauHoi });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi lấy chi tiết câu hỏi {maCauHoi}");
                return StatusCode(500, "Lỗi hệ thống.");
            }
        }

        [HttpPut("sua-cau-hoi/{id}")]
        public async Task<IActionResult> SuaCauHoi(int id, [FromBody] EditBaiTap model)
        {
            if (model == null) return BadRequest("Dữ liệu không hợp lệ.");
            if (model.IdCauHoi.HasValue && id != model.IdCauHoi.Value) return BadRequest("ID câu hỏi không đồng nhất.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Lấy dữ liệu Câu hỏi, Include cả Đáp án và Đoạn văn
                var existingCauHoi = await _context.CauHois
                    .Include(c => c.DapAns)
                    .Include(c => c.IdDoanVanNavigation)
                    .FirstOrDefaultAsync(c => c.IdCauHoi == id);

                if (existingCauHoi == null) return NotFound("Không tìm thấy câu hỏi.");

                // 2. Cập nhật bảng Câu Hỏi
                existingCauHoi.NoiDung = model.NoiDung;
                existingCauHoi.AmThanhPath = model.AmThanhPath;
                existingCauHoi.GiaiThich = model.GiaiThich;
                existingCauHoi.IdLoai = model.IdLoai;
                existingCauHoi.IdCapDo = model.IdCapDo;
                existingCauHoi.IsGeneratedOnly =true;
                existingCauHoi.DoKho = model.DoKho;
                
                // 3. Cập nhật bảng Đoạn Văn (Nếu câu hỏi có kèm đoạn văn)
                if (existingCauHoi.IdDoanVanNavigation != null)
                {
                    existingCauHoi.IdDoanVanNavigation.IdChuDe = model.IdChuDe;
                    existingCauHoi.IdDoanVanNavigation.TieuDe = model.TieuDe;
                    existingCauHoi.IdDoanVanNavigation.NoiDungHan = model.NoiDungHan;
                    existingCauHoi.IdDoanVanNavigation.NoiDungPinyin = model.NoiDungPinyin;
                    existingCauHoi.IdDoanVanNavigation.NoiDungViet = model.NoiDungViet;
                    existingCauHoi.IdDoanVanNavigation.AnhMinhHoa = model.AnhMinhHoa;
                }

                // 4. Xử lý thông minh bảng Đáp Án (Thêm / Sửa / Xóa)
                if (model.DapAns != null && model.DapAns.Any())
                {
                    var incomingDapAnIds = model.DapAns.Where(d => d.IdDapAn > 0).Select(d => d.IdDapAn).ToList();

                    // Xóa đáp án cũ bị loại bỏ trên UI
                    var dapAnsToRemove = existingCauHoi.DapAns
                        .Where(d => !incomingDapAnIds.Contains(d.IdDapAn))
                        .ToList();
                    _context.DapAns.RemoveRange(dapAnsToRemove);

                    // Cập nhật đáp án hiện có hoặc thêm mới
                    foreach (var item in model.DapAns)
                    {
                        if (item.IdDapAn > 0)
                        {
                            var existingDapAn = existingCauHoi.DapAns.FirstOrDefault(d => d.IdDapAn == item.IdDapAn);
                            if (existingDapAn != null)
                            {
                                existingDapAn.NoiDungDapAn = item.NoiDungDapAn;
                                existingDapAn.LaDapAnDung = item.LaDapAnDung;
                            }
                        }
                        else
                        {
                            existingCauHoi.DapAns.Add(new DapAn
                            {
                                IdCauHoi = id,
                                NoiDungDapAn = item.NoiDungDapAn,
                                LaDapAnDung = item.LaDapAnDung
                            });
                        }
                    }
                }

                // 5. Lưu xuống DB
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                ResetGlobalCache();
                return Ok(new { success = true, message = "Cập nhật bài tập thành công." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Lỗi khi sửa bài tập ID: {id}");
                return StatusCode(500, "Lỗi khi cập nhật dữ liệu.");
            }
        }

        [HttpDelete("xoa-cau-hoi/{id}")]
        public async Task<IActionResult> XoaCauHoi(int id)
        {
            // Sử dụng Transaction để đảm bảo tính toàn vẹn dữ liệu
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Lấy câu hỏi kèm THEO TẤT CẢ các dữ liệu liên quan
                var cauHoi = await _context.CauHois
                    .Include(c => c.DapAns)
                    .Include(c => c.ChiTietLichSus) // Liên kết bảng Lịch sử làm bài
                    .Include(c => c.IdNguPhaps)     // Liên kết Many-to-Many Ngữ Pháp
                    .Include(c => c.IdTuVungs)      // Liên kết Many-to-Many Từ Vựng
                    .FirstOrDefaultAsync(c => c.IdCauHoi == id);

                if (cauHoi == null) return NotFound(new { success = false, message = "Không tìm thấy câu hỏi để xóa." });

                // 2. XÓA DỮ LIỆU TỪ CÁC BẢNG LIÊN QUAN TRƯỚC (Bảng Con)

                // A. Xóa Chi Tiết Lịch Sử
                if (cauHoi.ChiTietLichSus.Any())
                {
                    _context.ChiTietLichSus.RemoveRange(cauHoi.ChiTietLichSus);
                }

                // B. Xóa Đáp Án
                if (cauHoi.DapAns.Any())
                {
                    _context.DapAns.RemoveRange(cauHoi.DapAns);
                }

                // C. Xóa liên kết Many-to-Many (Ngữ pháp & Từ vựng)
                // Việc gọi .Clear() sẽ yêu cầu EF Core tự động xóa các dòng trong bảng trung gian (ví dụ: CauHoi_TuVung)
                cauHoi.IdNguPhaps.Clear();
                cauHoi.IdTuVungs.Clear();

                // 3. SAU CÙNG LÀ XÓA CÂU HỎI (Bảng Cha)
                _context.CauHois.Remove(cauHoi);

                // 4. Lưu thay đổi và Commit Transaction
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 5. Làm mới Cache để App cập nhật ngay lập tức
                ResetGlobalCache();

                return Ok(new { success = true, message = "Đã xóa câu hỏi và toàn bộ dữ liệu liên quan thành công." });
            }
            catch (Exception ex)
            {
                // Nếu có bất kỳ lỗi nào xảy ra, Rollback lại toàn bộ để không bị rác dữ liệu
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Lỗi nghiêm trọng khi xóa câu hỏi ID: {id}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: Không thể xóa câu hỏi và các dữ liệu liên quan." });
            }
        }

        [HttpPatch("vo-hieu-hoa-hang-loat")]
        public async Task<IActionResult> VoHieuHoaHangLoat([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any()) return BadRequest("Không có ID nào được chọn.");

            try
            {
                // Lấy danh sách các câu hỏi thuộc các ID này
                var cauHois = await _context.CauHois
                    .Where(c => ids.Contains(c.IdCauHoi))
                    .ToListAsync();

                foreach (var item in cauHois)
                {
                    item.TrangThai = false; // Vô hiệu hóa
                }

                await _context.SaveChangesAsync();
                ResetGlobalCache();

                return Ok(new { success = true, message = $"Đã vô hiệu hóa {cauHois.Count} câu hỏi." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi vô hiệu hóa hàng loạt");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống." });
            }
        }

        [HttpPatch("kich-hoat-hang-loat")]
        public async Task<IActionResult> KichHoatHangLoat([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any()) return BadRequest("Không có ID nào được chọn.");

            try
            {
                // Lấy danh sách các câu hỏi thuộc các ID này
                var cauHois = await _context.CauHois
                    .Where(c => ids.Contains(c.IdCauHoi))
                    .ToListAsync();

                foreach (var item in cauHois)
                {
                    item.TrangThai = true; // Kích hoạt
                }

                await _context.SaveChangesAsync();
                ResetGlobalCache();

                return Ok(new { success = true, message = $"Đã kích hoạt {cauHois.Count} câu hỏi." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kích hoạt hàng loạt");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống." });
            }
        }

        /// <summary>
        /// 1. API Lấy danh sách toàn bộ loại bài tập (Dành cho trang Quản lý của Admin)
        /// </summary>
        [HttpGet("admin/danhsach-loaibaitap")]
        public async Task<IActionResult> GetDanhSachLoaiBaiTapAdmin()
        {
            try
            {
                var danhSach = await _context.LoaiCauHois
                    .AsNoTracking()
                    .Select(l => new LoaiBaiTapModel
                    {
                        IdLoai = l.IdLoai,
                        TenLoai = l.TenLoai,
                        ChuongTrinh = l.ChuongTrinh
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = danhSach });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách loại bài tập Admin");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống nội bộ." });
            }
        }

        /// <summary>
        /// 2. API Thêm mới loại bài tập
        /// </summary>
        [HttpPost("admin/them-loaibaitap")]
        public async Task<IActionResult> ThemLoaiBaiTap([FromBody] LoaiBaiTapAdd model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TenLoai))
            {
                return BadRequest(new { success = false, message = "Dữ liệu đầu vào không hợp lệ hoặc Tên loại bị trống." });
            }

            try
            {
                // Khởi tạo thực thể định dạng bảng Database
                var newLoai = new LoaiCauHoi
                {
                    TenLoai = model.TenLoai.Trim(),
                    ChuongTrinh = model.ChuongTrinh
                };

                _context.LoaiCauHois.Add(newLoai);
                await _context.SaveChangesAsync();

                // PHÁT TÍN HIỆU XOÁ CACHE: Vì danh sách loại bài tập đã bị thay đổi
                ResetGlobalCache();

                return Ok(new
                {
                    success = true,
                    message = "Thêm mới loại bài tập thành công!",
                    idLoai = newLoai.IdLoai
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm mới loại bài tập");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi thêm mới." });
            }
        }

        /// <summary>
        /// 3. API Chỉnh sửa loại bài tập
        /// </summary>
        [HttpPut("admin/sua-loaibaitap/{id}")]
        public async Task<IActionResult> SuaLoaiBaiTap(int id, [FromBody] LoaiBaiTapEdit model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TenLoai))
            {
                return BadRequest(new { success = false, message = "Dữ liệu cập nhật không hợp lệ." });
            }

            try
            {
                // Tìm kiếm loại câu hỏi cũ trong DB
                var loaiHienTai = await _context.LoaiCauHois.FirstOrDefaultAsync(x => x.IdLoai == id);

                if (loaiHienTai == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy loại bài tập cần sửa." });
                }

                // Cập nhật giá trị mới
                loaiHienTai.TenLoai = model.TenLoai.Trim();
                loaiHienTai.ChuongTrinh = model.ChuongTrinh;

                _context.LoaiCauHois.Update(loaiHienTai);
                await _context.SaveChangesAsync();

                // PHÁT TÍN HIỆU XOÁ CACHE: Đảm bảo dữ liệu hiển thị phía client đồng bộ lập tức
                ResetGlobalCache();

                return Ok(new { success = true, message = "Cập nhật loại bài tập thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật loại bài tập ID: {Id}", id);
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi chỉnh sửa." });
            }
        }


        [HttpPost("nop-bai-nhieu-cau")]
        public async Task<IActionResult> NopBaiVaChamDiemAiList([FromBody] List<BaiTapAi> submissions)
        {
            if (submissions == null || !submissions.Any())
            {
                return BadRequest("Dữ liệu nộp bài không hợp lệ.");
            }

            string apiKey = _configuration["Gemini:ApiKey"] ?? string.Empty;
            if (string.IsNullOrEmpty(apiKey))
            {
                return StatusCode(500, "Chưa thiết lập Gemini API Key.");
            }

            // 1. Lấy toàn bộ ID câu hỏi để truy vấn DB 1 lần (Tối ưu hiệu năng DB)
            var listIdCauHoi = submissions.Select(s => s.IdCauHoi).ToList();
            var dictCauHoi = await _context.CauHois
                .Include(c => c.IdCapDoNavigation)
                .Where(c => listIdCauHoi.Contains(c.IdCauHoi))
                .ToDictionaryAsync(c => c.IdCauHoi);

            // 2. Chuẩn bị danh sách các Tác vụ (Tasks) để chạy song song
            var taskList = new List<Task<BaiTapAi>>();

            foreach (var submission in submissions)
            {
                if (dictCauHoi.TryGetValue(submission.IdCauHoi, out var cauHoi))
                {
                    submission.CauHoiGoc = cauHoi.NoiDung ?? string.Empty;
                    submission.IdLoai = cauHoi.IdLoai ?? 0;
                    submission.TenCapDo = cauHoi.IdCapDoNavigation?.TenCapDo ?? "HSK 3";

                    // Đưa việc gọi AI (kèm Retry) vào một Task độc lập
                    taskList.Add(ProcessSingleSubmissionAsync(submission, apiKey));
                }
            }

            // 3. CHẠY SONG SONG TẤT CẢ CÁC CÂU CÙNG LÚC (Phép màu tốc độ ở đây)
            // Lệnh này sẽ đợi đến khi tất cả 5 câu đều được AI trả kết quả về
            BaiTapAi[] results = await Task.WhenAll(taskList);

            // 4. Lọc ra các câu lỗi (nếu có) để báo cho Client
            var failedSubmissions = results.Where(r => r.Evaluation == null).ToList();

            return Ok(new
            {
                Message = failedSubmissions.Any() ? "Có một số câu AI quá tải, vui lòng thử lại." : "Chấm điểm thành công.",
                TotalProcessed = results.Length,
                FailedCount = failedSubmissions.Count,
                Data = results // Trả về danh sách đã được điền Evaluation
            });
        }

        // --- Hàm phụ trợ: Bọc logic Retry lại để tái sử dụng cho từng Task ---
        private async Task<BaiTapAi> ProcessSingleSubmissionAsync(BaiTapAi submission, string apiKey)
        {
            int maxRetries = 3;
            bool isSuccess = false;

            for (int i = 0; i < maxRetries; i++)
            {
                var result = await submission.EvaluateTranslationAsync(apiKey);
                isSuccess = result.IsSuccess;

                if (isSuccess && submission.Evaluation != null)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(2 * (i + 1)));
            }

            return submission; // Trả về object chứa kết quả chấm
        }

        private void ResetGlobalCache()
        {
            _resetCacheSignal.Cancel();
            _resetCacheSignal.Dispose();
            _resetCacheSignal = new CancellationTokenSource();
            _logger.LogWarning("Global Cache has been invalidated due to data change.");
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId)) return userId;

            var sessionUserId = HttpContext.Session.GetString("MaNguoiDung");
            if (!string.IsNullOrEmpty(sessionUserId) && int.TryParse(sessionUserId, out int sUserId)) return sUserId;

            return null;
        }
    }
}