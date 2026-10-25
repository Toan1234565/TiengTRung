using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using HocTiengTrung_3._0_T.Models12;
using HocTiengTrung_3._0_T.Services; // Thêm namespace chứa IUserService (điều chỉnh lại nếu khác)
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HocTiengTrung_3._0_T.Controllers.PhanTichBoThu
{
    public class PhanTichCauTaoChuHan : Controller
    {
        private readonly TmdtContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PhanTichCauTaoChuHan> _logger;
        private readonly IUserService _userService;

        private readonly string apiBaseUrl;

        public PhanTichCauTaoChuHan(
            IHttpClientFactory httpClientFactory,
            ILogger<PhanTichCauTaoChuHan> logger,
            IConfiguration configuration,
            TmdtContext context,
            IUserService userService)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _context = context;
            _userService = userService;

            string baseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7151";
            apiBaseUrl = baseUrl.TrimEnd('/');
        }

        public IActionResult PhanTichChuHan()
        {
            return View();
        }

        public async Task<IActionResult> DanhSachBoThu(string? search, int page = 1, int pageSize = 30)
        {
            // 1. Đảm bảo số trang tối thiểu luôn lớn hơn hoặc bằng 1
            if (page < 1) page = 1;

            var client = _httpClientFactory.CreateClient("BypassSSL");
            dynamic pagedResult = null;

            // 2. Tạo URL kèm Query String cho API Danh sách Bộ Thủ
            var queryString = new List<string>
            {
                $"page={page}",
                $"pagesize={pageSize}"
            };

            if (!string.IsNullOrWhiteSpace(search))
                queryString.Add($"search={Uri.EscapeDataString(search.Trim())}");

            // Đường dẫn API chuẩn: https://localhost:7151/api/phan-tich-tu-vung/danh-sach?...
            string apiUrl = $"{apiBaseUrl}/api/phan-tich-tu-vung/danh-sach?{string.Join("&", queryString)}";

            try
            {
                // 3. Kiểm tra trạng thái VIP của User
                bool isUserVip = await _userService.IsUserPremiumAsync(User, HttpContext);
                ViewBag.IsUserVip = isUserVip;

                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // 4. Giải mã JSON nhận từ API
                    pagedResult = JsonConvert.DeserializeObject<dynamic>(jsonString);

                    // 5. Lưu trữ các tham số lọc vào ViewBag để giữ trạng thái trên giao diện
                    ViewBag.Search = search;
                    ViewBag.CurrentPage = page;
                    ViewBag.PageSize = pageSize;

                    return View(pagedResult);
                }

                _logger.LogWarning($"API Lấy danh sách bộ thủ thất bại với Status Code: {response.StatusCode} | URL: {apiUrl}");
                return View(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi kết nối khi lấy danh sách bộ thủ. Search: {search}, Trang: {page}");
                return View(null);
            }
        }
    }
}
