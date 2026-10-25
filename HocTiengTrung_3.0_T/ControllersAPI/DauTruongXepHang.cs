using HocTiengTrung_3._0_T.Models12.DauTruongSieuToc;
using HocTiengTrung_3._0_T.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
  
namespace HocTiengTrung_3._0_T.ControllersAPI
{
    [ApiController]
    [Route("api/dau-truong")]
    public class DauTruongXepHang : ControllerBase
    {
        private readonly TmdtContext _context;

        // Danh sách tên Bot 
        private static readonly List<string> BotNames = new()
        {
            "Trần An", "Trần Mai Phương", "Trần Thùy Linh", "Trần Ngọc Diệp", "Trần Cát Tường",
            "Trần Đức Minh", "Trần Hải Đăng", "Trần Quốc Bảo", "Trần Nhật Anh", "Trần Minh Triết",
            "Nguyễn Tuệ Nhi", "Nguyễn Bảo Anh", "Nguyễn Thanh Vân", "Nguyễn Nhã Phương", "Nguyễn Ánh Dương",
            "Nguyễn Gia Hưng", "Nguyễn Hoàng Long", "Nguyễn Minh Quân", "Nguyễn Đức Anh", "Nguyễn Thiện Nhân"
        };

        public DauTruongXepHang(TmdtContext context)
        {
            _context = context;
        }

        /// <summary>
        /// TH1: Khởi tạo trận đấu solo với Máy
        /// </summary>
        /// <summary>
        /// TH1: Khởi tạo trận đấu solo với Máy (Đã tối ưu Query tránh SQL Timeout)
        /// </summary>
        [HttpPost("khoi-tao-th1")]
        public async Task<IActionResult> KhoiTaoTranDauTH1([FromBody] KhoiTaoDauTruongDTO req)
        {
            // 1. Kiểm tra đăng nhập
            var idTaiKhoanStr = HttpContext.Session.GetString("IdTaiKhoan") ?? User.FindFirst("IdTaiKhoan")?.Value;
            if (string.IsNullOrEmpty(idTaiKhoanStr))
            {
                return Unauthorized(new { success = false, message = "Vui lòng đăng nhập để tham gia đấu trường." });
            }

            // 2. TỐI ƯU: Chỉ truy vấn danh sách ID thỏa mãn điều kiện (Cực nhẹ, không JOIN bảng DapAn)
            var queryIds = _context.CauHois
                .Where(c => c.TrangThai == true);

            if (req.IdCapDo.HasValue)
                queryIds = queryIds.Where(c => c.IdCapDo == req.IdCapDo.Value);

            // Lọc theo Loại bài tập:
            if (req.IdLoai.HasValue)
            {
                // Nếu chọn 1 loại cụ thể
                queryIds = queryIds.Where(c => c.IdLoai == req.IdLoai.Value);
            }
            else
            {
                // Nếu chọn "Tất cả các loại" (req.IdLoai == null), chỉ lấy các loại 1005, 1008, 1009
                var dsLoaiMacDinh = new List<int> { 1005, 1008, 1009 };
                queryIds = queryIds.Where(c => c.IdLoai.HasValue && dsLoaiMacDinh.Contains(c.IdLoai.Value));
            }

            var listIds = await queryIds.Select(c => c.IdCauHoi).ToListAsync();

            if (listIds.Count < 10)
            {
                return BadRequest(new { success = false, message = "Không đủ 10 câu hỏi trong hệ thống theo điều kiện đã chọn." });
            }

            // Tráo ngẫu nhiên và lấy 10 ID
            var random = Random.Shared;
            var random10Ids = listIds.OrderBy(_ => random.Next()).Take(10).ToList();

            // Truy vấn thông tin chi tiết + Đáp án CHỈ CHO 10 CÂU HỎI ĐÃ CHỌN
            var randomCauHoi = await _context.CauHois
                .Include(c => c.DapAns)
                .Where(c => random10Ids.Contains(c.IdCauHoi))
                .ToListAsync();

            var danhSachCauHoiDTO = randomCauHoi.Select(c => new CauHoiDauTruongDTO
            {
                IdCauHoi = c.IdCauHoi,
                NoiDung = c.NoiDung,
                AmThanhPath = c.AmThanhPath,
                DiemGoc = random.Next(10, 41), // Ngẫu nhiên 10 - 40 điểm
                DapAns = c.DapAns.Select(d => new DapAnDTO
                {
                    IdDapAn = d.IdDapAn,
                    NoiDung = d.NoiDungDapAn
                }).ToList()
            }).ToList();

            // 3. Khởi tạo ngẫu nhiên từ 5 đến 9 Bot
            int soLuongBot = random.Next(5, 10);
            var selectedBotNames = BotNames.OrderBy(_ => random.Next()).Take(soLuongBot).ToList();

            var danhSachBot = selectedBotNames.Select((name, index) => new BotDTO
            {
                IdBot = $"BOT_{index + 1}_{Guid.NewGuid().ToString("N")[..4]}",
                TenBot = name,
                Avatar = $"https://api.dicebear.com/7.x/avataaars/svg?seed={Uri.EscapeDataString(name)}"
            }).ToList();

            return Ok(new ThongTinTranDauResponseDTO
            {
                DanhSachCauHoi = danhSachCauHoiDTO,
                DanhSachBot = danhSachBot,
                ThoiGianMoiCau = 30,
                TongThoiGianChuan = 300,
                ThoiGianToiDa = 360
            });
        }

