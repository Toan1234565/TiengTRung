using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models12.PhanTichBoThu
{
    public class RequestTuDonDto
    {
        public string ChuHan { get; set; } = string.Empty;
    }

    public class BoThuItemDto
    {
        public string KyHieu { get; set; } = string.Empty;
        public string TenGoi { get; set; } = string.Empty;
        public string? Pinyin { get; set; }
        public string? YNghia { get; set; }
        public string? VaiTro { get; set; }
    }

    public class ChiTietKyTuPhanTichDto
    {
        public string KyTu { get; set; } = string.Empty;
        public List<BoThuItemDto> DanhSachBoThu { get; set; } = new();
    }

    public class PhanTichHanTuDto
    {
        public string ChuHan { get; set; } = string.Empty;
        public string Pinyin { get; set; } = string.Empty;
        public string AmHanViet { get; set; } = string.Empty;
        public string TuLoai { get; set; } = string.Empty;
        public string NghiaTiengViet { get; set; } = string.Empty;
        public string BoThuChinh { get; set; } = string.Empty;
        public List<ChiTietKyTuPhanTichDto> CauTaoKyTu { get; set; } = new();
        public string GiaiThichTrietTu { get; set; } = string.Empty;
        public string MeoGhiNho { get; set; } = string.Empty;
    }
}