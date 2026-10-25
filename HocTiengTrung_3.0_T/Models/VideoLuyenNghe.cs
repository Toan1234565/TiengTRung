using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class VideoLuyenNghe
{
    public int IdVideo { get; set; }

    public string TieuDe { get; set; } = null!;

    public string? VideoPath { get; set; }

    public int? IdCapDo { get; set; }

    public string? MoTa { get; set; }

    public DateTime? NgayTao { get; set; }

    public virtual CapDo? IdCapDoNavigation { get; set; }

    public virtual ICollection<VideoSubtitle> VideoSubtitles { get; set; } = new List<VideoSubtitle>();
}
