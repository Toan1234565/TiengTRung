namespace HocTiengTrung_3._0_T.Models12.QuanLyTrungTam
{
    public class LopHocModels
    {
        public int IdLopHoc { get; set; }

        public int IdTrungTam { get; set; }

        public string TenLopHoc { get; set; } = null!;

        public string? MoTa { get; set; }

        public DateOnly? NgayKhaiGiang { get; set; }

        public DateOnly? NgayKetThuc { get; set; }

        public string? MaLopHoc { get; set; }

        public bool TrangThai { get; set; }

        public int TongSoHocVien { get; set; }
        public List<PhanCongGiangVienItem>? DanhSachGiangVien { get; set; }

    }
    public class PhanCongGiangVienItem
    {
        public int IdTaiKhoan { get; set; }
        public string? VaiTroDay { get; set; }
    }
}
