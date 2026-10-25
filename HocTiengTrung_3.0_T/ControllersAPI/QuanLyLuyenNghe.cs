using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12.QuanLyLuyenNghe;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using MiniExcelLibs;

namespace HocTiengTrung_3._0_T.ControllersAPI
{
    [Route("api/quanlyluyennghe")]
    [ApiController]
    public class QuanLyLuyenNghe : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<QuanLyLuyenNghe> _logger;

        // Định nghĩa các key cho cache
        private const string VideoListCacheKey = "VideoLuyenNghe_List";
        private const string VideoDetailCacheKeyPrefix = "VideoLuyenNghe_Detail_";
        // Thêm static để dùng chung giữa các request
        private static CancellationTokenSource _resetCacheToken = new CancellationTokenSource();

        public QuanLyLuyenNghe(TmdtContext context, IMemoryCache cache, ILogger<QuanLyLuyenNghe> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        [HttpGet("get-all-videos")]
        public async Task<IActionResult> GetAllVideos([FromQuery] int? idCapDo)
        {
            try
            {
                string dynamicCacheKey = $"{VideoListCacheKey}_{idCapDo ?? 0}";

                if (!_cache.TryGetValue(dynamicCacheKey, out List<VideoLuyenNgheModels>? videos))
                {
                    var query = _context.VideoLuyenNghes.AsQueryable();

                    if (idCapDo.HasValue && idCapDo > 0)
                    {
                        query = query.Where(v => v.IdCapDo == idCapDo);
                    }

                    videos = await query
                        .Include(v => v.IdCapDoNavigation)
                        .Select(v => new VideoLuyenNgheModels
                        {
                            IdVideo = v.IdVideo,
                            TieuDe = v.TieuDe,
                            VideoPath = v.VideoPath,
                            MoTa = v.MoTa,
                            IdCapDo = v.IdCapDo,
                            TenCapDo = v.IdCapDoNavigation != null ? v.IdCapDoNavigation.TenCapDo : "Chưa xác định",
                            NgayTao = v.NgayTao
                        })
                        .OrderByDescending(v => v.NgayTao)
                        .ToListAsync();

                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                        .AddExpirationToken(new CancellationChangeToken(_resetCacheToken.Token));

                    _cache.Set(dynamicCacheKey, videos, cacheEntryOptions);
                }

                return Ok(new { success = true, data = videos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách video");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpGet("get-video-detail/{id}")]
        public async Task<IActionResult> GetVideoDetail(int id)
        {
            string cacheKey = $"{VideoDetailCacheKeyPrefix}{id}";
            try
            {
                if (!_cache.TryGetValue(cacheKey, out VideoLuyenNgheModels? video))
                {
                    video = await _context.VideoLuyenNghes
                        .Include(v => v.IdCapDoNavigation)
                        .Include(v => v.VideoSubtitles)
                        .Where(v => v.IdVideo == id)
                        .Select(v => new VideoLuyenNgheModels
                        {
                            IdVideo = v.IdVideo,
                            TieuDe = v.TieuDe,
                            VideoPath = v.VideoPath,
                            MoTa = v.MoTa,
                            NgayTao = v.NgayTao,
                            IdCapDo = v.IdCapDo,
                            VideoSubtitles = v.VideoSubtitles
                                .OrderBy(s => s.ThuTu)
                                .Select(s => new VideoSubtitleModels
                                {
                                    IdSub = s.IdSub,
                                    IdVideo = s.IdVideo,
                                    StartTime = s.StartTime,
                                    EndTime = s.EndTime,
                                    HanTu = s.HanTu,
                                    Pinyin = s.Pinyin,
                                    DichNghia = s.DichNghia,
                                    ThuTu = s.ThuTu
                                }).ToList()
                        })
                        .FirstOrDefaultAsync();

                    if (video == null)
                        return NotFound(new { success = false, message = "Không tìm thấy video" });

                    _cache.Set(cacheKey, video, TimeSpan.FromMinutes(15));
                }

                return Ok(new { success = true, data = video });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi lấy chi tiết video ID: {id}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        // --- NEW: Endpoint để lưu ghi chép (Chuẩn bị cho tính năng lưu DB) ---
        [HttpPost("save-notes")]
        public IActionResult SaveNotes([FromBody] NoteRequest request)
        {
            try
            {
                // Tương lai: var newNote = new UserNote { UserId = ..., VideoId = request.VideoId, Content = request.Notes };
                // _context.UserNotes.Add(newNote); _context.SaveChanges();

                _logger.LogInformation($"Đã nhận ghi chép cho video {request.VideoId}: {request.Notes}");
                return Ok(new { success = true, message = "Lưu ghi chép thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("delete-video/{id}")]
        public async Task<IActionResult> DeleteVideo(int id)
        {
            try
            {
                var video = await _context.VideoLuyenNghes.FindAsync(id);
                if (video == null) return NotFound();

                // 1. Tìm tất cả phụ đề liên quan
                var subs = _context.VideoSubtitles.Where(s => s.IdVideo == id);

                // 2. Xóa danh sách phụ đề trước
                _context.VideoSubtitles.RemoveRange(subs);

                // 3. Sau đó mới xóa video
                _context.VideoLuyenNghes.Remove(video);
                ClearVideoCache(id); // Invalidate cache liên quan đến video này
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
        
        [HttpPost("add-video")]
        public async Task<IActionResult> AddVideo([FromForm] VideoLuyenNgheRequest request)
        {
            try
            {
                if (request == null) return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });

                if (string.IsNullOrWhiteSpace(request.VideoPath))
                {
                    return BadRequest(new { success = false, message = "Vui lòng nhập đường dẫn video!" });
                }

                // 1. Tạo Entity và Map dữ liệu
                var newVideo = new VideoLuyenNghe
                {
                    TieuDe = request.TieuDe,
                    VideoPath = request.VideoPath.Trim(), // Lưu đường dẫn văn bản
                    IdCapDo = request.IdCapDo,
                    MoTa = request.MoTa,
                    NgayTao = DateTime.Now
                };

                // 2. Parse danh sách phụ đề từ JSON string
                if (!string.IsNullOrEmpty(request.SubtitlesJson))
                {
                    var subs = Newtonsoft.Json.JsonConvert.DeserializeObject<List<VideoSubtitle>>(request.SubtitlesJson);
                    if (subs != null && subs.Any())
                    {
                        foreach (var s in subs)
                        {
                            newVideo.VideoSubtitles.Add(new VideoSubtitle
                            {
                                StartTime = s.StartTime,
                                EndTime = s.EndTime,
                                HanTu = s.HanTu,
                                Pinyin = s.Pinyin,
                                DichNghia = s.DichNghia,
                                ThuTu = s.ThuTu
                            });
                        }
                    }
                }

                _context.VideoLuyenNghes.Add(newVideo);
                await _context.SaveChangesAsync();

                // ClearVideoCache(); // Mở lại nếu bạn có cache
                return Ok(new { success = true, message = "Thêm video thành công", id = newVideo.IdVideo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm video");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost("import-video-excel")]
        public async Task<IActionResult> ImportVideoExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, message = "Vui lòng chọn file Excel!" });
            }

            try
            {
                using (var stream = file.OpenReadStream())
                {
                    // Đọc toàn bộ file Excel thành danh sách các Dictionary (mỗi dòng là 1 dòng dữ liệu)
                    var rows = stream.Query(useHeaderRow: false)
                                     .Cast<IDictionary<string, object>>()
                                     .ToList();

                    // Kiểm tra tối thiểu phải có đủ phần header phụ đề (ít nhất 6 dòng)
                    if (rows.Count < 6)
                    {
                        return BadRequest(new { success = false, message = "File Excel không đúng cấu trúc quy định!" });
                    }

                    // Hàm helper để lấy chuỗi an toàn từ ô Excel dựa trên tên cột (A, B, C...)
                    string GetCellValue(IDictionary<string, object> row, string columnName)
                    {
                        return row.ContainsKey(columnName) ? row[columnName]?.ToString()?.Trim() : null;
                    }

                    // 1. Đọc thông tin Video tổng quan (Từ dòng 1 đến dòng 4 - tương ứng index 0 đến 3)
                    var tieuDe = GetCellValue(rows[0], "B");   // Ô B1
                    var moTa = GetCellValue(rows[1], "B");    // Ô B2
                    var capDoStr = GetCellValue(rows[2], "B"); // Ô B3
                    var videoPath = GetCellValue(rows[3], "B"); // Ô B4

                    if (string.IsNullOrWhiteSpace(videoPath))
                    {
                        return BadRequest(new { success = false, message = "Đường dẫn video ở ô B4 không được để trống!" });
                    }

                    int? idCapDo = null;
                    if (int.TryParse(capDoStr, out int capDoId))
                    {
                        idCapDo = capDoId;
                    }

                    // Khởi tạo đối tượng Video
                    var newVideo = new VideoLuyenNghe
                    {
                        TieuDe = tieuDe,
                        MoTa = moTa,
                        IdCapDo = idCapDo,
                        VideoPath = videoPath,
                        NgayTao = DateTime.Now,
                        VideoSubtitles = new List<VideoSubtitle>()
                    };

                    // Định dạng chuỗi thời gian hỗ trợ cả có ff (khung hình) và không có ff
                    string[] timeFormats = { "HH:mm:ss:ff", "HH:mm:ss", "mm:ss" };

                    // 2. Đọc danh sách phụ đề từ dòng thứ 7 trở đi (tương ứng index 6)
                    for (int i = 6; i < rows.Count; i++)
                    {
                        var row = rows[i];

                        var sttStr = GetCellValue(row, "A");
                        var startStr = GetCellValue(row, "B");
                        var endStr = GetCellValue(row, "C");
                        var hanTu = GetCellValue(row, "D");
                        var pinyin = GetCellValue(row, "E");
                        var dichNghia = GetCellValue(row, "F");

                        // Bỏ qua các dòng trống hoàn toàn hoặc thiếu thông tin cốt lõi
                        if (string.IsNullOrEmpty(startStr) && string.IsNullOrEmpty(endStr) && string.IsNullOrEmpty(hanTu))
                        {
                            continue;
                        }

                        int.TryParse(sttStr, out int thuTu);

                        // Chuẩn hóa dấu hai chấm tiếng Trung "：" thành ":" chuẩn và loại bỏ khoảng trắng
                        string cleanStartStr = startStr?.Replace("：", ":").Trim() ?? "";
                        string cleanEndStr = endStr?.Replace("：", ":").Trim() ?? "";

                        // Thực hiện ép kiểu chính xác bằng TryParseExact
                        TimeOnly.TryParseExact(cleanStartStr, timeFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out TimeOnly startTime);
                        TimeOnly.TryParseExact(cleanEndStr, timeFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out TimeOnly endTime);

                        newVideo.VideoSubtitles.Add(new VideoSubtitle
                        {
                            ThuTu = thuTu,
                            StartTime = startTime,
                            EndTime = endTime,
                            HanTu = hanTu,
                            Pinyin = pinyin,
                            DichNghia = dichNghia
                        });
                    }

                    // 3. Lưu vào cơ sở dữ liệu
                    _context.VideoLuyenNghes.Add(newVideo);
                    await _context.SaveChangesAsync();
                    ClearVideoCache();

                    return Ok(new { success = true, message = "Nhập video và phụ đề từ Excel thành công", id = newVideo.IdVideo });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi nhập file Excel video");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPut("update-video/{id}")]
        public async Task<IActionResult> UpdateVideo(int id, [FromForm] VideoLuyenNgheUploadRequest request)
        {
            try
            {
                var videoEntity = await _context.VideoLuyenNghes
                    .Include(v => v.VideoSubtitles)
                    .FirstOrDefaultAsync(v => v.IdVideo == id);

                if (videoEntity == null) return NotFound(new { success = false, message = "Không tìm thấy video" });

                // Xử lý nếu có file mới
                if (request.VideoFile != null)
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(request.VideoFile.FileName);
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Audio", fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await request.VideoFile.CopyToAsync(stream);
                    }
                    videoEntity.VideoPath = "/Audio/" + fileName;
                }

                videoEntity.TieuDe = request.TieuDe;
                videoEntity.IdCapDo = request.IdCapDo;
                videoEntity.MoTa = request.MoTa;

                // Cập nhật Subtitles (Xóa cũ thêm mới)
                _context.VideoSubtitles.RemoveRange(videoEntity.VideoSubtitles);
                if (!string.IsNullOrEmpty(request.SubtitlesJson))
                {
                    var subs = Newtonsoft.Json.JsonConvert.DeserializeObject<List<VideoSubtitle>>(request.SubtitlesJson);
                    if (subs != null)
                    {
                        foreach (var s in subs)
                        {
                            videoEntity.VideoSubtitles.Add(new VideoSubtitle
                            {
                                IdVideo = id,
                                StartTime = s.StartTime,
                                EndTime = s.EndTime,
                                HanTu = s.HanTu,
                                Pinyin = s.Pinyin,
                                DichNghia = s.DichNghia,
                                ThuTu = s.ThuTu
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                ClearVideoCache(id);
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        // Hàm phụ trợ để dọn dẹp Cache
        private void ClearVideoCache(int? videoId = null)
        {
            // 1. Hủy token cũ -> Toàn bộ cache có gắn token này sẽ bay màu lập tức
            _resetCacheToken.Cancel();
            _resetCacheToken.Dispose();

            // 2. Tạo token mới cho các lượt cache tiếp theo
            _resetCacheToken = new CancellationTokenSource();

            // 3. Xóa thêm cache chi tiết (vì nó thường không thay đổi theo list)
            if (videoId.HasValue)
            {
                _cache.Remove($"{VideoDetailCacheKeyPrefix}{videoId}");
            }

            _logger.LogInformation("Đã invalidate toàn bộ cache danh sách luyện nghe.");
        }

        // DTO cho Note
        public class NoteRequest
        {
            public int VideoId { get; set; }
            public string Notes { get; set; } = string.Empty;
        }
    }
}