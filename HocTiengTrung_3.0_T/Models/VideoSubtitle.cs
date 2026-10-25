using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class VideoSubtitle
{
    public int IdSub { get; set; }

    public int IdVideo { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string? HanTu { get; set; }

    public string? Pinyin { get; set; }

    public string? DichNghia { get; set; }

    public int? ThuTu { get; set; }

    public virtual VideoLuyenNghe IdVideoNavigation { get; set; } = null!;
}
