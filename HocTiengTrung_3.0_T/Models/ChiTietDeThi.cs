using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class ChiTietDeThi
{
    public int IdChiTiet { get; set; }

    public int? IdDeThi { get; set; }

    public int? ThuTu { get; set; }

    public string? NoiDungJson { get; set; }

    public string? AmThanhPath { get; set; }

    public string? HinhAnhPath { get; set; }

    public string? DapAnDung { get; set; }

    public int? IdLoai { get; set; }

    public string? GiaiThich { get; set; }

    public int? KyNang { get; set; }

    public string? GhiChu { get; set; }

    public virtual ICollection<ChiTietBaiLam> ChiTietBaiLams { get; set; } = new List<ChiTietBaiLam>();

    public virtual DeThi? IdDeThiNavigation { get; set; }

    public virtual LoaiDeThi? IdLoaiNavigation { get; set; }
}
