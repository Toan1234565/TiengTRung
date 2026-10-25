namespace HocTiengTrung_3._0_T.Models12.BaiTap
{
    public class EditBaiTap
    {
        // --- Thông tin Câu Hỏi ---
        public int? IdCauHoi { get; set; }
        public int? IdLoai { get; set; }
        public int? IdCapDo { get; set; }
        public string? NoiDung { get; set; }
        public string? AmThanhPath { get; set; }
        public string? GiaiThich { get; set; }
        public bool? IsGeneratedOnly { get; set; }
        public int? DoKho { get; set; }

        // --- Thông tin Đoạn Văn (Nằm trong Câu Hỏi) ---
        public int? IdDoanVan { get; set; }
        public int? IdChuDe { get; set; }
        public string? TieuDe { get; set; }
        public string? NoiDungHan { get; set; }
        public string? NoiDungPinyin { get; set; }
        public string? NoiDungViet { get; set; }
        public string? AnhMinhHoa { get; set; }

        // --- Danh sách Đáp Án ---
        public List<EditDapAn> DapAns { get; set; } = new List<EditDapAn>();
    }

    public class EditDapAn
    {
        public int IdDapAn { get; set; }
        public string? NoiDungDapAn { get; set; }
        public bool? LaDapAnDung { get; set; }
    }
}
