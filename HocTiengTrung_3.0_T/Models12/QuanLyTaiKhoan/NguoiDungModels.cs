using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12.ThanhTich;

namespace HocTiengTrung_3._0_T.Models12.QuanLyTaiKhoan
{
    public class NguoiDungModels
    {
        public int IdUser { get; set; }

        public int? IdTaiKhoan { get; set; }

        public string? TenTrungTam { get; set; }

        public string? HoTen { get; set; }

        public string? Email { get; set; }

        public string? SoDienThoai { get; set; }

        public DateOnly? NgaySinh { get; set; }

        public string? AnhDaiDien { get; set; }

        public bool IsVip { get; set; }

        public DateTime? NgayBatDauVip { get; set; }

        public DateTime? NgayKetThucVip { get; set; }

        public virtual ICollection<TuVungNguoiDungModels> DanhSachTuVungs { get; set; } = new List<TuVungNguoiDungModels>();

        public virtual TaiKhoanModels? IdTaiKhoanNavigation { get; set; }

    }
}
