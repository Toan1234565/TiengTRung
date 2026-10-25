using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12.BaiTap;

namespace HocTiengTrung_3._0_T.Models12.QuanLyBaiHoc
{
    public class BaiHocCauHoiModel
    {
        public int IdBaiHoc { get; set; }

        public int IdCauHoi { get; set; }

        public int ThuTu { get; set; }

        public virtual CauHoiModels IdCauHoiNavigation { get; set; } = null!;
    }
}
