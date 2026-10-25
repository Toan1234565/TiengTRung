using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class LichSuLamBai
{
    public int IdLichSu { get; set; }

    public int IdNguoiDung { get; set; }

    public string? TenDe { get; set; }

    public int? IdCapDo { get; set; }

    public int? IdLoai { get; set; }

    public int? SoCauDung { get; set; }

    public int? TongSoCau { get; set; }

    public DateTime? NgayLam { get; set; }

    public string? DanhSachIdCauHoi { get; set; }

    public virtual ICollection<ChiTietLichSu> ChiTietLichSus { get; set; } = new List<ChiTietLichSu>();

    public virtual CapDo? IdCapDoNavigation { get; set; }

    public virtual LoaiCauHoi? IdLoaiNavigation { get; set; }

    public virtual NguoiDung IdNguoiDungNavigation { get; set; } = null!;
}
