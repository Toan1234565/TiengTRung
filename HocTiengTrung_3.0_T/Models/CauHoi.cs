using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class CauHoi
{
    public int IdCauHoi { get; set; }

    public int? IdLoai { get; set; }

    public int? IdCapDo { get; set; }

    public int? IdDoanVan { get; set; }

    public string? NoiDung { get; set; }

    public string? AmThanhPath { get; set; }

    public string? GiaiThich { get; set; }

    public bool? IsGeneratedOnly { get; set; }

    public int? DoKho { get; set; }

    public bool? TrangThai { get; set; }

    public int? IdTrungTam { get; set; }

    public virtual ICollection<BaiHocCauHoi> BaiHocCauHois { get; set; } = new List<BaiHocCauHoi>();

    public virtual ICollection<ChiTietLichSu> ChiTietLichSus { get; set; } = new List<ChiTietLichSu>();

    public virtual ICollection<DapAn> DapAns { get; set; } = new List<DapAn>();

    public virtual CapDo? IdCapDoNavigation { get; set; }

    public virtual DoanVan? IdDoanVanNavigation { get; set; }

    public virtual LoaiCauHoi? IdLoaiNavigation { get; set; }

    public virtual TrungTam? IdTrungTamNavigation { get; set; }

    public virtual ICollection<NguPhap> IdNguPhaps { get; set; } = new List<NguPhap>();

    public virtual ICollection<TuVung> IdTuVungs { get; set; } = new List<TuVung>();
}
