using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class TaiLieu
{
    public int IdTaiLieu { get; set; }

    public string TenTaiLieu { get; set; } = null!;

    public string? MoTa { get; set; }

    public string DuongLink { get; set; } = null!;

    public string? HinhAnh { get; set; }

    public int IdCapDo { get; set; }

    public DateTime NgayTao { get; set; }

    public bool TrangThai { get; set; }

    public virtual CapDo IdCapDoNavigation { get; set; } = null!;
}
