using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class CapDo
{
    public int IdCapDo { get; set; }

    public string TenCapDo { get; set; } = null!;

    public bool? TrangThai { get; set; }

    public virtual ICollection<BaiHoc> BaiHocs { get; set; } = new List<BaiHoc>();

    public virtual ICollection<CauHoi> CauHois { get; set; } = new List<CauHoi>();

    public virtual ICollection<DeThi> DeThis { get; set; } = new List<DeThi>();

    public virtual ICollection<LichSuLamBai> LichSuLamBais { get; set; } = new List<LichSuLamBai>();

    public virtual ICollection<NguPhap> NguPhaps { get; set; } = new List<NguPhap>();

    public virtual ICollection<TaiLieu> TaiLieus { get; set; } = new List<TaiLieu>();

    public virtual ICollection<TuVung> TuVungs { get; set; } = new List<TuVung>();

    public virtual ICollection<VideoLuyenNghe> VideoLuyenNghes { get; set; } = new List<VideoLuyenNghe>();
}
