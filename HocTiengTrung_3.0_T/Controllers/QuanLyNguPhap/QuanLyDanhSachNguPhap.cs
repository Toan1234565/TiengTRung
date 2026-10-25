using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12;
using HocTiengTrung_3._0_T.Models12.BaiTap;
using HocTiengTrung_3._0_T.Models12.NguPhap;
using HocTiengTrung_3._0_T.Models12.TuVung;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;

namespace HocTiengTrung_3._0_T.Controllers.QuanLyNguPhap
{
    public class QuanLyDanhSachNguPhap : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyDanhSachNguPhap> _logger;

        // SỬA: Loại bỏ gán cứng localhost
        private readonly string apiBaseUrlTV;
        private readonly string apiBaseUrlNP;
        private readonly IUserService _userService;
        // SỬA: Thêm IConfiguration vào Constructor
        public QuanLyDanhSachNguPhap(IHttpClientFactory httpClientFactory, ILogger<QuanLyDanhSachNguPhap> logger, IConfiguration configuration, IUserService userService)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _userService = userService;

            // 1. Lấy link gốc từ appsettings.json (http://Hoctiengtrung-001-site1.jtempurl.com)
            string baseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7151";
            //string baseUrl = "https://localhost:7151";
            baseUrl = baseUrl.TrimEnd('/');

            // 2. Gán link cho API Từ vựng (để lấy Cấp độ)
            apiBaseUrlTV = $"{baseUrl}/api/quanlytuvung";

