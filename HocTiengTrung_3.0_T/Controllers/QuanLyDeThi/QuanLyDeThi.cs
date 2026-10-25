using HocTiengTrung_3._0_T.Controllers.QuanLyBaiTap;
using HocTiengTrung_3._0_T.Models12.LuuLichSu;
using HocTiengTrung_3._0_T.Models12.QuanLyDeThi; // Đảm bảo đúng namespace của các Model đề thi
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net.Http;
using static HocTiengTrung_3._0_T.Controllers.QuanLyBaiTap.QuanLyDanhSachBaiTap;

namespace HocTiengTrung_3._0_T.Controllers.QuanLyDeThi
{
    public class QuanLyDeThi : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyDeThi> _logger;
        // SỬA: Không gán cứng link localhost ở đây nữa
        private readonly string apiBaseUrl;

        // SỬA: Thêm IConfiguration vào Constructor
        public QuanLyDeThi(IHttpClientFactory httpClientFactory, ILogger<QuanLyDeThi> logger, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            // 1. Lấy link gốc từ appsettings.json (http://Hoctiengtrung-001-site1.jtempurl.com)
            // Nếu không có (khi chạy local) thì mặc định lấy localhost
            string baseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7151";
            //string baseUrl = "https://localhost:7151";
            // 2. Xử lý dấu gạch chéo để tránh lỗi link (ví dụ: .com//api/...)
            baseUrl = baseUrl.TrimEnd('/');

            // 3. Gán đường dẫn đến Controller DeThi cụ thể
            apiBaseUrl = $"{baseUrl}/api/quanlydethi";
        }

        // 1. Trang chọn cấp độ (HSK 1, 2, 3...)
        public IActionResult DanhSachCapDo()
        {
            return View();
        }

        // 2. Trang hiển thị danh sách đề thi theo cấp độ
        // URL: /QuanLyDeThi/DanhSachDeThi?idCapDo=3
        public async Task<IActionResult> DanhSachDeThi(int idCapDo)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                // Gọi API lấy danh sách đề theo cấp độ
                var response = await client.GetAsync($"{apiBaseUrl}/danh-sach-de-thi?idCapDo={idCapDo}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonData = await response.Content.ReadAsStringAsync();
                    // Deserialize dựa trên cấu trúc { success, data } của API
                    var result = JsonConvert.DeserializeObject<ApiResponse<List<DeThiModels>>>(jsonData);

                    ViewBag.IdCapDo = idCapDo;
                    return View(result?.Data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi call API danh sách đề thi");
            }
            return View(new List<DeThiModels>());
        }

        // URL: /QuanLyDeThi/LamBaiThi/1
        public async Task<IActionResult> LamBaiThi(int id)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                // Gọi API lấy dữ liệu tổng hợp (Info + Questions)
                var response = await client.GetAsync($"{apiBaseUrl}/chi-tiet-de-thi/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonData = await response.Content.ReadAsStringAsync();

                    

                    // Map vào Object mới chứa cả thông tin thời gian và câu hỏi
                    var result = JsonConvert.DeserializeObject<ApiResponse<ExamDataResponse>>(jsonData);

                    if (result != null && result.Success && result.Data != null)
                    {
                        // Truyền thông tin thời gian riêng biệt vào ViewBag để JavaScript xử lý
                        ViewBag.TimeNghe = (result.Data.Info.ThoiGianNghe ?? 0) * 60;
                        ViewBag.TimeDoc = (result.Data.Info.ThoiGianDoc ?? 0) * 60;
                        ViewBag.TimeViet = (result.Data.Info.ThoiGianViet ?? 0) * 60;
                        ViewBag.TimeChuyen = (result.Data.Info.ThoiGianChuyenTiep ?? 0) * 60;

                        ViewBag.DeThiId = id;
                        ViewBag.TenDe = result.Data.Info.TenDe;

                        // Trả về danh sách câu hỏi cho View
                        return View(result.Data.Questions);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi call API chi tiết đề thi ID: {id}");
            }
            return RedirectToAction("DanhSachCapDo");
        }

        [HttpGet]
        public async Task<IActionResult> ThemDeThi()
        {
            var client = _httpClientFactory.CreateClient();
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
            };

