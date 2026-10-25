using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12.AI;
using HocTiengTrung_3._0_T.Models12.PhanTichBoThu;
using HocTiengTrung_3._0_T.Models12.TuVung;
using HocTiengTrung_3._0_T.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using MiniExcelLibs;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.ControllersAPI
{
    [Route("api/phan-tich-tu-vung")]
    [ApiController]
    public class PhanTichTuVungController : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IHanziDecomposerService _decomposerService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PhanTichTuVungController> _logger;
        private static CancellationTokenSource _resetCacheSignal = new CancellationTokenSource();
        public PhanTichTuVungController(TmdtContext context, IHanziDecomposerService decomposerService, IMemoryCache cache, ILogger<PhanTichTuVungController> logger)
        {
            _context = context;
            _decomposerService = decomposerService;
            _cache = cache;
            _logger = logger;
        }

        private void ClearCache()
        {
            var oldSignal = Interlocked.Exchange(ref _resetCacheSignal, new CancellationTokenSource());
            oldSignal.Cancel();
            oldSignal.Dispose();
            _logger.LogInformation("Toàn bộ Cache của Bộ Thủ đã được làm mới.");
        }

        // 1. LẤY DANH SÁCH BỘ THỦ (Phân trang + Tìm kiếm + Luôn lọc Trạng thái = true + Cache)
        [HttpGet("danh-sach")]
        public async Task<IActionResult> DanhSachBoThu(
            [FromQuery] string? search,
            [FromQuery] int pagesize = 30,
            [FromQuery] int page = 1)
        {
            try
            {
                var cacheKey = $"DanhSachBoThu_{search}_{pagesize}_{page}";

                if (!_cache.TryGetValue(cacheKey, out object result))
                {
                    // Luôn lọc Trạng Thái = true ngay tại bước khởi tạo query
                    var query = _context.BoThus.AsNoTracking()
                        .Where(b => b.TrangThai == true);

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        string s = search.Trim().ToLower();
                        query = query.Where(b =>
                            b.BoThu1.Contains(s) ||
                            b.HanViet.ToLower().Contains(s) ||
                            b.Pinyin.ToLower().Contains(s) ||
                            (b.BienThe != null && b.BienThe.ToLower().Contains(s)) ||
                            (b.Ynghia != null && b.Ynghia.ToLower().Contains(s)));
                    }

                    var totalItems = await query.CountAsync();

                    var data = await query
                        .OrderBy(b => b.Stt)
                        .ThenBy(b => b.SoNet)
                        .Skip((page - 1) * pagesize)
                        .Take(pagesize)
                        .Select(b => new BoThuDto
                        {
                            IdBoThu = b.IdBoThu,
                            Stt = b.Stt,
                            BoThu1 = b.BoThu1,
                            BienThe = b.BienThe,
                            HanViet = b.HanViet,
                            Pinyin = b.Pinyin,
                            Ynghia = b.Ynghia,
                            SoNet = b.SoNet,
                            TrangThai = b.TrangThai
                        })
                        .ToListAsync();

                    result = new { success = true, totalItems, data, page, pageSize = pagesize };

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
                _logger.LogError(ex, "Lỗi khi lấy danh sách bộ thủ");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống." });
            }
        }
        // 2. CHI TIẾT BỘ THỦ THEO ID
        [HttpGet("chi-tiet/{id}")]
        public async Task<IActionResult> ChiTiet(int id)
        {
            try
            {
                var boThu = await _context.BoThus
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.IdBoThu == id);

                if (boThu == null)
                    return NotFound(new { success = false, message = "Không tìm thấy bộ thủ yêu cầu." });

                var result = new BoThuDto
                {
                    IdBoThu = boThu.IdBoThu,
                    Stt = boThu.Stt,
                    BoThu1 = boThu.BoThu1,
                    BienThe = boThu.BienThe,
                    HanViet = boThu.HanViet,
                    Pinyin = boThu.Pinyin,
                    Ynghia = boThu.Ynghia,
                    SoNet = boThu.SoNet,
                    TrangThai = boThu.TrangThai
                };

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi lấy chi tiết bộ thủ ID: {id}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi tải dữ liệu." });
            }
        }

        // 3. THÊM MỚI BỘ THỦ
        [HttpPost("them-moi")]
        public async Task<IActionResult> ThemMoi([FromBody] BoThuCreateUpdateDto dto)
        {
            try
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.BoThu1))
                    return BadRequest(new { success = false, message = "Vui lòng nhập ký tự bộ thủ!" });

                var boThu = new BoThu
                {
                    Stt = dto.Stt,
                    BoThu1 = dto.BoThu1.Trim(),
                    BienThe = dto.BienThe?.Trim(),
                    HanViet = dto.HanViet?.Trim() ?? string.Empty,
                    Pinyin = dto.Pinyin?.Trim() ?? string.Empty,
                    Ynghia = dto.Ynghia?.Trim(),
                    SoNet = dto.SoNet,
                    TrangThai = dto.TrangThai
                };

                _context.BoThus.Add(boThu);
                await _context.SaveChangesAsync();

                ClearCache();

                return Ok(new { success = true, message = "Thêm bộ thủ thành công.", id = boThu.IdBoThu });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm mới bộ thủ");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // 4. CẬP NHẬT BỘ THỦ
        [HttpPut("cap-nhat/{id}")]
        public async Task<IActionResult> CapNhat(int id, [FromBody] BoThuCreateUpdateDto dto)
        {
            try
            {
                var boThu = await _context.BoThus.FirstOrDefaultAsync(b => b.IdBoThu == id);

                if (boThu == null)
                    return NotFound(new { success = false, message = "Không tìm thấy bộ thủ." });

                boThu.Stt = dto.Stt;
                boThu.BoThu1 = dto.BoThu1.Trim();
                boThu.BienThe = dto.BienThe?.Trim();
                boThu.HanViet = dto.HanViet?.Trim() ?? string.Empty;
                boThu.Pinyin = dto.Pinyin?.Trim() ?? string.Empty;
                boThu.Ynghia = dto.Ynghia?.Trim();
                boThu.SoNet = dto.SoNet;
                boThu.TrangThai = dto.TrangThai;

                await _context.SaveChangesAsync();

                ClearCache();

                return Ok(new { success = true, message = "Cập nhật bộ thủ thành công." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi cập nhật bộ thủ ID: {id}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // 5. XÓA BỘ THỦ (Hỗ trợ Xóa mềm / Xóa cứng)
        [HttpDelete("xoa/{id}")]
        public async Task<IActionResult> Xoa(int id, [FromQuery] bool softDelete = true)
        {
            try
            {
                var boThu = await _context.BoThus.FirstOrDefaultAsync(b => b.IdBoThu == id);

                if (boThu == null)
                    return NotFound(new { success = false, message = "Không tìm thấy bộ thủ." });

                if (softDelete)
                {
                    boThu.TrangThai = false; // Xóa mềm: Chuyển trạng thái ẩn
                }
                else
                {
                    _context.BoThus.Remove(boThu); // Xóa cứng khỏi Database
                }

                await _context.SaveChangesAsync();

                ClearCache();

                return Ok(new
                {
                    success = true,
                    message = softDelete ? "Đã ẩn bộ thủ thành công." : "Đã xóa vĩnh viễn bộ thủ."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi xóa bộ thủ ID: {id}");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi xóa." });
            }
        }

        // 5.1. XÓA TOÀN BỘ DANH SÁCH BỘ THỦ
        [HttpDelete("xoa-tat-ca")]
        public async Task<IActionResult> XoaTatCa([FromQuery] bool softDelete = true)
        {
            try
            {
                if (softDelete)
                {
                    // Xóa mềm: Chuyển toàn bộ TrangThai = false
                    await _context.BoThus
                        .Where(b => b.TrangThai == true)
                        .ExecuteUpdateAsync(s => s.SetProperty(b => b.TrangThai, false));
                }
                else
                {
                    // Xóa cứng: Xóa vĩnh viễn toàn bộ bản ghi khỏi Database
                    await _context.BoThus.ExecuteDeleteAsync();
                }

                // Làm mới bộ nhớ Cache
                ClearCache();

                return Ok(new
                {
                    success = true,
                    message = softDelete
                        ? "Đã ẩn toàn bộ danh sách bộ thủ thành công."
                        : "Đã xóa vĩnh viễn toàn bộ danh sách bộ thủ khỏi hệ thống."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa toàn bộ danh sách bộ thủ.");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi xóa dữ liệu: " + ex.Message });
            }
        }

        // 6. IMPORT BỘ THỦ TỪ FILE EXCEL (Sử dụng MiniExcel)
        [HttpPost("import-excel")]
        public async Task<IActionResult> ImportExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Vui lòng chọn file Excel!" });

            var listBoThu = new List<BoThu>();

            using (var stream = file.OpenReadStream())
            {
                // Đọc không cần header row để ánh xạ trực tiếp A, B, C, D, E, F, G
                var rows = await stream.QueryAsync(useHeaderRow: false);

                // Bỏ qua dòng Header (Dòng 1), bắt đầu đọc từ Dòng 2
                foreach (var row in rows.Skip(1))
                {
                    string boThu1 = row.B?.ToString()?.Trim();

                    // Bỏ qua nếu cột Bộ Thủ trống
                    if (string.IsNullOrEmpty(boThu1)) continue;

                    int.TryParse(row.A?.ToString()?.Trim(), out int stt);
                    int.TryParse(row.G?.ToString()?.Trim(), out int soNet);

                    var boThu = new BoThu
                    {
                        Stt = stt,
                        BoThu1 = boThu1,
                        BienThe = row.C?.ToString()?.Trim(),
                        HanViet = row.D?.ToString()?.Trim() ?? string.Empty,
                        Pinyin = row.E?.ToString()?.Trim() ?? string.Empty,
                        Ynghia = row.F?.ToString()?.Trim(),
                        SoNet = soNet,
                        TrangThai = true // Luôn bật trạng thái hoạt động mặc định
                    };

                    listBoThu.Add(boThu);
                }
            }

            if (!listBoThu.Any())
            {
                return BadRequest(new { success = false, message = "Không tìm thấy dữ liệu hợp lệ trong file Excel!" });
            }

            // Lưu danh sách vào Database
            await _context.BoThus.AddRangeAsync(listBoThu);
            await _context.SaveChangesAsync();

            // Làm mới cache (nếu đang dùng Signal cancellation token)
            _resetCacheSignal?.Cancel();

            return Ok(new
            {
                success = true,
                total = listBoThu.Count,
                message = $"Đã nhập thành công {listBoThu.Count} bộ thủ vào hệ thống!"
            });
        }

        [HttpPost("tu-don")]
        public async Task<IActionResult> PhanTichTuDon(
            [FromBody] RequestTuDonDto request,
            [FromServices] IGeminiHanTuService geminiService)
        {
            if (string.IsNullOrWhiteSpace(request?.ChuHan))
                return BadRequest(new { success = false, message = "Vui lòng nhập chữ Hán!" });

            var chuHan = request.ChuHan.Trim();

            try
            {
                // 1. Gọi AI phân tích (từ đơn hoặc từ ghép giữ nguyên 1 đối tượng)
                var aiAnalysis = await geminiService.PhanTichChuHanAsync(chuHan);
                if (aiAnalysis == null)
                    return StatusCode(500, new { success = false, message = "Không nhận được phản hồi từ AI." });

                // 2. Tìm kiếm duy nhất 1 câu ví dụ từ Database (TuVung -> CauViDu)
                var viDuDb = await _context.TuVungs
                    .Where(tv => tv.ChuHan == chuHan)
                    .SelectMany(tv => tv.CauViDus)
                    .Select(vd => new CauViDuModes
                    {
                        NoiDungHan = vd.NoiDungHan,
                        NoiDungPinyin = vd.NoiDungPinyin,
                        NoiDungViet = vd.NoiDungViet
                    })
                    .FirstOrDefaultAsync();

                // Chỉ chứa đúng 1 ví dụ duy nhất
                var danhSachViDu = new List<CauViDuModes>
        {
            viDuDb ?? new CauViDuModes
            {
                NoiDungHan = $"学习{chuHan}。",
                NoiDungPinyin = $"Xuéxí {aiAnalysis.Pinyin}.",
                NoiDungViet = $"Học từ {chuHan}."
            }
        };

                // 3. Tra cứu Bộ thủ chính trong Database BoThu (khớp BoThu1 hoặc BienThe)
                var allBoThu = await _context.BoThus.Where(b => b.TrangThai).ToListAsync();
                var boChinhMatch = allBoThu.FirstOrDefault(b =>
                    b.BoThu1 == aiAnalysis.BoThuChinh ||
                    b.BienThe == aiAnalysis.BoThuChinh ||
                    (!string.IsNullOrEmpty(b.BienThe) && b.BienThe.Contains(aiAnalysis.BoThuChinh))
                );

                string boChinhHienThi = boChinhMatch != null
                    ? $"{boChinhMatch.BoThu1} ({boChinhMatch.HanViet})"
                    : aiAnalysis.BoThuChinh;

                // 4. Trả về Response đồng bộ cho Web UI và Export PowerPoint
                var responseData = new
                {
                    chuHan = aiAnalysis.ChuHan,
                    pinyin = aiAnalysis.Pinyin,
                    amHanViet = aiAnalysis.AmHanViet,
                    amHan = aiAnalysis.AmHanViet,
                    tuLoai = aiAnalysis.TuLoai,
                    loaiTu = aiAnalysis.TuLoai,
                    nghiaTiengViet = aiAnalysis.NghiaTiengViet,
                    nghiaViet = aiAnalysis.NghiaTiengViet,
                    boThuChinh = boChinhHienThi,
                    boChinh = boChinhHienThi,
                    cauTaoKyTu = aiAnalysis.CauTaoKyTu,
                    giaiThichTrietTu = aiAnalysis.GiaiThichTrietTu,
                    meoGhiNho = aiAnalysis.MeoGhiNho,
                    danhSachViDu = danhSachViDu
                };

                return Ok(new
                {
                    success = true,
                    data = responseData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi phân tích từ vựng: {ChuHan}", chuHan);
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost("excel-tu-vung")]
        public async Task<IActionResult> NhapTuVungExcel(
            IFormFile file,
            [FromServices] IGeminiHanTuService geminiService)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Vui lòng chọn file Excel!" });

            try
            {
                var allBoThu = await _context.BoThus.Where(b => b.TrangThai).ToListAsync();
                var listKetQuaPhanTich = new List<object>();

                using (var stream = file.OpenReadStream())
                {
                    var rows = await stream.QueryAsync(useHeaderRow: false);

                    foreach (var row in rows.Skip(1))
                    {
                        string chuHan = row.B?.ToString()?.Trim();
                        if (string.IsNullOrEmpty(chuHan)) continue;

                        string pinyinExcel = row.C?.ToString()?.Trim();
                        string amHanExcel = row.D?.ToString()?.Trim();
                        string loaiTuExcel = row.E?.ToString()?.Trim();
                        string nghiaVietExcel = row.F?.ToString()?.Trim();

                        try
                        {
                            var aiAnalysis = await geminiService.PhanTichChuHanAsync(chuHan);
                            if (aiAnalysis != null)
                            {
                                // Lấy duy nhất 1 câu ví dụ từ Database
                                var viDuDb = await _context.TuVungs
                                    .Where(tv => tv.ChuHan == chuHan)
                                    .SelectMany(tv => tv.CauViDus)
                                    .Select(vd => new CauViDuModes
                                    {
                                        NoiDungHan = vd.NoiDungHan,
                                        NoiDungPinyin = vd.NoiDungPinyin,
                                        NoiDungViet = vd.NoiDungViet
                                    })
                                    .FirstOrDefaultAsync();

                                var danhSachViDu = new List<CauViDuModes>
                        {
                            viDuDb ?? new CauViDuModes
                            {
                                NoiDungHan = $"学习{chuHan}。",
                                NoiDungPinyin = $"Xuéxí {aiAnalysis.Pinyin}.",
                                NoiDungViet = $"Học từ {chuHan}."
                            }
                        };

                                var boChinhMatch = allBoThu.FirstOrDefault(b =>
                                    b.BoThu1 == aiAnalysis.BoThuChinh ||
                                    b.BienThe == aiAnalysis.BoThuChinh ||
                                    (!string.IsNullOrEmpty(b.BienThe) && b.BienThe.Contains(aiAnalysis.BoThuChinh))
                                );

                                string boChinhHienThi = boChinhMatch != null
                                    ? $"{boChinhMatch.BoThu1} ({boChinhMatch.HanViet})"
                                    : aiAnalysis.BoThuChinh;

                                var ketQuaItem = new
                                {
                                    chuHan = aiAnalysis.ChuHan,
                                    pinyin = !string.IsNullOrEmpty(pinyinExcel) ? pinyinExcel : aiAnalysis.Pinyin,
                                    amHanViet = !string.IsNullOrEmpty(amHanExcel) ? amHanExcel : aiAnalysis.AmHanViet,
                                    amHan = !string.IsNullOrEmpty(amHanExcel) ? amHanExcel : aiAnalysis.AmHanViet,
                                    tuLoai = !string.IsNullOrEmpty(loaiTuExcel) ? loaiTuExcel : aiAnalysis.TuLoai,
                                    loaiTu = !string.IsNullOrEmpty(loaiTuExcel) ? loaiTuExcel : aiAnalysis.TuLoai,
                                    nghiaTiengViet = !string.IsNullOrEmpty(nghiaVietExcel) ? nghiaVietExcel : aiAnalysis.NghiaTiengViet,
                                    nghiaViet = !string.IsNullOrEmpty(nghiaVietExcel) ? nghiaVietExcel : aiAnalysis.NghiaTiengViet,
                                    boThuChinh = boChinhHienThi,
                                    boChinh = boChinhHienThi,
                                    cauTaoKyTu = aiAnalysis.CauTaoKyTu,
                                    giaiThichTrietTu = aiAnalysis.GiaiThichTrietTu,
                                    meoGhiNho = aiAnalysis.MeoGhiNho,
                                    danhSachViDu = danhSachViDu
                                };

                                listKetQuaPhanTich.Add(ketQuaItem);
                            }
                        }
                        catch (Exception exItem)
                        {
                            _logger.LogWarning(exItem, "Bỏ qua lỗi khi phân tích từ '{ChuHan}'", chuHan);
                        }
                    }
                }

                if (!listKetQuaPhanTich.Any())
                    return BadRequest(new { success = false, message = "Không tìm thấy dữ liệu từ vựng hợp lệ!" });

                return Ok(new
                {
                    success = true,
                    total = listKetQuaPhanTich.Count,
                    data = listKetQuaPhanTich
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi đọc file Excel");
                return StatusCode(500, new { success = false, message = "Lỗi xử lý file: " + ex.Message });
            }
        }


    }
}