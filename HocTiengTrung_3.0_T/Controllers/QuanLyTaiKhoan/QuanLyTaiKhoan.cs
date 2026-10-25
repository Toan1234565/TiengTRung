using HocTiengTrung_3._0_T.Models12.QuanLyTaiKhoan;
using HocTiengTrung_3._0_T.Models12.ThanhTich; // Chứa NguoiDungModels
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Security.Claims;
using System.Text.Json;

namespace HocTiengTrung_3._0_T.Controllers.QuanLyTaiKhoan
{
    public class QuanLyTaiKhoan : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyTaiKhoan> _logger;
        // SỬA: Bỏ gán cứng localhost
        private readonly string apiBaseUrl;

        // SỬA: Thêm IConfiguration vào Constructor
        public QuanLyTaiKhoan(IHttpClientFactory httpClientFactory, ILogger<QuanLyTaiKhoan> logger, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            // 1. Lấy tên miền từ appsettings.json
            string baseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7151";
            //string baseUrl = "https://localhost:7151";
            // 2. Làm sạch dấu gạch chéo
            baseUrl = baseUrl.TrimEnd('/');

            // 3. Nối tới API quản lý tài khoản người dùng
            // Kết quả: http://Hoctiengtrung-001-site1.jtempurl.com/api/quanlytaikhoannguoidung
            apiBaseUrl = $"{baseUrl}/api/quanlytaikhoannguoidung";
        }

        #region ĐĂNG KÝ
        [HttpGet]
        public IActionResult DangKy() => View();

        [HttpPost]
        public async Task<IActionResult> DangKy(RegisterRequest model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                var client = _httpClientFactory.CreateClient();

                // 1. Chuyển tiếp Cookie/Session hiện tại sang HttpClient để API nhận diện được 
                // User đang đăng nhập hoặc Session hiện tại (chứa IdTrungTam của trung tâm đang truy cập)
                if (Request.Headers.TryGetValue("Cookie", out var cookieHeader))
                {
                    client.DefaultRequestHeaders.Add("Cookie", cookieHeader.ToString());
                }

                // 2. Gửi request đến API tạo tài khoản
                var response = await client.PostAsJsonAsync($"{apiBaseUrl}/tao-tai-khoan", model);

                if (response.IsSuccessStatusCode)
                {
                    // Đọc thông báo thành công trả về từ API (ví dụ: kèm thông báo tặng 10 ngày VIP)
                    var successData = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
                    string successMessage = successData != null && successData.ContainsKey("message")
                        ? successData["message"].ToString()
                        : "Đăng ký thành công! Vui lòng đăng nhập.";

                    TempData["Success"] = successMessage;
                    return RedirectToAction("DangNhap");
                }

                // 3. Xử lý khi API trả về lỗi (Conflict, BadRequest, v.v.)
                var errorResult = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
                ViewBag.Error = errorResult != null && errorResult.ContainsKey("message")
                    ? errorResult["message"].ToString()
                    : "Đăng ký thất bại.";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi DangKy: {ex.Message}");
                ViewBag.Error = "Lỗi kết nối máy chủ.";
            }

