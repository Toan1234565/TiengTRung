using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class BaiHoc
{
    public int IdBaiHoc { get; set; }

    public int IdCapDo { get; set; }

    public int SoThuTuBai { get; set; }

    public string TenBaiHoc { get; set; } = null!;

    public string? MoTa { get; set; }

    public bool TrangThai { get; set; }

    public int? IdGiaoTrinh { get; set; }

    public int? IdTrungTam { get; set; }

    public virtual ICollection<BaiHocCauHoi> BaiHocCauHois { get; set; } = new List<BaiHocCauHoi>();

    public virtual ICollection<BaiHocTuVung> BaiHocTuVungs { get; set; } = new List<BaiHocTuVung>();

    public virtual CapDo IdCapDoNavigation { get; set; } = null!;

    public virtual ChuongTrinhGiaoTrinh? IdGiaoTrinhNavigation { get; set; }

    public virtual TrungTam? IdTrungTamNavigation { get; set; }
}
