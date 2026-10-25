using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models12.QuanLyTrungTam
{
    public class ChiTietHocVienViewModel
    {
        public int IdUser { get; set; }
        public int? IdTaiKhoan { get; set; }
        public string? HoTen { get; set; }
        public string? Email { get; set; }
        public string? SoDienThoai { get; set; }
        public string? AnhDaiDien { get; set; }
        public DateOnly? NgaySinh { get; set; }
        public DateTime? NgayThamGia { get; set; }
    }

    public class ChiTietGiangVienViewModel
    {
        public int IdTaiKhoan { get; set; }
        public int? IdUser { get; set; }
        public string TenDangNhap { get; set; } = string.Empty;
        public string? HoTen { get; set; }
        public string? Email { get; set; }
        public string? SoDienThoai { get; set; }
        public string? AnhDaiDien { get; set; }
        public string? VaiTroDay { get; set; } // Giảng viên chính / Trợ giảng
        public DateTime? NgayPhanCong { get; set; }
    }

    public class ChiTietLopHocViewModel
    {
        public int IdLopHoc { get; set; }
        public int IdTrungTam { get; set; }
        public string? TenTrungTam { get; set; }
        public string TenLopHoc { get; set; } = null!;
        public string? MaLopHoc { get; set; }
        public string? MoTa { get; set; }
        public DateOnly? NgayKhaiGiang { get; set; }
        public DateOnly? NgayKetThuc { get; set; }
        public bool TrangThai { get; set; }
        public int TongSoHocVien { get; set; }

        // Danh sách giảng viên phụ trách lớp
        public List<ChiTietGiangVienViewModel> DanhSachGiangVien { get; set; } = new();

        // Danh sách học viên của lớp
        public List<ChiTietHocVienViewModel> DanhSachHocVien { get; set; } = new();
    }
}