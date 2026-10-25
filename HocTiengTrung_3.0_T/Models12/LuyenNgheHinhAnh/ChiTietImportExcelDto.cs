using MiniExcelLibs.Attributes;

namespace HocTiengTrung_3._0_T.Models12.LuyenNgheHinhAnh
{
    public class ChiTietImportExcelDto
    {
        [ExcelColumnName("IdLuyenNghe")]
        public int? IdLuyenNghe { get; set; }

        [ExcelColumnName("ChieuHanText")]
        public string? ChieuHanText { get; set; }

        [ExcelColumnName("PhienAmPinyin")]
        public string? PhienAmPinyin { get; set; }

        [ExcelColumnName("NghiaTiengViet")]
        public string? NghiaTiengViet { get; set; }

        [ExcelColumnName("FileAudio")]
        public string? FileAudio { get; set; }

        [ExcelColumnName("HinhAnh")]
        public string? HinhAnh { get; set; }
        [ExcelColumnName("SoBai")]
        public int SoBai { get; set; }
    }
}