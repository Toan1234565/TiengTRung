using System;

namespace HocTiengTrung_3._0_T.Models12.QuanLyTrungTam
{
    public class NhanSuTrungTamModel
    {
        public int IdTaiKhoan { get; set; }
        public int IdUser { get; set; }
        public string TenDangNhap { get; set; } = null!;
        public string HoTen { get; set; } = null!;
        public string? Email { get; set; }
        public string? SoDienThoai { get; set; }
        public string? AnhDaiDien { get; set; }
        public int? IdVaiTro { get; set; }
        public string TenVaiTro { get; set; } = null!;
        public bool TrangThai { get; set; }
        public DateTime? NgayTao { get; set; }
    }
}