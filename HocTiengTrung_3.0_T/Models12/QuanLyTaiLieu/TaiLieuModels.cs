using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations;

namespace HocTiengTrung_3._0_T.Models12.QuanLyTaiLieu
{
    public class TaiLieuModels
    {
        public int IdTaiLieu { get; set; }

        [Required(ErrorMessage = "Tên tài liệu không được để trống!")]
        public string TenTaiLieu { get; set; } = string.Empty;

        public string? MoTa { get; set; }
        public string? DuongLink { get; set; }
        public string? HinhAnh { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn cấp độ!")]
        public int IdCapDo { get; set; }

        public DateTime? NgayTao { get; set; }
        public bool TrangThai { get; set; } = true;

        // Tệp đính kèm khi Thêm/Sửa
        public IFormFile? FileTaiLieu { get; set; }
        public IFormFile? FileHinhAnh { get; set; }
    }
}