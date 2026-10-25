using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class HocVienLopHoc
{
    public int IdUser { get; set; }

    public int IdLopHoc { get; set; }

    public DateTime? NgayThamGia { get; set; }

    public virtual LopHoc IdLopHocNavigation { get; set; } = null!;

    public virtual NguoiDung IdUserNavigation { get; set; } = null!;
}
