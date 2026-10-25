using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class TrungTam
{
    public int IdTrungTam { get; set; }

    public string TenTrungTam { get; set; } = null!;

    public string? MaTrungTam { get; set; }

    public string? Subdomain { get; set; }

    public string? SoDienThoai { get; set; }

    public string? Email { get; set; }

    public string? DiaChi { get; set; }

    public string? Logo { get; set; }

    public DateTime? NgayKichHoat { get; set; }

    public DateTime? NgayHetHan { get; set; }

    public bool TrangThai { get; set; }

    public virtual ICollection<BaiHoc> BaiHocs { get; set; } = new List<BaiHoc>();

    public virtual ICollection<CauHoi> CauHois { get; set; } = new List<CauHoi>();

    public virtual ICollection<DeThi> DeThis { get; set; } = new List<DeThi>();

    public virtual ICollection<LopHoc> LopHocs { get; set; } = new List<LopHoc>();

    public virtual ICollection<NguPhap> NguPhaps { get; set; } = new List<NguPhap>();

    public virtual ICollection<TaiKhoan> TaiKhoans { get; set; } = new List<TaiKhoan>();
}
