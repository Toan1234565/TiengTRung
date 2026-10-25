using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models12.DauTruongSieuToc
{
    // =========================================================================
    // 1. ENUMS DÀNH CHO TH2
    // =========================================================================

    public enum TrangThaiNguoiChoi
    {
        DangCho = 0,      // Trong phòng chờ, chưa sẵn sàng ("Đang chờ")
        DaSanSang = 1,    // Đã bấm sẵn sàng
        DangThiDau = 2,   // Đang trong thời gian làm bài
        HoanThanh = 3,    // Đã nộp bài, đang ở Modal chờ kết quả
        DangXemLai = 4,   // Thi đấu xong, đang xem lại chi tiết bài làm (treo trạng thái)
        BoCuoc = 5        // Bị Kick / Thoát ứng dụng / Hủy trận (0 điểm)
    }

    public enum TrangThaiPhong
    {
        ChoNguoiChoi = 0, // Đang ở sảnh chờ
        DangThiDau = 1,   // Cả phòng đang thi đấu
        DaKetThuc = 2,    // Trận đấu hoàn thành
        ChoSieuCap = 3
    }

    // =========================================================================
    // 2. REQUEST DTOs (Dữ liệu gửi từ Client lên Server)
    // =========================================================================

    public class TaoPhongTH2RequestDTO
    {
        public string IdNguoiChoi { get; set; } = string.Empty;
        public string TenNguoiChoi { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;

        public int? IdCapDo { get; set; }
        public int? IdLoai { get; set; }
        public int SoLuongCauHoi { get; set; }
        public int ThoiGianMoiCau { get; set; } = 15; // Thời gian đếm ngược mỗi câu (giây)
        public bool ChuyenCauNgayKhiChon { get; set; } = true; // Chuyển câu ngay lập tức hay chờ timeout
    }

    public class VaoPhongTH2RequestDTO
    {
        public string MaPhong { get; set; } = string.Empty;
        public string IdNguoiChoi { get; set; } = string.Empty;
        public string TenNguoiChoi { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
    }

    public class SanSangTH2RequestDTO
    {
        public string MaPhong { get; set; } = string.Empty;
        public string IdNguoiChoi { get; set; } = string.Empty;
        public bool IsSanSang { get; set; }
    }

    public class KickNguoiChoiTH2RequestDTO
    {
        public string MaPhong { get; set; } = string.Empty;
        public string IdNguoiChoi { get; set; } = string.Empty; // ID của Host
        public string IdNguoiChoiBiKick { get; set; } = string.Empty;
    }

    public class RoiPhongTH2RequestDTO
    {
        public string MaPhong { get; set; } = string.Empty;
        public string IdNguoiChoi { get; set; } = string.Empty;
    }

    public class BatDauTH2RequestDTO
    {
        public string MaPhong { get; set; } = string.Empty;
        public string IdNguoiChoi { get; set; } = string.Empty; // ID của Host
    }

    public class TraLoiTH2RequestDTO
    {
        public string MaPhong { get; set; } = string.Empty;
        public string IdNguoiChoi { get; set; } = string.Empty;
        public int IdCauHoi { get; set; }
        public int ViTriDapAnChon { get; set; } // Index đáp án người chơi chọn (0, 1, 2, 3)
        public double ThoiGianGuiTraLoi { get; set; } // Thời gian tính bằng giây từ lúc câu hỏi xuất hiện
    }

    public class NopBaiTH2RequestDTO
    {
        public string MaPhong { get; set; } = string.Empty;
        public string IdNguoiChoi { get; set; } = string.Empty;
        public TrangThaiNguoiChoi TrangThai { get; set; } = TrangThaiNguoiChoi.HoanThanh;
        public int TongThoiGianThucHien { get; set; }
        public List<ChiTietLamBaiDTOTH2> ChiTietLamBaiTH2 { get; set; } = new List<ChiTietLamBaiDTOTH2>();
    }

    public class ChiTietLamBaiDTOTH2
    {
        public int IdCauHoi { get; set; }
        public int? IdDapAnChon { get; set; }
        public int ThoiGianTraLoiGiay { get; set; }
        public int DiemGoc { get; set; }
    }

    public class XemLaiBaiTH2RequestDTO
    {
        public string MaPhong { get; set; } = string.Empty;
        public string IdNguoiChoi { get; set; } = string.Empty;
    }

    public class QuayLaiPhongTH2RequestDTO
    {
        public string MaPhong { get; set; } = string.Empty;
        public string IdNguoiChoi { get; set; } = string.Empty;
    }

    // =========================================================================
    // 3. RESPONSE & DATA DTOs (Dữ liệu trả về Client hoặc trung gian)
    // =========================================================================

    public class NguoiChoiTH2DTO
    {
        public string IdNguoiChoi { get; set; } = string.Empty;
        public string TenNguoiChoi { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public bool IsHost { get; set; }
        public TrangThaiNguoiChoi TrangThai { get; set; } = TrangThaiNguoiChoi.DangCho;
    }

    public class PhongDauTH2ResponseDTO
    {
        public string MaPhong { get; set; } = string.Empty;
        public int? IdCapDo { get; set; }
        public int? IdLoai { get; set; }
        public int SoLuongCauHoi { get; set; }
        public int ThoiGianMoiCau { get; set; } = 15;
        public bool ChuyenCauNgayKhiChon { get; set; } = true;
        public TrangThaiPhong TrangThaiPhong { get; set; } = TrangThaiPhong.ChoNguoiChoi;
        public List<NguoiChoiTH2DTO> DanhSachNguoiChoi { get; set; } = new List<NguoiChoiTH2DTO>();
    }

    public class CauHoiDauTruongDTOTH2
    {
        public int IdCauHoi { get; set; }
        public string NoiDung { get; set; } = string.Empty;
        public string PhiênÂm { get; set; } = string.Empty;
        public string DịchNghĩa { get; set; } = string.Empty;
        public string AmThanhUrl { get; set; } = string.Empty;
        public List<string> DanhSachDapAn { get; set; } = new List<string>();
        public int DapAnDungIndex { get; set; }
        public int DiemGoc { get; set; } // 10 - 40 điểm
    }

    public class ThongTinTranDauTH2ResponseDTO
    {
        public string MaPhong { get; set; } = string.Empty;
        public List<CauHoiDauTruongDTOTH2> DanhSachCauHoi { get; set; } = new List<CauHoiDauTruongDTOTH2>();
        public int ThoiGianMoiCau { get; set; } = 15;
        public bool ChuyenCauNgayKhiChon { get; set; } = true;
        public int TongThoiGianChuan { get; set; }
        public int ThoiGianToiDa { get; set; }
    }

    public class TraLoiTH2ResponseDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool LaDapAnDung { get; set; }
        public int DapAnDungIndex { get; set; }
        public int DiemCong { get; set; }
        public int TongDiemHienTai { get; set; }
    }

    public class NopBaiTH2ResponseDTO
    {
        public bool IsAllFinished { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class NguoiChoiXepHangTH2DTO
    {
        public int Hang { get; set; }
        public string IdNguoiChoi { get; set; } = string.Empty;
        public string TenNguoiChoi { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public bool IsHost { get; set; }
        public int TongDiem { get; set; }
        public double TongThoiGianGiay { get; set; }
        public TrangThaiNguoiChoi TrangThaiThiDau { get; set; }
    }

    public class BangXepHangTH2ResponseDTO
    {
        public string MaPhong { get; set; } = string.Empty;
        public List<NguoiChoiXepHangTH2DTO> DanhSachXepHang { get; set; } = new List<NguoiChoiXepHangTH2DTO>();
    }
}