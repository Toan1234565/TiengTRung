namespace HocTiengTrung_3._0_T.Models12.QuanLyTaiKhoan
{
    public class RegisterRequest
    {
        public string TenDangNhap { get; set; } = null!;
        public string MatKhau { get; set; } = null!;
        public string HoTen { get; set; } = null!; // Thêm trường này
    }
}
