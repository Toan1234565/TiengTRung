using HocTiengTrung_3._0_T.Models;

namespace HocTiengTrung_3._0_T.Models12.QuanLyLuyenNghe
{
    public class VideoLuyenNgheRequest
    {
        public int IdVideo { get; set; }

        public string TieuDe { get; set; } = null!;

        public string? VideoPath { get; set; }

        public int? IdCapDo { get; set; }

        public string? MoTa { get; set; }

        public string? TenCapDo { get; set; }

        public DateTime? NgayTao { get; set; }

        // Nhận file từ người dùng
        public IFormFile? VideoFile { get; set; }

        // Vì FormData không gửi trực tiếp List<Object> phức tạp được, 
        // chúng ta nhận chuỗi JSON và sẽ Deserialize sau.
        public string? SubtitlesJson { get; set; }
    }
}
