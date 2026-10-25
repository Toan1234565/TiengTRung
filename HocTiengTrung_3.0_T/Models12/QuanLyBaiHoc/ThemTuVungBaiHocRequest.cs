namespace HocTiengTrung_3._0_T.Models12.QuanLyBaiHoc
{
    public class ThemTuVungBaiHocRequest
    {
        public int IdBaiHoc { get; set; }
        public int IdCapDo { get; set; }

       
        public string ChuHan { get; set; }
        public string? Pinyin { get; set; }
        public string? NghiaViet { get; set; }
        public string? LoaiTu { get; set; }
        public string? AmHan { get; set; }

        // Phần dành cho Câu Ví Dụ
        public string? ViDuHan { get; set; }
        public string? ViDuPinyin { get; set; }
        public string? ViDuViet { get; set; }
    }
}
