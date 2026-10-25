using HocTiengTrung_3._0_T.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace HocTiengTrung_3._0_T.Middlewares
{
    public class TenantMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
        {
            string host = context.Request.Host.Host.ToLower(); // Ví dụ: localhost, myduc.localhost, myduc.domain.com
            string path = context.Request.Path.Value?.ToLower() ?? "";
            string? subDomain = null;

            // =========================================================================
            // 1. ƯU TIÊN 1: Bắt qua Path URL /trungtam/{subdomain}
            // Ví dụ: https://localhost:7151/trungtam/KimHoa001 -> Xóa session cũ, lưu session mới và Redirect về /
            // =========================================================================
            if (path.StartsWith("/trungtam/"))
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2)
                {
                    subDomain = segments[1].Trim();
                    await LoadAndSaveSessionAsync(context, serviceProvider, subDomain);
                    context.Response.Redirect("/");
                    return;
                }
            }

            // =========================================================================
            // 2. ƯU TIÊN 2: Bắt qua Query String ?trungtam={subdomain}
            // Ví dụ: https://localhost:7151/?trungtam=KimHoa001
            // =========================================================================
            if (context.Request.Query.ContainsKey("trungtam"))
            {
                subDomain = context.Request.Query["trungtam"].ToString().Trim();
            }

            // =========================================================================
            // 3. ƯU TIÊN 3: Bắt qua Subdomain thật hoặc Subdomain trên localhost
            // =========================================================================
            if (string.IsNullOrEmpty(subDomain))
            {
                // Trường hợp test Local với Subdomain: myduc.localhost
                if (host.EndsWith(".localhost"))
                {
                    subDomain = host.Replace(".localhost", "").Trim();
                }
                // Trường hợp Domain chính thức trên Production (ví dụ: myduc.hskonline.com)
                else if (!host.Contains("ftempurl.com") && host != "localhost")
                {
                    var hostParts = host.Split('.');
                    if (hostParts.Length >= 3 && hostParts[0] != "www")
                    {
                        subDomain = hostParts[0].Trim();
                    }
                }
            }

            // =========================================================================
            // 4. Nếu tìm được Subdomain và trung tâm mới khác với trung tâm đang lưu trong Session
            // -> Xóa Session cũ và nạp lại trung tâm mới
            // =========================================================================
            if (!string.IsNullOrEmpty(subDomain))
            {
                string? currentMaTT = context.Session.GetString("MaTrungTam");
                string? currentSub = context.Session.GetString("Subdomain");

                bool isDiff = !string.Equals(currentMaTT, subDomain, StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(currentSub, subDomain, StringComparison.OrdinalIgnoreCase);

                if (isDiff)
                {
                    await LoadAndSaveSessionAsync(context, serviceProvider, subDomain);
                }
            }

            await _next(context);
        }

        /// <summary>
        /// Xóa sạch toàn bộ Session liên quan đến trung tâm trước đó
        /// </summary>
        private static void ClearTenantSession(HttpContext context)
        {
            context.Session.Remove("IdTrungTam");
            context.Session.Remove("MaTrungTam");
            context.Session.Remove("TenTrungTam");
            context.Session.Remove("Subdomain");
            context.Session.Remove("LogoTrungTam");
        }

        /// <summary>
        /// Hàm phụ trách truy vấn Database và nạp dữ liệu trung tâm mới vào Session
        /// </summary>
        private static async Task LoadAndSaveSessionAsync(HttpContext context, IServiceProvider serviceProvider, string subDomain)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TmdtContext>();

                var trungTam = await db.TrungTams
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TrangThai &&
                        (t.Subdomain == subDomain || t.MaTrungTam == subDomain));

                if (trungTam != null)
                {
                    // 1. TỰ ĐỘNG ĐĂNG XUẤT TÀI KHOẢN CŨ NẾU CÓ
                    if (context.User.Identity != null && context.User.Identity.IsAuthenticated)
                    {
                        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        await context.SignOutAsync();
                    }

                    // 2. Xóa toàn bộ Session trung tâm cũ
                    ClearTenantSession(context);

                    // 3. Gán Session trung tâm mới
                    context.Session.SetString("IdTrungTam", trungTam.IdTrungTam.ToString());
                    context.Session.SetString("MaTrungTam", trungTam.MaTrungTam ?? "");
                    context.Session.SetString("TenTrungTam", trungTam.TenTrungTam);
                    context.Session.SetString("Subdomain", trungTam.Subdomain ?? "");
                    context.Session.SetString("LogoTrungTam", trungTam.Logo ?? "");
                }
            }
        }
    }
}