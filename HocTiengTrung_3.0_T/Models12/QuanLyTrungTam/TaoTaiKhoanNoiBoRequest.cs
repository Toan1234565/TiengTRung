namespace HocTiengTrung_3._0_T.Models12.QuanLyTaiKhoan
{
    public class TaoTaiKhoanNoiBoRequest
    {
        public string TenDangNhap { get; set; } = null!;
        public string MatKhau { get; set; } = null!;
        public string HoTen { get; set; } = null!;
        public string? Email { get; set; }
        public string? SoDienThoai { get; set; }
        public int IdVaiTro { get; set; } // Giảng viên, Học viên,...
    }
}