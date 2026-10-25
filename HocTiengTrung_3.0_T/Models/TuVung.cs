using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class TuVung
{
    public int IdTuVung { get; set; }

    public string? ChuHan { get; set; }

    public string? Pinyin { get; set; }

    public string? NghiaViet { get; set; }

    public string? LoaiTu { get; set; }

    public string? AmThanhPath { get; set; }

    public int? IdCapDo { get; set; }

    public string? AmHan { get; set; }

    public virtual ICollection<BaiHocTuVung> BaiHocTuVungs { get; set; } = new List<BaiHocTuVung>();

    public virtual ICollection<CauViDu> CauViDus { get; set; } = new List<CauViDu>();

    public virtual CapDo? IdCapDoNavigation { get; set; }

    public virtual ICollection<TuVungNguoiDung> TuVungNguoiDungs { get; set; } = new List<TuVungNguoiDung>();

    public virtual ICollection<CauHoi> IdCauHois { get; set; } = new List<CauHoi>();
}