            // 3. Gán link cho API Ngữ pháp
            apiBaseUrlNP = $"{baseUrl}/api/quanlynguphap";
        }

        
        // 1. Trang chọn cấp độ (HSK 1, 2, 3...)
        public async Task<IActionResult> DanhSachCapDoNguPhap()
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // SỬA Ở ĐÂY: Bổ sung bộ lọc ?status=true vào URL API
            string apiUrl = $"{apiBaseUrlTV}/danhsachcapdo?status=true";

            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var listCapDo = JsonConvert.DeserializeObject<List<CapDoModels>>(jsonString);
                    return View(listCapDo ?? new List<CapDoModels>());
                }
                return View(new List<CapDoModels>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi lấy danh sách cấp độ");
                return View(new List<CapDoModels>());
            }
        }

        // 2. Trang danh sách ngữ pháp theo cấp độ (Có phân trang & tìm kiếm)
        public async Task<IActionResult> DanhsachNguPhap(int capdo = 1, string? searchTerm = null, int page = 1, int pagesize = 10)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // 1. Chuyển tiếp Cookie & Token xác thực sang API backend nếu có
            if (Request.Headers.ContainsKey("Cookie"))
            {
                client.DefaultRequestHeaders.Add("Cookie", Request.Headers["Cookie"].ToString());
            }

            if (Request.Headers.ContainsKey("Authorization"))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", Request.Headers["Authorization"].ToString());
            }

            // 2. Lấy IdTrungTam và MaTrungTam của tài khoản đang đăng nhập
            string? idTrungTamStr = User.FindFirst("IdTrungTam")?.Value
                                    ?? HttpContext.Session.GetString("IdTrungTam");

            string? maTrungTam = User.FindFirst("MaTrungTam")?.Value
                                 ?? HttpContext.Session.GetString("MaTrungTam");

            int.TryParse(idTrungTamStr, out int idTrungTamFinal);

            // 3. Khởi tạo Query String gọi sang API backend kèm bộ lọc Trung tâm
            var queryParams = new List<string>
    {
        $"capdo={capdo}",
        $"page={page}",
        $"pagesize={pagesize}"
    };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                queryParams.Add($"search={Uri.EscapeDataString(searchTerm.Trim())}");
            }

            if (idTrungTamFinal > 0)
            {
                queryParams.Add($"idTrungTam={idTrungTamFinal}");
            }

            if (!string.IsNullOrWhiteSpace(maTrungTam))
            {
                queryParams.Add($"maTrungTam={Uri.EscapeDataString(maTrungTam.Trim())}");
            }

            string apiUrl = $"{apiBaseUrlNP}/danh-sach-ngu-phap?{string.Join("&", queryParams)}";

            try
            {
                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // 4. Giải mã dữ liệu JSON
                    var resultObj = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(jsonString);
                    var listNguPhap = resultObj?["data"]?.ToObject<List<NguPhapModels>>() ?? new List<NguPhapModels>();

                    // 5. Đổ dữ liệu vào ViewBag để View sử dụng và duy trì bộ lọc
                    ViewBag.CurrentCapDo = capdo;
                    ViewBag.SearchTerm = searchTerm;
                    ViewBag.CurrentPage = page;
                    ViewBag.TotalItems = (int?)resultObj?["totalItems"] ?? 0;
                    ViewBag.PageSize = (int?)resultObj?["pageSize"] ?? pagesize;
                    ViewBag.IdTrungTam = idTrungTamFinal;
                    ViewBag.MaTrungTam = maTrungTam;

                    return View(listNguPhap);
                }
                else
                {
                    _logger.LogWarning($"API Lấy ngữ pháp thất bại: {response.StatusCode} | URL: {apiUrl}");
                    ViewBag.TotalItems = 0;
                    ViewBag.CurrentCapDo = capdo;
                    ViewBag.SearchTerm = searchTerm;
                    ViewBag.CurrentPage = page;
                    ViewBag.PageSize = pagesize;
                    ViewBag.IdTrungTam = idTrungTamFinal;
                    ViewBag.MaTrungTam = maTrungTam;

                    return View(new List<NguPhapModels>());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi lấy danh sách ngữ pháp HSK cho trung tâm {IdTrungTam}", idTrungTamFinal);
                ViewBag.TotalItems = 0;
                ViewBag.CurrentCapDo = capdo;
                ViewBag.SearchTerm = searchTerm;
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pagesize;
                ViewBag.IdTrungTam = idTrungTamFinal;
                ViewBag.MaTrungTam = maTrungTam;

                return View(new List<NguPhapModels>());
            }
        }
        // 3. Trang làm bài tập theo ngữ pháp cụ thể (kèm lọc loại câu hỏi)
        // 3. Trang làm bài tập (Sửa ID tham số)
        public async Task<IActionResult> BaiTapNguPhap(int id, int? idLoai = 1)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            string apiUrl = $"{apiBaseUrlNP}/bai-tap-theo-ngu-phap/{id}?idLoai={idLoai}";

            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, apiUrl);

            // Chuyển tiếp cookie xác thực người dùng an toàn
            if (Request.Headers.TryGetValue("Cookie", out var cookieValue))
            {
                requestMessage.Headers.TryAddWithoutValidation("Cookie", cookieValue.ToString());
            }

            try
            {
                var response = await client.SendAsync(requestMessage);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<ApiResponse<NguPhapModels>>(jsonString);

                    if (apiResponse != null && apiResponse.Success && apiResponse.Data != null)
                    {
                        var model = apiResponse.Data;

                        ViewBag.IdNguPhap = id;
                        ViewBag.SelectedLoai = idLoai;
                        ViewBag.TenNguPhap = model.TenNguPhap;
                        ViewBag.IdCapDo = model.IdCapDo;

                        return View("BaiTapNguPhap", model);
                    }
                }

                _logger.LogWarning($"API trả về lỗi hoặc không có dữ liệu: {response.StatusCode} | URL: {apiUrl}");
                return View("Error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi kết nối API lấy câu hỏi bài tập ngữ pháp ID: {id}");
                return View("Error");
            }
        }


        // 4. Trang quản trị tổng hợp: Xem danh sách ngữ pháp kèm toàn bộ câu hỏi/đáp án (Có phân trang)
        public async Task<IActionResult> DanhSachBaiTapAdmin(int? capdo, int page = 1, int pagesize = 5)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // Xây dựng URL gọi API phân trang tổng hợp đã viết ở tầng Core API
            string apiUrl = $"{apiBaseUrlNP}/admin/danh-sach-kem-bai-tap?page={page}&pagesize={pagesize}";
            if (capdo.HasValue && capdo > 0)
            {
                apiUrl += $"&capdo={capdo.Value}";
            }

            try
            {
                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // Giải mã JObject để bóc tách cấu trúc phân trang linh hoạt
                    var resultObj = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(jsonString);

                    // Bóc mảng "data" chứa danh sách Ngữ pháp kèm sẵn Bài tập
                    var listNguPhapKemBaiTap = resultObj["data"]?.ToObject<List<NguPhapModels>>() ?? new List<NguPhapModels>();

                    // Đẩy dữ liệu điều hướng phân trang ra ViewBag để Front-end vẽ giao diện
                    ViewBag.CurrentCapDo = capdo;
                    ViewBag.CurrentPage = page;
                    ViewBag.TotalItems = (int?)resultObj["totalItems"] ?? 0;
                    ViewBag.PageSize = (int?)resultObj["pageSize"] ?? pagesize;

                    // Trả về View quản trị kèm dữ liệu danh sách DTO
                    return View(listNguPhapKemBaiTap);
                }
                else
                {
                    _logger.LogWarning($"API Quản trị lấy ngữ pháp kèm bài tập thất bại: {response.StatusCode}");
                    ViewBag.TotalItems = 0;
                    return View(new List<NguPhapModels>());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối API khi tải trang quản trị tổng hợp ngữ pháp và bài tập");
                ViewBag.TotalItems = 0;
                return View(new List<NguPhapModels>());
            }
        }

        // Class bổ trợ để bóc tách JSON từ API
        public class ApiResponse<T>
        {
            public bool Success { get; set; }
            public T Data { get; set; }
        }
    }
}