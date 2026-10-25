namespace HocTiengTrung_3._0_T.Models12.LuyenNgheHinhAnh
{
    public class LuyenNgheNguoiDungModels
    {
        public int IdUser { get; set; }

        public int IdChiTiet { get; set; } // Thay IdLuyenNghe bằng IdChiTiet

        public bool DaThuoc { get; set; }

        public DateTime NgayDanhDau { get; set; }
    }
}