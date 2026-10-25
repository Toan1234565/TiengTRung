using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12;
using HocTiengTrung_3._0_T.Models12.BaiTap;
using HocTiengTrung_3._0_T.Models12.TuVung;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using System.Text;

namespace HocTiengTrung_3._0_T.Controllers.QuanLyTuVung
{
    public class QuanLyDanhSachTuVung : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyDanhSachTuVung> _logger;
        // Chú ý: Đảm bảo port 7151 khớp với dự án API của bạn
        // SỬA: Bỏ gán cứng localhost
        private readonly string apiBaseUrl;
        private readonly IUserService _userService;

        // SỬA: Thêm IConfiguration vào tham số của Constructor
        public QuanLyDanhSachTuVung(IHttpClientFactory httpClientFactory, ILogger<QuanLyDanhSachTuVung> logger, IConfiguration configuration, IUserService userService)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _userService = userService;

            // 1. Lấy tên miền từ appsettings.json
            // Nếu chạy local mà không có file cấu hình, mặc định vẫn dùng localhost:7151
            string baseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7151";
            //string baseUrl = "https://localhost:7151";
            // 2. Làm sạch dấu gạch chéo ở cuối để tránh lỗi nối chuỗi
            baseUrl = baseUrl.TrimEnd('/');

            // 3. Nối tới API quản lý từ vựng
            // Kết quả: http://Hoctiengtrung-001-site1.jtempurl.com/api/quanlytuvung
            apiBaseUrl = $"{baseUrl}/api/quanlytuvung";
        }

        /// <summary>
        /// Lấy danh sách các cấp độ (HSK 1, 2, 3...)
        /// </summary>
        public async Task<IActionResult> DanhSachCapDo([FromQuery] bool? status)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // Thay vì fix cứng, ta nối tham số status động từ view truyền xuống
            // Nếu status là null (Tất cả), URL sẽ thành: .../danhsachcapdo?status=
            // Nếu status là true, URL sẽ thành: .../danhsachcapdo?status=true
            string apiUrl = $"{apiBaseUrl}/danhsachcapdo?status={status}";

            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var listCapDo = JsonConvert.DeserializeObject<List<CapDoModels>>(jsonString);

                    // Truyền trạng thái hiện tại sang View để giữ cấu hình selected trên dropdown thanh lọc (nếu cần)
                    ViewBag.CurrentStatus = status;

                    return View(listCapDo ?? new List<CapDoModels>());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi lấy danh sách cấp độ");
            }
            return View(new List<CapDoModels>());
        }

        /// <summary>
        /// Hiển thị danh sách từ vựng có lọc và phân trang
        /// </summary>

        public async Task<IActionResult> DanhSachTuVung(int capdo = 1, string? searchTerm = null, string loaiTu = null, string trangThai = "all", int page = 1)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            foreach (var cookie in Request.Cookies)
            {
                client.DefaultRequestHeaders.Add("Cookie", $"{cookie.Key}={cookie.Value}");
            }

            // 1. Xây dựng tham số query string
            var queryParams = new Dictionary<string, string?>();
            if (!string.IsNullOrEmpty(searchTerm)) queryParams.Add("searchTerm", searchTerm);

            // --- PHẦN SỬA: Thêm loaiTu vào query gửi lên API ---
            if (!string.IsNullOrEmpty(loaiTu)) queryParams.Add("loaiTu", loaiTu);

            queryParams.Add("trangThai", trangThai);
            queryParams.Add("page", page.ToString());

            string apiUrl = QueryHelpers.AddQueryString($"{apiBaseUrl}/danhsachtuvung/{capdo}", queryParams);

            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<TuVungPagedResponse>(jsonString);

                    ViewBag.CurrentCapDo = capdo;
                    ViewBag.CurrentPage = result?.CurrentPage ?? 1;
                    ViewBag.TotalPages = result?.TotalPages ?? 0;
                    ViewBag.TotalItems = result?.TotalItems ?? 0;
                    ViewBag.SearchTerm = searchTerm;
                    ViewBag.TrangThai = trangThai;
                    ViewBag.LoaiTu = loaiTu; // Đưa ra view để giữ trạng thái chọn

                    return View(result?.Items ?? new List<TuVungModes>());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối API");
            }

            return View(new List<TuVungModes>());
        }

        public async Task<IActionResult> TuVungChuyenNganh(
             int? capdo = null, // Nhận biết người dùng có chủ động chọn hay không
             string? searchTerm = null,
             string? loaiTu = null,
             string trangThai = "all",
             int page = 1)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            

            // Khởi tạo các biến chứa dữ liệu trả về cho View
            List<CapDoModels> danhSachCapDo = new List<CapDoModels>();
            int? targetCapDo = capdo;

            try
            {
                // 1. GỌI API 1: Lấy danh sách cấp độ đang hoạt động
                // Lưu ý: Comment của bạn ghi status=true nhưng url đang để status=false, bạn tự điều chỉnh chỗ này nhé
                string apiCapDoUrl = $"{apiBaseUrl}/danhsachcapdo?status=false";

                var capDoResponse = await client.GetAsync(apiCapDoUrl);
                if (capDoResponse.IsSuccessStatusCode)
                {
                    var jsonCapDo = await capDoResponse.Content.ReadAsStringAsync();
                    danhSachCapDo = JsonConvert.DeserializeObject<List<CapDoModels>>(jsonCapDo) ?? new List<CapDoModels>();
                }

                // 2. XỬ LÝ LOGIC: Xác định targetCapDo nếu người dùng không truyền vào
                if (targetCapDo == null && danhSachCapDo.Any())
                {
                    // Lấy ID của cấp độ đầu tiên trong danh sách làm mặc định (Giả định thuộc tính là Id hoặc MaCapDo)
                    // Bạn hãy thay .Id thành tên thuộc tính định danh chính xác của CapDoModels nhé
                    targetCapDo = danhSachCapDo.First().IdCapDo;
                }

                // 3. GỌI API 2: Xây dựng URL lấy danh sách từ vựng theo targetCapDo
                TuVungPagedResponse? result = null;

                // Chỉ gọi API từ vựng nếu đã xác định được targetCapDo
                if (targetCapDo != null)
                {
                    var queryParams = new Dictionary<string, string?>();
                    if (!string.IsNullOrEmpty(searchTerm)) queryParams.Add("searchTerm", searchTerm);
                    if (!string.IsNullOrEmpty(loaiTu)) queryParams.Add("loaiTu", loaiTu);
                    queryParams.Add("trangThai", trangThai);
                    queryParams.Add("page", page.ToString());

                    string apiTuVungUrl = QueryHelpers.AddQueryString($"{apiBaseUrl}/danhsachtuvung/{targetCapDo}", queryParams);

                    // Thực hiện gọi API từ vựng
                    var tuVungResponse = await client.GetAsync(apiTuVungUrl);
                    if (tuVungResponse.IsSuccessStatusCode)
                    {
                        var jsonTuVung = await tuVungResponse.Content.ReadAsStringAsync();
                        result = JsonConvert.DeserializeObject<TuVungPagedResponse>(jsonTuVung);
                    }
                }

                // 4. Đẩy toàn bộ dữ liệu ra ViewBag phục vụ cho View hiển thị Sidebar/Dropdown điều hướng
                ViewBag.DanhSachCapDo = danhSachCapDo;
                ViewBag.CurrentCapDo = targetCapDo;
                ViewBag.CurrentPage = result?.CurrentPage ?? 1;
                ViewBag.TotalPages = result?.TotalPages ?? 0;
                ViewBag.TotalItems = result?.TotalItems ?? 0;
                ViewBag.SearchTerm = searchTerm;
                ViewBag.TrangThai = trangThai;
                ViewBag.LoaiTu = loaiTu;

                return View(result?.Items ?? new List<TuVungModes>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối hệ thống API khi lấy dữ liệu từ vựng chuyên ngành.");

                // Đảm bảo View không bị Crash khi lỗi xảy ra
                ViewBag.DanhSachCapDo = new List<CapDoModels>();
                ViewBag.CurrentCapDo = targetCapDo;
            }

            return View(new List<TuVungModes>());
        }

        /// <summary>
        /// Action xử lý khi người dùng nhấn "Đã thuộc" từ giao diện MVC
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DanhDauTuVung(int idTuVung)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // Gửi kèm Token để API biết là User nào đang đánh dấu
            var token = Request.Cookies["JWToken"];
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            string apiUrl = $"{apiBaseUrl}/danh-dau-tu-vung";

            try
            {
                // Gửi ID dưới dạng JSON body
                var content = new StringContent(JsonConvert.SerializeObject(idTuVung), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResult = await response.Content.ReadAsStringAsync();
                    return Json(JsonConvert.DeserializeObject<dynamic>(jsonResult));
                }

                return Json(new { success = false, message = "Không thể cập nhật trạng thái." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi gọi API đánh dấu từ vựng");
                return Json(new { success = false, message = "Lỗi kết nối máy chủ." });
            }
        }
        
        [HttpPost]
        public async Task<IActionResult> BatDauLamBaiHienThi(string jsonCauHoi)
        {
            if (string.IsNullOrEmpty(jsonCauHoi)) return RedirectToAction("Index");

            // Giải mã thành DeThiViewModel (có chứa TenDe và List<CauHoiModels>)
            var deThi = JsonConvert.DeserializeObject<DeThiViewModel>(jsonCauHoi);

            // Truyền trực tiếp đối tượng deThi vào View
            return View("LamBaiTapTaoAI", deThi);
        }
        
        public IActionResult LamBaiTapTaoAI()
        {
            return View();
        }

        /// <summary>
        /// API Ôn luyện ngẫu nhiên từ vựng - Giới hạn tối đa 100 từ với người dùng miễn phí
        /// </summary>
        /// <param name="idCapDo">Cấp độ (1-6), nếu 0 hoặc null là lấy tất cả</param>
        public async Task<IActionResult> OnLuyenNgauNhien(int? idCapDo = 1, int loaiOnLuyen = 3, int? soLuong = 20)
        {
            // Khởi tạo một client duy nhất dùng cho cả 2 lượt gọi API
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // Khởi tạo các biến chứa dữ liệu trả về cho View
            List<CapDoModels> danhSachCapDo = new List<CapDoModels>();

            // 1. Lấy danh sách cấp độ từ API mang về cho View chọn lọc
            try
            {
                string apiCapDoUrl = $"{apiBaseUrl}/danhsachcapdo";
                var capDoResponse = await client.GetAsync(apiCapDoUrl);
                if (capDoResponse.IsSuccessStatusCode)
                {
                    var jsonCapDo = await capDoResponse.Content.ReadAsStringAsync();
                    danhSachCapDo = JsonConvert.DeserializeObject<List<CapDoModels>>(jsonCapDo) ?? new List<CapDoModels>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách cấp độ");
            }

            // Giữ cấu hình bộ lọc ban đầu và danh sách cấp độ cho View nhận diện
            ViewBag.DanhSachCapDo = danhSachCapDo; // <--- QUAN TRỌNG: Đã đẩy danh sách cấp độ vào đây
            ViewBag.CurrentCapDo = idCapDo;
            ViewBag.LoaiOnLuyen = loaiOnLuyen;
            ViewBag.SoLuong = soLuong;
            ViewBag.RequiresPremium = false;

            // ================= CHẶN NHANH Ở TẦNG WEB CONTROLLER (BẢO VỆ 1) =================
            bool layTatCaSoLuong = !soLuong.HasValue || soLuong.Value <= 0;
            int tongSoTuYeuCau = layTatCaSoLuong ? int.MaxValue : soLuong.Value;

            bool isUserVip = await _userService.IsUserPremiumAsync(User, HttpContext);

            if (tongSoTuYeuCau > 100 && !isUserVip)
            {
                ViewBag.RequiresPremium = true;
                ViewBag.ErrorMessage = "Chức năng ôn luyện trên 100 từ hoặc toàn bộ từ vựng chỉ dành cho tài khoản VIP. Vui lòng nâng cấp tài khoản!";
                return View(new List<TuVungModes>());
            }
            // ===============================================================================

            // 2. Chuyển tiếp Cookie/Token để API xác thực User
            foreach (var cookie in Request.Cookies)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"{cookie.Key}={cookie.Value}");
            }

            var token = Request.Cookies["JWToken"];
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // 3. Xây dựng URL đồng bộ tham số (Sử dụng chính idCapDo của hàm)
            string apiUrl = $"{apiBaseUrl}/on-luyen-ngau-nhien?idCapDo={idCapDo}&loaiOnLuyen={loaiOnLuyen}";
            if (soLuong.HasValue && soLuong.Value > 0)
            {
                apiUrl += $"&soLuong={soLuong.Value}";
            }

            try
            {
                var response = await client.GetAsync(apiUrl);

                // TRƯỜNG HỢP 1: Thành công (200 OK)
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var apiResult = JsonConvert.DeserializeObject<ApiResultResponse<List<TuVungModes>>>(jsonString);

                    return View(apiResult?.Data ?? new List<TuVungModes>());
                }

                // TRƯỜNG HỢP 2: Bị chặn từ Backend (403 Forbidden - Hết lượt xáo trộn hoặc yêu cầu vượt mức VIP)
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // Đọc message từ API backend trả về để hiển thị chuẩn lên UI
                    dynamic backendResult = JsonConvert.DeserializeObject(jsonString);
                    string message = backendResult?.message ?? "Tính năng này đã vượt hạn mức miễn phí. Vui lòng nâng cấp VIP!";

                    ViewBag.RequiresPremium = true;
                    ViewBag.ErrorMessage = message;
                    return View(new List<TuVungModes>());
                }

                // TRƯỜNG HỢP 3: Các lỗi HTTP khác (500, 404, v.v.)
                _logger.LogWarning($"API ôn luyện trả về lỗi: {response.StatusCode}");
                TempData["Error"] = "Không thể lấy dữ liệu ôn luyện từ hệ thống.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi API ôn luyện ngẫu nhiên");
                TempData["Error"] = "Lỗi kết nối máy chủ.";
            }

            return View(new List<TuVungModes>());
        }
        [HttpPost]
        public async Task<IActionResult> KiemTraDapAn([FromBody] KiemTraDapAnRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.CauTraLoi))
            {
                return Json(new { success = false, message = "Vui lòng nhập câu trả lời." });
            }

            var client = _httpClientFactory.CreateClient("BypassSSL");

            // 1. Chuyển tiếp Cookie/Token để API xác thực User (giống như hàm GET)
            foreach (var cookie in Request.Cookies)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"{cookie.Key}={cookie.Value}");
            }

            var token = Request.Cookies["JWToken"];
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // 2. Xây dựng URL gọi đến API Backend POST
            string apiUrl = $"{apiBaseUrl}/kiem-tra-dap-an"; // Đảm bảo apiBaseUrl đã được khai báo

            try
            {
                // Chuyển object request thành chuỗi JSON
                var jsonContent = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

                // Gọi POST API
                var response = await client.PostAsync(apiUrl, jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    // Đọc cục JSON từ API Backend trả về
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // Trả thẳng cục JSON này về cho Frontend (AJAX/Fetch) xử lý
                    return Content(jsonString, "application/json");
                }
                else
                {
                    _logger.LogWarning($"API chấm điểm trả về lỗi: {response.StatusCode}");
                    return Json(new { success = false, message = "Lỗi từ hệ thống chấm điểm." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi API kiểm tra đáp án");
                return Json(new { success = false, message = "Lỗi kết nối máy chủ." });
            }
        }
        // Class phụ để hứng cấu trúc JSON trả về từ API
        public class ApiResultResponse<T>
        {
            public bool Success { get; set; }
            public T Data { get; set; }
            public string Message { get; set; }
        }

    }
}