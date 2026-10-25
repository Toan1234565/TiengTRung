namespace HocTiengTrung_3._0_T.Models12.QuanLyTaiKhoan
{
    public class UpdateProfileRequest
    {
        public string HoTen { get; set; }
        public string? Email { get; set; }
        public string? SoDienThoai { get; set; }
        public DateOnly? NgaySinh { get; set; }
        public string? AnhDaiDien { get; set; }
    }
}
