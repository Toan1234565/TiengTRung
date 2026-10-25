using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class LoaiDeThi
{
    public int IdLoai { get; set; }

    public string TenLoai { get; set; } = null!;

    public string? MoTa { get; set; }

    public virtual ICollection<ChiTietDeThi> ChiTietDeThis { get; set; } = new List<ChiTietDeThi>();
}
