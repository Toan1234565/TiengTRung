using HocTiengTrung_3._0_T.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.Controllers.QuanLyVaiTro
{
    public class QuanLyVaiTro : Controller
    {
        private readonly TmdtContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyVaiTro> _logger;
        private readonly string apiBaseUrl;

        public QuanLyVaiTro(
            IHttpClientFactory httpClientFactory,
            ILogger<QuanLyVaiTro> logger,
            IConfiguration configuration,
            TmdtContext context)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _context = context;

            string baseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7151";
            baseUrl = baseUrl.TrimEnd('/');
            apiBaseUrl = $"{baseUrl}/api/quanlyvaitro";
        }

        // ==========================================
        // DANH SÁCH VAI TRÒ
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> DanhSachVaiTro()
        {
            // =========================================================================
            // 1. KIỂM TRA QUYỀN TRỰC TIẾP TẠI TẦNG MVC (TRÁNH PHẢI GỌI API NẾU SAI QUYỀN)
            // =========================================================================
            var tenVaiTro = (User.FindFirst("TenVaiTro")?.Value
                             ?? HttpContext.Session?.GetString("TenVaiTro")
                             ?? "").Trim();

            var roleClaims = User.FindAll(ClaimTypes.Role).Select(c => c.Value.Trim()).ToList();

            bool isAdmin = tenVaiTro.Equals("Quản trị viên", StringComparison.OrdinalIgnoreCase)
                           || tenVaiTro.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                           || roleClaims.Any(r => r.Equals("Quản trị viên", StringComparison.OrdinalIgnoreCase)
                                               || r.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                                               || r == "1");

            if (!isAdmin)
            {
                TempData["AccessDenied"] = "true";
                TempData["ErrorMessage"] = "Bạn không có quyền truy cập vào trang Quản lý Trung tâm!";
                return View(null); // Trả về view để Script kích hoạt SweetAlert rồi chuyển về trang chủ
            }

            var client = _httpClientFactory.CreateClient("BypassSSL");

            // 2. Chuyển tiếp Cookie xác thực từ trình duyệt sang API
            if (Request.Headers.TryGetValue("Cookie", out var cookieValue))
            {
                client.DefaultRequestHeaders.Add("Cookie", cookieValue.ToString());
            }
          
            string apiUrl = $"{apiBaseUrl}/getallvaitro";

            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<dynamic>(jsonString);

                    // Lấy mảng data từ JSON { success: true, data: [...] }
                    var dataList = result?.data;
                    return View(dataList);
                }

                _logger.LogWarning($"API Lấy danh sách vai trò trả về mã lỗi: {response.StatusCode}");
                TempData["ErrorMessage"] = "Không thể tải danh sách vai trò từ máy chủ API.";
                return View(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi API danh sách vai trò.");
                TempData["ErrorMessage"] = "Lỗi kết nối tới hệ thống máy chủ.";
                return View(null);
            }
        }
    }
}