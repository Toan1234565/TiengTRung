using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class LichSuTruyCap
{
    public int IdTruyCap { get; set; }

    public int? IdTaiKhoan { get; set; }

    public DateTime? ThoiGian { get; set; }

    public string? DiaChiIp { get; set; }

    public string? ThietBi { get; set; }

    public virtual TaiKhoan? IdTaiKhoanNavigation { get; set; }
}
