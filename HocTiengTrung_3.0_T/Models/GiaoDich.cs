using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class GiaoDich
{
    public int IdGiaoDich { get; set; }

    public int IdTaiKhoan { get; set; }

    public DateTime NgayGiaoDich { get; set; }

    public string? NoiDung { get; set; }

    public string TrangThai { get; set; } = null!;

    public string? MaGiaoDich { get; set; }

    public int? IdGoi { get; set; }

    public virtual BangGiaTaiKhoan? IdGoiNavigation { get; set; }

    public virtual TaiKhoan IdTaiKhoanNavigation { get; set; } = null!;
}
