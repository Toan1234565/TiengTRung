using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12.DauTruongSieuToc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.Services
{
    public class DauTruongTH2Service : IDauTruongTH2Service
    {
        private static readonly ConcurrentDictionary<string, PhongDauTH2ResponseDTO> _activeRooms = new();
        private static readonly ConcurrentDictionary<string, List<NguoiChoiXepHangTH2DTO>> _roomResults = new();
        private static readonly ConcurrentDictionary<string, List<CauHoiDauTruongDTOTH2>> _roomQuestions = new();

        private readonly TmdtContext _context;

        public DauTruongTH2Service(TmdtContext context)
        {
            _context = context;
        }

        public Task<PhongDauTH2ResponseDTO> TaoPhongAsync(TaoPhongTH2RequestDTO request)
        {
            string maPhong;
            do
            {
                maPhong = Random.Shared.Next(100000, 999999).ToString();
            } while (_activeRooms.ContainsKey(maPhong));

            var hostPlayer = new NguoiChoiTH2DTO
            {
                IdNguoiChoi = request.IdNguoiChoi,
                TenNguoiChoi = request.TenNguoiChoi,
                Avatar = request.Avatar,
                IsHost = true,
                TrangThai = TrangThaiNguoiChoi.DaSanSang
            };

            var newRoom = new PhongDauTH2ResponseDTO
            {
                MaPhong = maPhong,
                IdCapDo = request.IdCapDo,
                IdLoai = request.IdLoai,
                SoLuongCauHoi = request.SoLuongCauHoi <= 0 ? 10 : request.SoLuongCauHoi,
                ThoiGianMoiCau = request.ThoiGianMoiCau <= 0 ? 15 : request.ThoiGianMoiCau,
                ChuyenCauNgayKhiChon = request.ChuyenCauNgayKhiChon,
                TrangThaiPhong = TrangThaiPhong.ChoNguoiChoi,
                DanhSachNguoiChoi = new List<NguoiChoiTH2DTO> { hostPlayer }
            };

            _activeRooms.TryAdd(maPhong, newRoom);
            return Task.FromResult(newRoom);
        }

        public Task<PhongDauTH2ResponseDTO> VaoPhongAsync(VaoPhongTH2RequestDTO request)
        {
            if (!_activeRooms.TryGetValue(request.MaPhong, out var phong))
                throw new InvalidOperationException("Mã phòng không tồn tại hoặc đã bị hủy.");

            lock (phong)
            {
                if (phong.TrangThaiPhong != TrangThaiPhong.ChoNguoiChoi)
                    throw new InvalidOperationException("Phòng đấu đã bắt đầu, không thể tham gia.");

                var existingPlayer = phong.DanhSachNguoiChoi.FirstOrDefault(x => x.IdNguoiChoi == request.IdNguoiChoi);
                if (existingPlayer == null)
                {
                    phong.DanhSachNguoiChoi.Add(new NguoiChoiTH2DTO
                    {
                        IdNguoiChoi = request.IdNguoiChoi,
                        TenNguoiChoi = request.TenNguoiChoi,
                        Avatar = request.Avatar,
                        IsHost = false,
                        TrangThai = TrangThaiNguoiChoi.DangCho
                    });
                }
            }

            return Task.FromResult(phong);
        }

        public Task<PhongDauTH2ResponseDTO> GetThongTinPhongAsync(string maPhong)
        {
            if (_activeRooms.TryGetValue(maPhong, out var phong))
                return Task.FromResult(phong);

            throw new KeyNotFoundException("Phòng không tồn tại.");
        }

        public Task<PhongDauTH2ResponseDTO> SanSangAsync(SanSangTH2RequestDTO request)
        {
            if (!_activeRooms.TryGetValue(request.MaPhong, out var phong))
                throw new KeyNotFoundException("Phòng không tồn tại.");

            lock (phong)
            {
                var player = phong.DanhSachNguoiChoi.FirstOrDefault(x => x.IdNguoiChoi == request.IdNguoiChoi);
                if (player != null && !player.IsHost)
                {
                    player.TrangThai = request.IsSanSang ? TrangThaiNguoiChoi.DaSanSang : TrangThaiNguoiChoi.DangCho;
                }
            }

            return Task.FromResult(phong);
        }

        public Task<PhongDauTH2ResponseDTO> KickNguoiChoiAsync(KickNguoiChoiTH2RequestDTO request)
        {
            if (!_activeRooms.TryGetValue(request.MaPhong, out var phong))
                throw new KeyNotFoundException("Phòng không tồn tại.");

            lock (phong)
            {
                var host = phong.DanhSachNguoiChoi.FirstOrDefault(x => x.IdNguoiChoi == request.IdNguoiChoi);
                if (host == null || !host.IsHost)
                    throw new UnauthorizedAccessException("Chỉ có chủ phòng mới có quyền kick người chơi.");

                var player = phong.DanhSachNguoiChoi.FirstOrDefault(x => x.IdNguoiChoi == request.IdNguoiChoiBiKick);
                if (player != null)
                {
                    phong.DanhSachNguoiChoi.Remove(player);
                }
            }

            return Task.FromResult(phong);
        }

        public Task<PhongDauTH2ResponseDTO> RoiPhongAsync(RoiPhongTH2RequestDTO request)
        {
            if (!_activeRooms.TryGetValue(request.MaPhong, out var phong))
                throw new KeyNotFoundException("Phòng không tồn tại.");

            lock (phong)
            {
                var player = phong.DanhSachNguoiChoi.FirstOrDefault(x => x.IdNguoiChoi == request.IdNguoiChoi);
                if (player != null)
                {
                    phong.DanhSachNguoiChoi.Remove(player);

                    if (player.IsHost)
                    {
                        if (phong.DanhSachNguoiChoi.Any())
                        {
                            var nextHost = phong.DanhSachNguoiChoi.First();
                            nextHost.IsHost = true;
                            nextHost.TrangThai = TrangThaiNguoiChoi.DaSanSang;
                        }
                        else
                        {
                            _activeRooms.TryRemove(request.MaPhong, out _);
                            _roomResults.TryRemove(request.MaPhong, out _);
                            _roomQuestions.TryRemove(request.MaPhong, out _);
                        }
                    }
                }
            }

            return Task.FromResult(phong);
        }

        public async Task<ThongTinTranDauTH2ResponseDTO> BatDauAsync(BatDauTH2RequestDTO request)
        {
            if (!_activeRooms.TryGetValue(request.MaPhong, out var phong))
                throw new KeyNotFoundException("Phòng không tồn tại.");

            lock (phong)
            {
                var host = phong.DanhSachNguoiChoi.FirstOrDefault(x => x.IdNguoiChoi == request.IdNguoiChoi);
                if (host == null || !host.IsHost)
                    throw new UnauthorizedAccessException("Chỉ chủ phòng mới có quyền bắt đầu trận đấu.");

                if (phong.DanhSachNguoiChoi.Count < 2)
                    throw new InvalidOperationException("Phòng cần tối thiểu 2 người chơi mới có thể bắt đầu.");

                var notReadyPlayers = phong.DanhSachNguoiChoi
                    .Where(x => x.TrangThai != TrangThaiNguoiChoi.DaSanSang)
                    .Select(x => x.TenNguoiChoi)
                    .ToList();

                if (notReadyPlayers.Any())
                {
                    var names = string.Join(", ", notReadyPlayers);
                    throw new InvalidOperationException($"Vẫn còn người chơi chưa sẵn sàng: {names}");
                }

                phong.TrangThaiPhong = TrangThaiPhong.DangThiDau;
                foreach (var p in phong.DanhSachNguoiChoi)
                {
                    p.TrangThai = TrangThaiNguoiChoi.DangThiDau;
                }
            }

            // Khởi tạo bảng kết quả ban đầu
            var initialResults = phong.DanhSachNguoiChoi.Select(p => new NguoiChoiXepHangTH2DTO
            {
                IdNguoiChoi = p.IdNguoiChoi,
                TenNguoiChoi = p.TenNguoiChoi,
                Avatar = p.Avatar,
                IsHost = p.IsHost,
                TongDiem = 0,
                TongThoiGianGiay = 0,
                TrangThaiThiDau = TrangThaiNguoiChoi.DangThiDau
            }).ToList();

            _roomResults[request.MaPhong] = initialResults;

            var danhSachCauHoi = await LayCauHoiTuDatabaseAsync(phong.IdCapDo, phong.IdLoai, phong.SoLuongCauHoi);

            if (danhSachCauHoi == null || !danhSachCauHoi.Any())
            {
                lock (phong)
                {
                    phong.TrangThaiPhong = TrangThaiPhong.ChoNguoiChoi;
                    foreach (var p in phong.DanhSachNguoiChoi)
                    {
                        p.TrangThai = p.IsHost ? TrangThaiNguoiChoi.DaSanSang : TrangThaiNguoiChoi.DangCho;
                    }
                }
                throw new InvalidOperationException("Không tìm thấy đủ câu hỏi phù hợp trong hệ thống.");
            }

            _roomQuestions[request.MaPhong] = danhSachCauHoi;

            int thoiGianMoiCau = phong.ThoiGianMoiCau > 0 ? phong.ThoiGianMoiCau : 15;

            return new ThongTinTranDauTH2ResponseDTO
            {
                MaPhong = phong.MaPhong,
                DanhSachCauHoi = danhSachCauHoi,
                ThoiGianMoiCau = thoiGianMoiCau,
                ChuyenCauNgayKhiChon = phong.ChuyenCauNgayKhiChon,
                TongThoiGianChuan = phong.SoLuongCauHoi * thoiGianMoiCau,
                ThoiGianToiDa = (phong.SoLuongCauHoi * thoiGianMoiCau) + 60
            };
        }

        public Task<TraLoiTH2ResponseDTO> XuLyTraLoiAsync(TraLoiTH2RequestDTO request)
        {
            if (!_activeRooms.TryGetValue(request.MaPhong, out var phong))
            {
                return Task.FromResult(new TraLoiTH2ResponseDTO { Success = false, Message = "Phòng không tồn tại!" });
            }

            if (!_roomQuestions.TryGetValue(request.MaPhong, out var cauHoiList))
            {
                return Task.FromResult(new TraLoiTH2ResponseDTO { Success = false, Message = "Chưa có dữ liệu câu hỏi!" });
            }

            var cauHoi = cauHoiList.FirstOrDefault(q => q.IdCauHoi == request.IdCauHoi);
            if (cauHoi == null)
            {
                return Task.FromResult(new TraLoiTH2ResponseDTO { Success = false, Message = "Câu hỏi không hợp lệ!" });
            }

            var player = phong.DanhSachNguoiChoi.FirstOrDefault(p => p.IdNguoiChoi == request.IdNguoiChoi);
            if (player == null)
            {
                return Task.FromResult(new TraLoiTH2ResponseDTO { Success = false, Message = "Người chơi không thuộc phòng đấu!" });
            }

            bool laDung = (request.ViTriDapAnChon == cauHoi.DapAnDungIndex);
            int diemCong = 0;
            int thoiGianChoPhep = phong.ThoiGianMoiCau > 0 ? phong.ThoiGianMoiCau : 15;

            if (laDung && request.ThoiGianGuiTraLoi <= thoiGianChoPhep)
            {
                double thoiGianConLai = Math.Max(0, thoiGianChoPhep - request.ThoiGianGuiTraLoi);
                diemCong = (int)Math.Round(cauHoi.DiemGoc * (thoiGianConLai / (double)thoiGianChoPhep));
            }

            var resultList = _roomResults.GetOrAdd(request.MaPhong, _ => new List<NguoiChoiXepHangTH2DTO>());
            int tongDiemHienTai = 0;

            lock (resultList)
            {
                var playerRank = resultList.FirstOrDefault(x => x.IdNguoiChoi == request.IdNguoiChoi);
                if (playerRank == null)
                {
                    playerRank = new NguoiChoiXepHangTH2DTO
                    {
                        IdNguoiChoi = request.IdNguoiChoi,
                        TenNguoiChoi = player.TenNguoiChoi,
                        Avatar = player.Avatar,
                        IsHost = player.IsHost,
                        TongDiem = 0,
                        TongThoiGianGiay = 0,
                        TrangThaiThiDau = TrangThaiNguoiChoi.DangThiDau
                    };
                    resultList.Add(playerRank);
                }

                playerRank.TongDiem += diemCong;
                playerRank.TongThoiGianGiay += request.ThoiGianGuiTraLoi;
                tongDiemHienTai = playerRank.TongDiem;
            }

            return Task.FromResult(new TraLoiTH2ResponseDTO
            {
                Success = true,
                Message = laDung ? "Chính xác!" : "Rất tiếc, đáp án chưa đúng!",
                LaDapAnDung = laDung,
                DapAnDungIndex = cauHoi.DapAnDungIndex,
                DiemCong = diemCong,
                TongDiemHienTai = tongDiemHienTai
            });
        }

        public Task<NopBaiTH2ResponseDTO> NopBaiAsync(NopBaiTH2RequestDTO request)
        {
            if (!_activeRooms.TryGetValue(request.MaPhong, out var phong))
                throw new KeyNotFoundException("Phòng không tồn tại.");

            lock (phong)
            {
                var player = phong.DanhSachNguoiChoi.FirstOrDefault(x => x.IdNguoiChoi == request.IdNguoiChoi);
                if (player != null)
                {
                    player.TrangThai = request.TrangThai == TrangThaiNguoiChoi.BoCuoc
                        ? TrangThaiNguoiChoi.BoCuoc
                        : TrangThaiNguoiChoi.HoanThanh;
                }
            }

            var resultList = _roomResults.GetOrAdd(request.MaPhong, _ => new List<NguoiChoiXepHangTH2DTO>());
            lock (resultList)
            {
                var playerRank = resultList.FirstOrDefault(x => x.IdNguoiChoi == request.IdNguoiChoi);
                if (playerRank != null)
                {
                    playerRank.TrangThaiThiDau = request.TrangThai;
                    if (request.TrangThai == TrangThaiNguoiChoi.BoCuoc)
                    {
                        playerRank.TongThoiGianGiay = 9999;
                    }
                }
            }

            bool isAllFinished;
            lock (phong)
            {
                isAllFinished = phong.DanhSachNguoiChoi.All(x =>
                    x.TrangThai == TrangThaiNguoiChoi.HoanThanh ||
                    x.TrangThai == TrangThaiNguoiChoi.BoCuoc);

                if (isAllFinished)
                {
                    phong.TrangThaiPhong = TrangThaiPhong.DaKetThuc;
                }
            }

            return Task.FromResult(new NopBaiTH2ResponseDTO
            {
                IsAllFinished = isAllFinished,
                Message = isAllFinished ? "Tất cả đã hoàn thành." : "Bạn bè đang làm bài, vui lòng chờ..."
            });
        }

        public Task<BangXepHangTH2ResponseDTO> GetBangXepHangAsync(string maPhong)
        {
            if (!_roomResults.TryGetValue(maPhong, out var resultList))
                throw new KeyNotFoundException("Chưa có dữ liệu xếp hạng.");

            List<NguoiChoiXepHangTH2DTO> sortedList;
            lock (resultList)
            {
                sortedList = resultList
                    .OrderByDescending(x => x.TongDiem)
                    .ThenBy(x => x.TongThoiGianGiay)
                    .Select((item, index) => new NguoiChoiXepHangTH2DTO
                    {
                        Hang = index + 1,
                        IdNguoiChoi = item.IdNguoiChoi,
                        TenNguoiChoi = item.TenNguoiChoi,
                        Avatar = item.Avatar,
                        IsHost = item.IsHost,
                        TongDiem = item.TongDiem,
                        TongThoiGianGiay = item.TongThoiGianGiay,
                        TrangThaiThiDau = item.TrangThaiThiDau
                    }).ToList();
            }

            return Task.FromResult(new BangXepHangTH2ResponseDTO
            {
                MaPhong = maPhong,
                DanhSachXepHang = sortedList
            });
        }

        public Task<bool> XemLaiBaiAsync(XemLaiBaiTH2RequestDTO request)
        {
            if (_activeRooms.TryGetValue(request.MaPhong, out var phong))
            {
                lock (phong)
                {
                    var player = phong.DanhSachNguoiChoi.FirstOrDefault(x => x.IdNguoiChoi == request.IdNguoiChoi);
                    if (player != null)
                    {
                        player.TrangThai = TrangThaiNguoiChoi.DangXemLai;
                        return Task.FromResult(true);
                    }
                }
            }
            return Task.FromResult(false);
        }

        public Task<PhongDauTH2ResponseDTO> QuayLaiPhongAsync(QuayLaiPhongTH2RequestDTO request)
        {
            if (!_activeRooms.TryGetValue(request.MaPhong, out var phong))
                throw new KeyNotFoundException("Phòng không tồn tại.");

            lock (phong)
            {
                var player = phong.DanhSachNguoiChoi.FirstOrDefault(x => x.IdNguoiChoi == request.IdNguoiChoi);
                if (player != null)
                {
                    player.TrangThai = player.IsHost ? TrangThaiNguoiChoi.DaSanSang : TrangThaiNguoiChoi.DangCho;
                }

                bool isAllBack = phong.DanhSachNguoiChoi.All(x =>
                    x.TrangThai == TrangThaiNguoiChoi.DangCho ||
                    x.TrangThai == TrangThaiNguoiChoi.DaSanSang ||
                    x.TrangThai == TrangThaiNguoiChoi.BoCuoc);

                if (isAllBack)
                {
                    phong.TrangThaiPhong = TrangThaiPhong.ChoNguoiChoi;
                }
            }

            return Task.FromResult(phong);
        }

        /* ======================================================== */
        /* HÀM TRUY VẤN CÂU HỎI TỪ DATABASE ĐÃ ĐƯỢC TỐI ƯU CỰC MẠNH */
        /* ======================================================== */
        private async Task<List<CauHoiDauTruongDTOTH2>> LayCauHoiTuDatabaseAsync(int? idCapDo, int? idLoai, int soLuongCauHoi)
        {
            int targetCount = soLuongCauHoi <= 0 ? 10 : soLuongCauHoi;
            var validLoaiIds = new List<int> { 1005, 1008, 1009 };

            // 1. Tạo Query lọc theo tiêu chuẩn
            var baseQuery = _context.CauHois
                .AsNoTracking()
                .Where(q => (q.TrangThai == true || q.TrangThai == null) && q.DapAns.Any());

            if (idLoai.HasValue && validLoaiIds.Contains(idLoai.Value))
            {
                baseQuery = baseQuery.Where(q => q.IdLoai == idLoai.Value);
            }
            else
            {
                baseQuery = baseQuery.Where(q => q.IdLoai.HasValue && validLoaiIds.Contains(q.IdLoai.Value));
            }

            // 2. Lọc theo Cấp độ (nếu có)
            IQueryable<CauHoi> filteredQuery = baseQuery;
            if (idCapDo.HasValue && idCapDo.Value > 0)
            {
                filteredQuery = filteredQuery.Where(q => q.IdCapDo == idCapDo.Value);
            }

            // Lấy danh sách ID thỏa mãn lọc cấp độ
            var matchedIds = await filteredQuery.Select(q => q.IdCauHoi).ToListAsync();

            // 3. Fallback: Nếu không đủ số lượng theo cấp độ, lấy thêm câu hỏi ngẫu nhiên khác cấp độ
            if (matchedIds.Count < targetCount)
            {
                var existingSet = new HashSet<int>(matchedIds);
                var fallbackIds = await baseQuery
                    .Where(q => !existingSet.Contains(q.IdCauHoi))
                    .Select(q => q.IdCauHoi)
                    .ToListAsync();

                var needed = targetCount - matchedIds.Count;
                matchedIds.AddRange(fallbackIds.OrderBy(_ => Random.Shared.Next()).Take(needed));
            }

            if (!matchedIds.Any()) return new List<CauHoiDauTruongDTOTH2>();

            // 4. Chọn ngẫu nhiên N ID trong tập thỏa điều kiện
            var selectedIds = matchedIds.OrderBy(_ => Random.Shared.Next()).Take(targetCount).ToList();

            // 5. Query lấy chi tiết kèm Từ Vựng (Lấy Pinyin) và Đáp Án
            var rawQuestions = await _context.CauHois
                .AsNoTracking()
                .Include(q => q.DapAns)
                .Include(q => q.IdTuVungs) // Include để lấy Pinyin từ Từ Vựng liên kết
                .Where(q => selectedIds.Contains(q.IdCauHoi))
                .ToListAsync();

            var dictQuestions = rawQuestions.ToDictionary(q => q.IdCauHoi);
            var resultList = new List<CauHoiDauTruongDTOTH2>();

            foreach (var id in selectedIds)
            {
                if (!dictQuestions.TryGetValue(id, out var q)) continue;

                var dapAnsOriginal = q.DapAns.ToList();
                if (!dapAnsOriginal.Any()) continue;

                // Xáo trộn đáp án để người chơi không đoán theo vị trí cố định
                var shuffledDapAns = dapAnsOriginal.OrderBy(_ => Random.Shared.Next()).ToList();
                int correctIndex = shuffledDapAns.FindIndex(d => d.LaDapAnDung == true);

                // Lấy Pinyin từ Từ Vựng liên kết (nếu có)
                string pinyinText = q.IdTuVungs.FirstOrDefault()?.Pinyin ?? string.Empty;

                resultList.Add(new CauHoiDauTruongDTOTH2
                {
                    IdCauHoi = q.IdCauHoi,
                    NoiDung = q.NoiDung ?? "Nội dung câu hỏi",
                    PhiênÂm = pinyinText,
                    DịchNghĩa = q.GiaiThich ?? string.Empty,
                    AmThanhUrl = q.AmThanhPath ?? string.Empty,
                    DanhSachDapAn = shuffledDapAns.Select(d => d.NoiDungDapAn ?? string.Empty).ToList(),
                    DapAnDungIndex = correctIndex >= 0 ? correctIndex : 0,
                    DiemGoc = (q.DoKho.HasValue && q.DoKho.Value > 0) ? q.DoKho.Value * 10 : Random.Shared.Next(10, 41)
                });
            }

            return resultList;
        }
    }
}