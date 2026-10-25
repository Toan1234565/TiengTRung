using HocTiengTrung_3._0_T.Models;

namespace HocTiengTrung_3._0_T.Models12.BaiTap
{
    public class BaiTapExcelModel
    {
        public int ID_Loai { get; set; }        // Cột A
        public int ID_CapDo { get; set; }       // Cột B
        public string TenChuDe { get; set; }     // Cột C
        public string TieuDe { get; set; }       // Cột D
        public string NoiDungHan { get; set; }   // Cột E
        public string NoiDungPinyin { get; set; } // Cột F
        public string NoiDungViet { get; set; }   // Cột G
        public string CauHoi { get; set; }       // Cột H
        public string DapAn1 { get; set; }       // Cột I
        public string DapAn2 { get; set; }       // Cột J
        public string DapAn3 { get; set; }       // Cột K
        public string DapAn4 { get; set; }       // Cột L
        public string IsDung { get; set; }       // Cột M
        public string GiaiThich { get; set; }    // Cột N
        public string AmThanh { get; set; }      // Cột O

        // Thêm các thuộc tính bổ sung nếu cần
        public int? IdChuDe { get; set; }
        public string AnhMinhHoa { get; set; }
    }
}
