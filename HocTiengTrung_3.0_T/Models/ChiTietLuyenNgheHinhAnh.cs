using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class ChiTietLuyenNgheHinhAnh
{
    public int IdChiTiet { get; set; }

    public int IdLuyenNghe { get; set; }

    public string? FileAudio { get; set; }

    public string? HinhAnh { get; set; }

    public string? PhienAmPinyin { get; set; }

    public string? ChieuHanText { get; set; }

    public string? NghiaTiengViet { get; set; }

    public int? SoBai { get; set; }

    public virtual LuyenNgheHinhAnh IdLuyenNgheNavigation { get; set; } = null!;

    public virtual ICollection<LuyenNgheNguoiDung> LuyenNgheNguoiDungs { get; set; } = new List<LuyenNgheNguoiDung>();
}
