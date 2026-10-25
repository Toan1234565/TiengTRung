using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class GiangVienLopHoc
{
    public int IdLopHoc { get; set; }

    public int IdTaiKhoan { get; set; }

    public string? VaiTroDay { get; set; }

    public DateTime? NgayPhanCong { get; set; }

    public virtual LopHoc IdLopHocNavigation { get; set; } = null!;

    public virtual TaiKhoan IdTaiKhoanNavigation { get; set; } = null!;
}
