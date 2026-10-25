using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class LuyenNgheNguoiDung
{
    public int IdUser { get; set; }

    public int IdChiTiet { get; set; }

    public bool DaThuoc { get; set; }

    public DateTime NgayDanhDau { get; set; }

    public virtual ChiTietLuyenNgheHinhAnh IdChiTietNavigation { get; set; } = null!;
}
