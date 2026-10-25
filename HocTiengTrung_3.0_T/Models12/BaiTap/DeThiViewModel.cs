namespace HocTiengTrung_3._0_T.Models12.BaiTap
{
    public class DeThiViewModel
    {
        public string TenDe { get; set; }
        public int SoLuongCauHoi { get; set; }
        public bool IsLocked { get; set; }
        public List<CauHoiModels> CauHois { get; set; }
        public List<int> DanhSachId { get; set; }
    }
}
