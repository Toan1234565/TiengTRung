using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class DapAn
{
    public int IdDapAn { get; set; }

    public int? IdCauHoi { get; set; }

    public string? NoiDungDapAn { get; set; }

    public bool? LaDapAnDung { get; set; }

    public virtual CauHoi? IdCauHoiNavigation { get; set; }
}
