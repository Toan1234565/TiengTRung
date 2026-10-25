using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class BangGiaTaiKhoan
{
    public int IdGoi { get; set; }

    public string TenGoi { get; set; } = null!;

    public decimal SoTien { get; set; }

    public int SoThang { get; set; }

    public string? MoTa { get; set; }

    public bool? TrangThai { get; set; }

    public DateTime? NgayTao { get; set; }

    public virtual ICollection<GiaoDich> GiaoDiches { get; set; } = new List<GiaoDich>();
}
