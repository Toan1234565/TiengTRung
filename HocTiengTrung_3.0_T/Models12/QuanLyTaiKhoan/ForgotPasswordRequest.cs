namespace HocTiengTrung_3._0_T.Models12.QuanLyTaiKhoan
{
    public class ForgotPasswordRequest
    {
        // Thêm thuộc tính này vào để API của bạn hết báo lỗi
        public string TenDangNhap { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
    }
}
