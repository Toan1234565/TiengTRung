namespace HocTiengTrung_3._0_T.Models12.QuanLyLuyenNghe
{
    public class VideoLuyenNgheUploadRequest
    {
        public int IdVideo { get; set; }
        public string TieuDe { get; set; }
        public int IdCapDo { get; set; }
        public string? MoTa { get; set; }

        // Nhận file từ người dùng
        public IFormFile? VideoFile { get; set; }

        // Vì FormData không gửi trực tiếp List<Object> phức tạp được, 
        // chúng ta nhận chuỗi JSON và sẽ Deserialize sau.
        public string? SubtitlesJson { get; set; }
    }
}
