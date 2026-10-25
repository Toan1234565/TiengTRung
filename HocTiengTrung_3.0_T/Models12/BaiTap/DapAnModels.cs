using HocTiengTrung_3._0_T.Models;

namespace HocTiengTrung_3._0_T.Models12.BaiTap
{
    public class DapAnModels
    {
        public int IdDapAn { get; set; }

        public int? IdCauHoi { get; set; }

        public string? NoiDungDapAn { get; set; }

        public bool LaDapAnDung { get; set; }

        public virtual CauHoiModels? IdCauHoiNavigation { get; set; }
    }
}
