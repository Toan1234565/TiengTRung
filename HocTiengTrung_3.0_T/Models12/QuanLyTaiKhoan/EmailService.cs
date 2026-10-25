using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace HocTiengTrung_3._0_T.Models12.QuanLyTaiKhoan
{
    public interface IEmailService
    {
        Task SendForgotPasswordEmailAsync(string emailNhan, string tenNhan, string tenDangNhap, string matKhauMoi);
    }
    public class EmailService : IEmailService
    {
        private const string AdminEmail = "nguuentoanbs2k4@gmail.com";

        private const string AdminPassword = "nadf mrjb rxqm jtxx";

        public async Task SendForgotPasswordEmailAsync(string emailNhan, string tenNhan, string tenDangNhap, string matKhauMoi)
        {
            var email = new MimeKit.MimeMessage();
            email.From.Add(MimeKit.MailboxAddress.Parse(AdminEmail));
            email.To.Add(MimeKit.MailboxAddress.Parse(emailNhan));
            email.Subject = "[HỆ THỐNG] Khôi phục mật khẩu tài khoản";

            var builder = new MimeKit.BodyBuilder
            {
                HtmlBody = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; border: 1px solid #ddd; padding: 20px;'>
                <h2 style='color: #28a745; text-align: center;'>KHÔI PHỤC MẬT KHẨU</h2>
                <p>Xin chào <strong>{tenNhan}</strong>,</p>
                <p>Hệ thống đã nhận được yêu cầu khôi phục mật khẩu. Thông tin tài khoản của bạn:</p>
                <div style='background: #f4f4f4; padding: 15px; border-radius: 5px;'>
                    <p><strong>Tên đăng nhập:</strong> {tenDangNhap}</p>
                    <p><strong>Mật khẩu mới:</strong> <span style='color: #d9534f; font-weight: bold;'>{matKhauMoi}</span></p>
                </div>
                <p style='margin-top: 20px;'>Vui lòng đăng nhập và đổi lại mật khẩu ngay lập tức.</p>
            </div>"
            };
            email.Body = builder.ToMessageBody();

            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            await smtp.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(AdminEmail, AdminPassword);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
    }
}
