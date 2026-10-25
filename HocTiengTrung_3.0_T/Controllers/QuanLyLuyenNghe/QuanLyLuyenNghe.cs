using HocTiengTrung_3._0_T.Models12.QuanLyLuyenNghe;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net.Http;

namespace HocTiengTrung_3._0_T.Controllers.QuanLyLuyenNoi
{
    public class QuanLyLuyenNghe : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyLuyenNghe> _logger;

        // SỬA: Xóa link cứng localhost
        private readonly string apiBaseUrlLN;

        // SỬA: Thêm IConfiguration vào tham số
        public QuanLyLuyenNghe(IHttpClientFactory httpClientFactory, ILogger<QuanLyLuyenNghe> logger, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            // 1. Lấy link gốc từ file appsettings.json
            string baseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7151";
            //string baseUrl = "https://localhost:7151";
            // 2. Xử lý xóa dấu gạch chéo thừa ở cuối (nếu có)
            baseUrl = baseUrl.TrimEnd('/');

            // 3. Nối thêm đường dẫn API cho Luyện Nghe
            // Kết quả: http://Hoctiengtrung-001-site1.jtempurl.com/api/quanlyluyennghe
            apiBaseUrlLN = $"{baseUrl}/api/quanlyluyennghe";
        }

        public IActionResult CapDoLuyenNghe()
        {
            return View();
        }

        // API Response Wrapper (để khớp với cấu trúc { success, data } từ API)
        public class ApiResponse<T>
        {
            public bool Success { get; set; }
            public T Data { get; set; }
        }

        public async Task<IActionResult> DanhSachLuyeNghe(int? idCapDo)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                // 1. Xây dựng URL có kèm tham số lọc nếu có
                string apiUrl = $"{apiBaseUrlLN}/get-all-videos";
                if (idCapDo.HasValue && idCapDo > 0)
                {
                    apiUrl += $"?idCapDo={idCapDo}";
                }

                // 2. Gọi API
                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var jsonData = await response.Content.ReadAsStringAsync();

                    // Giải mã JSON
                    var result = JsonConvert.DeserializeObject<ApiResponse<List<VideoLuyenNgheModels>>>(jsonData);

                    if (result != null && result.Success)
                    {
                        // Lưu lại idCapDo hiện tại vào ViewBag để View biết đang lọc cái nào
                        ViewBag.CurrentCapDo = idCapDo;
                        return View(result.Data);
                    }
                }

                _logger.LogWarning($"API {apiUrl} trả về lỗi hoặc không có dữ liệu.");
                return View(new List<VideoLuyenNgheModels>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi API danh sách luyện nghe");
                return View("Error");
            }
        }

        // Thêm Action Chi Tiết để xem Video và Subtitles
        public async Task<IActionResult> ChiTietLuyenNghe(int id)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync($"{apiBaseUrlLN}/get-video-detail/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonData = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<ApiResponse<VideoLuyenNgheModels>>(jsonData);

                    if (result != null && result.Success)
                    {
                        return View(result.Data);
                    }
                }

                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi gọi API chi tiết video {id}");
                return View("Error");
            }
        }
    }
}