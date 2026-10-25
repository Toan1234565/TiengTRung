using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class GhiChu
{
    public int IdGhiChu { get; set; }

    public int IdUser { get; set; }

    public string? TieuDe { get; set; }

    public string TenFile { get; set; } = null!;

    public string DuongLinkFile { get; set; } = null!;

    public long? KichThuocFile { get; set; }

    public DateTime? NgayTao { get; set; }

    public DateTime? NgayCapNhat { get; set; }

    public virtual NguoiDung IdUserNavigation { get; set; } = null!;
}
