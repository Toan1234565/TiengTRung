namespace HocTiengTrung_3._0_T.Models12.QuanLyLichSu
{
    public class LichSuThayDoiModels
    {
        public long IdLichSu { get; set; }

        public int? IdTaiKhoan { get; set; }

        public string? TenTaiKhoan { get; set; }

        public string? TenNguoiDung { get; set; }

        public string HanhDong { get; set; } = null!;

        public string? DuLieuCu { get; set; }

        public string? DuLieuMoi { get; set; }

        public DateTime ThoiGian { get; set; }

        public string? DiaChiIp { get; set; }

        public string? UserAgent { get; set; }

        public string? MoTa { get; set; }
    }
}
