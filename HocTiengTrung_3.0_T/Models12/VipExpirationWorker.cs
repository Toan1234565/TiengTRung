using Microsoft.EntityFrameworkCore;
using HocTiengTrung_3._0_T.Models; // Thay đổi theo namespace chứa DbContext của bạn

namespace HocTiengTrung_3._0_T.Services
{
    public class VipExpirationWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<VipExpirationWorker> _logger;

        public VipExpirationWorker(IServiceScopeFactory scopeFactory, ILogger<VipExpirationWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Dịch vụ quét hạn VIP đã được khởi chạy ngầm.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;

                // Cấu hình mốc thời gian chạy là 12h đêm (00:00:00 ngày tiếp theo)
                var nextRun = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0).AddDays(1);
                // Nếu hôm nay đã qua 12:00 trưa rồi, hẹn giờ chạy vào 12:00 trưa ngày mai
                if (now >= nextRun)
                {
                    nextRun = nextRun.AddDays(1);
                }

                var delayTime = nextRun - now;
                _logger.LogInformation("Sẽ tiến hành quét hạn VIP tiếp theo sau: {DelayTime}", delayTime);

                // Đợi cho đến đúng 12:00 trưa
                await Task.Delay(delayTime, stoppingToken);

                // Khi đến giờ hẹn, thực hiện quét và cập nhật Database
                try
                {
                    _logger.LogInformation("Bắt đầu tiến trình quét tài khoản hết hạn VIP lúc: {Time}", DateTime.Now);

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        // 🛠️ HÃY THAY 'HocTiengTrungDbContext' thành tên DbContext chính xác trong dự án của bạn
                        var context = scope.ServiceProvider.GetRequiredService<TmdtContext>();

                        // Tìm các tài khoản đang là VIP (IsVip == true) và đã quá ngày kết thúc
                        var expiredUsers = await context.TaiKhoans
                            .Where(t => t.IsVip && t.NgayKetThucVip != null && t.NgayKetThucVip < DateTime.Now)
                            .ToListAsync(stoppingToken);

                        if (expiredUsers.Any())
                        {
                            foreach (var user in expiredUsers)
                            {
                                user.IsVip = false; // Vô hiệu hóa VIP
                                _logger.LogInformation("Tài khoản ID {Id} ({User}) đã bị hủy quyền VIP do hết hạn.", user.IdTaiKhoan, user.TenDangNhap);
                            }

                            // Lưu thay đổi xuống Database
                            await context.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation("Đã xử lý khóa VIP cho {Count} tài khoản.", expiredUsers.Count);
                        }
                        else
                        {
                            _logger.LogInformation("Không phát hiện tài khoản nào hết hạn VIP trong hôm nay.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Có lỗi xảy ra trong quá trình quét hạn VIP.");
                }
            }
        }
    }
}