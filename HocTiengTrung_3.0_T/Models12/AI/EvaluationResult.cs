namespace HocTiengTrung_3._0_T.Models12.AI
{
    public class EvaluationResult
    {
        // Điểm số dựa trên thang 100 hoặc 10
        public double Score { get; set; }

        // Đánh giá xem câu dịch có phù hợp với cấp độ HSK yêu cầu không (True/False)
        public bool IsLevelAppropriate { get; set; }

        // Nhận xét chi tiết về ngữ pháp, từ vựng
        public string Comment { get; set; } = string.Empty;

        // Phương án dịch tối ưu nhất do AI đề xuất
        public string BestAlternative { get; set; } = string.Empty;

        // Chỉ ra các lỗi sai cụ thể (nếu có)
        public List<string> ErrorDetails { get; set; } = new List<string>();
    }
}