            // Chỉ cần gọi API lấy danh sách loại đề thi
            try
            {
                var response = await client.GetAsync($"{apiBaseUrl}/danh-sach-loai-de-thi");

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    // Lưu ý: API của bạn trả về object { success: true, data: [...] } 
                    // nên cần parse đúng cấu trúc
                    var apiResult = JsonConvert.DeserializeObject<ApiResponse<List<LoaiDeThiModels>>>(jsonResponse, settings);

                    ViewBag.ListLoaiDeThi = apiResult?.Data ?? new List<LoaiDeThiModels>();
                }
                else
                {
                    ViewBag.ListLoaiDeThi = new List<LoaiDeThiModels>();
                    _logger.LogWarning("API lấy danh sách loại đề thi trả về lỗi: {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi call API danh sách loại đề thi");
                ViewBag.ListLoaiDeThi = new List<LoaiDeThiModels>();
            }

            return View(); // Trả về View giao diện thêm đề thi
        }

        public async Task<IActionResult> LichSuaDeThi()
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // 1. Chuyển tiếp Cookie để API nhận diện User
            var cookie = Request.Headers["Cookie"].ToString();
            if (!string.IsNullOrEmpty(cookie))
            {
                client.DefaultRequestHeaders.Add("Cookie", cookie);
            }

            // 2. Cập nhật đúng đường dẫn API đã viết ở Controller API
            string apiUrl = $"{apiBaseUrl}/lich-su-thi";

            try
            {
                var response = await client.GetAsync(apiUrl);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Chưa đăng nhập, chuyển hướng về Login.");
                    return RedirectToAction("DangNhap", "QuanLyTaiKhoan");
                }

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // Giải mã JSON theo cấu trúc { success: bool, data: List<...> }
                    var result = JsonConvert.DeserializeObject<ApiResponseLichSu>(jsonString);

                    if (result != null && result.Success)
                    {
                        return View(result.Data ?? new List<KetQuaThiViewModel>());
                    }
                }

                _logger.LogWarning($"API Lấy lịch sử thất bại: {response.StatusCode}");
                return View(new List<KetQuaThiViewModel>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi API lich-su-thi");
                // Đảm bảo trả về đúng kiểu List mà View đang mong đợi
                return View(new List<KetQuaThiViewModel>());
            }
        }


        public async Task<IActionResult> KetQuaBaiThi(int id)
        {
            // Kiểm tra ID hợp lệ trước khi gọi API
            if (id <= 0)
            {
                _logger.LogWarning("ID kết quả không hợp lệ: {Id}", id);
                return RedirectToAction("LichSuaDeThi");
            }

            var client = _httpClientFactory.CreateClient("BypassSSL");

            // 1. Chuyển tiếp Cookie
            var cookie = Request.Headers["Cookie"].ToString();
            if (!string.IsNullOrEmpty(cookie))
            {
                client.DefaultRequestHeaders.Add("Cookie", cookie);
            }

            string apiUrl = $"{apiBaseUrl}/ket-qua-chi-tiet/{id}";

            try
            {
                var response = await client.GetAsync(apiUrl);

                // Trường hợp 1: Chưa đăng nhập hoặc Session hết hạn
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized khi truy cập kết quả {Id}. Chuyển hướng đăng nhập.", id);
                    return RedirectToAction("DangNhap", "QuanLyTaiKhoan");
                }

                // Trường hợp 2: Không tìm thấy bài thi (ID sai hoặc đã bị xóa)
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy thông tin chi tiết cho bài thi này.";
                    return RedirectToAction("LichSuaDeThi");
                }

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // Trường hợp 3: API trả về thành công nhưng JSON trống
                    if (string.IsNullOrEmpty(jsonString))
                    {
                        _logger.LogError("API trả về nội dung rỗng cho ID: {Id}", id);
                        return RedirectToAction("LichSuaDeThi");
                    }

                    var result = JsonConvert.DeserializeObject<ApiResponseKetQuaToanBo>(jsonString);

                    if (result != null && result.Success)
                    {
                        return View(result);
                    }
                    else
                    {
                        // API trả về success = false kèm message lỗi từ logic Backend
                        TempData["ErrorMessage"] = result?.Message ?? "Có lỗi xảy ra khi tải dữ liệu bài thi.";
                        return RedirectToAction("LichSuaDeThi");
                    }
                }

                // Trường hợp 4: Các lỗi HTTP khác (500, 400...)
                _logger.LogError("Lỗi hệ thống từ API. Status: {StatusCode}", response.StatusCode);
                TempData["ErrorMessage"] = "Hệ thống đang bảo trì, vui lòng thử lại sau.";
                return RedirectToAction("LichSuaDeThi");
            }
            catch (JsonException jex)
            {
                _logger.LogError(jex, "Lỗi giải mã JSON kết quả ID: {Id}", id);
                TempData["ErrorMessage"] = "Dữ liệu bài thi không đúng định dạng.";
                return RedirectToAction("LichSuaDeThi");
            }
            catch (HttpRequestException hex)
            {
                _logger.LogError(hex, "Lỗi kết nối mạng khi gọi API ID: {Id}", id);
                TempData["ErrorMessage"] = "Không thể kết nối đến máy chủ. Vui lòng kiểm tra mạng.";
                return RedirectToAction("LichSuaDeThi");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi xem lại bài làm ID: {id}", id);
                return RedirectToAction("LichSuaDeThi");
            }
        }

        public async Task<IActionResult> DanhSachDeThiAdmin(int idCapDo = 3)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                // Gọi API lấy danh sách đề theo cấp độ
                var response = await client.GetAsync($"{apiBaseUrl}/danh-sach-de-thi?idCapDo={idCapDo}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonData = await response.Content.ReadAsStringAsync();
                    // Deserialize dựa trên cấu trúc { success, data } của API
                    var result = JsonConvert.DeserializeObject<ApiResponse<List<DeThiModels>>>(jsonData);

                    ViewBag.IdCapDo = idCapDo;
                    return View(result?.Data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi call API danh sách đề thi");
            }
            return View(new List<DeThiModels>());
        }

        // Thêm hàm này vào trong class QuanLyDeThi
        public async Task<IActionResult> ChiTietDeThiAdmin(int id)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                // Gọi API lấy dữ liệu chi tiết đề thi (Gồm Info và Questions)
                // URL API: {apiBaseUrl}/chi-tiet-de-thi/{id}
                var response = await client.GetAsync($"{apiBaseUrl}/chi-tiet-de-thi/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonData = await response.Content.ReadAsStringAsync();

                    // Sử dụng class ExamDataResponse bạn đã định nghĩa bên dưới để map dữ liệu
                    var result = JsonConvert.DeserializeObject<ApiResponse<ExamDataResponse>>(jsonData);

                    if (result != null && result.Success && result.Data != null)
                    {
                        // Truyền thông tin đề thi vào ViewBag nếu cần hiển thị Header
                        ViewBag.TenDe = result.Data.Info.TenDe;
                        ViewBag.TongSoCau = result.Data.Info.TongSoCau;
                        ViewBag.IdDeThi = id;

                        // Trả về Model là object chứa cả Info và Questions 
                        // để Admin có thể xem được toàn bộ thông tin
                        return View(result.Data);
                    }
                    else
                    {
                        TempData["ErrorMessage"] = result?.Message ?? "Không thể lấy thông tin chi tiết đề thi.";
                    }
                }
                else
                {
                    _logger.LogWarning("API chi tiết đề thi trả về lỗi: {StatusCode}", response.StatusCode);
                    TempData["ErrorMessage"] = "Lỗi kết nối đến máy chủ API.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi nghiêm trọng khi call API chi tiết đề thi ID: {id}");
                TempData["ErrorMessage"] = "Hệ thống gặp sự cố khi tải dữ liệu.";
            }

            // Nếu có lỗi, quay lại trang danh sách Admin
            return RedirectToAction("DanhSachDeThiAdmin");
        }
    }
    public class ApiResponseLichSu
    {
        public bool Success { get; set; }
        public List<KetQuaThiViewModel> Data { get; set; }
    }
    

    public class ExamDataResponse
    {
        public DeThiModels Info { get; set; }
        public List<ChiTietDeThiModels> Questions { get; set; }
    }

    // Class phụ trợ để map dữ liệu từ API
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Message { get; set; }
    }
    
}