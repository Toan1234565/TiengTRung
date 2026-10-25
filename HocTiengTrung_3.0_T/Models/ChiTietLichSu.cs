using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class ChiTietLichSu
{
    public int IdChiTiet { get; set; }

    public int IdLichSu { get; set; }

    public int IdCauHoi { get; set; }

    public string? CauTraLoiNguoiDung { get; set; }

    public bool? LaDung { get; set; }

    public virtual CauHoi IdCauHoiNavigation { get; set; } = null!;

    public virtual LichSuLamBai IdLichSuNavigation { get; set; } = null!;
}
