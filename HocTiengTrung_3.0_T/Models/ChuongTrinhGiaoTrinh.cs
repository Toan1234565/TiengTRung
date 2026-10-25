using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class ChuongTrinhGiaoTrinh
{
    public int IdGiaoTrinh { get; set; }

    public string TenChuongTrinh { get; set; } = null!;

    public string TenGiaoTrinh { get; set; } = null!;

    public string? MoTa { get; set; }

    public bool? TrangThai { get; set; }

    public virtual ICollection<BaiHoc> BaiHocs { get; set; } = new List<BaiHoc>();
}
