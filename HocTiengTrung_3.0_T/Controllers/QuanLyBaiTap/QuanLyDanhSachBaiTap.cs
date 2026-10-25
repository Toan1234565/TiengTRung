using HocTiengTrung_3._0_T.Controllers.QuanLyNguPhap;
using HocTiengTrung_3._0_T.Models12;
using HocTiengTrung_3._0_T.Models12.BaiTap;
using HocTiengTrung_3._0_T.Models12.LuuLichSu;
using HocTiengTrung_3._0_T.Models12.TuVung;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Security.Claims;
using static HocTiengTrung_3._0_T.Controllers.QuanLyBaiTap.QuanLyDanhSachBaiTap;

namespace HocTiengTrung_3._0_T.Controllers.QuanLyBaiTap
{
    public class QuanLyDanhSachBaiTap : Controller
    {
        private readonly TmdtContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyDanhSachBaiTap> _logger;
        // Chú ý: Đảm bảo port 7151 khớp với dự án API của bạn
        private readonly string apiBaseUrl;
        private readonly string apiBaseUrl1;
        private readonly IUserService _userService;
        // SỬA: Thêm IConfiguration vào tham số của Constructor
        public QuanLyDanhSachBaiTap(IHttpClientFactory httpClientFactory, ILogger<QuanLyDanhSachBaiTap> logger, IConfiguration configuration, TmdtContext context, IUserService userService)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _context = context;
            _userService = userService;

            // 1. Lấy link gốc từ appsettings.json (http://Hoctiengtrung-001-site1.jtempurl.com)
            // Nếu không tìm thấy trong file cấu hình, mặc định sẽ lấy localhost để chạy ở máy
            string baseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7151";
            //string baseUrl = "https://localhost:7151";
            // 2. Đảm bảo không bị thừa dấu "/" ở cuối link gốc
            baseUrl = baseUrl.TrimEnd('/');

            // 3. Gán giá trị cho các đường dẫn API cụ thể
            // Kết quả sẽ là: http://Hoctiengtrung-001-site1.jtempurl.com/api/quanlytuvung
            apiBaseUrl = $"{baseUrl}/api/quanlytuvung";

            // Kết quả sẽ là: http://Hoctiengtrung-001-site1.jtempurl.com/api/quanlybaitap
            apiBaseUrl1 = $"{baseUrl}/api/quanlybaitap";
        }


        public async Task<IActionResult> DanhSachCapDoBaiTap()
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // SỬA Ở ĐÂY: Thêm ?status=true vào cuối đường dẫn API
            string apiUrl = $"{apiBaseUrl}/danhsachcapdo?status=true";

