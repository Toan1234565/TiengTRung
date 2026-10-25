using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12;
using HocTiengTrung_3._0_T.Models12.AI;
using HocTiengTrung_3._0_T.Models12.QuanLyTaiKhoan;
using HocTiengTrung_3._0_T.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using PayOS;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// LƯU Ý MÀU ĐỎ: Nếu chữ DauTruongTH2Hub ở dưới cùng bị gạch chân đỏ, 
// bạn hãy trỏ chuột vào nó và chọn "using..." (hoặc Uncomment dòng dưới đây và sửa lại cho đúng thư mục chứa Hub của bạn)
// using HocTiengTrung_3._0_T.Hubs; 

namespace HocTiengTrung_3._0_T
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            var builder = WebApplication.CreateBuilder(args);

            // --- 1. DATABASE ---
            builder.Services.AddDbContext<TmdtContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // --- 2. DATA PROTECTION ---
            var keyPath = Path.Combine(Directory.GetCurrentDirectory(), "SharedKeys");
            if (!Directory.Exists(keyPath)) Directory.CreateDirectory(keyPath);

            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
                .SetApplicationName("HocTiengTrung_Shared");

            // --- 3. CORS CONFIGURATION ---
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.WithOrigins(
                              "https://localhost:7151",
                              "http://localhost:5151"
                          )
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials(); // Bắt buộc cho SignalR + Cookie
                });
            });

            // --- 4. AUTHENTICATION & COOKIES ---
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.Name = "HocTiengTrung_Auth";
                options.LoginPath = "/QuanLyTaiKhoan/DangNhap";
                options.LogoutPath = "/QuanLyTaiKhoan/DangXuat";
                options.AccessDeniedPath = "/Home/Error";
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Unspecified;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            })
            .AddGoogle(options =>
            {
                options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "YOUR_ID";
                options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "YOUR_SECRET";
            });

            // --- 5. SESSION & SERVICES ---
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromHours(2);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            builder.Services.AddControllersWithViews()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                });

            builder.Services.AddSingleton(new PayOSClient(
                builder.Configuration["PayOS:ClientId"],
                builder.Configuration["PayOS:ApiKey"],
                builder.Configuration["PayOS:ChecksumKey"]
            ));

            builder.Services.AddHttpContextAccessor();
            
            builder.Services.AddHttpClient("BypassSSL", client =>
            {
                // Cấu hình timeout nếu cần
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
                UseCookies = false // BẮT BUỘC ĐỂ KHÔNG BỊ NUỐT COOKIE CHUYỂN TIẾP TỪ TRÌNH DUYỆT
            });
            // Đăng ký các Dependency Injection
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IEmailService, EmailService>();
            builder.Services.AddHostedService<VipExpirationWorker>();
            builder.Services.AddScoped<IDauTruongTH2Service, DauTruongTH2Service>();
            builder.Services.AddSingleton<IHanziDecomposerService, HanziDecomposerService>();
            builder.Services.AddHttpClient<IGeminiHanTuService, GeminiHanTuService>();
            builder.Services.AddScoped<ITtsService, TtsService>();
            builder.Services.AddScoped<IAuditLogService, AuditLogService>();
            builder.Services.AddScoped<ITrungTamService, TrungTamService>();

            // Khai báo SignalR
            builder.Services.AddSignalR();

            var app = builder.Build();

            // --- 6. MIDDLEWARE PIPELINE (ĐÚNG THỨ TỰ) ---
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            // Bật WebSockets & CORS trước Authentication
            app.UseWebSockets();
            app.UseCors("AllowAll");

            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseMiddleware<HocTiengTrung_3._0_T.Middlewares.TenantMiddleware>();
            // Map Hub SignalR
            app.MapHub<DauTruongTH2Hub>("/dautruongTH2Hub");

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.MapControllers();

            app.Run();
        }
    }
}