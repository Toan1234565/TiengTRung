using HocTiengTrung_3._0_T.Controllers.QuanLyBaiHoc;
using HocTiengTrung_3._0_T.Models12;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace HocTiengTrung_3._0_T.Controllers.LuyenNgheHinhAnh
{
    public class LuyenNgheHinhAnh : Controller
    {
        private readonly TmdtContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LuyenNgheHinhAnh> _logger;
        // Chú ý: Đảm bảo port 7151 khớp với dự án API của bạn
        private readonly string apiBaseUrl;
        private readonly string apiBaseUrl1;
        

        // SỬA: Thêm IConfiguration vào tham số của Constructor
        public LuyenNgheHinhAnh(IHttpClientFactory httpClientFactory, ILogger<LuyenNgheHinhAnh> logger, IConfiguration configuration, TmdtContext context)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _context = context;

            
            string baseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7151";
            //string baseUrl = "https://localhost:7151";
            // 2. Đảm bảo không bị thừa dấu "/" ở cuối link gốc
            baseUrl = baseUrl.TrimEnd('/');


            // 3. Gán giá trị cho các đường dẫn API cụ thể
            // Kết quả sẽ là: http://Hoctiengtrung-001-site1.jtempurl.com/api/quanlytuvung
            apiBaseUrl = $"{baseUrl}/api/luyennghehinhanh";
           
        }
        [HttpGet]
        public async Task<IActionResult> DanhSachTieuDe(int capdo = 1, int page = 1)
        {
            // 1. Đảm bảo số trang tối thiểu luôn >= 1
            if (page < 1) page = 1;

            var client = _httpClientFactory.CreateClient("BypassSSL");
            dynamic pagedResult = null;

            // 2. Định tuyến URL API (Giả định apiBaseUrl cấu hình dạng: https://domain/api/LuyenNgheHinhAnh)
            string apiUrl = $"{apiBaseUrl}/danhsach/{capdo}?page={page}";

            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // 3. Giải mã JSON nhận từ API
                    pagedResult = JsonConvert.DeserializeObject<dynamic>(jsonString);

                    // 4. Lưu thông tin phân trang và cấp độ vào ViewBag để dựng UI
                    ViewBag.IdCapDo = capdo;
                    ViewBag.CurrentPage = (int)(pagedResult?.currentPage ?? page);
                    ViewBag.TotalPages = (int)(pagedResult?.totalPages ?? 0);
                    ViewBag.TotalItems = (int)(pagedResult?.totalItems ?? 0);

                    // Truyền pagedResult sang View (ở View chỉ cần dùng @Model.data để duyệt danh sách tiêu đề)
                    return View(pagedResult);
                }

                _logger.LogWarning($"API Lấy danh sách tiêu đề bài luyện nghe thất bại: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi kết nối khi lấy danh sách tiêu đề cho cấp độ {capdo}");
            }

            // Trường hợp lỗi hoặc API thất bại
            ViewBag.IdCapDo = capdo;
            ViewBag.CurrentPage = 1;
            ViewBag.TotalPages = 0;
            ViewBag.TotalItems = 0;

            return View(null);
        }

        [HttpGet]
        public async Task<IActionResult> ChiTietLuyenNgheHA(
            int capdo = 1,
            int? idLuyenNghe = null,
            int baiHoc = 0,
            bool? daThuoc = null,
            int page = 1)
        {
            if (page < 1) page = 1;

            int idUser = 0;
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim))
            {
                int.TryParse(userIdClaim, out idUser);
            }

            var client = _httpClientFactory.CreateClient("BypassSSL");

            // =========================================================
            // GỌI API LẤY DANH SÁCH SỐ BÀI HỌC
            // =========================================================
            List<int> dsSoBai = new List<int>();
            try
            {
                string apiSoBaiUrl = idLuyenNghe.HasValue
                    ? $"{apiBaseUrl}/so-bai-hoc/{idLuyenNghe.Value}"
                    : $"{apiBaseUrl}/so-bai-hoc/{capdo}?isCapDo=true";

                var responseBaiHoc = await client.GetAsync(apiSoBaiUrl);
                if (responseBaiHoc.IsSuccessStatusCode)
                {
                    var jsonBaiHoc = await responseBaiHoc.Content.ReadAsStringAsync();
                    var resultBaiHoc = JsonConvert.DeserializeObject<dynamic>(jsonBaiHoc);
                    if (resultBaiHoc?.success == true && resultBaiHoc?.availableLessons != null)
                    {
                        dsSoBai = resultBaiHoc.availableLessons.ToObject<List<int>>();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi API lấy danh sách số bài học.");
            }

            ViewBag.DanhSachSoBai = dsSoBai;
            // =========================================================

            dynamic pagedResult = null;

            var queryParams = new List<string>
            {
                $"idUser={idUser}",
                $"page={page}",
                $"baiHoc={baiHoc}"
            };

            if (idLuyenNghe.HasValue) queryParams.Add($"idLuyenNghe={idLuyenNghe.Value}");
            if (daThuoc.HasValue) queryParams.Add($"daThuoc={daThuoc.Value.ToString().ToLower()}");

            string queryString = string.Join("&", queryParams);
            string apiUrl = $"{apiBaseUrl}/chitiet/{capdo}?{queryString}";

            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    pagedResult = JsonConvert.DeserializeObject<dynamic>(jsonString);

                    ViewBag.IdCapDo = capdo;
                    ViewBag.IdLuyenNghe = idLuyenNghe;
                    ViewBag.BaiHoc = baiHoc;
                    ViewBag.DaThuocFilter = daThuoc;
                    ViewBag.CurrentPage = (int)(pagedResult?.currentPage ?? page);
                    ViewBag.TotalPages = (int)(pagedResult?.totalPages ?? 0);
                    ViewBag.TotalItems = (int)(pagedResult?.totalItems ?? 0);

                    return View(pagedResult);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi kết nối API lấy chi tiết cấp độ {capdo}");
            }

            ViewBag.IdCapDo = capdo;
            ViewBag.IdLuyenNghe = idLuyenNghe;
            ViewBag.BaiHoc = baiHoc;
            ViewBag.DaThuocFilter = daThuoc;
            ViewBag.CurrentPage = 1;
            ViewBag.TotalPages = 0;
            ViewBag.TotalItems = 0;

            return View(null);
        }


        public IActionResult Index()
        {
            return View();
        }
    }
}