        /// <summary>
        /// TH1: Nộp bài, giả lập Bot & Tính toán Bảng xếp hạng
        /// </summary>
        [HttpPost("nop-bai-th1")]
        public async Task<IActionResult> NopBaiTH1([FromBody] NopBaiDauTruongDTO req)
        {
            var idTaiKhoanStr = HttpContext.Session.GetString("IdTaiKhoan") ?? User.FindFirst("IdTaiKhoan")?.Value;
            if (string.IsNullOrEmpty(idTaiKhoanStr) || !int.TryParse(idTaiKhoanStr, out int idTaiKhoan))
            {
                return Unauthorized(new { success = false, message = "Phiên đăng nhập không hợp lệ." });
            }

            var nguoiDung = await _context.NguoiDungs.FirstOrDefaultAsync(n => n.IdTaiKhoan == idTaiKhoan);
            string tenUser = nguoiDung?.HoTen ?? "Người chơi";
            string avatarUser = nguoiDung?.AnhDaiDien ?? "https://api.dicebear.com/7.x/avataaars/svg?seed=Player";

            var random = Random.Shared;
            var listCauHoiIds = req.ChiTietLamBai.Select(c => c.IdCauHoi).ToList();

            // Lấy danh sách đáp án đúng từ Database (FIX LỖI: d.LaDapAnDung == true)
            var dapAnDungDict = await _context.DapAns
                .Where(d => listCauHoiIds.Contains(d.IdCauHoi ?? 0) && d.LaDapAnDung == true)
                .ToDictionaryAsync(d => d.IdCauHoi!.Value, d => d.IdDapAn);

            // 1. Tính điểm Người dùng
            int tongDiemUser = 0;
            int tongThoiGianUser = Math.Min(req.TongThoiGianThucHien, 460); // Giới hạn max 460s

            foreach (var item in req.ChiTietLamBai)
            {
                // Kiểm tra đúng và trong thời gian hợp lệ (<= 40s)
                if (item.IdDapAnChon.HasValue &&
                    dapAnDungDict.TryGetValue(item.IdCauHoi, out int idDapAnDung) &&
                    item.IdDapAnChon.Value == idDapAnDung &&
                    item.ThoiGianTraLoiGiay <= 40)
                {
                    int thoiGianConLai = Math.Max(0, 40 - item.ThoiGianTraLoiGiay);
                    int diemNhan = (int)Math.Round(item.DiemGoc * ((double)thoiGianConLai / 40.0));
                    tongDiemUser += diemNhan;
                }
            }

            var leaderboard = new List<NguoiChoiXepHangDTO>
            {
                new NguoiChoiXepHangDTO
                {
                    TenNguoiChoi = tenUser,
                    Avatar = string.IsNullOrEmpty(avatarUser) ? "https://api.dicebear.com/7.x/avataaars/svg?seed=Player" : avatarUser,
                    IsBot = false,
                    TongDiem = tongDiemUser,
                    TongThoiGianGiay = tongThoiGianUser
                }
            };

            // 2. Giả lập hành vi & điểm số của từng Bot
            foreach (var bot in req.DanhSachBot)
            {
                int tongDiemBot = 0;
                int tongThoiGianBot = 0;

                // Tỷ lệ chính xác ngẫu nhiên của Bot (50% - 90%)
                double tyLeDung = random.Next(50, 91) / 100.0;

                foreach (var item in req.ChiTietLamBai)
                {
                    // Thời gian trả lời ngẫu nhiên 2s - 35s
                    int thoiGianBot = random.Next(2, 36);
                    tongThoiGianBot += thoiGianBot;

                    bool botTraLoiDung = random.NextDouble() <= tyLeDung;
                    if (botTraLoiDung)
                    {
                        int thoiGianConLai = 40 - thoiGianBot;
                        int diemNhan = (int)Math.Round(item.DiemGoc * ((double)thoiGianConLai / 40.0));
                        tongDiemBot += diemNhan;
                    }
                }

                leaderboard.Add(new NguoiChoiXepHangDTO
                {
                    TenNguoiChoi = bot.TenBot,
                    Avatar = bot.Avatar,
                    IsBot = true,
                    TongDiem = tongDiemBot,
                    TongThoiGianGiay = tongThoiGianBot
                });
            }

            // 3. Sắp xếp Xếp hạng: Tổng điểm giảm dần -> Tổng thời gian tăng dần
            var sortedLeaderboard = leaderboard
                .OrderByDescending(x => x.TongDiem)
                .ThenBy(x => x.TongThoiGianGiay)
                .ToList();

            // Gán thứ hạng
            for (int i = 0; i < sortedLeaderboard.Count; i++)
            {
                sortedLeaderboard[i].Hang = i + 1;
            }

            return Ok(new
            {
                success = true,
                bangXepHang = sortedLeaderboard,
                ketQuaBanThan = sortedLeaderboard.FirstOrDefault(x => !x.IsBot)
            });
        }

