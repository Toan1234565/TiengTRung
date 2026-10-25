namespace HocTiengTrung_3._0_T.Models12.LuyenNgheHinhAnh
{
    public class ChiTietLuyenNgheHinhAnhModels
    {
        public int IdChiTiet { get; set; }

        public int IdLuyenNghe { get; set; }

        public string? FileAudio { get; set; }

        public string? HinhAnh { get; set; }

        public string? PhienAmPinyin { get; set; }

        public string? ChieuHanText { get; set; }

        public string? NghiaTiengViet { get; set; }

        public int SoBai { get; set; }

        public virtual ICollection<LuyenNgheNguoiDungModels> LuyenNgheNguoiDungs { get; set; } = new List<LuyenNgheNguoiDungModels>();
    }
}
