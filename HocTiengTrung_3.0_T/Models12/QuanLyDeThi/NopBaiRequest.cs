namespace HocTiengTrung_3._0_T.Models12.QuanLyDeThi
{
    public class NopBaiRequest
    {
        public int ExamId { get; set; }
        public string? HoTen { get; set; } // Dành cho khách làm bài không cần đăng nhập
        public DateTime StartTime { get; set; }

        // Dictionary lưu đáp án: Key là ThuTu (Số thứ tự câu), Value là Đáp án (A, B, C...)
        public Dictionary<int, string> UserAnswers { get; set; } = new Dictionary<int, string>();
    }
}
