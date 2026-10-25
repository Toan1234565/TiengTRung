using HocTiengTrung_3._0_T.Models;

namespace HocTiengTrung_3._0_T.Models12.QuanLyLuyenNghe
{
    public class VideoLuyenNgheModels
    {
        public int IdVideo { get; set; }

        public string TieuDe { get; set; } = null!;

        public string? VideoPath { get; set; }

        public int? IdCapDo { get; set; }

        public string? MoTa { get; set; }

        public string? TenCapDo { get; set; }

        public DateTime? NgayTao { get; set; }

        public virtual CapDo? IdCapDoNavigation { get; set; }

        public virtual ICollection<VideoSubtitleModels> VideoSubtitles { get; set; } = new List<VideoSubtitleModels>();
    }
}
