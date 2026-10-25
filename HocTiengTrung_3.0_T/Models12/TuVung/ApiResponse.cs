namespace HocTiengTrung_3._0_T.Models12.TuVung
{
    public class ApiResponse
    {
        public int TotalItems { get; set; }
        public List<TuVungModes> Data { get; set; } = new List<TuVungModes>();
    }
}
