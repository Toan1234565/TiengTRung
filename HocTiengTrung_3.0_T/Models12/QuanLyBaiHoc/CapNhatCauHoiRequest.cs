namespace HocTiengTrung_3._0_T.Models12.QuanLyBaiHoc
{
    public class CapNhatCauHoiRequest
    {
        public int? IdCauHoi { get; set; } // Có giá trị -> Sửa. Bằng null -> Thêm mới.
        public int IdBaiHoc { get; set; }
        public int ThuTu { get; set; }
        public int IdLoai { get; set; }
        public int IdCapDo { get; set; }
        public string NoiDung { get; set; }
        public string GiaiThich { get; set; }
        // Thêm các trường khác (Đáp án, Độ khó, v.v.) tùy theo Model của bạn...
    }
}
