using HocTiengTrung_3._0_T.Models;

namespace HocTiengTrung_3._0_T.Models12.QuanLyDeThi
{
    public class ChiTietDeThiModels
    {
        public int IdChiTiet { get; set; }

        public int? IdDeThi { get; set; }

        public int? ThuTu { get; set; }

        public string? NoiDungJson { get; set; }

        public string? AmThanhPath { get; set; }

        public string? HinhAnhPath { get; set; }

        public string? DapAnDung { get; set; }

        public int? IdLoai { get; set; }

        public string? GiaiThich { get; set; }

        public int? KyNang { get; set; }

        public virtual DeThiModels? IdDeThiNavigation { get; set; }

        public virtual LoaiDeThiModels? IdLoaiNavigation { get; set; }
        public string TenLoai { get; internal set; }
        public string? GhiChu { get; set; }
    }
}
