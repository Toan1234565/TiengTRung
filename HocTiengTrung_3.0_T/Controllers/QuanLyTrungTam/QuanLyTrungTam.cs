using HocTiengTrung_3._0_T.Controllers.QuanLyBaiHoc;
using HocTiengTrung_3._0_T.Models12;
using HocTiengTrung_3._0_T.Models12.QuanLyTrungTam;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Security.Claims;

namespace HocTiengTrung_3._0_T.Controllers.QuanLyTrungTam
{
    public class QuanLyTrungTam : Controller
    {
        private readonly TmdtContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyTrungTam> _logger;
        private readonly string apiBaseUrl;


        public QuanLyTrungTam(IHttpClientFactory httpClientFactory, ILogger<QuanLyTrungTam> logger, IConfiguration configuration, TmdtContext context)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _context = context;


            string baseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7151";

            baseUrl = baseUrl.TrimEnd('/');


            // 3. Gán giá trị cho các đường dẫn API cụ thể
            // Kết quả sẽ là: http://Hoctiengtrung-001-site1.jtempurl.com/api/quanlytuvung
            apiBaseUrl = $"{baseUrl}/api/quanlytrungtam";

        }

        public async Task<IActionResult> DanhSachTrungTam(string? search, bool? trangThai, int page = 1, int pageSize = 10)
        {
            // =========================================================================
            // 1. KIỂM TRA QUYỀN TRỰC TIẾP TẠI TẦNG MVC (TRÁNH PHẢI GỌI API NẾU SAI QUYỀN)
            // =========================================================================
            //var tenVaiTro = (User.FindFirst("TenVaiTro")?.Value
            //                 ?? HttpContext.Session?.GetString("TenVaiTro")
            //                 ?? "").Trim();

            //var roleClaims = User.FindAll(ClaimTypes.Role).Select(c => c.Value.Trim()).ToList();

            //bool isAdmin = tenVaiTro.Equals("Quản trị viên", StringComparison.OrdinalIgnoreCase)
            //               || tenVaiTro.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            //               || roleClaims.Any(r => r.Equals("Quản trị viên", StringComparison.OrdinalIgnoreCase)
            //                                   || r.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            //                                   || r == "1");

            //if (!isAdmin)
            //{
            //    TempData["AccessDenied"] = "true";
            //    TempData["ErrorMessage"] = "Bạn không có quyền truy cập vào trang Quản lý Trung tâm!";
            //    return View(null); // Trả về view để Script kích hoạt SweetAlert rồi chuyển về trang chủ
            //}

            if (page < 1) page = 1;

            var client = _httpClientFactory.CreateClient("BypassSSL");

            // 2. Chuyển tiếp Cookie xác thực từ trình duyệt sang API
            if (Request.Headers.TryGetValue("Cookie", out var cookieValue))
            {
                client.DefaultRequestHeaders.Add("Cookie", cookieValue.ToString());
            }

            dynamic pagedResult = null;
            string apiUrl = $"{apiBaseUrl}/getalltrungtam?page={page}&pageSize={pageSize}";

            if (!string.IsNullOrWhiteSpace(search))
            {
                apiUrl += $"&search={Uri.EscapeDataString(search.Trim())}";
            }

            if (trangThai.HasValue)
            {
                apiUrl += $"&trangThai={trangThai.Value}";
            }

            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    pagedResult = JsonConvert.DeserializeObject<dynamic>(jsonString);

                    ViewBag.Search = search;
                    ViewBag.TrangThai = trangThai;
                    ViewBag.CurrentPage = page;
                    ViewBag.PageSize = pageSize;

                    return View(pagedResult);
                }

