using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class KetQuaThi
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

    public virtual ICollection<ChiTietBaiLam> ChiTietBaiLams { get; set; } = new List<ChiTietBaiLam>();

    public virtual DeThi IdDeThiNavigation { get; set; } = null!;

    public virtual NguoiDung? IdUserNavigation { get; set; }
}
