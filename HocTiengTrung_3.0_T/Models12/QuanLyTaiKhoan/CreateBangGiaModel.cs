namespace HocTiengTrung_3._0_T.Models12.QuanLyTaiKhoan
{
    public class CreateBangGiaModel
    {
        public string TenGoi { get; set; } = null!;
        public decimal SoTien { get; set; }
        public int SoThang { get; set; }
        public string? MoTa { get; set; }
    }

    public class UpdateBangGiaModel
    {
        public int IdGoi { get; set; }
        public string TenGoi { get; set; } = null!;
        public decimal SoTien { get; set; }
        public int SoThang { get; set; }
        public string? MoTa { get; set; }
    }
}
