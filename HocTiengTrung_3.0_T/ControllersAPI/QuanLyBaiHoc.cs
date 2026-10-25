using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12;
using HocTiengTrung_3._0_T.Models12.BaiTap;
using HocTiengTrung_3._0_T.Models12.QuanLyBaiHoc;
using HocTiengTrung_3._0_T.Models12.TuVung;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using MiniExcelLibs;

namespace HocTiengTrung_3._0_T.ControllersAPI
{
    [Route("api/quanlybaihoc")]
    [ApiController]
    public class QuanLyBaiHoc : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<QuanLyBaiHoc> _logger;
        private readonly IUserService _userService;

        // TokenSource để hủy toàn bộ cache liên quan đến câu hỏi khi có thay đổi (Add/Update/Delete)
        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();

        public QuanLyBaiHoc(TmdtContext context, IMemoryCache cache, ILogger<QuanLyBaiHoc> logger, IUserService userService)
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

        [HttpGet("danhsachbaihoc/{idCapDo}/{idGiaoTrinh}")]
        public async Task<IActionResult> DanhSachBaiHoc(
     [FromRoute] int idCapDo,
     [FromRoute] int idGiaoTrinh,
     [FromQuery] int page = 1)
        {
            if (page < 1) page = 1;
            int pageSize = 10;

            // =========================================================================
            // 1. TRÍCH XUẤT THÔNG TIN TRUNG TÂM TỪ CLAIMS / SESSION CỦA USER ĐĂNG NHẬP
            // =========================================================================
            string? idTrungTamClaim = User.FindFirst("IdTrungTam")?.Value
                                      ?? HttpContext.Session.GetString("IdTrungTam");

            string? maTrungTamClaim = User.FindFirst("MaTrungTam")?.Value
                                      ?? HttpContext.Session.GetString("MaTrungTam");

            int? userTrungTamId = null;

            if (int.TryParse(idTrungTamClaim, out int parsedTtId) && parsedTtId > 0)
            {
                userTrungTamId = parsedTtId;
            }
            else if (!string.IsNullOrWhiteSpace(maTrungTamClaim))
            {
                // Nếu claim chỉ lưu Mã trung tâm, truy vấn ID tương ứng
                var tt = await _context.TrungTams
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.MaTrungTam == maTrungTamClaim.Trim());
                if (tt != null)
                {
                    userTrungTamId = tt.IdTrungTam;
                }
            }

            // =========================================================================
            // 2. TẠO CACHE KEY CÓ CHỨA ID TRUNG TÂM ĐỂ TRÁNH NHẦM DỮ LIỆU GIỮA CÁC CƠ SỞ
            // =========================================================================
            bool isVip = await _userService.IsUserPremiumAsync(User, HttpContext);
            string ttCacheSegment = userTrungTamId.HasValue ? $"TT_{userTrungTamId.Value}" : "TT_Global";
            string cacheKey = $"BaiHoc_CapDo_{idCapDo}_GiaoTrinh_{idGiaoTrinh}_{ttCacheSegment}_Page_{page}_Vip_{isVip}";

            try
            {
                if (!_cache.TryGetValue(cacheKey, out object cacheResult))
                {
                    _logger.LogInformation($"Cache miss cho key: {cacheKey}. Đang lấy dữ liệu từ Database...");

                    // =========================================================================
                    // 3. THIẾT LẬP QUERY & LUÔN LỌC THEO TRUNG TÂM CỦA TÀI KHOẢN ĐANG ĐĂNG NHẬP
                    // =========================================================================
                    var query = _context.BaiHocs
                        .Where(b => b.IdCapDo == idCapDo
                                 && b.IdGiaoTrinh == idGiaoTrinh
                                 && b.TrangThai == true);

                    if (userTrungTamId.HasValue)
                    {
                        // Chỉ lấy bài học thuộc trung tâm của người dùng này
                        query = query.Where(b => b.IdTrungTam == userTrungTamId.Value);
                    }
                    else
                    {
                        // Nếu tài khoản không thuộc trung tâm nào, lấy bài học chung hệ thống (IdTrungTam == null hoặc = 0)
                        query = query.Where(b => b.IdTrungTam == null || b.IdTrungTam == 0);
                    }

                    query = query.OrderBy(b => b.SoThuTuBai);

                    int totalItems = await query.CountAsync();
                    int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                    var items = await query
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(b => new
                        {
                            IdBaiHoc = b.IdBaiHoc,
                            IdCapDo = b.IdCapDo,
                            IdTrungTam = b.IdTrungTam,
                            SoThuTuBai = b.SoThuTuBai,
                            TenBaiHoc = b.TenBaiHoc,
                            MoTa = b.MoTa,
                            TrangThai = b.TrangThai,

                            // Nếu không phải VIP và Số thứ tự bài lớn hơn 1 thì hiển thị trạng thái Khóa = true
                            IsLocked = !isVip && b.SoThuTuBai > 1
                        })
                        .ToListAsync();

                    cacheResult = new
                    {
                        TotalItems = totalItems,
                        TotalPages = totalPages,
                        CurrentPage = page,
                        PageSize = pageSize,
                        IdTrungTam = userTrungTamId,
                        Data = items
                    };

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromHours(2))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                    _cache.Set(cacheKey, cacheResult, cacheOptions);
                }
                else
                {
                    _logger.LogInformation($"Cache hit cho key: {cacheKey}. Trả dữ liệu từ Cache.");
                }