                // Nếu API trả về 403 Forbidden
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    TempData["AccessDenied"] = "true";
                    TempData["ErrorMessage"] = "Bạn không có quyền quản trị viên để xem danh sách trung tâm.";
                    return View(null);
                }

                _logger.LogWarning($"API Danh sách trung tâm thất bại với Status Code: {response.StatusCode} | URL: {apiUrl}");
                TempData["ErrorMessage"] = "Không thể tải danh sách trung tâm từ hệ thống.";
                return View(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi kết nối khi lấy danh sách trung tâm, Page: {page}");
                TempData["ErrorMessage"] = "Lỗi kết nối đến máy chủ API.";
                return View(null);
            }
        }
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ChiTietTrungTam(string? id)
        {
            // =========================================================================
            // 1. TRÍCH XUẤT CLAIMS & SESSION
            // =========================================================================
            var tenVaiTro = (User.FindFirst("TenVaiTro")?.Value
                             ?? HttpContext.Session?.GetString("TenVaiTro")
                             ?? "").Trim();
            var idVaiTro = (User.FindFirst("IdVaiTro")?.Value
                            ?? HttpContext.Session?.GetString("IdVaiTro")
                            ?? "").Trim();
            var roleClaims = User.FindAll(ClaimTypes.Role).Select(c => c.Value.Trim()).ToList();

            string myIdTrungTam = (User.FindFirst("IdTrungTam")?.Value
                                   ?? HttpContext.Session?.GetString("IdTrungTam")
                                   ?? "").Trim();
            string myMaTrungTam = (User.FindFirst("MaTrungTam")?.Value
                                   ?? HttpContext.Session?.GetString("MaTrungTam")
                                   ?? "").Trim();

            bool isAdmin = tenVaiTro.Equals("Quản trị viên", StringComparison.OrdinalIgnoreCase)
                           || tenVaiTro.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                           || roleClaims.Any(r => r.Equals("Quản trị viên", StringComparison.OrdinalIgnoreCase)
                                               || r.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                                               || r == "1");

            bool isQuanLy = tenVaiTro.Equals("Quản lý trung tâm", StringComparison.OrdinalIgnoreCase)
                            || idVaiTro == "1002"
                            || roleClaims.Any(r => r.Equals("Quản lý trung tâm", StringComparison.OrdinalIgnoreCase) || r == "1002");

            if (!isAdmin && !isQuanLy)
            {
                TempData["AccessDenied"] = "true";
                TempData["ErrorMessage"] = "Bạn không có quyền truy cập vào trang Quản lý Trung tâm!";
                return RedirectToAction("Index", "Home");
            }

            // =========================================================================
            // 2. XÁC ĐỊNH & BẢO VỆ MULTI-TENANT CƠ SỞ CẦN XEM
            // =========================================================================
            string targetId = (id ?? "").Trim();

            if (string.IsNullOrWhiteSpace(targetId) || targetId == "0")
            {
                targetId = !string.IsNullOrEmpty(myIdTrungTam) && myIdTrungTam != "0" ? myIdTrungTam : myMaTrungTam;
            }

            // Nếu là Quản lý: chỉ được xem trung tâm của chính mình, tự ép về ID của họ
            if (!isAdmin && isQuanLy)
            {
                bool isMyCenter = targetId.Equals(myIdTrungTam, StringComparison.OrdinalIgnoreCase)
                                  || (!string.IsNullOrEmpty(myMaTrungTam) && targetId.Equals(myMaTrungTam, StringComparison.OrdinalIgnoreCase));

                if (!isMyCenter)
                {
                    targetId = !string.IsNullOrEmpty(myIdTrungTam) && myIdTrungTam != "0" ? myIdTrungTam : myMaTrungTam;
                }
            }

            if (string.IsNullOrWhiteSpace(targetId) || targetId == "0")
            {
                TempData["ErrorMessage"] = "Không xác định được cơ sở đào tạo của tài khoản.";
                return RedirectToAction("Index", "Home");
            }

            // =========================================================================
            // 3. GỌI API CHUYỂN TIẾP COOKIE VÀ AUTHORIZATION TOKEN
            // =========================================================================
            var client = _httpClientFactory.CreateClient("BypassSSL");
            string apiUrl = $"{apiBaseUrl}/chitiettrungtam/{Uri.EscapeDataString(targetId)}";

            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, apiUrl);

            if (Request.Headers.TryGetValue("Cookie", out var cookieValue))
            {
                requestMessage.Headers.TryAddWithoutValidation("Cookie", cookieValue.ToString());
            }

            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                requestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }

            IActionResult RedirectOnError(string msg)
            {
                TempData["ErrorMessage"] = msg;
                if (isAdmin)
                {
                    return RedirectToAction(nameof(DanhSachTrungTam));
                }
                return RedirectToAction("Index", "Home");
            }

            try
            {
                var response = await client.SendAsync(requestMessage);

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var errorData = JsonConvert.DeserializeObject<dynamic>(jsonString);

                    ViewBag.IsExpired = true;
                    ViewBag.Message = errorData?.message?.ToString() ?? "Cơ sở này đã hết hạn dịch vụ hoặc bạn không có quyền xem.";
                    return View(null);
                }

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    ChiTietTrungTamViewModel? chiTiet = null;

                    try
                    {
                        var jObj = Newtonsoft.Json.Linq.JObject.Parse(jsonString);
                        chiTiet = jObj["data"] != null
                            ? jObj["data"]!.ToObject<ChiTietTrungTamViewModel>()
                            : jObj.ToObject<ChiTietTrungTamViewModel>();
                    }
                    catch
                    {
                        chiTiet = JsonConvert.DeserializeObject<ChiTietTrungTamViewModel>(jsonString);
                    }

                    if (chiTiet == null)
                    {
                        return RedirectOnError("Dữ liệu trung tâm trả về không hợp lệ.");
                    }

                    return View(chiTiet);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return RedirectOnError("Không tìm thấy thông tin trung tâm hoặc trung tâm đã bị vô hiệu hóa.");
                }

                _logger.LogWarning("API Chi tiết trung tâm [{TargetId}] thất bại: {StatusCode} | URL: {Url}", targetId, response.StatusCode, apiUrl);
                return RedirectOnError($"Không thể tải thông tin trung tâm (Mã lỗi: {response.StatusCode}).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi lấy chi tiết trung tâm: {TargetId}", targetId);
                return RedirectOnError("Lỗi kết nối đến dịch vụ.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DanhSachNhanSu(
            int? idTrungTam,
            string? tuKhoa,
            int? idVaiTro,
            bool? trangThai)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // 1. Chuyển tiếp Cookie & Token xác thực sang API (Bắt buộc do API có [Authorize])
            if (Request.Headers.ContainsKey("Cookie"))
            {
                client.DefaultRequestHeaders.Add("Cookie", Request.Headers["Cookie"].ToString());
            }

            if (Request.Headers.ContainsKey("Authorization"))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", Request.Headers["Authorization"].ToString());
            }

            // 2. Xác định IdTrungTam (Ưu tiên param -> Session -> Claims)
            int targetIdTrungTam = idTrungTam ?? 0;
            if (targetIdTrungTam <= 0)
            {
                string? idTrungTamStr = User.FindFirst("IdTrungTam")?.Value
                                        ?? HttpContext.Session.GetString("IdTrungTam");
                int.TryParse(idTrungTamStr, out targetIdTrungTam);
            }

            // 3. Khởi tạo Query String gọi sang API
            var queryParams = new List<string>();

            if (targetIdTrungTam > 0)
            {
                queryParams.Add($"idTrungTam={targetIdTrungTam}");
            }

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                queryParams.Add($"tuKhoa={Uri.EscapeDataString(tuKhoa.Trim())}");
            }

            if (idVaiTro.HasValue && idVaiTro.Value > 0)
            {
                queryParams.Add($"idVaiTro={idVaiTro.Value}");
            }

            if (trangThai.HasValue)
            {
                queryParams.Add($"trangThai={trangThai.Value}");
            }

            string queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
            string apiUrl = $"{apiBaseUrl}/danh-sach-nhan-su{queryString}";

            try
            {
                var response = await client.GetAsync(apiUrl);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    TempData["ErrorMessage"] = "Bạn không có quyền truy cập danh sách nhân sự của cơ sở này.";
                    return RedirectToAction("Index", "Home");
                }

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var resultObj = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(jsonString);

                    // Bóc tách mảng "data" thành List<NhanSuTrungTamModel>
                    var listNhanSu = resultObj?["data"]?.ToObject<List<NhanSuTrungTamModel>>()
                                     ?? new List<NhanSuTrungTamModel>();

                    // Lưu bộ lọc vào ViewBag để giao diện giữ trạng thái tìm kiếm
                    ViewBag.IdTrungTam = targetIdTrungTam;
                    ViewBag.TuKhoa = tuKhoa;
                    ViewBag.IdVaiTro = idVaiTro;
                    ViewBag.TrangThai = trangThai;
                    ViewBag.TongSoLuong = (int?)resultObj?["tongSoLuong"] ?? listNhanSu.Count;

                    return View(listNhanSu);
                }

                _logger.LogWarning($"API Lấy danh sách nhân sự thất bại: {response.StatusCode} | URL: {apiUrl}");
                ViewBag.IdTrungTam = targetIdTrungTam;
                ViewBag.TongSoLuong = 0;
                TempData["ErrorMessage"] = "Không thể tải danh sách nhân sự từ máy chủ.";
                return View(new List<NhanSuTrungTamModel>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi API lấy danh sách nhân sự trung tâm ID: {IdTrungTam}", targetIdTrungTam);
                ViewBag.IdTrungTam = targetIdTrungTam;
                ViewBag.TongSoLuong = 0;
                TempData["ErrorMessage"] = "Lỗi kết nối máy chủ dịch vụ nhân sự.";
                return View(new List<NhanSuTrungTamModel>());
            }
        }

        [HttpGet]
        [Authorize] // Bắt buộc đăng nhập
        public async Task<IActionResult> ChiTietNhanSu(int id)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] = "Mã nhân sự không hợp lệ.";
                return RedirectToAction(nameof(DanhSachNhanSu));
            }

            // =========================================================================
            // 1. KIỂM TRA PHÂN QUYỀN SƠ BỘ TẠI MVC (GIẢNG VIÊN HOẶC QUẢN LÝ TRUNG TÂM)
            // =========================================================================
            var tenVaiTro = (User.FindFirst("TenVaiTro")?.Value
                             ?? HttpContext.Session?.GetString("TenVaiTro")
                             ?? "").Trim();
            var idVaiTroStr = (User.FindFirst("IdVaiTro")?.Value
                               ?? HttpContext.Session?.GetString("IdVaiTro")
                               ?? "").Trim();
            var roleClaims = User.FindAll(ClaimTypes.Role).Select(c => c.Value.Trim()).ToList();

            string? currentIdTrungTamStr = User.FindFirst("IdTrungTam")?.Value
                                           ?? HttpContext.Session?.GetString("IdTrungTam");
            int.TryParse(currentIdTrungTamStr, out int currentIdTrungTam);

            string roleClean = tenVaiTro.ToLower();

            // 1. Nhận diện Quản lý trung tâm
            bool isQuanLy = roleClean.Contains("quản lý")
                            || roleClean.Contains("quan ly")
                            || roleClean.Contains("manager")
                            || roleClaims.Any(r => r.Equals("Quản lý trung tâm", StringComparison.OrdinalIgnoreCase)
                                                || r.Equals("Quan ly trung tam", StringComparison.OrdinalIgnoreCase));

            // 2. Nhận diện Giảng viên (Khớp theo tên Claim hoặc IdVaiTro = 1002)
            bool isGiangVien = roleClean.Contains("giảng viên")
                               || roleClean.Contains("giang vien")
                               || roleClean.Contains("teacher")
                               || idVaiTroStr == "1002"
                               || roleClaims.Any(r => r.Equals("Giảng viên", StringComparison.OrdinalIgnoreCase)
                                                   || r.Equals("Giang vien", StringComparison.OrdinalIgnoreCase)
                                                   || r == "1002");

            // Chỉ Giảng viên hoặc Quản lý trung tâm mới được phép xem
            if (!isQuanLy && !isGiangVien)
            {
                TempData["ErrorMessage"] = "Chỉ Giảng viên và Quản lý trung tâm mới có quyền truy cập hồ sơ này.";
                return RedirectToAction(nameof(DanhSachNhanSu));
            }

            // Không xác định được trung tâm của tài khoản
            if (currentIdTrungTam <= 0)
            {
                TempData["ErrorMessage"] = "Không xác định được cơ sở đào tạo của tài khoản bạn.";
                return RedirectToAction(nameof(DanhSachNhanSu));
            }

            // =========================================================================
            // 2. GỬI REQUEST CHUYỂN TIẾP COOKIE VÀ TOKEN SANG API
            // =========================================================================
            var client = _httpClientFactory.CreateClient("BypassSSL");
            string apiUrl = $"{apiBaseUrl}/chitiet-giangvien/{id}";

            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, apiUrl);

            if (Request.Headers.TryGetValue("Cookie", out var cookieValue))
            {
                requestMessage.Headers.TryAddWithoutValidation("Cookie", cookieValue.ToString());
            }

            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                requestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }

            try
            {
                var response = await client.SendAsync(requestMessage);

                // Xử lý khi bị từ chối quyền (401 Unauthorized / 403 Forbidden)
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    var errObj = JsonConvert.DeserializeObject<dynamic>(errorBody);
                    string msg = errObj?.message?.ToString() ?? "Bạn không có quyền xem thông tin nhân sự thuộc cơ sở khác.";

                    TempData["ErrorMessage"] = msg;
                    return View();
                }

                // Xử lý khi không tìm thấy tài khoản (404 Not Found)
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy hồ sơ nhân sự trong hệ thống.";
                    return View();
                }

                // Đọc dữ liệu thành công
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var resultObj = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(jsonString);

                    // Bóc tách object "data" thành ChiTietGiangVienDto
                    var chiTiet = resultObj?["data"]?.ToObject<ChiTietGiangVienDto>();

                    if (chiTiet == null)
                    {
                        TempData["ErrorMessage"] = "Không thể giải mã dữ liệu hồ sơ nhân sự.";
                        return View();
                    }

                    return View(chiTiet);
                }

                _logger.LogWarning("API ChiTietNhanSu #{Id} trả về status: {StatusCode} | URL: {Url}", id, response.StatusCode, apiUrl);
                TempData["ErrorMessage"] = $"Không thể tải thông tin chi tiết nhân sự (Mã lỗi: {response.StatusCode}).";
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi API xem chi tiết nhân sự ID: {Id}", id);
                TempData["ErrorMessage"] = "Lỗi kết nối tới hệ thống máy chủ dịch vụ: " + ex.Message;
                return View();
            }
        }
    }
}
