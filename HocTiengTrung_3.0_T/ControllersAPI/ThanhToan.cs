using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12.QuanLyTaiKhoan;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// SỬ DỤNG NAMESPACE CHUẨN CỦA THƯ VIỆN PAYOS
using PayOSLib = PayOS;
using PayOSModels = PayOS.Models;
using PayOS.Models;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace HocTiengTrung_3._0_T.ControllersAPI
{
    // DTO nhận dữ liệu đăng ký mua gói VIP từ Client
    public class DangKyMuaVipDTO
    {
        public int IdGoi { get; set; }
    }

    [ApiController]
    [Route("api/thanh-toan")]
    public class ThanhToanController : ControllerBase
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ThanhToanController> _logger;
        private readonly PayOSLib.PayOSClient _payOS;
        private readonly IConfiguration _config;

        public ThanhToanController(
            TmdtContext context,
            IMemoryCache cache,
            ILogger<ThanhToanController> logger,
            IConfiguration configuration)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _config = configuration;

            // Khởi tạo PayOSClient từ AppSettings
            _payOS = new PayOSLib.PayOSClient(
                _config["PayOS:ClientId"],
                _config["PayOS:ApiKey"],
                _config["PayOS:ChecksumKey"]
            );
        }

        /// <summary>
        /// Lấy danh sách bảng giá đang hoạt động (có Caching)
        /// </summary>
        [HttpGet("so-tien-thanh-toan")]
        public async Task<ActionResult> SoTienThanhToan()
        {
            string cacheKey = "danhSachBangGia";
            if (_cache.TryGetValue(cacheKey, out IEnumerable<BangGiaTaiKhoanModels>? danhSach))
            {
                return Ok(danhSach);
            }

            try
            {
                danhSach = await _context.BangGiaTaiKhoans
                    .Where(x => x.TrangThai == true)
                    .Select(x => new BangGiaTaiKhoanModels
                    {
                        IdGoi = x.IdGoi,
                        TenGoi = x.TenGoi,
                        SoTien = x.SoTien,
                        SoThang = x.SoThang,
                        MoTa = x.MoTa
                    })
                    .ToListAsync();

                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2));

                _cache.Set(cacheKey, danhSach, cacheEntryOptions);
                return Ok(danhSach);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách bảng giá từ Database.");
                return StatusCode(500, new { message = "Lỗi hệ thống máy chủ." });
            }
        }

        /// <summary>
        /// Đăng ký mua gói VIP dựa trên gói được chọn (IdGoi)
        /// </summary>
        [HttpPost("dang-ky-mua")]
        public async Task<IActionResult> DangKyMuaVip([FromBody] DangKyMuaVipDTO dto)
        {
            // 1. Kiểm tra đăng nhập & Parse ID an toàn
            var idTaiKhoanStr = HttpContext.Session.GetString("IdTaiKhoan") ?? User.FindFirst("IdTaiKhoan")?.Value;
            if (string.IsNullOrEmpty(idTaiKhoanStr) || !int.TryParse(idTaiKhoanStr, out int idTaiKhoan))
            {
                return Unauthorized(new { success = false, message = "Vui lòng đăng nhập lại." });
            }

            // 2. Tìm chính xác gói VIP theo IdGoi truyền từ Frontend
            var goiVip = await _context.BangGiaTaiKhoans
                .FirstOrDefaultAsync(g => g.IdGoi == dto.IdGoi && g.TrangThai == true);

            if (goiVip == null)
            {
                return BadRequest(new { success = false, message = "Gói VIP không tồn tại hoặc đã dừng áp dụng." });
            }

            long tongTienThucTe = (long)goiVip.SoTien;

            // 3. Sinh orderCode an toàn (Nối chuỗi 3 số ngẫu nhiên vào cuối để tránh trùng)
            long orderCode = long.Parse(DateTime.Now.ToString("yyMMddHHmmss")) * 1000 + Random.Shared.Next(100, 999);
            string maGiaoDichNgoai = orderCode.ToString();

            // 4. Lưu đơn hàng tạm thời
            var giaoDichMoi = new GiaoDich
            {
                IdTaiKhoan = idTaiKhoan,
                IdGoi = goiVip.IdGoi,
                NgayGiaoDich = DateTime.Now,
                NoiDung = $"Đăng ký gói {goiVip.TenGoi}",
                TrangThai = "Chờ thanh toán",
                MaGiaoDich = maGiaoDichNgoai
            };

            _context.GiaoDiches.Add(giaoDichMoi);
            await _context.SaveChangesAsync();

            // 5. Chuẩn bị request cổng PayOS
            var returnUrl = _config["PayOSSettings:ReturnUrl"] ?? _config["PayOS:ReturnUrl"];
            var cancelUrl = _config["PayOSSettings:CancelUrl"] ?? _config["PayOS:CancelUrl"];

            var paymentRequest = new CreatePaymentLinkRequest
            {
                OrderCode = orderCode,
                Amount = (int)tongTienThucTe,
                Description = $"TT HSK goi {goiVip.IdGoi}", // Chuỗi ngắn <25 ký tự, không dấu
                CancelUrl = cancelUrl,
                ReturnUrl = returnUrl
            };

            try
            {
                var paymentLink = await _payOS.PaymentRequests.CreateAsync(paymentRequest);
                return Ok(new { success = true, checkoutUrl = paymentLink.CheckoutUrl, maGiaoDich = maGiaoDichNgoai });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo link thanh toán PayOS");
                return StatusCode(500, new { success = false, message = "Lỗi kết nối PayOS, vui lòng thử lại." });
            }
        }

        /// <summary>
        /// Webhook nhận thông báo tự động từ PayOS
        /// </summary>
        [HttpPost("webhook-payos")]
        public async Task<IActionResult> WebhookPayOS([FromBody] Webhook webhookBody)
        {
            // 1. Kiểm tra body rỗng
            if (webhookBody == null)
            {
                return Ok(new { success = true, message = "Ping thử nghiệm thành công" });
            }

            // 2. Xử lý Ping kích hoạt URL trên Webhook Dashboard PayOS
            if (webhookBody.Code == "00" && (webhookBody.Data == null || string.IsNullOrEmpty(webhookBody.Signature)))
            {
                _logger.LogInformation("Nhận request test ping kích hoạt từ PayOS.");
                return Ok(new { success = true, message = "Webhook active successfully" });
            }

            try
            {
                // 3. Xác thực tính hợp lệ của chữ ký Webhook
                var data = await _payOS.Webhooks.VerifyAsync(webhookBody);

                // Nếu là dữ liệu thử nghiệm
                if (data.Description == "ma giao dich thu nghiem" || data.OrderCode == 0)
                {
                    return Ok(new { success = true });
                }

                _logger.LogInformation($"Nhận webhook PayOS hợp lệ cho đơn hàng {data.OrderCode}");

                string maGiaoDichNgoai = data.OrderCode.ToString();

                // Lấy đơn hàng đang chờ thanh toán
                var giaoDich = await _context.GiaoDiches
                    .Include(g => g.IdGoiNavigation)
                    .FirstOrDefaultAsync(g => g.MaGiaoDich == maGiaoDichNgoai && g.TrangThai == "Chờ thanh toán");

                if (giaoDich == null)
                {
                    // Trả về 200 OK để PayOS biết đơn này đã xử lý xong, không gửi lại nữa
                    return Ok(new { success = true, message = "Giao dịch không tồn tại hoặc đã được xử lý trước đó." });
                }

                // Kiểm tra số tiền nhận về có đủ không
                if (data.Amount < giaoDich.IdGoiNavigation?.SoTien)
                {
                    _logger.LogWarning($"Khách hàng thanh toán thiếu tiền cho đơn {maGiaoDichNgoai}. Yêu cầu: {giaoDich.IdGoiNavigation?.SoTien}, Thực tế: {data.Amount}");
                    return Ok(new { success = true, message = "Số tiền không đủ" });
                }

                // 4. Cập nhật trạng thái giao dịch & Kích hoạt/Gia hạn VIP
                giaoDich.TrangThai = "Thành công";

                var taiKhoan = await _context.TaiKhoans.FirstOrDefaultAsync(t => t.IdTaiKhoan == giaoDich.IdTaiKhoan);

                if (taiKhoan != null)
                {
                    int soThangCong = giaoDich.IdGoiNavigation?.SoThang ?? 1;
                    var homNay = DateTime.Now;

                    taiKhoan.IsVip = true;

                    // Nếu VIP vẫn còn hạn thì cộng dồn số tháng
                    if (taiKhoan.NgayKetThucVip.HasValue && taiKhoan.NgayKetThucVip.Value > homNay)
                    {
                        taiKhoan.NgayKetThucVip = taiKhoan.NgayKetThucVip.Value.AddMonths(soThangCong);
                    }
                    else
                    {
                        taiKhoan.NgayBatDauVip = homNay;
                        taiKhoan.NgayKetThucVip = homNay.AddMonths(soThangCong);
                    }
                }

                await _context.SaveChangesAsync();

                // 5. Xóa Cache danh sách tài khoản VIP
                _cache.Remove("danhSachNguoiDung_vip_all");
                _cache.Remove("danhSachNguoiDung_vip_true");
                _cache.Remove("danhSachNguoiDung_vip_false");

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi xử lý webhook PayOS: {ex.Message}");
                // Trả về Ok() để PayOS không lưu lỗi liên tục trên Dashboard
                return Ok(new { success = false, message = "Xác thực thất bại nhưng đã phản hồi 200." });
            }
        }

        /// <summary>
        /// Client Polling kiểm tra trạng thái thanh toán đơn hàng
        /// </summary>
        [HttpGet("kiem-tra-trang-thai/{maGiaoDich}")]
        public async Task<IActionResult> KiemTraTrangThai(string maGiaoDich)
        {
            var giaoDich = await _context.GiaoDiches
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.MaGiaoDich == maGiaoDich);

            return Ok(new
            {
                isPaid = (giaoDich?.TrangThai == "Thành công"),
                trangThai = giaoDich?.TrangThai ?? "Không tìm thấy"
            });
        }
    


    }
}