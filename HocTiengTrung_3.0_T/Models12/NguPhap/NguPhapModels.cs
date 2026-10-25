using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12.BaiTap;

namespace HocTiengTrung_3._0_T.Models12.NguPhap
{
    public class NguPhapModels
    {
        public int IdNguPhap { get; set; }

        public string TenNguPhap { get; set; } = null!;

        public string? CauTruc { get; set; }

        public string? GiaiThich { get; set; }

        public string? ViDu { get; set; }

        public int? IdCapDo { get; set; }

        public virtual ICollection<CauHoiModels> SelectedCauHoiIds { get; set; } = new List<CauHoiModels>();
    }
}