            try
            {
                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // Giải mã JSON sang danh sách Model CapDoModels
                    var listCapDo = JsonConvert.DeserializeObject<List<CapDoModels>>(jsonString);

                    // Trả về View cùng với danh sách cấp độ
                    return View(listCapDo ?? new List<CapDoModels>());
                }
                else
                {
                    _logger.LogWarning($"API Lấy cấp độ thất bại: {response.StatusCode}");
                    return View(new List<CapDoModels>());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi lấy danh sách cấp độ HSK");
                return View(new List<CapDoModels>());
            }
        }

        public async Task<IActionResult> DanhSachLoaiBaiTap(int? capdo, [FromQuery] string chuongTrinh = null)
        {
            // 1. Kiểm tra và đồng bộ bộ lọc với Session
            bool hasCapDo = capdo.HasValue && capdo.Value > 0;
            bool hasChuongTrinh = !string.IsNullOrWhiteSpace(chuongTrinh);

            if (hasCapDo || hasChuongTrinh)
            {
                // Lưu giá trị mới người dùng chọn vào Session
                if (hasCapDo)
                {
                    HttpContext.Session.SetInt32("HSK_Saved_CapDo", capdo.Value);
                }

                if (hasChuongTrinh)
                {
                    HttpContext.Session.SetString("HSK_Saved_ChuongTrinh", chuongTrinh.Trim());
                }
            }
            else
            {
                // Nếu người dùng quay lại trang từ đâu đó mà không truyền Query String
                int? savedCapDo = HttpContext.Session.GetInt32("HSK_Saved_CapDo");
                string savedChuongTrinh = HttpContext.Session.GetString("HSK_Saved_ChuongTrinh");

                // Nếu đã có bộ lọc lưu trước đó -> Redirect lại URL có đầy đủ param
                if (savedCapDo.HasValue || !string.IsNullOrEmpty(savedChuongTrinh))
                {
                    return RedirectToAction("DanhSachLoaiBaiTap", new
                    {
                        capdo = savedCapDo ?? 1,
                        chuongTrinh = savedChuongTrinh ?? "3"
                    });
                }
            }

            // 2. Xác định giá trị cuối cùng để truyền tới API và ViewBag (Mặc định HSK 1, Chương trình 3.0)
            int finalCapDo = HttpContext.Session.GetInt32("HSK_Saved_CapDo") ?? capdo ?? 1;
            string finalChuongTrinh = HttpContext.Session.GetString("HSK_Saved_ChuongTrinh") ?? chuongTrinh ?? "3";

            // Gán dữ liệu cho ViewBag
            ViewBag.IdCapDo = finalCapDo;
            ViewBag.ChuongTrinh = finalChuongTrinh;

            // 3. Chuẩn hóa URL và gọi API lấy dữ liệu
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // Nếu API backend của bạn hỗ trợ nhận cả capdo và chuongTrinh
            string apiUrl = $"{apiBaseUrl1}/danhsachloaibaitap?capDo={finalCapDo}&chuongTrinh={Uri.EscapeDataString(finalChuongTrinh)}";

            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var listLoai = JsonConvert.DeserializeObject<List<LoaiCauHoiModels>>(jsonString);

                    return View(listLoai ?? new List<LoaiCauHoiModels>());
                }

                _logger.LogWarning($"API Lấy loại bài tập thất bại với StatusCode: {response.StatusCode}");
                return View(new List<LoaiCauHoiModels>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi lấy danh sách loại bài tập");
                return View(new List<LoaiCauHoiModels>());
            }
        }

        //[HttpGet]
        //public async Task<IActionResult> LamBaiTap(int idCapDo, int idLoai, bool shuffle = false)
        //{
        //    var client = _httpClientFactory.CreateClient("BypassSSL");

        //    // SỬA: Truyền thêm tham số shuffle vào query string để API biết khi nào cần trộn
        //    string apiUrl = $"{apiBaseUrl1}/lay-bai-tap/{idCapDo}/{idLoai}?shuffle={shuffle}";

        //    try
        //    {
        //        var response = await client.GetAsync(apiUrl);
        //        if (response.IsSuccessStatusCode)
        //        {
        //            var jsonString = await response.Content.ReadAsStringAsync();
        //            var danhSachDeThi = JsonConvert.DeserializeObject<List<DeThiViewModel>>(jsonString);

        //            if (danhSachDeThi == null || !danhSachDeThi.Any())
        //            {
        //                return View("Error");
        //            }

        //            ViewBag.IdCapDo = idCapDo;
        //            ViewBag.IdLoai = idLoai;
        //            ViewBag.IsShuffled = shuffle; // Để View biết hiện tại có đang ở chế độ trộn hay không

        //            return View("DanhSachDeThi", danhSachDeThi);
        //        }

        //        _logger.LogWarning($"API trả về lỗi: {response.StatusCode}");
        //        return View("Error");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Lỗi kết nối API lấy câu hỏi");
        //        return View("Error");
        //    }
        //}

        [HttpGet]
        public async Task<IActionResult> LamBaiTap(int idCapDo, int idLoai, bool shuffle = false)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // 1. Chuyển tiếp Cookie & Authorization Header sang API nếu có
            if (Request.Headers.ContainsKey("Cookie"))
            {
                client.DefaultRequestHeaders.Add("Cookie", Request.Headers["Cookie"].ToString());
            }

            if (Request.Headers.ContainsKey("Authorization"))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", Request.Headers["Authorization"].ToString());
            }

            // =========================================================================
            // 2. CHỈ LẤY ID TRUNG TÂM CỦA NGƯỜI DÙNG ĐANG ĐĂNG NHẬP
            // =========================================================================
            int idTrungTamFinal = 0;

            var idTrungTamClaim = User.FindFirst("IdTrungTam")?.Value;
            if (!string.IsNullOrEmpty(idTrungTamClaim))
            {
                int.TryParse(idTrungTamClaim, out idTrungTamFinal);
            }
            else
            {
                idTrungTamFinal = HttpContext.Session.GetInt32("IdTrungTam")
                                  ?? (int.TryParse(HttpContext.Session.GetString("IdTrungTam"), out var idTT) ? idTT : 0);
            }

            // 3. Khởi tạo URL gọi API lấy bài tập theo Trung tâm
            string apiUrl = $"{apiBaseUrl1}/lay-bai-tap/{idCapDo}/{idLoai}?shuffle={shuffle}";
            if (idTrungTamFinal > 0)
            {
                apiUrl += $"&idTrungTam={idTrungTamFinal}";
            }

            try
            {
                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    var danhSachDeThi = JsonConvert.DeserializeObject<List<DeThiViewModel>>(jsonString);
                    if (danhSachDeThi == null || !danhSachDeThi.Any())
                    {
                        return View("Error");
                    }

                    // Gán dữ liệu vào ViewBag để View sử dụng
                    ViewBag.IdCapDo = idCapDo;
                    ViewBag.IdLoai = idLoai;
                    ViewBag.IsShuffled = shuffle;
                    ViewBag.IdTrungTam = idTrungTamFinal;

                    return View("DanhSachDeThi", danhSachDeThi);
                }

                _logger.LogWarning($"API Lấy bài tập trả về mã lỗi: {response.StatusCode} | URL: {apiUrl}");
                return View("Error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối API lấy câu hỏi cho Cấp độ: {IdCapDo}, Loại: {IdLoai}, Trung tâm: {IdTrungTam}", idCapDo, idLoai, idTrungTamFinal);
                return View("Error");
            }
        }

        [HttpPost]
        public async Task<IActionResult> BatDauLamBaiHienThi(string jsonCauHoi)
        {
            if (string.IsNullOrWhiteSpace(jsonCauHoi))
                return RedirectToAction("Index");

            try
            {
                // 1. Giải mã danh sách câu hỏi từ chuỗi JSON
                var listCauHoi = JsonConvert.DeserializeObject<List<CauHoiModels>>(jsonCauHoi);

                // 2. Kiểm tra nếu không có câu hỏi hợp lệ
                if (listCauHoi == null || !listCauHoi.Any())
                {
                    return View("Error");
                }

                // 3. Luồng làm bài tập miễn phí bình thường
                return View("LamBaiTapThucTe21", listCauHoi);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi giải mã JSON câu hỏi khi bắt đầu làm bài");
                return View("Error");
            }
        }
        [HttpGet]
        public async Task<IActionResult> DanhSachDeThi(int? capdo, int? loai)
        {
            
           
            return View();
        }

        public IActionResult LamBaiTapThucTe21()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> LichSuLuyenTap(int? idHocVien = null)
        {
            // Lưu trạng thái vào ViewBag để View biết đang xem của ai
            ViewBag.IdHocVien = idHocVien;
            ViewBag.IsXemHocVien = idHocVien.HasValue;

            // 1. Khởi tạo Client từ Factory
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // 2. CHUYỂN TIẾP COOKIE: Bắt buộc để API nhận diện User / Role
            var cookie = Request.Headers["Cookie"].ToString();
            if (!string.IsNullOrEmpty(cookie))
            {
                client.DefaultRequestHeaders.Add("Cookie", cookie);
            }

            // 3. XÂY DỰNG URL GỌI API: Nối thêm param idHocVien nếu có
            string apiUrl = $"{apiBaseUrl1.TrimEnd('/')}/danh-sach-lich-su";
            if (idHocVien.HasValue && idHocVien.Value > 0)
            {
                apiUrl += $"?idHocVien={idHocVien.Value}";
            }

            try
            {
                // 4. Thực hiện gọi API GET
                var response = await client.GetAsync(apiUrl);

                // Xử lý khi chưa đăng nhập
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("API báo chưa đăng nhập, chuyển hướng về Login.");
                    return RedirectToAction("DangNhap", "QuanLyTaiKhoan", new { returnUrl = Request.Path + Request.QueryString });
                }

                // Xử lý khi không có quyền xem học viên này (Forbidden 403)
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    TempData["ErrorMessage"] = "Bạn không có quyền xem lịch sử bài làm của học viên này.";
                    return RedirectToAction("Index", "Home");
                }

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // 5. Giải mã JSON
                    var result = JsonConvert.DeserializeObject<ApiResponseLichSu>(jsonString);

                    if (result != null && result.Success)
                    {
                        // Truyền tên học viên (nếu API có trả về) ra ViewBag để View render tiêu đề
                        ViewBag.TenHocVien = result.TenHocVien ?? "Học viên";

                        return View(result.Data ?? new List<LichSuLamBaiModels>());
                    }
                }

                _logger.LogWarning($"API Lấy lịch sử thất bại: {response.StatusCode}");
                return View(new List<LichSuLamBaiModels>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi API danh-sach-lich-su");
                return View(new List<LichSuLamBaiModels>());
            }
        }

        public async Task<IActionResult> XemLaiBaiLam(int idLichSu)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // 1. Chuyển tiếp Cookie để giữ Session nhận diện người dùng
            var cookie = Request.Headers["Cookie"].ToString();
            if (!string.IsNullOrEmpty(cookie))
            {
                client.DefaultRequestHeaders.Add("Cookie", cookie);
            }

            // 2. Xây dựng URL chuẩn (đảm bảo đúng route API)
            string apiUrl = $"{apiBaseUrl1.TrimEnd('/')}/chi-tiet-lich-su/{idLichSu}";

            try
            {
                // 3. Gọi API
                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // 4. GIẢI MÃ CHUẨN: Dùng Class cụ thể thay vì dynamic để tránh lỗi RuntimeBinder
                    var result = JsonConvert.DeserializeObject<ApiResponseChiTiet>(jsonString);

                    if (result != null && result.Success)
                    {
                        // Trả về View với danh sách ChiTietLichSuModels đã được định nghĩa
                        return View(result.Data);
                    }
                }

                // Nếu không thành công hoặc Unauthorized, quay về danh sách lịch sử
                _logger.LogWarning($"Khong the lay chi tiet lich su {idLichSu}. Status: {response.StatusCode}");
                return RedirectToAction("LichSuLuyenTap");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi kết nối API khi xem lại bài làm ID: {idLichSu}");
                return RedirectToAction("LichSuLuyenTap");
            }
        }


        [HttpGet]
        [Authorize] // Bắt buộc đăng nhập
        public async Task<IActionResult> DanhSachQuanLyAdmin(
            int page = 1,
            int idLoai = 1,
            int idCapDo = 1,
            bool trangThai = true,
            string? maTrungTam = null,
            int? idTrungTam = null)
        {
            // =========================================================================
            // 1. KIỂM TRA PHÂN QUYỀN TRUY CẬP (CHỈ ADMIN HOẶC QUẢN LÝ TRUNG TÂM)
            // =========================================================================
            var tenVaiTro = (User.FindFirst("TenVaiTro")?.Value
                             ?? HttpContext.Session?.GetString("TenVaiTro")
                             ?? "").Trim();
            var idVaiTroStr = (User.FindFirst("IdVaiTro")?.Value
                               ?? HttpContext.Session?.GetString("IdVaiTro")
                               ?? "").Trim();
            var roleClaims = User.FindAll(ClaimTypes.Role).Select(c => c.Value.Trim()).ToList();

            string roleClean = tenVaiTro.ToLower();

            bool isAdmin = roleClean.Contains("quản trị viên")
                           || roleClean.Contains("quan tri vien")
                           || roleClean.Contains("admin")
                           || idVaiTroStr == "1"
                           || roleClaims.Any(r => r.Equals("Quản trị viên", StringComparison.OrdinalIgnoreCase)
                                               || r.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                                               || r == "1");

            bool isQuanLy = !isAdmin && (roleClean.Contains("quản lý")
                                      || roleClean.Contains("quan ly")
                                      || roleClean.Contains("manager")
                                      || roleClaims.Any(r => r.Equals("Quản lý trung tâm", StringComparison.OrdinalIgnoreCase)
                                                          || r.Equals("Quan ly trung tam", StringComparison.OrdinalIgnoreCase)));

            if (!isAdmin && !isQuanLy)
            {
                TempData["AccessDenied"] = "true";
                TempData["ErrorMessage"] = "Chỉ Admin và Quản lý trung tâm mới có quyền truy cập trang này!";
                return RedirectToAction("Index", "Home");
            }

            // =========================================================================
            // 2. XÁC ĐỊNH & CÔ LẬP TRUNG TÂM THEO VAI TRÒ
            // =========================================================================
            if (page < 1) page = 1;

            string? myIdTrungTamStr = User.FindFirst("IdTrungTam")?.Value
                                      ?? HttpContext.Session?.GetString("IdTrungTam");
            int.TryParse(myIdTrungTamStr, out int myIdTrungTam);

            string? myMaTrungTam = (User.FindFirst("MaTrungTam")?.Value
                                    ?? HttpContext.Session?.GetString("MaTrungTam"))?.Trim();

            int? targetIdTrungTam = null;
            string? targetMaTrungTam = null;

            if (isQuanLy)
            {
                // QUẢN LÝ TRUNG TÂM: Khóa chặt ID/Mã trung tâm của chính họ, không cho phép URL ghi đè
                targetIdTrungTam = myIdTrungTam > 0 ? myIdTrungTam : null;
                targetMaTrungTam = myMaTrungTam;

                if (!targetIdTrungTam.HasValue || targetIdTrungTam.Value <= 0)
                {
                    TempData["ErrorMessage"] = "Tài khoản của bạn chưa được liên kết với cơ sở đào tạo nào.";
                    return RedirectToAction("Index", "Home");
                }
            }
            else if (isAdmin)
            {
                // ADMIN: Có thể lọc theo bất kỳ cơ sở nào truyền qua tham số
                targetIdTrungTam = (idTrungTam.HasValue && idTrungTam.Value > 0) ? idTrungTam.Value : null;
                targetMaTrungTam = !string.IsNullOrWhiteSpace(maTrungTam) ? maTrungTam.Trim() : null;
            }

            // =========================================================================
            // 3. TẠO QUERY PARAMETERS ĐỒNG BỘ VỚI API BACKEND
            // =========================================================================
            var queryParams = new List<string>
    {
        $"page={page}",
        $"idLoai={idLoai}",
        $"idCapDo={idCapDo}",
        $"TrangThai={trangThai}"
    };

            if (targetIdTrungTam.HasValue && targetIdTrungTam.Value > 0)
            {
                queryParams.Add($"idTrungTam={targetIdTrungTam.Value}");
            }

            if (!string.IsNullOrWhiteSpace(targetMaTrungTam))
            {
                queryParams.Add($"maTrungTam={Uri.EscapeDataString(targetMaTrungTam)}");
            }

            string apiUrl = $"{apiBaseUrl1}/danh-sach-quan-ly?{string.Join("&", queryParams)}";

            // =========================================================================
            // 4. CHUYỂN TIẾP COOKIE VÀ AUTHORIZATION TOKEN AN TOÀN
            // =========================================================================
            var client = _httpClientFactory.CreateClient("BypassSSL");
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, apiUrl);

            if (Request.Headers.TryGetValue("Cookie", out var cookieValue))
            {
                requestMessage.Headers.TryAddWithoutValidation("Cookie", cookieValue.ToString());
            }

            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                requestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }

            ApiResponsePager<CauHoiModels>? result = null;

            try
            {
                var response = await client.SendAsync(requestMessage);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    result = JsonConvert.DeserializeObject<ApiResponsePager<CauHoiModels>>(jsonString);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    TempData["AccessDenied"] = "true";
                    TempData["ErrorMessage"] = "Bạn không có quyền xem câu hỏi của cơ sở này.";
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    _logger.LogWarning("API DanhSachQuanLyAdmin trả về mã lỗi: {StatusCode} | URL: {Url}", response.StatusCode, apiUrl);
                    TempData["ErrorMessage"] = $"Không thể tải danh sách câu hỏi (Mã lỗi: {response.StatusCode}).";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi API DanhSachQuanLyAdmin với URL: {Url}", apiUrl);
                TempData["ErrorMessage"] = "Lỗi kết nối đến máy chủ dịch vụ quản lý câu hỏi.";
            }

            // =========================================================================
            // 5. LƯU BỘ LỌC VÀO VIEWBAG ĐỂ DUY TRÌ GIAO DIỆN
            // =========================================================================
            ViewBag.IsAdmin = isAdmin;
            ViewBag.IsQuanLy = isQuanLy;
            ViewBag.CurrentCapDo = idCapDo;
            ViewBag.CurrentLoai = idLoai;
            ViewBag.CurrentPage = result?.CurrentPage ?? page;
            ViewBag.TotalPages = result?.TotalPages ?? 0;
            ViewBag.TotalItems = result?.TotalItems ?? 0;
            ViewBag.CurrentTrangThai = trangThai;
            ViewBag.CurrentMaTrungTam = targetMaTrungTam;
            ViewBag.CurrentIdTrungTam = targetIdTrungTam;

            return View(result?.Data ?? new List<CauHoiModels>());
        }
        public async Task<IActionResult> QuanLyLoaiBaiTapAdmin()
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // Đường dẫn gọi tới API lấy toàn bộ danh sách loại bài tập của Admin
            string apiUrl = $"{apiBaseUrl1.TrimEnd('/')}/admin/danhsach-loaibaitap";

            try
            {
                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // Giải mã JSON theo cấu trúc Object phản hồi của API Admin
                    var result = JsonConvert.DeserializeObject<ApiResponseLoaiBaiTap>(jsonString);

                    if (result != null && result.Success)
                    {
                        // Trả về View kèm danh sách Model bài tập để lặp foreach hiển thị ra bảng
                        return View(result.Data ?? new List<LoaiBaiTapModel>());
                    }
                }

                _logger.LogWarning($"API lấy danh sách loại bài tập Admin thất bại: {response.StatusCode}");
                return View(new List<LoaiBaiTapModel>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi API admin/danhsach-loaibaitap");
                return View(new List<LoaiBaiTapModel>());
            }
        }



        public class ApiResponseChiTiet
        {
            public bool Success { get; set; }
            public List<ChiTietLichSuModels> Data { get; set; }
            public string Message { get; set; }
        }
        // Thêm class bổ trợ này để hứng dữ liệu từ API (Bạn có thể để cuối file Controller)
        public class ApiResponseLichSu
        {
            public bool Success { get; set; }
            public List<LichSuLamBaiModels> Data { get; set; }
            public string Message { get; set; }

            public string? TenHocVien { get; set; }
        }
        // Thêm class này để hứng dữ liệu danh sách loại bài tập từ API Admin trả về
        public class ApiResponseLoaiBaiTap
        {
            public bool Success { get; set; }
            public List<LoaiBaiTapModel> Data { get; set; }
            public string Message { get; set; }
        }
    }
}
