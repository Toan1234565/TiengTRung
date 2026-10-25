namespace HocTiengTrung_3._0_T.Models12.QuanLyDeThi
{
    public class KetQuaThiViewModel
    {
        public int IdKetQua { get; set; }

        public int IdDeThi { get; set; }

        public int? IdUser { get; set; }

        public string? HoTen { get; set; }

        public int? SoCauDung { get; set; }

        public int TongSoCau { get; set; }

        public decimal? DiemSo { get; set; }

        public DateTime? ThoiGianBatDau { get; set; }

        public DateTime? ThoiGianHoanThanh { get; set; }

        public bool? TrangThai { get; set; }

        public string TenDe { get; set; } = null!;
    }
}
