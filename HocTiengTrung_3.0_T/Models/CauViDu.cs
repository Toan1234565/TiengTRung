using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class CauViDu
{
    public int IdViDu { get; set; }

    public int? IdTuVung { get; set; }

    public string? NoiDungHan { get; set; }

    public string? NoiDungPinyin { get; set; }

    public string? NoiDungViet { get; set; }

    public string? AmThanhPath { get; set; }

    public virtual TuVung? IdTuVungNavigation { get; set; }
}
