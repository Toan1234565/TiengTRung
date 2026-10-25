using HocTiengTrung_3._0_T.Models;

namespace HocTiengTrung_3._0_T.Models12.BaiTap
{
    public class LoaiCauHoiModels
    {
        public int IdLoai { get; set; }

        public string? TenLoai { get; set; }

        public virtual ICollection<CauHoiModels> CauHois { get; set; } = new List<CauHoiModels>();
    }
}
