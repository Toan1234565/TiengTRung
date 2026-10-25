using HocTiengTrung_3._0_T.Models;

namespace HocTiengTrung_3._0_T.Models12.QuanLyDeThi
{
    public class DeThiModels
    {
        public int IdDeThi { get; set; }

        public string TenDe { get; set; } = null!;

        public int? ThoiGianLamBai { get; set; }

        public int? TongSoCau { get; set; }

        public int? IdCapDo { get; set; }

        public DateTime? NgayTao { get; set; }

        public bool? TrangThai { get; set; }

        public int? ThoiGianNghe { get; set; }

        public int? ThoiGianDoc { get; set; }

        public int? ThoiGianViet { get; set; }

        public int? ThoiGianChuyenTiep { get; set; }

        public bool? IsPremium { get; set; }

        public virtual ICollection<ChiTietDeThiModels> ChiTietDeThis { get; set; } = new List<ChiTietDeThiModels>();

        public virtual CapDo? IdCapDoNavigation { get; set; }
    }
}