                return Ok(cacheResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi lấy danh sách bài học cho cấp độ {idCapDo}, giáo trình {idGiaoTrinh}, Trung tâm: {userTrungTamId}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi hệ thống.");
            }
        }

        [HttpPost("import-excel-tao-bai-hoc")]
        public async Task<IActionResult> ImportExcelTaoBaiHocV2(IFormFile file)
        {
            // 1. Kiểm tra file hợp lệ
            if (file == null || file.Length <= 0)
                return BadRequest(new { success = false, message = "Vui lòng chọn file Excel." });

            var extension = Path.GetExtension(file.FileName).ToLower();
            if (extension != ".xlsx" && extension != ".xls")
            {
                return BadRequest(new { success = false, message = "Chỉ chấp nhận file định dạng Excel (.xlsx, .xls)!" });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                using var stream = file.OpenReadStream();

                // Đọc toàn bộ các dòng trong file Excel dưới dạng danh sách (bắt đầu từ index 0)
                var allRows = stream.Query(useHeaderRow: false).ToList();

                // Cấu trúc mới: Tối thiểu 5 hàng thông tin + 1 hàng từ vựng + 1 hàng tiêu đề cột câu hỏi = 7 hàng
                if (allRows.Count < 7)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Cấu trúc file không hợp lệ. Phải bao gồm Hàng 1 (ID_TrungTam), Hàng 2-5 (Thông tin bài học), Hàng 6 (Danh sách từ vựng) và dữ liệu câu hỏi từ Hàng 8."
                    });
                }

                // --- BƯỚC 1: BÓC TÁCH THÔNG TIN TRUNG TÂM & BÀI HỌC (HÀNG 1 ĐẾN HÀNG 5) ---
                var row1 = allRows[0] as IDictionary<string, object>; // Hàng 1: ID_TrungTam
                var row2 = allRows[1] as IDictionary<string, object>; // Hàng 2: Tên bài học (B2) & ID Chương trình/Giáo trình (C2)
                var row3 = allRows[2] as IDictionary<string, object>; // Hàng 3: Số thứ tự bài (B3)
                var row4 = allRows[3] as IDictionary<string, object>; // Hàng 4: Cấp độ ID (B4)
                var row5 = allRows[4] as IDictionary<string, object>; // Hàng 5: Mô tả bài học (B5)

                // 1.1. Lấy và kiểm tra ID_TrungTam (Hàng 1 - Cột B / Index 1)
                string? idTrungTamStr = row1?.Values.Skip(1).FirstOrDefault()?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(idTrungTamStr))
                {
                    return BadRequest(new { success = false, message = "Lỗi Hàng 1: ID_TrungTam không được để trống (tại ô B1)." });
                }

                bool isTtNumber = int.TryParse(idTrungTamStr, out int idTtParsed);
                var trungTam = await _context.TrungTams.FirstOrDefaultAsync(t =>
                    (isTtNumber && t.IdTrungTam == idTtParsed) || t.MaTrungTam == idTrungTamStr
                );

                if (trungTam == null)
                {
                    return BadRequest(new { success = false, message = $"Không tìm thấy Trung tâm với mã/ID '{idTrungTamStr}' trong hệ thống." });
                }

                // Bảo mật Multi-tenant: Kiểm tra tài khoản đăng nhập có thuộc trung tâm này không
                string? claimIdTrungTamStr = User.FindFirst("IdTrungTam")?.Value
                                             ?? HttpContext.Session.GetString("IdTrungTam");
                if (int.TryParse(claimIdTrungTamStr, out int currentIdTrungTam) && currentIdTrungTam > 0)
                {
                    if (trungTam.IdTrungTam != currentIdTrungTam)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new
                        {
                            success = false,
                            message = $"Bạn không có quyền nhập bài học cho trung tâm '{trungTam.TenTrungTam}'!"
                        });
                    }
                }

                // 1.2. Lấy thông tin bài học từ Hàng 2 đến Hàng 5
                string? tenBaiHoc = row2?.Values.Skip(1).FirstOrDefault()?.ToString();
                string? idChuongTrinh = row2?.Values.Skip(2).FirstOrDefault()?.ToString(); // Cột C (Index 2) của Hàng 2
                string? soThuTuBaiStr = row3?.Values.Skip(1).FirstOrDefault()?.ToString();
                string? idCapDoStr = row4?.Values.Skip(1).FirstOrDefault()?.ToString();
                string? moTa = row5?.Values.Skip(1).FirstOrDefault()?.ToString();

                // Kiểm tra tính hợp lệ
                if (string.IsNullOrWhiteSpace(tenBaiHoc))
                    return BadRequest(new { success = false, message = "Lỗi Hàng 2: Tên bài học không được để trống (ô B2)." });

                if (!int.TryParse(idChuongTrinh, out int idChuongTrinhInt))
                    return BadRequest(new { success = false, message = "Lỗi Hàng 2: ID Chương Trình/Giáo Trình phải là một số nguyên hợp lệ (ô C2)." });

                if (!int.TryParse(idCapDoStr, out int idCapDo))
                    return BadRequest(new { success = false, message = "Lỗi Hàng 4: Cấp độ (ID) phải là một số nguyên hợp lệ (ô B4)." });

                if (!int.TryParse(soThuTuBaiStr, out int soThuTuBai))
                {
                    int maxSoThuTuBai = await _context.BaiHocs
                        .Where(b => b.IdCapDo == idCapDo && b.IdTrungTam == trungTam.IdTrungTam)
                        .Select(b => (int?)b.SoThuTuBai)
                        .MaxAsync() ?? 0;
                    soThuTuBai = maxSoThuTuBai + 1;
                }

                var capDoExists = await _context.CapDos.AnyAsync(c => c.IdCapDo == idCapDo);
                if (!capDoExists)
                    return BadRequest(new { success = false, message = $"Cấp độ với ID = {idCapDo} không tồn tại trong hệ thống." });

                // --- BƯỚC 2: KHỞI TẠO BÀI HỌC MỚI GẮN VỚI TRUNG TÂM ---
                var newBaiHoc = new BaiHoc
                {
                    IdTrungTam = trungTam.IdTrungTam, // Gán IdTrungTam
                    IdCapDo = idCapDo,
                    TenBaiHoc = tenBaiHoc.Trim(),
                    MoTa = moTa?.Trim(),
                    SoThuTuBai = soThuTuBai,
                    IdGiaoTrinh = idChuongTrinhInt,
                    TrangThai = true
                };

                _context.BaiHocs.Add(newBaiHoc);
                await _context.SaveChangesAsync(); // Lưu đợt 1 để sinh IdBaiHoc

                // --- BƯỚC 3: TẢI VÀ CHUẨN HÓA KHO TỪ VỰNG TRÊN RAM ---
                var rawTuVungList = await _context.TuVungs
                    .Where(t => t.ChuHan != null && t.ChuHan != "")
                    .Select(t => new { t.ChuHan, t.IdTuVung })
                    .ToListAsync();

                var tuVungDict = rawTuVungList
                    .Select(t => new
                    {
                        ChuHanCleaned = t.ChuHan!.Trim()
                                                 .Replace("\u200B", "")
                                                 .Replace("\u00A0", "")
                                                 .Replace(" ", "")
                                                 .Replace(" ", ""),
                        t.IdTuVung
                    })
                    .Where(t => !string.IsNullOrEmpty(t.ChuHanCleaned))
                    .GroupBy(t => t.ChuHanCleaned)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(x => x.IdTuVung).ToList()
                    );

                // --- BƯỚC 4: ĐỌC VÀ XỬ LÝ TỪ VỰNG TẠI HÀNG THỨ 6 (Index 5) ---
                var row6 = allRows[5] as IDictionary<string, object>;
                string? danhSachTuVungStr = row6?.Values.Skip(1).FirstOrDefault()?.ToString();

                var danhSachBaiHocTuVung = new List<BaiHocTuVung>();
                var idTuVungDaThemTrongBai = new HashSet<int>();
                int thuTuTuVung = 1;

                if (!string.IsNullOrWhiteSpace(danhSachTuVungStr))
                {
                    var tuVungList = danhSachTuVungStr
                        .Split(new[] { ',', '，', '、' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(w => w.Trim().Replace("\u200B", "").Replace("\u00A0", ""))
                        .Where(w => !string.IsNullOrEmpty(w))
                        .ToList();

                    foreach (var word in tuVungList)
                    {
                        if (tuVungDict.TryGetValue(word, out List<int>? listIdTuVung))
                        {
                            foreach (var idTuVung in listIdTuVung)
                            {
                                if (!idTuVungDaThemTrongBai.Contains(idTuVung))
                                {
                                    idTuVungDaThemTrongBai.Add(idTuVung);
                                    danhSachBaiHocTuVung.Add(new BaiHocTuVung
                                    {
                                        IdBaiHoc = newBaiHoc.IdBaiHoc,
                                        IdTuVung = idTuVung,
                                        ThuTu = thuTuTuVung++,
                                        LaTuMoi = true
                                    });
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"Từ vựng '{word}' ở hàng 6 không tồn tại trong kho TuVung.");
                        }
                    }
                }

                // --- BƯỚC 5: DUYỆT CÁC DÒNG CÂU HỎI TỪ HÀNG THỨ 8 TRỞ ĐI (Index = 7) ---
                var validLoaiIds = await _context.LoaiCauHois.Select(l => l.IdLoai).ToListAsync();
                var validCapDoIds = await _context.CapDos.Select(c => c.IdCapDo).ToListAsync();

                int countSuccess = 0;
                int maxThuTuCauHoi = 0;
                var danhSachCauHoiMoi = new List<CauHoi>();

                // allRows[5] là Hàng 6 (Từ vựng)
                // allRows[6] là Hàng 7 (Tiêu đề cột câu hỏi)
                // Dữ liệu câu hỏi bắt đầu từ index 7 (Hàng 8 trong Excel)
                for (int i = 7; i < allRows.Count; i++)
                {
                    var cellValues = (allRows[i] as IDictionary<string, object>)?.Values.ToList();
                    if (cellValues == null || cellValues.Count == 0) continue;

                    string? idLoaiStr = cellValues.ElementAtOrDefault(0)?.ToString();
                    string? idCapDoRowStr = cellValues.ElementAtOrDefault(1)?.ToString();

                    if (!int.TryParse(idLoaiStr, out int idLoai) || idLoai <= 0) break;
                    if (!int.TryParse(idCapDoRowStr, out int idCapDoRow)) break;

                    if (!validLoaiIds.Contains(idLoai) || !validCapDoIds.Contains(idCapDoRow)) break;

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

                    // Xử lý Chủ Đề (ChuDe)
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

                    // Xử lý Đoạn Văn (DoanVan)
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

                    // Khởi tạo Câu hỏi & Đáp án (Gán IdTrungTam cho câu hỏi)
                    var cauHoi = new CauHoi
                    {
                        IdTrungTam = trungTam.IdTrungTam,
                        NoiDung = cauHoiText?.Trim(),
                        GiaiThich = giaiThich?.Trim(),
                        IdLoai = idLoai,
                        IdCapDo = idCapDoRow,
                        IdDoanVan = currentDoanVanId,
                        IsGeneratedOnly = false,
                        TrangThai = true,
                        DapAns = new List<DapAn>(),
                        AmThanhPath = amThanh?.Trim()
                    };

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
                    danhSachCauHoiMoi.Add(cauHoi);
                    countSuccess++;
                }

                // --- BƯỚC 6: LIÊN KẾT CÂU HỎI VÀ TỪ VỰNG VỚI BÀI HỌC VỪA TẠO ---
                if (countSuccess > 0)
                {
                    await _context.SaveChangesAsync(); // Lưu đợt 2 để sinh IdCauHoi

                    // 6.1. Thêm liên kết bài học - câu hỏi
                    foreach (var cauHoiMoi in danhSachCauHoiMoi)
                    {
                        maxThuTuCauHoi++;
                        var baiHocCauHoi = new BaiHocCauHoi
                        {
                            IdBaiHoc = newBaiHoc.IdBaiHoc,
                            IdCauHoi = cauHoiMoi.IdCauHoi,
                            ThuTu = maxThuTuCauHoi
                        };
                        _context.BaiHocCauHois.Add(baiHocCauHoi);
                    }

                    // 6.2. Thêm toàn bộ liên kết bài học - từ vựng
                    if (danhSachBaiHocTuVung.Any())
                    {
                        _context.BaiHocTuVungs.AddRange(danhSachBaiHocTuVung);
                    }

                    await _context.SaveChangesAsync(); // Lưu đợt 3
                    await transaction.CommitAsync();

                    ClearAllBaiHocCache();

                    return Ok(new
                    {
                        success = true,
                        message = $"Tạo thành công bài học '{newBaiHoc.TenBaiHoc}' (STT: {newBaiHoc.SoThuTuBai}) cho trung tâm '{trungTam.TenTrungTam}' với {countSuccess} câu hỏi và {danhSachBaiHocTuVung.Count} liên kết từ vựng."
                    });
                }

                await transaction.RollbackAsync();
                return BadRequest(new { success = false, message = "Không tìm thấy dữ liệu câu hỏi hợp lệ để khởi tạo bài học." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi xử lý cấu trúc file Excel kết hợp tạo bài học.");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost("import-excel-tuvung-vao-bai-hoc")]
        public async Task<IActionResult> ImportExcelTuVungVaoBaiHoc(IFormFile file)
        {
            // 1. Kiểm tra file đầu vào
            if (file == null || file.Length <= 0)
                return BadRequest(new { success = false, message = "Vui lòng chọn file Excel." });

            try
            {
                using var stream = file.OpenReadStream();
                // Đọc toàn bộ các dòng, coi dòng 0 là Hàng 1 trong Excel
                var allRows = stream.Query(useHeaderRow: false).ToList();

                // Cần ít nhất 4 dòng: Dòng 1 (Số TT), Dòng 2 (Cấp độ), Dòng 3 (Tiêu đề), Dòng 4 (Dữ liệu)
                if (allRows.Count <= 3)
                    return BadRequest(new { success = false, message = "File Excel không có đủ dữ liệu từ vựng." });

                // 2. Đọc Số thứ tự bài từ Hàng 1 (Index 0), Cột B (Index 1)
                var row1 = allRows[0] as IDictionary<string, object>;
                string? soThuTuStr = row1?.Values.ElementAtOrDefault(1)?.ToString();

                if (!int.TryParse(soThuTuStr, out int soThuTu) || soThuTu <= 0)
                    return BadRequest(new { success = false, message = "Không tìm thấy Số thứ tự bài hợp lệ ở hàng 1, cột B." });

                // Đọc Cấp độ (ID) từ Hàng 2 (Index 1), Cột B (Index 1)
                var row2 = allRows[1] as IDictionary<string, object>;
                string? idCapDoStr = row2?.Values.ElementAtOrDefault(1)?.ToString();

                if (!int.TryParse(idCapDoStr, out int idCapDo) || idCapDo <= 0)
                    return BadRequest(new { success = false, message = "Không tìm thấy Cấp độ (ID) hợp lệ ở hàng 2, cột B." });

                // Bắt đầu Transaction
                using var transaction = await _context.Database.BeginTransactionAsync();

                // 3. Tìm Bài học dựa vào Số thứ tự bài và ID Cấp độ
                // Lưu ý: Điều chỉnh 'SoThuTu' thành tên thuộc tính thực tế trong Entity BaiHoc của bạn (VD: 'ThuTu', 'SoThuTuBai'...)
                var baiHoc = await _context.BaiHocs.FirstOrDefaultAsync(b => b.SoThuTuBai == soThuTu && b.IdCapDo == idCapDo);
                if (baiHoc == null)
                    return NotFound(new { success = false, message = $"Không tìm thấy bài học có Số thứ tự {soThuTu} thuộc Cấp độ (ID: {idCapDo})." });

                // Lấy ID bài học thực tế để sử dụng cho các thao tác bên dưới
                int idBaiHoc = baiHoc.IdBaiHoc;

                // 4. Tải kho từ vựng và liên kết hiện tại lên RAM để tối ưu tốc độ kiểm tra
                var rawTuVungList = await _context.TuVungs
                    .Where(t => t.ChuHan != null && t.ChuHan != "")
                    .Select(t => new { t.ChuHan, t.IdTuVung })
                    .ToListAsync();

                var tuVungDict = rawTuVungList
                    .Select(t => new
                    {
                        ChuHanCleaned = t.ChuHan!.Trim()
                                              .Replace("\u200B", "")
                                              .Replace("\u00A0", "")
                                              .Replace(" ", "")
                                              .Replace(" ", ""), // Xử lý khoảng trắng tiếng Trung
                        t.IdTuVung
                    })
                    .Where(t => !string.IsNullOrEmpty(t.ChuHanCleaned))
                    .GroupBy(t => t.ChuHanCleaned)
                    .ToDictionary(g => g.Key, g => g.First().IdTuVung);

                // Lấy danh sách ID từ vựng đã có trong bài học này để kiểm tra nhanh
                var idTuVungDaLienKetRaw = await _context.BaiHocTuVungs
                    .Where(bhtv => bhtv.IdBaiHoc == idBaiHoc)
                    .Select(bhtv => bhtv.IdTuVung)
                    .ToListAsync();

                var idTuVungDaLienKet = idTuVungDaLienKetRaw.ToHashSet();

                // Lấy thứ tự lớn nhất hiện tại trong bài
                int maxThuTu = await _context.BaiHocTuVungs
                    .Where(bhtv => bhtv.IdBaiHoc == idBaiHoc)
                    .Select(bhtv => (int?)bhtv.ThuTu)
                    .MaxAsync() ?? 0;

                int countTuVungThanhCong = 0;
                int countTuVungMoiTao = 0;

                // 5. Bắt đầu xử lý từng dòng từ vựng 
                // Chạy từ index 3 (tức là Hàng 4 trong Excel), bỏ qua Hàng 3 (Tiêu đề)
                for (int i = 3; i < allRows.Count; i++)
                {
                    var cellValues = (allRows[i] as IDictionary<string, object>)?.Values.ToList();
                    if (cellValues == null || cellValues.Count == 0) continue;

                    string? chuHan = cellValues.ElementAtOrDefault(0)?.ToString();
                    string? pinyin = cellValues.ElementAtOrDefault(1)?.ToString();
                    string? nghiaViet = cellValues.ElementAtOrDefault(2)?.ToString();
                    string? loaiTu = cellValues.ElementAtOrDefault(3)?.ToString();
                    string? amHan = cellValues.ElementAtOrDefault(4)?.ToString();
                    string? ghiAmTuVung = cellValues.ElementAtOrDefault(5)?.ToString();

                    string? viDuHan = cellValues.ElementAtOrDefault(6)?.ToString();
                    string? viDuPinyin = cellValues.ElementAtOrDefault(7)?.ToString();
                    string? viDuViet = cellValues.ElementAtOrDefault(8)?.ToString();
                    string? ghiAmViDu = cellValues.ElementAtOrDefault(9)?.ToString();

                    if (string.IsNullOrWhiteSpace(chuHan)) continue;

                    string chuHanCleaned = chuHan.Trim()
                                                 .Replace("\u200B", "")
                                                 .Replace("\u00A0", "")
                                                 .Replace(" ", "")
                                                 .Replace(" ", "");

                    if (string.IsNullOrEmpty(chuHanCleaned)) continue;

                    int currentIdTuVung;

                    // 5.1. Xử lý Từ Vựng
                    if (tuVungDict.TryGetValue(chuHanCleaned, out int existingIdTuVung))
                    {
                        currentIdTuVung = existingIdTuVung;

                        // KIỂM TRA NGAY: Nếu từ vựng này đã được gắn vào bài học -> Bỏ qua dòng này
                        if (idTuVungDaLienKet.Contains(currentIdTuVung))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        // Chưa có trong hệ thống -> Tạo mới
                        var tuVungMoi = new TuVung
                        {
                            ChuHan = chuHanCleaned,
                            Pinyin = pinyin?.Trim(),
                            NghiaViet = nghiaViet?.Trim(),
                            LoaiTu = loaiTu?.Trim(),
                            AmHan = amHan?.Trim(),
                            AmThanhPath = ghiAmTuVung?.Trim(),
                            IdCapDo = idCapDo // Gắn cấp độ từ Excel
                        };

                        _context.TuVungs.Add(tuVungMoi);
                        await _context.SaveChangesAsync(); // Lưu để sinh IdTuVung

                        currentIdTuVung = tuVungMoi.IdTuVung;
                        tuVungDict.Add(chuHanCleaned, currentIdTuVung);
                        countTuVungMoiTao++;
                    }

                    // 5.2. Xử lý Câu Ví Dụ (Chỉ chạy khi từ vựng CHƯA được gắn vào bài học)
                    if (!string.IsNullOrWhiteSpace(viDuHan))
                    {
                        string viDuHanCleaned = viDuHan.Trim();

                        bool viDuDaTonTai = await _context.CauViDus
                            .AnyAsync(cv => cv.IdTuVung == currentIdTuVung && cv.NoiDungHan == viDuHanCleaned);

                        if (!viDuDaTonTai)
                        {
                            var cauViDu = new CauViDu
                            {
                                IdTuVung = currentIdTuVung,
                                NoiDungHan = viDuHanCleaned,
                                NoiDungPinyin = viDuPinyin?.Trim(),
                                NoiDungViet = viDuViet?.Trim(),
                                AmThanhPath = ghiAmViDu?.Trim()
                            };
                            _context.CauViDus.Add(cauViDu);
                            await _context.SaveChangesAsync();
                        }
                    }

                    // 5.3. Liên kết Từ Vựng vào Bài Học
                    maxThuTu++;
                    var baiHocTuVung = new BaiHocTuVung
                    {
                        IdBaiHoc = idBaiHoc, // Dùng IdBaiHoc lấy được từ DB
                        IdTuVung = currentIdTuVung,
                        ThuTu = maxThuTu,
                        LaTuMoi = true
                    };

                    _context.BaiHocTuVungs.Add(baiHocTuVung);

                    // Cập nhật lại HashSet để chặn lặp từ trong cùng 1 file Excel
                    idTuVungDaLienKet.Add(currentIdTuVung);
                    countTuVungThanhCong++;
                }

                // 6. Lưu toàn bộ
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 7. Xóa cache
                try
                {
                    ClearAllBaiHocCache();
                }
                catch (Exception cacheEx)
                {
                    _logger.LogWarning(cacheEx, "Lỗi xóa cache khi import excel từ vựng nhưng DB đã lưu thành công.");
                }

                return Ok(new
                {
                    success = true,
                    message = $"Import thành công {countTuVungThanhCong} từ vựng vào bài học '{baiHoc.TenBaiHoc}'. Đã tạo mới {countTuVungMoiTao} từ vựng chưa có trong hệ thống."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi import file Excel từ vựng.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost("import-excel-baitap-vao-bai-hoc")]
        public async Task<IActionResult> ImportExcelBaiTapVaoBaiHoc(IFormFile file)
        {
            // 1. Kiểm tra file hợp lệ
            if (file == null || file.Length <= 0)
                return BadRequest(new { success = false, message = "Vui lòng chọn file Excel." });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                using var stream = file.OpenReadStream();

                // Đọc toàn bộ dữ liệu thô từ file Excel dưới dạng danh sách các hàng
                var allRows = stream.Query(useHeaderRow: false).ToList();

                // Cấu trúc chuẩn tối thiểu cần 6 hàng đầu
                if (allRows.Count < 6)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Cấu trúc file không hợp lệ. Phải bao gồm thông tin bài học và dữ liệu câu hỏi từ hàng 7 trở đi."
                    });
                }

                // --- BƯỚC 1: ĐỌC SỐ THỨ TỰ BÀI VÀ CẤP ĐỘ ĐỂ TÌM BÀI HỌC HIỆN CÓ ---
                var row2 = allRows[1] as IDictionary<string, object>;
                var row3 = allRows[2] as IDictionary<string, object>;

                string? soThuTuBaiStr = row2?.Values.Skip(1).FirstOrDefault()?.ToString();
                string? idCapDoStr = row3?.Values.Skip(1).FirstOrDefault()?.ToString();

                if (!int.TryParse(idCapDoStr, out int idCapDo))
                    return BadRequest(new { success = false, message = "Lỗi Hàng 3: Cấp độ phải là một số nguyên hợp lệ." });

                if (!int.TryParse(soThuTuBaiStr, out int soThuTuBai))
                    return BadRequest(new { success = false, message = "Lỗi Hàng 2: Số thứ tự bài phải là một số nguyên hợp lệ." });

                // HOÀN THIỆN: Truy vấn bài học mục tiêu
                var existingBaiHoc = await _context.BaiHocs
                    .FirstOrDefaultAsync(b => b.SoThuTuBai == soThuTuBai && b.IdCapDo == idCapDo);

                // NẾU KHÔNG TÌM THẤY BÀI HỌC
                if (existingBaiHoc == null)
                {
                    // Bắt buộc rollback trước khi ngắt luồng
                    await transaction.RollbackAsync();

                    // Kiểm tra xem IdCapDo có thực sự tồn tại trong DB không để báo lỗi chi tiết
                    var isCapDoExist = await _context.CapDos.AnyAsync(c => c.IdCapDo == idCapDo);
                    if (!isCapDoExist)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Lỗi dữ liệu: Hệ thống không có Cấp độ nào mang ID = {idCapDo} (Kiểm tra lại Hàng 3)."
                        });
                    }

                    // Nếu cấp độ có tồn tại nhưng bài học thì không
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Không tìm thấy Bài học số {soThuTuBai} trong Cấp độ ID = {idCapDo}. Vui lòng tạo bài học này trên hệ thống trước khi nạp bài tập!"
                    });
                }

                // --- BƯỚC 2: CHUẨN BỊ THÔNG TIN ĐỂ ĐỌC CÂU HỎI ---
                var validLoaiIds = await _context.LoaiCauHois.Select(l => l.IdLoai).ToListAsync();
                var validCapDoIds = await _context.CapDos.Select(c => c.IdCapDo).ToListAsync();

                int maxThuTuCauHoi = await _context.BaiHocCauHois
                    .Where(bhch => bhch.IdBaiHoc == existingBaiHoc.IdBaiHoc)
                    .Select(bhch => (int?)bhch.ThuTu)
                    .MaxAsync() ?? 0;

                int countSuccess = 0;
                var danhSachCauHoiMoi = new List<CauHoi>();

                // Dữ liệu câu hỏi thực sự chạy từ index 6 (tức Hàng 7 trong Excel)
                for (int i = 6; i < allRows.Count; i++)
                {
                    var cellValues = (allRows[i] as IDictionary<string, object>)?.Values.ToList();
                    if (cellValues == null || cellValues.Count == 0) continue;

                    string? idLoaiStr = cellValues.ElementAtOrDefault(0)?.ToString();
                    string? idCapDoRowStr = cellValues.ElementAtOrDefault(1)?.ToString();

                    if (!int.TryParse(idLoaiStr, out int idLoai) || idLoai <= 0) break;
                    if (!int.TryParse(idCapDoRowStr, out int idCapDoRow)) break;

                    if (!validLoaiIds.Contains(idLoai) || !validCapDoIds.Contains(idCapDoRow)) break;

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

                    var cauHoi = new CauHoi
                    {
                        NoiDung = cauHoiText?.Trim(),
                        GiaiThich = giaiThich?.Trim(),
                        IdLoai = idLoai,
                        IdCapDo = idCapDoRow,
                        IdDoanVan = currentDoanVanId,
                        IsGeneratedOnly = false,
                        TrangThai = true,
                        DapAns = new List<DapAn>()
                    };

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
                    danhSachCauHoiMoi.Add(cauHoi);
                    countSuccess++;
                }

                // --- BƯỚC 3: KẾT NỐI CÂU HỎI MỚI VÀO BÀI HỌC HIỆN TẠI ---
                if (countSuccess > 0)
                {
                    await _context.SaveChangesAsync();

                    foreach (var cauHoiMoi in danhSachCauHoiMoi)
                    {
                        maxThuTuCauHoi++;
                        var baiHocCauHoi = new BaiHocCauHoi
                        {
                            IdBaiHoc = existingBaiHoc.IdBaiHoc,
                            IdCauHoi = cauHoiMoi.IdCauHoi,
                            ThuTu = maxThuTuCauHoi
                        };
                        _context.BaiHocCauHois.Add(baiHocCauHoi);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    ClearAllBaiHocCache();

                    return Ok(new
                    {
                        success = true,
                        message = $"Nạp bài tập thành công! Đã thêm {countSuccess} câu hỏi mới vào bài học gốc '{existingBaiHoc.TenBaiHoc}' (Bài số: {existingBaiHoc.SoThuTuBai} - Cấp độ ID: {existingBaiHoc.IdCapDo})."
                    });
                }

                await transaction.RollbackAsync();
                return BadRequest(new { success = false, message = "Không tìm thấy dữ liệu câu hỏi hợp lệ để nạp vào hệ thống." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi xử lý nạp câu hỏi từ file Excel vào bài học.");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpGet("chitietbaihoc/{idBaiHoc}")]
        public async Task<IActionResult> GetChiTietBaiHoc([FromRoute] int idBaiHoc)
        {
            // Khóa cache riêng biệt cho cấu trúc bài học gốc
            string cacheKey = $"ChiTiet_BaiHoc_{idBaiHoc}";

            try
            {
                BaiHocModels chiTietBaiHoc;

                // 1. Thử lấy dữ liệu bài học gốc từ Cache
                if (!_cache.TryGetValue(cacheKey, out object cacheResult))
                {
                    _logger.LogInformation($"Cache miss cho key: {cacheKey}. Đang lấy chi tiết bài học {idBaiHoc} từ DB...");

                    chiTietBaiHoc = await _context.BaiHocs
                        .Where(b => b.IdBaiHoc == idBaiHoc && b.TrangThai == true)
                        .Select(b => new BaiHocModels
                        {
                            IdBaiHoc = b.IdBaiHoc,
                            IdCapDo = b.IdCapDo,
                            SoThuTuBai = b.SoThuTuBai,
                            TenBaiHoc = b.TenBaiHoc,
                            MoTa = b.MoTa,
                            TrangThai = b.TrangThai,
                            IdGiaoTrinh = b.IdGiaoTrinh,
                            BaiHocTuVungs = b.BaiHocTuVungs
                                .OrderBy(bhtv => bhtv.ThuTu)
                                .Select(bhtv => new BaiHocTuVungModel
                                {
                                    IdBaiHoc = bhtv.IdBaiHoc,
                                    IdTuVung = bhtv.IdTuVung,
                                    ThuTu = bhtv.ThuTu,
                                    LaTuMoi = bhtv.LaTuMoi,
                                    IdTuVungNavigation = new TuVungBaiHocModel
                                    {
                                        IdTuVung = bhtv.IdTuVung,
                                        ChuHan = bhtv.IdTuVungNavigation.ChuHan,
                                        LoaiTu = bhtv.IdTuVungNavigation.LoaiTu,
                                        Pinyin = bhtv.IdTuVungNavigation.Pinyin,
                                        NghiaViet = bhtv.IdTuVungNavigation.NghiaViet,
                                        AmHan = bhtv.IdTuVungNavigation.AmHan,
                                        AmThanhPath = bhtv.IdTuVungNavigation.AmThanhPath,
                                        CauViDus = bhtv.IdTuVungNavigation.CauViDus.Select(cv => new CauViDuModes
                                        {                                                                                   
                                            NoiDungHan = cv.NoiDungHan,
                                            NoiDungPinyin = cv.NoiDungPinyin,
                                            NoiDungViet = cv.NoiDungViet
                                        }).ToList()

                                    }
                                }).ToList(),

                            BaiHocCauHois = b.BaiHocCauHois
                                .OrderBy(bhch => bhch.ThuTu)
                                .Select(bhch => new BaiHocCauHoiModel
                                {
                                    IdBaiHoc = bhch.IdBaiHoc,
                                    IdCauHoi = bhch.IdCauHoi,
                                    ThuTu = bhch.ThuTu,

                                    IdCauHoiNavigation = new CauHoiModels
                                    {
                                        IdCauHoi = bhch.IdCauHoiNavigation.IdCauHoi,
                                        IdLoai = bhch.IdCauHoiNavigation.IdLoai,
                                        IdCapDo = bhch.IdCauHoiNavigation.IdCapDo,
                                        IdDoanVan = bhch.IdCauHoiNavigation.IdDoanVan,
                                        NoiDung = bhch.IdCauHoiNavigation.NoiDung,
                                        AmThanhPath = bhch.IdCauHoiNavigation.AmThanhPath,
                                        GiaiThich = bhch.IdCauHoiNavigation.GiaiThich,
                                        DoKho = bhch.IdCauHoiNavigation.DoKho,
                                        IsGeneratedOnly = bhch.IdCauHoiNavigation.IsGeneratedOnly,

                                        DoanVan = bhch.IdCauHoiNavigation.IdDoanVanNavigation != null ? new DoanVanModels
                                        {
                                            IdDoanVan = bhch.IdCauHoiNavigation.IdDoanVanNavigation.IdDoanVan,
                                            TieuDe = bhch.IdCauHoiNavigation.IdDoanVanNavigation.TieuDe,
                                            NoiDungHan = bhch.IdCauHoiNavigation.IdDoanVanNavigation.NoiDungHan,
                                            NoiDungPinyin = bhch.IdCauHoiNavigation.IdDoanVanNavigation.NoiDungPinyin,
                                            NoiDungViet = bhch.IdCauHoiNavigation.IdDoanVanNavigation.NoiDungViet
                                        } : null,

                                        DapAns = bhch.IdCauHoiNavigation.DapAns.Select(da => new DapAnModels
                                        {
                                            IdDapAn = da.IdDapAn,
                                            IdCauHoi = da.IdCauHoi,
                                            NoiDungDapAn = da.NoiDungDapAn,
                                            LaDapAnDung = da.LaDapAnDung ?? false,
                                        }).ToList()
                                    }
                                }).ToList()
                        })
                        .AsSplitQuery()
                        .FirstOrDefaultAsync();

                    if (chiTietBaiHoc == null)
                    {
                        return NotFound(new { success = false, message = $"Không tìm thấy chi tiết bài học hoặc bài học đã bị ẩn (ID: {idBaiHoc})." });
                    }

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromHours(2))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheSignal.Token));

                    _cache.Set(cacheKey, chiTietBaiHoc, cacheOptions);
                }
                else
                {
                    _logger.LogInformation($"Cache hit cho key: {cacheKey}. Trả dữ liệu từ Cache.");
                    chiTietBaiHoc = (BaiHocModels)cacheResult;
                }

               

                return Ok(chiTietBaiHoc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi nghiêm trọng khi lấy chi tiết bài học cho ID {idBaiHoc}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [AllowAnonymous] // Cho phép khách và tất cả mọi người truy cập tự do
        [HttpGet("on-luyen-bai-hoc")]
        public async Task<IActionResult> OnLuyenBaiHoc(
              [FromQuery] int idBaiHoc,
              [FromQuery] int loaiOnLuyen = 3) // Giữ tham số này để tránh lỗi Front-end nếu có truyền lên
        {
            try
            {
                // Kiểm tra tham số đầu vào
                if (idBaiHoc <= 0)
                {
                    return BadRequest(new { success = false, message = "ID bài học không hợp lệ." });
                }

                int tongSoCachLay = 20;

                // 1. Lấy toàn bộ danh sách ID từ vựng thuộc Bài Học này
                var listIdsTuVung = await _context.BaiHocTuVungs
                    .AsNoTracking()
                    .Where(bhtv => bhtv.IdBaiHoc == idBaiHoc)
                    .Select(bhtv => bhtv.IdTuVung)
                    .ToListAsync();

                if (!listIdsTuVung.Any())
                {
                    return Ok(new { success = false, message = "Không tìm thấy dữ liệu từ vựng cho bài học này." });
                }

                // 2. Xáo trộn ngẫu nhiên danh sách ID và chỉ lấy tối đa 15 từ
                var rng = new Random();
                var finalIds = listIdsTuVung.OrderBy(_ => rng.Next()).Take(tongSoCachLay).ToList();

                // 3. Query lấy chi tiết thông tin từ vựng và Map sang Model (TuVungModes)
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
                        IsLearned = false // Không check userId nên mặc định là false
                    })
                    .ToListAsync();

                // 4. Trộn ngẫu nhiên lần cuối và gán kiểu câu hỏi (chiều ôn luyện)
                var finalData = result.OrderBy(_ => rng.Next()).ToList();

                foreach (var item in finalData)
                {
                    item.KieuCauHoi = rng.Next(1, 3); // Ngẫu nhiên kiểu 1 hoặc 2
                }

                return Ok(new { success = true, data = finalData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi API ôn luyện bài học ID: {idBaiHoc}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi tải từ vựng bài học." });
            }
        }

        [HttpPost("themtuvung-baihoc")]
        public async Task<IActionResult> ThemTuVungVaoBaiHoc([FromBody] ThemTuVungBaiHocRequest request)
        {
            // 1. Kiểm tra Dữ liệu đầu vào
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(request.ChuHan))
                return BadRequest(new { success = false, message = "Chữ Hán không được để trống." });

            // 2. Khởi tạo Transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 3. Kiểm tra Bài học có tồn tại hay không
                var baiHoc = await _context.BaiHocs.FindAsync(request.IdBaiHoc);
                if (baiHoc == null)
                    return NotFound(new { success = false, message = "Không tìm thấy bài học để thêm từ vựng." });

                // 4. Chuẩn hóa Chữ Hán
                string chuHanCleaned = request.ChuHan.Trim()
                                            .Replace("\u200B", "")
                                            .Replace("\u00A0", "")
                                            .Replace(" ", "");

                // 5. Kiểm tra Từ vựng đã tồn tại chưa, chưa có thì tạo
                var tuVung = await _context.TuVungs
                    .FirstOrDefaultAsync(tv => tv.ChuHan == chuHanCleaned);

                if (tuVung == null)
                {
                    tuVung = new TuVung
                    {
                        ChuHan = chuHanCleaned,
                        Pinyin = request.Pinyin?.Trim(),
                        NghiaViet = request.NghiaViet?.Trim(),
                        LoaiTu = request.LoaiTu?.Trim(),
                        AmHan = request.AmHan?.Trim(),
                        IdCapDo = request.IdCapDo
                    };

                    _context.TuVungs.Add(tuVung);
                    await _context.SaveChangesAsync();
                }

                // 6. Thêm Câu Ví Dụ (Áp dụng cho cả từ vựng mới và cũ)
                if (!string.IsNullOrWhiteSpace(request.ViDuHan))
                {
                    string viDuHanCleaned = request.ViDuHan.Trim();

                    bool viDuDaTonTai = await _context.CauViDus
                        .AnyAsync(cv => cv.IdTuVung == tuVung.IdTuVung && cv.NoiDungHan == viDuHanCleaned);

                    if (!viDuDaTonTai)
                    {
                        var cauViDu = new CauViDu
                        {
                            IdTuVung = tuVung.IdTuVung,
                            NoiDungHan = viDuHanCleaned,
                            NoiDungPinyin = request.ViDuPinyin?.Trim(),
                            NoiDungViet = request.ViDuViet?.Trim()
                        };
                        _context.CauViDus.Add(cauViDu);
                        await _context.SaveChangesAsync();
                    }
                }

                // 7. Kiểm tra liên kết: Bài học đã có từ vựng này chưa?
                bool daLienKet = await _context.BaiHocTuVungs
                    .AnyAsync(bhtv => bhtv.IdBaiHoc == request.IdBaiHoc && bhtv.IdTuVung == tuVung.IdTuVung);

                if (daLienKet)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { success = false, message = $"Từ vựng '{tuVung.ChuHan}' đã tồn tại trong bài học này." });
                }

                // 8. Tính STT tự động tăng
                int maxThuTu = await _context.BaiHocTuVungs
                    .Where(bhtv => bhtv.IdBaiHoc == request.IdBaiHoc)
                    .Select(bhtv => (int?)bhtv.ThuTu)
                    .MaxAsync() ?? 0;

                // 9. Tạo mối liên kết BaiHoc - TuVung
                var baiHocTuVung = new BaiHocTuVung
                {
                    IdBaiHoc = request.IdBaiHoc,
                    IdTuVung = tuVung.IdTuVung,
                    ThuTu = maxThuTu + 1,
                    LaTuMoi = true
                };

                _context.BaiHocTuVungs.Add(baiHocTuVung);
                await _context.SaveChangesAsync();

                // 10. Hoàn tất Transaction
                await transaction.CommitAsync();

                // 11. Xóa Cache an toàn
                try
                {
                    ClearAllBaiHocCache();
                }
                catch (Exception cacheEx)
                {
                    _logger.LogWarning(cacheEx, "Lỗi xóa cache nhưng DB đã lưu.");
                }

                return Ok(new
                {
                    success = true,
                    message = $"Thêm thành công từ vựng '{tuVung.ChuHan}' vào bài học.",
                    data = new { IdTuVung = tuVung.IdTuVung, ChuHan = tuVung.ChuHan, ThuTu = baiHocTuVung.ThuTu }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Lỗi khi thêm từ vựng mới vào bài học {request.IdBaiHoc}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPatch("doitrangthai/{idBaiHoc}")]
        public async Task<IActionResult> DoiTrangThaiBaiHoc([FromRoute] int idBaiHoc)
        {
            try
            {
                var baiHoc = await _context.BaiHocs.FindAsync(idBaiHoc);
                if (baiHoc == null)
                    return NotFound(new { success = false, message = "Không tìm thấy bài học." });

                // Đảo ngược trạng thái hiện tại
                baiHoc.TrangThai = !baiHoc.TrangThai;

                _context.BaiHocs.Update(baiHoc);
                await _context.SaveChangesAsync();

                // Xóa cache để API Get lấy dữ liệu mới nhất
                ClearBaiHocCache(idBaiHoc);

                return Ok(new { success = true, message = "Đã thay đổi trạng thái bài học thành công.", trangThaiMoi = baiHoc.TrangThai });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi đổi trạng thái bài học {idBaiHoc}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpDelete("xoalienket-tuvung/{idBaiHoc}/{idTuVung}")]
        public async Task<IActionResult> XoaLienKetTuVung([FromRoute] int idBaiHoc, [FromRoute] int idTuVung)
        {
            try
            {
                var lienKet = await _context.BaiHocTuVungs
                    .FirstOrDefaultAsync(x => x.IdBaiHoc == idBaiHoc && x.IdTuVung == idTuVung);

                if (lienKet == null)
                    return NotFound(new { success = false, message = "Không tìm thấy liên kết từ vựng trong bài học này." });

                _context.BaiHocTuVungs.Remove(lienKet);
                await _context.SaveChangesAsync();

                ClearBaiHocCache(idBaiHoc);

                return Ok(new { success = true, message = "Đã xóa từ vựng khỏi bài học thành công." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi xóa liên kết từ vựng (Bài: {idBaiHoc}, Từ: {idTuVung})");
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpDelete("xoalienket-cauhoi/{idBaiHoc}/{idCauHoi}")]
        public async Task<IActionResult> XoaLienKetCauHoi([FromRoute] int idBaiHoc, [FromRoute] int idCauHoi)
        {
            try
            {
                var lienKet = await _context.BaiHocCauHois
                    .FirstOrDefaultAsync(x => x.IdBaiHoc == idBaiHoc && x.IdCauHoi == idCauHoi);

                if (lienKet == null)
                    return NotFound(new { success = false, message = "Không tìm thấy liên kết câu hỏi trong bài học này." });

                _context.BaiHocCauHois.Remove(lienKet);
                await _context.SaveChangesAsync();

                ClearBaiHocCache(idBaiHoc);

                return Ok(new { success = true, message = "Đã xóa câu hỏi khỏi bài học thành công." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi xóa liên kết câu hỏi (Bài: {idBaiHoc}, Câu: {idCauHoi})");
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost("capnhat-cauhoi")]
        public async Task<IActionResult> CapNhatCauHoiBaiHoc([FromBody] CapNhatCauHoiRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Bắt đầu Transaction để đảm bảo tính toàn vẹn dữ liệu (nếu có lỗi giữa chừng sẽ Rollback)
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                CauHoi cauHoi; // Sửa lại tên Entity gốc của bạn nếu khác (ví dụ: CauHoiEntity)
                               // Tạo câu hỏi mới
                cauHoi = new CauHoi
                {
                    NoiDung = request.NoiDung,
                    GiaiThich = request.GiaiThich,
                    IdLoai = request.IdLoai,
                    IdCapDo = request.IdCapDo

                };
                await _context.CauHois.AddAsync(cauHoi);
                await _context.SaveChangesAsync(); // Lưu để lấy IdCauHoi mới sinh ra

                // Tạo liên kết với bài học
                var lienKetMoi = new BaiHocCauHoi
                {
                    IdBaiHoc = request.IdBaiHoc,
                    IdCauHoi = cauHoi.IdCauHoi,
                    ThuTu = request.ThuTu
                };
                await _context.BaiHocCauHois.AddAsync(lienKetMoi);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                ClearBaiHocCache(request.IdBaiHoc);

                return Ok(new { success = true, message = "Cập nhật câu hỏi thành công.", idCauHoi = cauHoi.IdCauHoi });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi cập nhật câu hỏi cho bài học.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }


        [HttpDelete("xoabaihoc/{idBaiHoc}")]
        public async Task<IActionResult> XoaBaiHoc([FromRoute] int idBaiHoc)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Kiểm tra bài học có tồn tại không
                var baiHoc = await _context.BaiHocs.FindAsync(idBaiHoc);
                if (baiHoc == null)
                    return NotFound(new { success = false, message = "Không tìm thấy bài học." });

                // 2. Xóa các liên kết với Từ vựng
                var danhSachTuVung = _context.BaiHocTuVungs.Where(x => x.IdBaiHoc == idBaiHoc);
                _context.BaiHocTuVungs.RemoveRange(danhSachTuVung);

                // 3. Xóa các liên kết với Câu hỏi
                var danhSachCauHoiLienKet = _context.BaiHocCauHois.Where(x => x.IdBaiHoc == idBaiHoc);
                _context.BaiHocCauHois.RemoveRange(danhSachCauHoiLienKet);

                // 4. Xóa bài học
                _context.BaiHocs.Remove(baiHoc);

                // 5. Lưu thay đổi
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 6. Xóa cache toàn bộ danh sách bài học để cập nhật lại giao diện
                ClearAllBaiHocCache();

                return Ok(new { success = true, message = $"Đã xóa bài học '{baiHoc.TenBaiHoc}' cùng tất cả liên kết liên quan." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Lỗi khi xóa bài học {idBaiHoc}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpDelete("xoahangloat")]
        public async Task<IActionResult> XoaHangLoat([FromBody] List<int> danhSachIdBaiHoc)
        {
            if (danhSachIdBaiHoc == null || !danhSachIdBaiHoc.Any())
            {
                return BadRequest(new { success = false, message = "Danh sách ID bài học trống." });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Lấy danh sách các bài học thực sự tồn tại trong list ID truyền lên
                var danhSachBaiHoc = _context.BaiHocs.Where(b => danhSachIdBaiHoc.Contains(b.IdBaiHoc)).ToList();
                if (!danhSachBaiHoc.Any())
                {
                    return NotFound(new { success = false, message = "Không tìm thấy các bài học tương ứng trong hệ thống." });
                }

                // Lấy lại danh sách ID thực tế từ DB để tránh lỗi dữ liệu ảo
                var validIds = danhSachBaiHoc.Select(b => b.IdBaiHoc).ToList();

                // 2. Xóa các liên kết với Từ vựng thuộc list ID trên
                var danhSachTuVung = _context.BaiHocTuVungs.Where(x => validIds.Contains(x.IdBaiHoc));
                _context.BaiHocTuVungs.RemoveRange(danhSachTuVung);

                // 3. Xóa các liên kết với Câu hỏi thuộc list ID trên
                var danhSachCauHoiLienKet = _context.BaiHocCauHois.Where(x => validIds.Contains(x.IdBaiHoc));
                _context.BaiHocCauHois.RemoveRange(danhSachCauHoiLienKet);

                // 4. Xóa toàn bộ các bài học này
                _context.BaiHocs.RemoveRange(danhSachBaiHoc);

                // 5. Lưu thay đổi xuống Database
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 6. Xóa cache hệ thống (Dùng hàm có sẵn của bạn)
                ClearAllBaiHocCache();

                return Ok(new
                {
                    success = true,
                    message = $"Đã xóa thành công {danhSachBaiHoc.Count} bài học cùng tất cả các dữ liệu liên kết liên quan."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi xảy ra khi thực hiện xóa hàng loạt bài học.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi hệ thống khi xóa hàng loạt: " + ex.Message });
            }
        }

        private void ClearBaiHocCache(int idBaiHoc)
        {
            string cacheKey = $"ChiTiet_BaiHoc_{idBaiHoc}";
            _cache.Remove(cacheKey);
        }

        #region CRUD Chương Trình Giáo Trình


        /// <summary>
        /// API Lấy danh sách Chương trình giáo trình (Có phân trang & Cache)
        /// </summary>
        [HttpGet("danhsach-chuongtrinh")]
        public async Task<IActionResult> GetDanhSachChuongTrinh([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            string cacheKey = $"ChuongTrinhGiaoTrinh_Page_{page}_Size_{pageSize}";

            try
            {
                if (!_cache.TryGetValue(cacheKey, out object? cacheResult))
                {
                    _logger.LogInformation($"Cache miss cho key: {cacheKey}. Đang truy vấn Database...");

                    var query = _context.ChuongTrinhGiaoTrinhs.AsNoTracking();

                    int totalItems = await query.CountAsync();
                    int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                    var items = await query
                        .OrderByDescending(c => c.IdGiaoTrinh)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(c => new ChuongTrinhGiaoTrinhModels
                        {
                            IdGiaoTrinh = c.IdGiaoTrinh,
                            TenChuongTrinh = c.TenChuongTrinh,
                            TenGiaoTrinh = c.TenGiaoTrinh,
                            MoTa = c.MoTa,
                            TrangThai = c.TrangThai
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
                _logger.LogError(ex, "Lỗi khi lấy danh sách chương trình giáo trình.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi hệ thống.");
            }
        }

        /// <summary>
        /// API Thêm mới Chương trình giáo trình
        /// </summary>
        [HttpPost("them-chuongtrinh")]
        public async Task<IActionResult> ThemChuongTrinh([FromBody] ChuongTrinhGiaoTrinhModels model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var entity = new ChuongTrinhGiaoTrinh
                {
                    TenChuongTrinh = model.TenChuongTrinh,
                    TenGiaoTrinh = model.TenGiaoTrinh,
                    MoTa = model.MoTa,
                    TrangThai = model.TrangThai ?? true
                };

                _context.ChuongTrinhGiaoTrinhs.Add(entity);
                await _context.SaveChangesAsync();

                // Xóa cache khi có dữ liệu thay đổi
                ClearAllBaiHocCache();

                return Ok(new
                {
                    Success = true,
                    Message = "Thêm chương trình giáo trình thành công.",
                    IdGiaoTrinh = entity.IdGiaoTrinh
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm chương trình giáo trình.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi hệ thống.");
            }
        }

        /// <summary>
        /// API Cập nhật Chương trình giáo trình
        /// </summary>
        [HttpPut("sua-chuongtrinh/{id}")]
        public async Task<IActionResult> SuaChuongTrinh([FromRoute] int id, [FromBody] ChuongTrinhGiaoTrinhModels model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var entity = await _context.ChuongTrinhGiaoTrinhs.FindAsync(id);
                if (entity == null)
                {
                    return NotFound(new { Success = false, Message = "Không tìm thấy chương trình giáo trình." });
                }

                entity.TenChuongTrinh = model.TenChuongTrinh;
                entity.TenGiaoTrinh = model.TenGiaoTrinh;
                entity.MoTa = model.MoTa;
                entity.TrangThai = model.TrangThai;

                _context.ChuongTrinhGiaoTrinhs.Update(entity);
                await _context.SaveChangesAsync();

                // Xóa cache khi có dữ liệu thay đổi
                ClearAllBaiHocCache();

                return Ok(new
                {
                    Success = true,
                    Message = "Cập nhật chương trình giáo trình thành công."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi sửa chương trình giáo trình ID: {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi hệ thống.");
            }
        }

        /// <summary>
        /// API Xóa Chương trình giáo trình
        /// </summary>
        [HttpDelete("xoa-chuongtrinh/{id}")]
        public async Task<IActionResult> XoaChuongTrinh([FromRoute] int id)
        {
            try
            {
                var entity = await _context.ChuongTrinhGiaoTrinhs.FindAsync(id);
                if (entity == null)
                {
                    return NotFound(new { Success = false, Message = "Không tìm thấy chương trình giáo trình." });
                }

                // Kiểm tra ràng buộc khóa ngoại với bảng BaiHoc trước khi xóa
                bool hasChildBaiHoc = await _context.BaiHocs.AnyAsync(b => b.IdGiaoTrinh == id);
                if (hasChildBaiHoc)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Không thể xóa chương trình giáo trình này vì đang có các bài học liên quan."
                    });
                }

                _context.ChuongTrinhGiaoTrinhs.Remove(entity);
                await _context.SaveChangesAsync();

                // Xóa cache khi có dữ liệu thay đổi
                ClearAllBaiHocCache();

                return Ok(new
                {
                    Success = true,
                    Message = "Xóa chương trình giáo trình thành công."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi xóa chương trình giáo trình ID: {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi hệ thống.");
            }
        }

        #endregion
    }
}
