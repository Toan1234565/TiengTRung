using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class BaiHocTuVung
{
    public int IdBaiHoc { get; set; }

    public int IdTuVung { get; set; }

    public bool LaTuMoi { get; set; }

    public int ThuTu { get; set; }

    public virtual BaiHoc IdBaiHocNavigation { get; set; } = null!;

    public virtual TuVung IdTuVungNavigation { get; set; } = null!;
}
