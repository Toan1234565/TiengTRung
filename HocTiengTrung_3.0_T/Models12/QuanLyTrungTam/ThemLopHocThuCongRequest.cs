namespace HocTiengTrung_3._0_T.Models12.QuanLyTrungTam
{
    public class ThemLopHocThuCongRequest
    {
        // 1. THÔNG TIN LỚP HỌC (BẮT BUỘC)
        public int? IdTrungTam { get; set; }
        public string? MaLopHoc { get; set; }
        public string TenLopHoc { get; set; } = null!;
        public string? MoTa { get; set; }
        public DateOnly? NgayKhaiGiang { get; set; }
        public DateOnly? NgayKetThuc { get; set; }
        public bool? TrangThai { get; set; } = true;

        // 2. DANH SÁCH GIẢNG VIÊN (KHÔNG BẮT BUỘC)
        public List<GiangVienPhanCongRequest>? DanhSachGiangVien { get; set; }

        // 3. DANH SÁCH HỌC VIÊN (KHÔNG BẮT BUỘC)
        public List<HocVienThemMoiRequest>? DanhSachHocVien { get; set; }
    }

    public class GiangVienPhanCongRequest
    {
        public int IdTaiKhoan { get; set; }
        public string? VaiTroDay { get; set; }
    }

    public class HocVienThemMoiRequest
    {
        public string HoTen { get; set; } = null!;
        public string? Email { get; set; }
        public string? SoDienThoai { get; set; }
        public DateOnly? NgaySinh { get; set; }
    }
}