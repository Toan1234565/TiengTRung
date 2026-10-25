using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class ChiTietBaiLam
{
    public int IdChiTietKetQua { get; set; }

    public int IdKetQua { get; set; }

    public int IdChiTietDeThi { get; set; }

    public string? DapAnChon { get; set; }

    public bool? LaDapAnDung { get; set; }

    public virtual ChiTietDeThi IdChiTietDeThiNavigation { get; set; } = null!;

    public virtual KetQuaThi IdKetQuaNavigation { get; set; } = null!;
}
