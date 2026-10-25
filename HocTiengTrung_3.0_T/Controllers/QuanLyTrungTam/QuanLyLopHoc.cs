using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12.QuanLyTrungTam;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.Controllers.QuanLyTrungTam
{
    public class QuanLyLopHoc : Controller
    {
        private readonly TmdtContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyLopHoc> _logger;
        private readonly string apiBaseUrl;

        public QuanLyLopHoc(
            IHttpClientFactory httpClientFactory,
            ILogger<QuanLyLopHoc> logger,
            IConfiguration configuration,
            TmdtContext context)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _context = context;

            string baseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7151";
            baseUrl = baseUrl.TrimEnd('/');
            apiBaseUrl = $"{baseUrl}/api/quanlylophoc";
        }
        // ==========================================
        // 1. DANH SÁCH LỚP HỌC (CALL API DANH SÁCH)
        // ==========================================
        [Authorize(Roles = "Quản trị viên,Admin,Quản lý trung tâm,Giảng viên,1,1002")]
        [HttpGet]
        public async Task<IActionResult> DanhSachLopHoc(
     int? idTrungTam,
     int? idGiangVien,
     string? keyword,
     bool? trangThai)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            // =========================================================================
            // 1. CHUYỂN TIẾP COOKIE XÁC THỰC TỪ TRÌNH DUYỆT SANG API (BẮT BUỘC)
            // =========================================================================
            if (Request.Headers.TryGetValue("Cookie", out var cookieValue))
            {
                client.DefaultRequestHeaders.Add("Cookie", cookieValue.ToString());
            }

            // =========================================================================
            // 2. TỰ ĐỘNG NHẬN DIỆN VAI TRÒ ĐỂ GẮN ID TRUNG TÂM PHÙ HỢP
            // =========================================================================
            var tenVaiTro = (User.FindFirst("TenVaiTro")?.Value
                             ?? HttpContext.Session?.GetString("TenVaiTro")
                             ?? "").Trim();

            bool isQuanLy = tenVaiTro.Equals("Quản lý trung tâm", StringComparison.OrdinalIgnoreCase);

            // Chỉ tự động ép IdTrungTam đối với Quản lý trung tâm (Admin được chọn tùy ý)
            if (isQuanLy && (!idTrungTam.HasValue || idTrungTam <= 0))
            {
                string? sessionTt = HttpContext.Session.GetString("IdTrungTam")
                                    ?? User.FindFirst("IdTrungTam")?.Value;

                if (int.TryParse(sessionTt, out int parsedTt) && parsedTt > 0)
                {
                    idTrungTam = parsedTt;
                }
            }

            // =========================================================================
            // 3. XÂY DỰNG QUERY STRING GỌI API
            // =========================================================================
            var queryParams = new List<string>();

            if (idTrungTam.HasValue && idTrungTam.Value > 0)
                queryParams.Add($"idTrungTam={idTrungTam.Value}");

            if (idGiangVien.HasValue && idGiangVien.Value > 0)
                queryParams.Add($"idGiangVien={idGiangVien.Value}");

            if (!string.IsNullOrWhiteSpace(keyword))
                queryParams.Add($"keyword={Uri.EscapeDataString(keyword.Trim())}");

            if (trangThai.HasValue)
                queryParams.Add($"trangThai={trangThai.Value}");

            string queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            string apiUrl = $"{apiBaseUrl}/danhsachlophoc{queryString}";

            // Lưu bộ lọc vào ViewBag để hiển thị trên View
            ViewBag.IdTrungTam = idTrungTam;
            ViewBag.IdGiangVien = idGiangVien;
            ViewBag.Keyword = keyword;
            ViewBag.TrangThai = trangThai;
            ViewBag.TenVaiTro = tenVaiTro;

            try
            {
                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var resObj = JsonConvert.DeserializeObject<dynamic>(jsonString);

                    var dataString = resObj?.data != null ? resObj.data.ToString() : jsonString;
                    var danhSach = JsonConvert.DeserializeObject<List<LopHocModels>>(dataString) ?? new List<LopHocModels>();

                    return View(danhSach);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    TempData["ErrorMessage"] = "Bạn không có quyền xem danh sách lớp học này.";
                }
                else
                {
                    _logger.LogWarning($"API Danh sách lớp học trả về status: {response.StatusCode} | URL: {apiUrl}");
                    TempData["ErrorMessage"] = "Không thể lấy danh sách lớp học từ máy chủ.";
                }

                return View(new List<LopHocModels>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi API danh sách lớp học.");
                TempData["ErrorMessage"] = "Lỗi kết nối tới hệ thống máy chủ API: " + ex.Message;
                return View(new List<LopHocModels>());
            }
        }
        // ==========================================
        // CHI TIẾT LỚP HỌC (HIỂN THỊ HỌC VIÊN)
        // ==========================================
        [Authorize(Roles = "Quản trị viên,Admin,Quản lý trung tâm,Giảng viên,1,1002")]
        [HttpGet]
        public async Task<IActionResult> ChiTietLopHoc(int id)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] = "ID lớp học không hợp lệ.";
                return RedirectToAction("DanhSachLopHoc");
            }

            var client = _httpClientFactory.CreateClient("BypassSSL");

            // =========================================================================
            // 1. CHUYỂN TIẾP COOKIE XÁC THỰC TỪ TRÌNH DUYỆT SANG API (BẮT BUỘC)
            // =========================================================================
            if (Request.Headers.TryGetValue("Cookie", out var cookieValue))
            {
                client.DefaultRequestHeaders.Add("Cookie", cookieValue.ToString());
            }

            string apiUrl = $"{apiBaseUrl}/chitietlophoc/{id}";

            try
            {
                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var resObj = JsonConvert.DeserializeObject<dynamic>(jsonString);

                    // API trả về bọc trong { success: true, data: { ... } }
                    var dataString = resObj?.data != null ? resObj.data.ToString() : jsonString;
                    var chiTiet = JsonConvert.DeserializeObject<ChiTietLopHocViewModel>(dataString);

                    if (chiTiet == null)
                    {
                        TempData["ErrorMessage"] = "Không thể giải mã dữ liệu lớp học.";
                        return RedirectToAction("DanhSachLopHoc");
                    }

                    return View(chiTiet);
                }

                // Xử lý các mã phản hồi từ chối / không tìm thấy
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    var errObj = JsonConvert.DeserializeObject<dynamic>(errorBody);
                    string msg = errObj?.message?.ToString() ?? "Bạn không có quyền xem thông tin chi tiết của lớp học này.";

                    TempData["ErrorMessage"] = msg;
                    return RedirectToAction("DanhSachLopHoc");
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy thông tin lớp học hoặc lớp học đã bị xóa.";
                    return RedirectToAction("DanhSachLopHoc");
                }

                _logger.LogWarning($"API Chi tiết lớp học {id} trả về mã lỗi: {response.StatusCode} | URL: {apiUrl}");
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tải dữ liệu chi tiết lớp học.";
                return RedirectToAction("DanhSachLopHoc");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi kết nối API khi lấy chi tiết lớp học ID: {id}");
                TempData["ErrorMessage"] = "Lỗi kết nối tới hệ thống máy chủ API: " + ex.Message;
                return RedirectToAction("DanhSachLopHoc");
            }
        }
    }
}