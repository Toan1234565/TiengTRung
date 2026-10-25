namespace HocTiengTrung_3._0_T.Models12.BaiTap
{
    public class CauHoiRequest
    {
        public int? IdLoai { get; set; } // 1: Trắc nghiệm, 2: Sắp xếp câu...
        public string? NoiDung { get; set; }
        public string? GiaiThich { get; set; }
        public int? DoKho { get; set; }
        public List<DapAnRequest> DapAns { get; set; } = new();
    }
}
