using HocTiengTrung_3._0_T.Models;

namespace HocTiengTrung_3._0_T.Models12.QuanLyBaiHoc
{
    public class BaiHocModels
    {
        public int IdBaiHoc { get; set; }

        public int IdCapDo { get; set; }

        public int SoThuTuBai { get; set; }

        public string TenBaiHoc { get; set; } = null!;

        public string? MoTa { get; set; }

        public bool TrangThai { get; set; }

        public int? IdGiaoTrinh { get; set; }

        public int? IdTrungTam { get; set; }

        public virtual ICollection<BaiHocCauHoiModel> BaiHocCauHois { get; set; } = new List<BaiHocCauHoiModel>();

        public virtual ICollection<BaiHocTuVungModel> BaiHocTuVungs { get; set; } = new List<BaiHocTuVungModel>();
    }
}
