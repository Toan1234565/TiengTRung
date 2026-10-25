using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class DeThi
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

    public bool IsPremium { get; set; }

    public int? IdTrungTam { get; set; }

    public virtual ICollection<ChiTietDeThi> ChiTietDeThis { get; set; } = new List<ChiTietDeThi>();

    public virtual CapDo? IdCapDoNavigation { get; set; }

    public virtual TrungTam? IdTrungTamNavigation { get; set; }

    public virtual ICollection<KetQuaThi> KetQuaThis { get; set; } = new List<KetQuaThi>();
}
