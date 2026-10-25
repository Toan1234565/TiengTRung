using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class LopHoc
{
    public int IdLopHoc { get; set; }

    public int IdTrungTam { get; set; }

    public string TenLopHoc { get; set; } = null!;

    public string? MoTa { get; set; }

    public DateOnly? NgayKhaiGiang { get; set; }

    public bool TrangThai { get; set; }

    public DateOnly? NgayKetThuc { get; set; }

    public string? MaLopHoc { get; set; }

    public virtual ICollection<GiangVienLopHoc> GiangVienLopHocs { get; set; } = new List<GiangVienLopHoc>();

    public virtual ICollection<HocVienLopHoc> HocVienLopHocs { get; set; } = new List<HocVienLopHoc>();

    public virtual TrungTam IdTrungTamNavigation { get; set; } = null!;
}
