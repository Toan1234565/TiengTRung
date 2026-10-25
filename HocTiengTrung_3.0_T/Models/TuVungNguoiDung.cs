using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class TuVungNguoiDung
{
    public int IdUser { get; set; }

    public int IdTuVung { get; set; }

    public bool? DaThuoc { get; set; }

    public DateTime? NgayDanhDau { get; set; }

    public virtual TuVung IdTuVungNavigation { get; set; } = null!;

    public virtual NguoiDung IdUserNavigation { get; set; } = null!;
}
