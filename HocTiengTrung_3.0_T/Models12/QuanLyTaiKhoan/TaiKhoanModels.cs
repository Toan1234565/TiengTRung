using HocTiengTrung_3._0_T.Models;

namespace HocTiengTrung_3._0_T.Models12.QuanLyTaiKhoan
{
    public class TaiKhoanModels
    {
        public int IdTaiKhoan { get; set; }

        public string TenDangNhap { get; set; } = null!;

        public DateTime? NgayTao { get; set; }

        public bool IsVip { get; set; }

        public DateTime? NgayBatDauVip { get; set; }

        public DateTime? NgayKetThucVip { get; set; }

        public bool? TrangThai { get; set; }

        public string? MatKhau { get; set; }

        public int? MaVaiTro { get; set; }

        public string? TenVaiTro { get; set; }

        public string? TenTrungTam { get; set; }

        public virtual NguoiDungModels? NguoiDung { get; set; }

        public virtual ICollection<GiaoDichModels> GiaoDiches { get; set; } = new List<GiaoDichModels>();
        
    }
}