        /// <summary>
        /// TH1: Xem chi tiết bài làm, đáp án đúng/sai, giải thích và số điểm được cộng từng câu
        /// </summary>
        [HttpPost("xem-lai-ket-qua-th1")]
        public async Task<IActionResult> XemLaiKetQuaTH1([FromBody] XemLaiKetQuaDTO req)
        {
            // 1. Kiểm tra đăng nhập
            var idTaiKhoanStr = HttpContext.Session.GetString("IdTaiKhoan") ?? User.FindFirst("IdTaiKhoan")?.Value;
            if (string.IsNullOrEmpty(idTaiKhoanStr))
            {
                return Unauthorized(new { success = false, message = "Vui lòng đăng nhập để xem lại kết quả." });
            }

            if (req?.ChiTietLamBai == null || !req.ChiTietLamBai.Any())
            {
                return BadRequest(new { success = false, message = "Dữ liệu bài làm không hợp lệ." });
            }

            var listCauHoiIds = req.ChiTietLamBai.Select(c => c.IdCauHoi).ToList();

            // 2. Truy vấn chi tiết các câu hỏi kèm danh sách đáp án và giải thích
            var cauHois = await _context.CauHois
                .Include(c => c.DapAns)
                .Where(c => listCauHoiIds.Contains(c.IdCauHoi))
                .AsNoTracking()
                .ToListAsync();

            int tongDiemCong = 0;
            int soCauDung = 0;

            var chiTietKetQua = new List<object>();

            // 3. Khớp bài làm của người dùng với dữ liệu đúng trong DB
            foreach (var item in req.ChiTietLamBai)
            {
                var cauHoiDb = cauHois.FirstOrDefault(c => c.IdCauHoi == item.IdCauHoi);
                if (cauHoiDb == null) continue;

                var dapAnDung = cauHoiDb.DapAns.FirstOrDefault(d => d.LaDapAnDung == true);
                var dapAnChon = cauHoiDb.DapAns.FirstOrDefault(d => d.IdDapAn == item.IdDapAnChon);

                // Kiểm tra đúng/sai: Phải chọn đúng ID đáp án và thời gian <= 40s
                bool isDung = item.IdDapAnChon.HasValue &&
                             dapAnDung != null &&
                             item.IdDapAnChon.Value == dapAnDung.IdDapAn &&
                             item.ThoiGianTraLoiGiay <= 40;

                int diemCongCauNay = 0;
                if (isDung)
                {
                    int thoiGianConLai = Math.Max(0, 40 - item.ThoiGianTraLoiGiay);
                    diemCongCauNay = (int)Math.Round(item.DiemGoc * ((double)thoiGianConLai / 40.0));
                    tongDiemCong += diemCongCauNay;
                    soCauDung++;
                }

                chiTietKetQua.Add(new
                {
                    idCauHoi = cauHoiDb.IdCauHoi,
                    noiDungCauHoi = cauHoiDb.NoiDung,
                    amThanhPath = cauHoiDb.AmThanhPath,
                    giaiThich = cauHoiDb.GiaiThich ?? "Không có giải thích chi tiết.",
                    diemGoc = item.DiemGoc,
                    diemCong = diemCongCauNay,
                    isDung = isDung,
                    thoiGianTraLoiGiay = item.ThoiGianTraLoiGiay,
                    idDapAnChon = item.IdDapAnChon,
                    tenDapAnChon = dapAnChon?.NoiDungDapAn ?? "Chưa trả lời / Hết giờ",
                    idDapAnDung = dapAnDung?.IdDapAn,
                    tenDapAnDung = dapAnDung?.NoiDungDapAn ?? "",
                    danhSachDapAn = cauHoiDb.DapAns.Select(d => new
                    {
                        idDapAn = d.IdDapAn,
                        noiDung = d.NoiDungDapAn,
                        laDapAnDung = d.LaDapAnDung ?? false
                    }).ToList()
                });
            }

            return Ok(new
            {
                success = true,
                tongDiemCong = tongDiemCong,
                soCauDung = soCauDung,
                tongSoCau = req.ChiTietLamBai.Count,
                chiTietBaiLam = chiTietKetQua
            });
        }


    }
}