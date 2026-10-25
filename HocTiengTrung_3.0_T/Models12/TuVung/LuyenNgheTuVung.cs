namespace HocTiengTrung_3._0_T.Models12.TuVung
{
    namespace HocTiengTrung_3._0_T.DTOs.LuyenNghe
    {
        // Yêu cầu lấy danh sách câu hỏi nghe phản xạ
        public class LuyenNgheRequestDto
        {
            public int? IdCapDo { get; set; } // Lọc theo cấp độ (HSK 1, 2, 3...)
            public int SoLuong { get; set; } = 10; // Số câu hỏi (Mặc định 10)

            // Mode nghe: "ZH" (Trung), "VI" (Việt), "MIXED" (Ngẫu nhiên Trung/Việt)
            public int ThoiGianSuyNghi { get; set; } = 15;
            public string ModeNghe { get; set; } = "MIXED";
        }

        // DTO thông tin từng câu hỏi luyện nghe trả về cho Client
        public class LuyenNgheCauHoiDto
        {
            public int IdTuVung { get; set; }
            public string ModeLuyenTap { get; set; } = string.Empty; // "ZH" hoặc "VI"
            public string AudioUrl { get; set; } = string.Empty; // URL file âm thanh để phát
            public string GoiYPrompt { get; set; } = string.Empty; // VD: "Nghe âm thanh Trung và nhập đáp án Tiếng Việt"

            public int ThoiGianSuyNghi { get; set; }
        }

        // Yêu cầu nộp đáp án để chấm điểm
        public class NopDapAnItemDto
        {
            public int IdTuVung { get; set; }
            public string ModeLuyenTap { get; set; } = string.Empty; // "ZH" hoặc "VI"
            public string DapAnNguoiDung { get; set; } = string.Empty; // Từ người dùng nhập vào ô đáp án
        }

        public class NopDapAnRequestDto
        {
            public List<NopDapAnItemDto> DanhSachDapAn { get; set; } = new();
        }

        // Kết quả chấm điểm trả về
        public class ChiTietKetQuaDto
        {
            public int IdTuVung { get; set; }
            public bool IsDung { get; set; }
            public string ChuHan { get; set; } = string.Empty;
            public string Pinyin { get; set; } = string.Empty;
            public string NghiaViet { get; set; } = string.Empty;
            public string DapAnNguoiDung { get; set; } = string.Empty;
            public string DapAnDungStandard { get; set; } = string.Empty;
        }

        public class KetQuaLuyenNgheDto
        {
            public int TongSoCau { get; set; }
            public int SoCauDung { get; set; }
            public double DiemSo { get; set; }
            public List<ChiTietKetQuaDto> ChiTiet { get; set; } = new();
        }
    }
}
