using MiniExcelLibs.Attributes;

namespace HocTiengTrung_3._0_T.Models
{
    public class NguPhapExcel
    {
        [ExcelColumnName("TenNguPhap")]
        public string? TenNguPhap { get; set; }

        [ExcelColumnName("CauTruc")]
        public string? CauTruc { get; set; }

        [ExcelColumnName("GiaiThichNP")]
        public string? GiaiThichNP { get; set; }

        [ExcelColumnName("ViDuNP")]
        public string? ViDuNP { get; set; }

        [ExcelColumnName("IdCapDo")]
        public int? IdCapDo { get; set; }

        [ExcelColumnName("NoiDungCauHoi")]
        public string? NoiDungCauHoi { get; set; }

        [ExcelColumnName("DapAnA")]
        public string? DapAnA { get; set; }

        [ExcelColumnName("DapAnB")]
        public string? DapAnB { get; set; }

        [ExcelColumnName("DapAnC")]
        public string? DapAnC { get; set; }

        [ExcelColumnName("DapAnD")]
        public string? DapAnD { get; set; }

        [ExcelColumnName("DapAnDung")]
        public string? DapAnDung { get; set; }

        [ExcelColumnName("IdLoai")]
        public int? IdLoai { get; set; }

        [ExcelColumnName("GiaiThichCauHoi")]
        public string? GiaiThichCauHoi { get; set; }
    }
}