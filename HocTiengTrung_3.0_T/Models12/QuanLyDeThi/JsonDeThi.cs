namespace HocTiengTrung_3._0_T.Models12.QuanLyDeThi
{
    public class JsonDeThi
    {
        public class QuestionJsonBase { }

        // Loại 1: Nghe chọn tranh
        public class ListeningImageJson : QuestionJsonBase
        {
            public string? GhiChu { get; set; }
            public string? ViDu { get; set; }
            public Dictionary<string, string>? Options { get; set; }
        }

        // Loại 2: Đúng/Sai
        public class TrueFalseJson : QuestionJsonBase
        {
            public string? CauHoi { get; set; }
        }

        // Loại 3, 6, 13: Trắc nghiệm đơn
        public class MultipleChoiceJson : QuestionJsonBase
        {
            public string? CauHoi { get; set; }
            public Dictionary<string, string>? Options { get; set; }
            public string? DapAn { get; set; }
        }

        // Loại 9, 11: Trắc nghiệm đoạn văn / Đục lỗ
        public class ParagraphQuestionsJson : QuestionJsonBase
        {
            public string? DoanVan { get; set; }
            public List<SubQuestion>? Questions { get; set; }
        }

        public class SubQuestion
        {
            public string? ThuTu { get; set; }
            public string? CauHoi { get; set; }
            public Dictionary<string, string>? Options { get; set; }
            public string? DapAn { get; set; }
        }

        // Loại 7: Sắp xếp từ
        public class WordOrderJson : QuestionJsonBase
        {
            public List<string>? Words { get; set; }
        }

        // Loại 8: Điền chữ Hán
        public class FillHanJson : QuestionJsonBase
        {
            public string? Pinyin { get; set; }
            public string? Context { get; set; }
        }

        // Loại 10, 12, 14: Viết đoạn văn/Nhìn tranh
        public class WritingJson : QuestionJsonBase
        {
            public string? Image { get; set; }
            public string? Keyword { get; set; }
            public string? GhiChu { get; set; }
            public string? DapAn { get; set; }
        }
    }
}
