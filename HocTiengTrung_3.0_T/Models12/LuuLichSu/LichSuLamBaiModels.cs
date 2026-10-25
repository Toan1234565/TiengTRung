using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12.BaiTap;
using HocTiengTrung_3._0_T.Models12.QuanLyTaiKhoan;
using HocTiengTrung_3._0_T.Models12.TuVung;
using Microsoft.Identity.Client;

namespace HocTiengTrung_3._0_T.Models12.LuuLichSu
{
    public class LichSuLamBaiModels
    {
        public int IdLichSu { get; set; }

        public int IdNguoiDung { get; set; }

        public string? TenDe { get; set; }

        public int? IdCapDo { get; set; }

        public int? IdLoai { get; set; }

        public int? SoCauDung { get; set; }

        public int? TongSoCau { get; set; }

        public DateTime? NgayLam { get; set; }

        public string? DanhSachIdCauHoi { get; set; }

        public string? TenCapDo { get; set; }

        public virtual ICollection<ChiTietLichSuModels> ChiTietLichSus { get; set; } = new List<ChiTietLichSuModels>();

        public virtual CapDoModels? IdCapDoNavigation { get; set; }

        public virtual LoaiCauHoiModels? IdLoaiNavigation { get; set; }

        public virtual NguoiDungModels IdNguoiDungNavigation { get; set; } = null!;
        public string? TenLoai { get; internal set; }
    }
}
