namespace HocTiengTrung_3._0_T.Models12.DauTruongSieuToc
{
    public class KhoiTaoDauTruongDTO
    {
        public int? IdCapDo { get; set; }
        public int? IdLoai { get; set; }
    }

    public class DapAnDTO
    {
        public int IdDapAn { get; set; }
        public string? NoiDung { get; set; }
    }

    public class CauHoiDauTruongDTO
    {
        public int IdCauHoi { get; set; }
        public string? NoiDung { get; set; }
        public string? AmThanhPath { get; set; }
        public int DiemGoc { get; set; }
        public List<DapAnDTO> DapAns { get; set; } = new();
    }

    public class BotDTO
    {
        public string IdBot { get; set; } = string.Empty;
        public string TenBot { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
    }

    public class ThongTinTranDauResponseDTO
    {
        public List<CauHoiDauTruongDTO> DanhSachCauHoi { get; set; } = new();
        public List<BotDTO> DanhSachBot { get; set; } = new();
        public int ThoiGianMoiCau { get; set; } = 40;
        public int TongThoiGianChuan { get; set; } = 400;
        public int ThoiGianToiDa { get; set; } = 460;
    }

    public class ChiTietLamBaiDTO
    {
        public int IdCauHoi { get; set; }
        public int? IdDapAnChon { get; set; }
        public int ThoiGianTraLoiGiay { get; set; }
        public int DiemGoc { get; set; }
    }

    public class NopBaiDauTruongDTO
    {
        public int TongThoiGianThucHien { get; set; }
        public List<ChiTietLamBaiDTO> ChiTietLamBai { get; set; } = new();
        public List<BotDTO> DanhSachBot { get; set; } = new();
    }

    public class NguoiChoiXepHangDTO
    {
        public string TenNguoiChoi { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public bool IsBot { get; set; }
        public int TongDiem { get; set; }
        public int TongThoiGianGiay { get; set; }
        public int Hang { get; set; }
    }

    public class XemLaiKetQuaDTO
    {
        public List<ChiTietLamBaiDTO> ChiTietLamBai { get; set; } = new();
    }

    
}
