namespace HocTiengTrung_3._0_T.Models12.QuanLyDeThi
{
    // Class hỗ trợ nhận dữ liệu từ Client
    public class CreateDeThiRequest
    {
        public DeThiModels Info { get; set; }
        public List<ChiTietDeThiModels> Questions { get; set; }
    }
}
