using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class LuyenNgheHinhAnh
{
    public int IdLuyenNghe { get; set; }

    public string TieuDe { get; set; } = null!;

    public int IdCapDo { get; set; }

    public virtual ICollection<ChiTietLuyenNgheHinhAnh> ChiTietLuyenNgheHinhAnhs { get; set; } = new List<ChiTietLuyenNgheHinhAnh>();
}
