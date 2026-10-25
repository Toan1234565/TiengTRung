using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class BaiHocCauHoi
{
    public int IdBaiHoc { get; set; }

    public int IdCauHoi { get; set; }

    public int ThuTu { get; set; }

    public virtual BaiHoc IdBaiHocNavigation { get; set; } = null!;

    public virtual CauHoi IdCauHoiNavigation { get; set; } = null!;
}
