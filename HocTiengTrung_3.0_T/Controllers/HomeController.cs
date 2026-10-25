using HocTiengTrung_3._0_T.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly TmdtContext _context;

        public HomeController(ILogger<HomeController> logger, TmdtContext context)
        {
            _logger = logger;
            _context = context;
        }

        /// <summary>
        /// Đường dẫn chuyển hướng theo định dạng: /TrungTam/{subdomain}
        /// Luôn tự động đăng xuất tài khoản cũ (nếu có), xóa sạch Session cũ và nạp Session trung tâm mới.
        /// </summary>
        [HttpGet("TrungTam/{subdomain}")]
        public async Task<IActionResult> ChuyenHuongTrungTam(string subdomain)
        {
            if (!string.IsNullOrWhiteSpace(subdomain))
            {
                string cleanSub = subdomain.Trim();

                var trungTam = await _context.TrungTams
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TrangThai &&
                        (t.Subdomain == cleanSub || t.MaTrungTam == cleanSub));

                if (trungTam != null)
                {
                    // 1. ĐĂNG XUẤT TÀI KHOẢN HIỆN TẠI NẾU ĐANG ĐĂNG NHẬP
                    if (User.Identity != null && User.Identity.IsAuthenticated)
                    {
                        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        await HttpContext.SignOutAsync(); // Xóa sạch Auth Cookie
                    }

                    // 2. Xóa sạch toàn bộ Session của trung tâm cũ
                    HttpContext.Session.Remove("IdTrungTam");
                    HttpContext.Session.Remove("MaTrungTam");
                    HttpContext.Session.Remove("TenTrungTam");
                    HttpContext.Session.Remove("Subdomain");
                    HttpContext.Session.Remove("LogoTrungTam");

                    // 3. Gán dữ liệu Session của trung tâm mới
                    HttpContext.Session.SetString("IdTrungTam", trungTam.IdTrungTam.ToString());
                    HttpContext.Session.SetString("MaTrungTam", trungTam.MaTrungTam ?? "");
                    HttpContext.Session.SetString("TenTrungTam", trungTam.TenTrungTam);
                    HttpContext.Session.SetString("Subdomain", trungTam.Subdomain ?? "");
                    HttpContext.Session.SetString("LogoTrungTam", trungTam.Logo ?? "");
                }
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Trang chủ hệ thống - Tự động nhận diện Session/Claims để hiển thị View tương ứng của từng trung tâm
        /// </summary>
        public IActionResult Index()
        {
            // Ưu tiên lấy Session từ URL trước, nếu không có mới lấy từ Claims của tài khoản
            string? maTrungTam = HttpContext.Session.GetString("MaTrungTam")
                                 ?? User.FindFirst("MaTrungTam")?.Value;

            if (!string.IsNullOrEmpty(maTrungTam))
            {
                string code = maTrungTam.Trim();

                if (code.Equals("KimHoa001", StringComparison.OrdinalIgnoreCase) ||
                    code.Equals("TrungTamKimHoa01", StringComparison.OrdinalIgnoreCase))
                {
                    return View("IndexKimHoa");
                }

                if (code.Equals("MyDuc001", StringComparison.OrdinalIgnoreCase))
                {
                    return View("IndexMyDuc");
                }

                if (code.Equals("HoaiThuong001", StringComparison.OrdinalIgnoreCase))
                {
                    return View("IndexHoaiThuong");
                }
            }

            return View();
        }

        public IActionResult IndexKimHoa()
        {
            return View();
        }

        public IActionResult IndexMyDuc()
        {
            return View();
        }

        public IActionResult IndexHoaiThuong()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult HeThong()
        {
            return View();
        }

        public IActionResult Index123()
        {
            return View();
        }

        [HttpGet]
        [Authorize]
        public IActionResult QuanLyTT()
        {
            var tenVaiTro = (User.FindFirst("TenVaiTro")?.Value
                             ?? HttpContext.Session?.GetString("TenVaiTro")
                             ?? "").Trim();

            var idVaiTroStr = (User.FindFirst("IdVaiTro")?.Value
                               ?? HttpContext.Session?.GetString("IdVaiTro")
                               ?? "").Trim();

            var roleClaims = User.FindAll(ClaimTypes.Role).Select(c => c.Value.Trim()).ToList();

            string roleClean = tenVaiTro.ToLower();

            bool isQuanLyTrungTam = (roleClean.Contains("quản trị viên") || roleClean.Contains("quan ly") || roleClean.Contains("manager"))
                                    && !roleClean.Contains("giảng viên")
                                    && !roleClean.Contains("giang vien")
                                    || idVaiTroStr == "1002"
                                    || roleClaims.Any(r => r.Equals("Quản lý trung tâm", StringComparison.OrdinalIgnoreCase)
                                                        || r.Equals("Quan ly trung tam", StringComparison.OrdinalIgnoreCase));

            if (!isQuanLyTrungTam)
            {
                TempData["AccessDenied"] = "true";
                TempData["ErrorMessage"] = "Trang này chỉ dành riêng cho Quản lý trung tâm đào tạo!";
                return RedirectToAction(nameof(Index));
            }

            string? idTrungTamStr = User.FindFirst("IdTrungTam")?.Value
                                    ?? HttpContext.Session?.GetString("IdTrungTam");
            int.TryParse(idTrungTamStr, out int idTrungTam);

            ViewBag.IdTrungTam = idTrungTam;

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}