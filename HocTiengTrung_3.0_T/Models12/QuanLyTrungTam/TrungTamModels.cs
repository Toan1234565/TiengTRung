namespace HocTiengTrung_3._0_T.Models12.QuanLyTrungTam
{
    public class TrungTamModels
    {
        public int IdTrungTam { get; set; }

        public string TenTrungTam { get; set; } = null!;

        public string? MaTrungTam { get; set; }

        public string? Subdomain { get; set; }

        public string? SoDienThoai { get; set; }

        public string? Email { get; set; }

        public string? DiaChi { get; set; }

        public string? Logo { get; set; }

        public DateTime? NgayKichHoat { get; set; }

        public DateTime? NgayHetHan { get; set; }

        public bool TrangThai { get; set; }
    }
}
