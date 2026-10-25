namespace HocTiengTrung_3._0_T.Models12.QuanLyLuyenNghe
{
    public class VideoSubtitleRequest
    {
        public int IdSub { get; set; }

        public int IdVideo { get; set; }

        public TimeOnly? StartTime { get; set; }

        public TimeOnly? EndTime { get; set; }

        public string? HanTu { get; set; }

        public string? Pinyin { get; set; }

        public string? DichNghia { get; set; }

        public int? ThuTu { get; set; }

        public virtual VideoLuyenNgheRequest IdVideoNavigation { get; set; } = null!;
    }
}
