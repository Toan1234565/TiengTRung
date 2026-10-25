using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class NguPhap
{
    public int IdNguPhap { get; set; }

    public string TenNguPhap { get; set; } = null!;

    public string? CauTruc { get; set; }

    public string? GiaiThich { get; set; }

    public string? ViDu { get; set; }

    public int? IdCapDo { get; set; }

    public int? IdTrungTam { get; set; }

    public virtual CapDo? IdCapDoNavigation { get; set; }

    public virtual TrungTam? IdTrungTamNavigation { get; set; }

    public virtual ICollection<CauHoi> IdCauHois { get; set; } = new List<CauHoi>();
}
