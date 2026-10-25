using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class ChuDe
{
    public int IdChuDe { get; set; }

    public string? TenChuDe { get; set; }

    public virtual ICollection<DoanVan> DoanVans { get; set; } = new List<DoanVan>();
}
