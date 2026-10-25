using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12.BaiTap;

namespace HocTiengTrung_3._0_T.Models12.LuuLichSu
{
    public class ChiTietLichSuModels
    {
        public int IdChiTiet { get; set; }

        public int IdLichSu { get; set; }

        public int IdCauHoi { get; set; }

        public string? CauTraLoiNguoiDung { get; set; }

        public bool? LaDung { get; set; }

        public virtual CauHoiModels IdCauHoiNavigation { get; set; } = null!;

        public virtual LichSuLamBaiModels IdLichSuNavigation { get; set; } = null!;
    }
}
