using HocTiengTrung_3._0_T.Models12;
using HocTiengTrung_3._0_T.Models12.QuanLyTaiLieu;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace HocTiengTrung_3._0_T.Controllers.QuanLyTaiLieu
{
    public class QuanLyTaiLieu1 : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyTaiLieu1> _logger;

        // SỬA: Loại bỏ gán cứng localhost
        private readonly string apiBaseUrl;

        private readonly IUserService _userService;
        // SỬA: Thêm IConfiguration vào Constructor
        public QuanLyTaiLieu1(IHttpClientFactory httpClientFactory, ILogger<QuanLyTaiLieu1> logger, IConfiguration configuration, IUserService userService)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _userService = userService;

            // 1. Lấy link gốc từ appsettings.json (http://Hoctiengtrung-001-site1.jtempurl.com)
            string baseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7151";
            //string baseUrl = "https://localhost:7151";
            baseUrl = baseUrl.TrimEnd('/');

            // 2. Gán link cho API Từ vựng (để lấy Cấp độ)
            apiBaseUrl = $"{baseUrl}/api/quanlytailieu";


        }

        public async Task<IActionResult> DanhSachTaiLieu()
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // Lấy tài liệu đang có trạng thái true (được hiển thị)
            // Bạn có thể truyền thêm page và pageSize nếu muốn, ví dụ: ?trangThai=true&page=1&pageSize=20
            string apiUrl = $"{apiBaseUrl}/getalltailieu?trangThai=true";

            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // Deserialize về dạng PagedApiResponse bọc lấy TaiLieuModels thay vì List<CapDoModels>
                    var apiResult = JsonConvert.DeserializeObject<PagedApiResponse<TaiLieuModels>>(jsonString);

                    // Lấy danh sách tài liệu từ thuộc tính Data
                    var listTaiLieu = apiResult?.Data ?? new List<TaiLieuModels>();

                    return View(listTaiLieu);
                }

                // Nếu API lỗi (khác 200 OK) thì trả về danh sách rỗng
                return View(new List<TaiLieuModels>());
            }
            catch (Exception ex)
            {
                // Sửa lại log lỗi cho đúng ngữ cảnh
                _logger.LogError(ex, "Lỗi kết nối khi lấy danh sách tài liệu");
                return View(new List<TaiLieuModels>());
            }
        }
        public class PagedApiResponse<T>
        {
            public int TotalItems { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
            public int TotalPages { get; set; }
            public List<T> Data { get; set; } = new List<T>();
        }
    }
}
