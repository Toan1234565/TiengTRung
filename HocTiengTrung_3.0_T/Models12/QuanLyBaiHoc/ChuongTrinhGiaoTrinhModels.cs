namespace HocTiengTrung_3._0_T.Models12.QuanLyBaiHoc
{
    public class ChuongTrinhGiaoTrinhModels
    {
        public int IdGiaoTrinh { get; set; }

        public string TenChuongTrinh { get; set; } = null!;

        public string TenGiaoTrinh { get; set; } = null!;

        public string? MoTa { get; set; }

        public bool? TrangThai { get; set; }
    }
}
