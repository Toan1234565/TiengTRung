
using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12.TuVung.HocTiengTrung_3._0_T.DTOs.LuyenNghe;
using HocTiengTrung_3._0_T.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.ControllersAPI
{
    [Route("api/luyen-nghe-phan-xa")]
    [ApiController]
    public class LuyenNghePhanXaController : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly ITtsService _ttsService;

        // Danh sách mốc thời gian suy nghĩ hợp lệ (tính bằng giây)
        private static readonly int[] ValidThinkingTimes = { 5, 10, 15, 20, 30 };

        public LuyenNghePhanXaController(TmdtContext context, ITtsService ttsService)
        {
            _context = context;
            _ttsService = ttsService;
        }

        /// <summary>
        /// API 1: Lấy danh sách bài luyện nghe phản xạ
        /// ModeNghe: "ZH" (Phát âm Trung - Nhập Việt), "VI" (Phát âm Việt - Nhập Hán/Pinyin), "MIXED" (Trộn lẫn)
        /// ThoiGianSuyNghi: 5, 10, 15, 20, 30 (Mặc định 15s)
        /// </summary>
        [HttpPost("tao-bai-luyen")]
        public async Task<IActionResult> TaoBaiLuyenNghe([FromBody] LuyenNgheRequestDto request)
        {
            try
            {
                // Kiểm tra và gán thời gian suy nghĩ mặc định nếu không thuộc bộ lọc [5, 10, 15, 20, 30]
                int thoiGianSuyNghi = ValidThinkingTimes.Contains(request.ThoiGianSuyNghi)
                    ? request.ThoiGianSuyNghi
                    : 15;

                var query = _context.TuVungs.AsNoTracking().AsQueryable();

                if (request.IdCapDo.HasValue && request.IdCapDo.Value > 0)
                {
                    query = query.Where(t => t.IdCapDo == request.IdCapDo.Value);
                }

                // Lấy ngẫu nhiên các từ vựng có nghĩa tiếng Việt và chữ Hán hợp lệ
                var listTuVung = await query
                    .Where(t => !string.IsNullOrEmpty(t.ChuHan) && !string.IsNullOrEmpty(t.NghiaViet))
                    .OrderBy(t => Guid.NewGuid()) // Random ngẫu nhiên
                    .Take(request.SoLuong <= 0 ? 10 : request.SoLuong)
                    .ToListAsync();

                if (!listTuVung.Any())
                {
                    return NotFound(new { success = false, message = "Không tìm thấy từ vựng phù hợp!" });
                }

                var random = new Random();
                var result = new List<LuyenNgheCauHoiDto>();

                foreach (var item in listTuVung)
                {
                    string currentMode = string.IsNullOrWhiteSpace(request.ModeNghe) ? "ZH" : request.ModeNghe.ToUpper();
                    if (currentMode == "MIXED")
                    {
                        currentMode = random.Next(0, 2) == 0 ? "ZH" : "VI";
                    }

                    string audioUrl = "";
                    string prompt = "";

                    if (currentMode == "ZH")
                    {
                        // Ưu tiên dùng đường dẫn âm thanh chuẩn trong DB, nếu không có sẽ tự sinh link TTS tiếng Trung
                        audioUrl = !string.IsNullOrEmpty(item.AmThanhPath)
                            ? item.AmThanhPath
                            : _ttsService.GetChineseAudioUrl(item.ChuHan!);

                        prompt = "Nghe phát âm tiếng Trung và nhập nghĩa Tiếng Việt (hoặc Âm Hán Việt)";
                    }
                    else // Mode "VI"
                    {
                        // Phát âm tiếng Việt dựa trên NghiaViet
                        audioUrl = _ttsService.GetVietnameseAudioUrl(item.NghiaViet!);
                        prompt = "Nghe phát âm Tiếng Việt và nhập Chữ Hán hoặc Pinyin tương ứng";
                    }

                    result.Add(new LuyenNgheCauHoiDto
                    {
                        IdTuVung = item.IdTuVung,
                        ModeLuyenTap = currentMode,
                        AudioUrl = audioUrl,
                        GoiYPrompt = prompt,
                        ThoiGianSuyNghi = thoiGianSuyNghi
                    });
                }

                return Ok(new
                {
                    success = true,
                    total = result.Count,
                    thoiGianSuyNghiMoiCau = thoiGianSuyNghi,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        /// <summary>
        /// API 2: Nộp bài và chấm điểm đáp án nhập từ bàn phím
        /// </summary>
        [HttpPost("nop-bai")]
        public async Task<IActionResult> NopBaiLuyenNghe([FromBody] NopDapAnRequestDto request)
        {
            if (request == null || request.DanhSachDapAn == null || !request.DanhSachDapAn.Any())
            {
                return BadRequest(new { success = false, message = "Dữ liệu nộp bài không hợp lệ!" });
            }

            try
            {
                var listIds = request.DanhSachDapAn.Select(x => x.IdTuVung).Distinct().ToList();
                var dictTuVung = await _context.TuVungs
                    .AsNoTracking()
                    .Where(t => listIds.Contains(t.IdTuVung))
                    .ToDictionaryAsync(t => t.IdTuVung);

                int cauDung = 0;
                var listChiTiet = new List<ChiTietKetQuaDto>();

                foreach (var item in request.DanhSachDapAn)
                {
                    if (!dictTuVung.TryGetValue(item.IdTuVung, out var tv)) continue;

                    bool isCorrect = false;
                    string standardTarget = "";
                    string userAnsNorm = NormalizeString(item.DapAnNguoiDung);

                    if (item.ModeLuyenTap == "ZH")
                    {
                        // Nghe tiếng Trung -> Người dùng nhập Tiếng Việt hoặc Âm Hán Việt
                        string nghĩaNorm = NormalizeString(tv.NghiaViet);
                        string amHanNorm = NormalizeString(tv.AmHan);

                        standardTarget = $"{tv.NghiaViet} {(string.IsNullOrEmpty(tv.AmHan) ? "" : $"({tv.AmHan})")}";

                        // Kiểm tra nếu đáp án chứa từ khóa đúng
                        if (!string.IsNullOrEmpty(userAnsNorm))
                        {
                            if (nghĩaNorm.Contains(userAnsNorm) || userAnsNorm.Contains(nghĩaNorm) ||
                                (!string.IsNullOrEmpty(amHanNorm) && amHanNorm.Equals(userAnsNorm)))
                            {
                                isCorrect = true;
                            }
                        }
                    }
                    else // Mode "VI"
                    {
                        // Nghe Tiếng Việt -> Người dùng nhập Chữ Hán hoặc Pinyin
                        string chuHanNorm = NormalizeString(tv.ChuHan);
                        string pinyinNorm = NormalizeString(tv.Pinyin);

                        standardTarget = $"{tv.ChuHan} / {tv.Pinyin}";

                        if (!string.IsNullOrEmpty(userAnsNorm))
                        {
                            if (chuHanNorm.Equals(userAnsNorm) || pinyinNorm.Equals(userAnsNorm))
                            {
                                isCorrect = true;
                            }
                        }
                    }

                    if (isCorrect) cauDung++;

                    listChiTiet.Add(new ChiTietKetQuaDto
                    {
                        IdTuVung = tv.IdTuVung,
                        IsDung = isCorrect,
                        ChuHan = tv.ChuHan ?? "",
                        Pinyin = tv.Pinyin ?? "",
                        NghiaViet = tv.NghiaViet ?? "",
                        DapAnNguoiDung = item.DapAnNguoiDung,
                        DapAnDungStandard = standardTarget
                    });
                }

                double diem = Math.Round((double)cauDung / request.DanhSachDapAn.Count * 10, 2);

                return Ok(new
                {
                    success = true,
                    data = new KetQuaLuyenNgheDto
                    {
                        TongSoCau = request.DanhSachDapAn.Count,
                        SoCauDung = cauDung,
                        DiemSo = diem,
                        ChiTiet = listChiTiet
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi chấm điểm: " + ex.Message });
            }
        }

        /// <summary>
        /// API 3: Lấy 10 từ vựng ngẫu nhiên trong 1 bài học cụ thể để luyện nghe phản xạ
        /// Mặc định: Phát âm Tiếng Trung - Nhập Việt (ZH), 15 giây/câu, lấy tối đa 10 từ
        /// Endpoint: GET /api/luyen-nghe-phan-xa/bai-hoc/{idBaiHoc}
        /// </summary>
        [HttpGet("bai-hoc/{idBaiHoc}")]
        public async Task<IActionResult> TaoBaiLuyenTheoBaiHoc(int idBaiHoc)
        {
            try
            {
                if (idBaiHoc <= 0)
                {
                    return BadRequest(new { success = false, message = "ID bài học không hợp lệ!" });
                }

                // Lấy danh sách từ vựng thuộc bài học này thông qua bảng BaiHocTuVung
                var listTuVung = await _context.BaiHocTuVungs
                    .AsNoTracking()
                    .Where(bt => bt.IdBaiHoc == idBaiHoc
                                 && bt.IdTuVungNavigation != null
                                 && !string.IsNullOrEmpty(bt.IdTuVungNavigation.ChuHan)
                                 && !string.IsNullOrEmpty(bt.IdTuVungNavigation.NghiaViet))
                    .Select(bt => bt.IdTuVungNavigation)
                    .OrderBy(t => Guid.NewGuid()) // Trộn ngẫu nhiên
                    .Take(10) // Tối đa 10 từ
                    .ToListAsync();

                if (!listTuVung.Any())
                {
                    return NotFound(new { success = false, message = "Bài học này chưa có từ vựng hoặc dữ liệu từ vựng chưa đầy đủ!" });
                }

                // Cấu hình cố định theo yêu cầu
                const int thoiGianSuyNghi = 15; // Mặc định 15 giây
                const string currentMode = "ZH"; // Cố định phát âm Trung -> Viết Việt
                const string prompt = "Nghe phát âm tiếng Trung và nhập nghĩa Tiếng Việt (hoặc Âm Hán Việt)";

                var result = new List<LuyenNgheCauHoiDto>();

                foreach (var item in listTuVung)
                {
                    // Ưu tiên đường dẫn audio có sẵn trong DB, nếu trống sẽ tự động tạo link qua TTS
                    string audioUrl = !string.IsNullOrEmpty(item.AmThanhPath)
                        ? item.AmThanhPath
                        : _ttsService.GetChineseAudioUrl(item.ChuHan!);

                    result.Add(new LuyenNgheCauHoiDto
                    {
                        IdTuVung = item.IdTuVung,
                        ModeLuyenTap = currentMode,
                        AudioUrl = audioUrl,
                        GoiYPrompt = prompt,
                        ThoiGianSuyNghi = thoiGianSuyNghi
                    });
                }

                return Ok(new
                {
                    success = true,
                    total = result.Count,
                    thoiGianSuyNghiMoiCau = thoiGianSuyNghi,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        #region Helper Chuẩn hóa chuỗi nhập vào để so sánh đáp án linh hoạt
        private string NormalizeString(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            // Chuyển chữ thường, xóa khoảng trắng thừa và các dấu câu cơ bản
            string str = input.Trim().ToLower();
            str = Regex.Replace(str, @"\s+", " ");
            str = Regex.Replace(str, @"[.,!?;:'""\-]", "");
            return str;
        }
        #endregion
    }
}