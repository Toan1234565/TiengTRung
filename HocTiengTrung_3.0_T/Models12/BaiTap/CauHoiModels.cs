using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12.TuVung;

namespace HocTiengTrung_3._0_T.Models12.BaiTap
{
    public class CauHoiModels
    {
        public int IdCauHoi { get; set; }

        public int? IdLoai { get; set; }

        public int? IdCapDo { get; set; }

        public int? IdDoanVan { get; set; }

        public string? NoiDung { get; set; }

        public string? AmThanhPath { get; set; }

        public string? GiaiThich { get; set; }

        public int? DoKho { get; set; }

        public virtual ICollection<DapAnModels> DapAns { get; set; } = new List<DapAnModels>();

        public virtual CapDoModels? IdCapDoNavigation { get; set; }

        public DoanVanModels? DoanVan { get; set; }

        public virtual LoaiCauHoiModels? IdLoaiNavigation { get; set; }

        public bool? IsGeneratedOnly { get; internal set; }
    }
}
