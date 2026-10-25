using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class LoaiCauHoi
{
    public int IdLoai { get; set; }

    public string? TenLoai { get; set; }

    public double? ChuongTrinh { get; set; }

    public virtual ICollection<CauHoi> CauHois { get; set; } = new List<CauHoi>();

    public virtual ICollection<LichSuLamBai> LichSuLamBais { get; set; } = new List<LichSuLamBai>();
}