            return View(model);
        }
        #endregion

        #region ĐĂNG NHẬP
        [HttpGet]
        public IActionResult DangNhap(string returnUrl = null)
        {
            // Đẩy returnUrl vào ViewBag để file View (.cshtml) có thể bắt lấy
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> DangNhap(string tenDangNhap, string matKhau, string returnUrl = null)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var loginData = new { TenDangNhap = tenDangNhap, MatKhau = matKhau };

                var response = await client.PostAsJsonAsync($"{apiBaseUrl}/dang-nhap-tai-khoan", loginData);
                // BẮT TRƯỜNG HỢP TÀI KHOẢN BỊ KHÓA (HTTP 403 Forbidden từ API)
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var errorResult = await response.Content.ReadFromJsonAsync<LoginApiResponse>();
                    // Truyền cờ hiệu và thông điệp tài khoản bị khóa sang ViewBag
                    ViewBag.IsLocked = true;
                    ViewBag.Error = errorResult?.Message ?? "Tài khoản của bạn hiện đang bị khóa.";
                    ViewBag.ReturnUrl = returnUrl;
                    return View();
                }
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginApiResponse>();

                    if (result != null && result.Success)
                    {
                        string hoTen = result.Data.HoTen ?? tenDangNhap;
                        string userId = result.Data.IdUser.ToString();
                        string idTaiKhoan = result.Data.IdTaiKhoan.ToString();
                        string idTrungTam = result.Data.IdTrungTam?.ToString() ?? "0";
                        string maTrungTam = (result.Data.MaTrungTam ?? string.Empty).Trim();

                        // Chuẩn hóa chuỗi vai trò (cắt sạch khoảng trắng ở đầu và cuối)
                        string tenVaiTro = (result.Data.TenVaiTro?.ToString() ?? "Người dùng").Trim();
                        string idVaiTro = (result.Data.IdVaiTro?.ToString() ?? "1").Trim();

                        // 1. KHỞI TẠO DANH SÁCH CLAIMS
                        var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, hoTen),
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim("TenDangNhap", result.Data.TenDangNhap ?? tenDangNhap),
                    new Claim("IdTaiKhoan", idTaiKhoan),
                    new Claim("IdTrungTam", idTrungTam),
                    new Claim("MaTrungTam", maTrungTam),

                    // QUAN TRỌNG NHẤT: Bổ sung ClaimTypes.Role để [Authorize(Roles = "...")] hoạt động
                    new Claim(ClaimTypes.Role, tenVaiTro), // Nhận "Quản trị viên"
                    new Claim(ClaimTypes.Role, idVaiTro),   // Nhận "1" (ID của Quản trị viên)

                    // Lưu các claim phụ phục vụ hiển thị trên giao diện
                    new Claim("TenVaiTro", tenVaiTro),
                    new Claim("IdVaiTro", idVaiTro)
                };

                        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                        // 2. GHI NHẬN COOKIE XÁC THỰC
                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity),
                            new AuthenticationProperties
                            {
                                IsPersistent = true,
                                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
                            });

                        // 3. THIẾT LẬP SESSION ĐỒNG BỘ
                        HttpContext.Session.SetString("TenDangNhap", result.Data.TenDangNhap ?? tenDangNhap);
                        HttpContext.Session.SetString("HoTenNhanVien", hoTen);
                        HttpContext.Session.SetString("MaNguoiDung", userId);
                        HttpContext.Session.SetString("IdTaiKhoan", idTaiKhoan);
                        HttpContext.Session.SetString("IdTrungTam", idTrungTam);
                        HttpContext.Session.SetString("MaTrungTam", maTrungTam);
                        HttpContext.Session.SetString("IdVaiTro", idVaiTro);
                        HttpContext.Session.SetString("TenVaiTro", tenVaiTro);

                        // 4. KIỂM TRA VÀ ĐIỀU HƯỚNG VỀ TRANG CŨ
                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }

                        // Nếu không có trang cũ thì về trang chủ
                        return RedirectToAction("Index", "Home");
                    }
                }

                ViewBag.Error = "Tên đăng nhập hoặc mật khẩu không chính xác.";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi kết nối API DangNhap: {ex.Message}");
                ViewBag.Error = "Hệ thống đang bảo trì, vui lòng thử lại sau.";
            }

            // Nếu đăng nhập thất bại, giữ lại returnUrl trong ViewBag
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }
        #endregion
        // --- Thêm các class này vào cuối file hoặc thư mục Models để hứng dữ liệu API ---
        public class LoginApiResponse
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public LoginDataResponse? Data { get; set; }
        }

        public class LoginDataResponse
        {
            public int IdUser { get; set; }
            public string? HoTen { get; set; }
            public string? TenDangNhap { get; set; }
            public int? IdVaiTro { get; set; }
            public int IdTaiKhoan { get; set; }

            public int? IdTrungTam { get; set; }

            public string? MaTrungTam { get; set; } = null;

            public string? TenVaiTro { get; set; } = null;
        }

        #region HỒ SƠ NGƯỜI DÙNG (CẦN COOKIE)
        [HttpGet]
        public async Task<IActionResult> HoSoNguoiDung()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Get, $"{apiBaseUrl}/thong-tin-ho-so");

                // QUAN TRỌNG: Chuyển tiếp Cookie từ trình duyệt sang API để API nhận diện User
                var cookies = Request.Headers["Cookie"].ToString();
                if (!string.IsNullOrEmpty(cookies))
                {
                    request.Headers.Add("Cookie", cookies);
                }

                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var profile = await response.Content.ReadFromJsonAsync<NguoiDungModels>();
                    return View(profile);
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return RedirectToAction("DangNhap");
                }

                ViewBag.Error = "Không thể tải thông tin hồ sơ.";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi HoSo: {ex.Message}");
                ViewBag.Error = "Lỗi hệ thống.";
            }
            return View();
        }
        #endregion

        #region ĐĂNG XUẤT
        public async Task<IActionResult> DangXuat()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                await client.PostAsync($"{apiBaseUrl}/dang-xuat", null);
            }
            catch { /* Ignore error */ }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("DangNhap");
        }
        #endregion

        #region THỐNG KÊ TRUY CẬP
        [HttpGet]
        public async Task<IActionResult> ThongKeTruyCap(DateTime? tuNgay, DateTime? denNgay)
        {
            // 1. Thiết lập mặc định nếu không truyền ngày (ví dụ: 7 ngày qua)
            var start = tuNgay ?? DateTime.Now.AddDays(-7);
            var end = denNgay ?? DateTime.Now;

            ViewBag.TuNgay = start.ToString("yyyy-MM-dd");
            ViewBag.DenNgay = end.ToString("yyyy-MM-dd");

            try
            {
                var client = _httpClientFactory.CreateClient();

                // 2. Xây dựng URL với tham số query string
                string url = $"{apiBaseUrl}/thong-ke-truy-cap?tuNgay={start:yyyy-MM-dd}&denNgay={end:yyyy-MM-dd}";

                // 3. Gọi API
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<AccessStatsResponse>();
                    if (result != null && result.Success)
                    {
                        return View(result.Data); // Trả về danh sách dữ liệu cho View
                    }
                }

                ViewBag.Error = "Không thể lấy dữ liệu thống kê từ máy chủ.";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi ThongKeTruyCap: {ex.Message}");
                ViewBag.Error = "Lỗi kết nối hệ thống.";
            }

            return View(new List<AccessStatDetail>());
        }
        #endregion

        public async Task<IActionResult> DanhSach([FromQuery] bool? isVip, [FromQuery] string? searchTerm, [FromQuery] int page = 1)
        {
            // 1. Khởi tạo client và xây dựng query string
            var client = _httpClientFactory.CreateClient();
            var queryParams = new List<string> { $"page={(page <= 0 ? 1 : page)}" };

            if (isVip.HasValue) queryParams.Add($"isVip={isVip.Value}");
            if (!string.IsNullOrWhiteSpace(searchTerm))
                queryParams.Add($"searchTerm={Uri.EscapeDataString(searchTerm.Trim())}");

            var url = $"{apiBaseUrl}/danh-sach-tai-khoan?{string.Join("&", queryParams)}";

            try
            {
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var result = await response.Content.ReadFromJsonAsync<DanhSachTaiKhoanResponse>(options);

                    // Gán dữ liệu vào ViewBag để view sử dụng cho phân trang
                    ViewBag.IsVip = isVip;
                    ViewBag.SearchTerm = searchTerm;
                    ViewBag.CurrentPage = result?.CurrentPage ?? 1;
                    ViewBag.TotalPages = result?.TotalPages ?? 1;
                    ViewBag.TotalItems = result?.TotalItems ?? 0;

                    return View(result?.Items ?? new List<NguoiDungModels>());
                }

                // Log lỗi nếu cần thiết tại đây: _logger.LogError("API trả về lỗi: {Code}", response.StatusCode);
            }
            catch (Exception ex)
            {
                // Xử lý khi API không phản hồi (ví dụ: Service downtime)
                _logger.LogError(ex, "Lỗi kết nối tới API danh sách tài khoản");
            }

            // Giá trị mặc định khi có lỗi hoặc không có dữ liệu
            ViewBag.TotalItems = 0;
            return View(new List<NguoiDungModels>());
        }
        // Chi tiết người dùng kèm giao dịch
        public async Task<IActionResult> ChiTiet(int id)
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"{apiBaseUrl}/chi-tiet-tai-khoan/{id}");

            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<NguoiDungModels>();
                return View(user);
            }
            return NotFound();
        }

        [HttpGet]
        public IActionResult CongThanhToan() => View();

        [HttpGet]
        public IActionResult ThanhToanThanhCong() => View();


        public class DanhSachTaiKhoanResponse
        {
            // Đảm bảo tên thuộc tính khớp với key trong JSON (ví dụ: TotalItems)
            public int TotalItems { get; set; }
            public int TotalPages { get; set; }
            public int CurrentPage { get; set; }
            public int PageSize { get; set; }
            public List<NguoiDungModels> Items { get; set; }
        }
    }
}