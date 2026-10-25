using HocTiengTrung_3._0_T.Models;

namespace HocTiengTrung_3._0_T.Models12.QuanLyTaiKhoan
{
    public class GiaoDichModels
    {
        public int IdGiaoDich { get; set; }

        public int IdTaiKhoan { get; set; }

       
        public DateTime NgayGiaoDich { get; set; }

        public string? NoiDung { get; set; }

        public string TrangThai { get; set; } = null!;

        public string? MaGiaoDich { get; set; }

        public int? IdGoi { get; set; }

        public virtual BangGiaTaiKhoanModels? IdGoiNavigation { get; set; }

        public virtual TaiKhoanModels IdTaiKhoanNavigation { get; set; } = null!;
    }
}
