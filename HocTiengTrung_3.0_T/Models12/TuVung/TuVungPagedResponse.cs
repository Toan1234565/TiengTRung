namespace HocTiengTrung_3._0_T.Models12.TuVung
{
    // --- Thêm class bổ trợ để nhận dữ liệu phân trang từ API ---
    public class TuVungPagedResponse
    {
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public List<TuVungModes> Items { get; set; } = new List<TuVungModes>();
    }
}
