using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models12.QuanLyTrungTam
{
    public class LopDayCuaGiangVienDto
    {
        public int IdLopHoc { get; set; }
        public string MaLopHoc { get; set; } = string.Empty;
        public string TenLopHoc { get; set; } = string.Empty;
        public string? MoTa { get; set; }
        public string? VaiTroDay { get; set; } // Giảng viên chính / Trợ giảng
        public DateTime? NgayPhanCong { get; set; }
        public DateOnly? NgayKhaiGiang { get; set; }
        public DateOnly? NgayKetThuc { get; set; }
        public bool TrangThaiLop { get; set; }
        public int TongSoHocVien { get; set; }
    }

    public class ChiTietGiangVienDto
    {
        // Thông tin tài khoản
        public int IdTaiKhoan { get; set; }
        public string TenDangNhap { get; set; } = string.Empty;
        public bool TrangThaiTaiKhoan { get; set; }
        public DateTime? NgayTaoTaiKhoan { get; set; }
        public int? IdVaiTro { get; set; }
        public string TenVaiTro { get; set; } = string.Empty;

        // Thông tin cá nhân (NguoiDung)
        public int IdUser { get; set; }
        public string HoTen { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? SoDienThoai { get; set; }
        public DateOnly? NgaySinh { get; set; }
        public string? AnhDaiDien { get; set; }

        // Thông tin cơ sở / Trung tâm
        public int IdTrungTam { get; set; }
        public string? TenTrungTam { get; set; }
        public string? MaTrungTam { get; set; }

        // Danh sách lớp phụ trách
        public int TongSoLopPhuTrach { get; set; }
        public List<LopDayCuaGiangVienDto> DanhSachLopDay { get; set; } = new();
    }
}