using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class VaiTro
{
    public int IdVaiTro { get; set; }

    public string TenVaiTro { get; set; } = null!;

    public virtual ICollection<TaiKhoan> TaiKhoans { get; set; } = new List<TaiKhoan>();
}
